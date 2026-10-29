using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Accounts;

public sealed record RegistryBackfillRun(int Sanctions, int Restrictions, int Unresolved);

/// <summary>Positive termination/current-contact provenance only. Never infer original people from hash-only markers.</summary>
public sealed class RegistryPopulation(SwoovrDbContext db,BannedIdentifierKeys keys)
{
    /// <summary>Caller owns exclusive preservation authority and commits sanction, case, entries and notes together.</summary>
    public async Task<int> StageAsync(Sanction sanction,DateTimeOffset recordedAt,CancellationToken token)
    {
        if (sanction.Kind != SanctionKind.Termination || sanction.LiftedAt is not null || sanction.StartsAt > recordedAt) throw new ArgumentException("An active termination is required.");
        if (!keys.Configured || keys.CurrentVersion is not { } version) throw BannedIdentifierRegistry.Unavailable();
        var source = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == sanction.AccountId && a.Realm == AccountRealm.Members,token)
            ?? throw new InvalidOperationException("Termination source is not a member account.");
        if (sanction.CaseId is null)
        {
            var review = new ModerationCase { Id = Guid.NewGuid(),SubjectAccountId = source.Id,Status = CaseStatus.Closed,Queue = CaseQueue.Admin,
                Priority = ReportPriority.High,CreatedAt = sanction.StartsAt,UpdatedAt = recordedAt,ClosedAt = sanction.StartsAt,
                ClosedBy = sanction.CreatedBy,SystemFlag = "account-termination" };
            db.ModerationCases.Add(review);sanction.CaseId = review.Id;
        }
        var reviewId = sanction.CaseId.Value;
        var trackedCase=db.ModerationCases.Local.FirstOrDefault(c=>c.Id==reviewId);
        var caseSubject=trackedCase?.SubjectAccountId??await db.ModerationCases.Where(c=>c.Id==reviewId).Select(c=>(Guid?)c.SubjectAccountId).SingleOrDefaultAsync(token);
        if(caseSubject!=source.Id)throw new InvalidOperationException("Termination case provenance requires restricted review.");
        var phone = await db.MemberPhones.Where(p => p.AccountId == source.Id).Select(p => p.Number).SingleOrDefaultAsync(token);
        var identifiers = new[] { (Type:BannedIdentifierType.Email,Value:EmailAddress.Normalise(source.Email)),
            (Type:BannedIdentifierType.Mobile,Value:PhoneVerificationPolicy.Normalise(phone)) };
        var added = 0;
        foreach (var (type,value) in identifiers)
        {
            if (value is null) continue;
            // Lawfully disposed released history remains authoritative; do not recreate it from surviving contact data.
            if(await db.BanDispositions.AnyAsync(d=>d.CaseId==reviewId&&d.IdentifierType==type,token))continue;
            // Existing active, revoked or expired entries are authoritative: migration/deletion never silently reactivates one.
            var existing = await db.BannedIdentifiers.Where(b => b.CaseId == reviewId && b.IdentifierType == type).ToListAsync(token);
            var already = existing.Any(b => keys.HasVersion(b.KeyVersion) && keys.Fingerprint(type,value,b.KeyVersion).SequenceEqual(b.IdentifierHash))
                || db.BannedIdentifiers.Local.Any(b => b.CaseId == reviewId && b.IdentifierType == type && b.KeyVersion == version && b.IdentifierHash.SequenceEqual(keys.Fingerprint(type,value,version)));
            if (existing.Any(b => !keys.HasVersion(b.KeyVersion))) throw BannedIdentifierRegistry.Unavailable();
            if (already) continue;
            var entry = BannedIdentifierPolicy.Issue(type,keys.Fingerprint(type,value,version),version,reviewId,sanction.CreatedBy,sanction.StartsAt,
                "Restriction created from a permanent account termination; contact value is not copied into the review note.");
            // BannedAt is original enforcement time; creation audit time records this write/backfill truthfully.
            entry.Transition.At = recordedAt;entry.Note.At = recordedAt;
            db.BannedIdentifiers.Add(entry.Entry);db.BanTransitions.Add(entry.Transition);db.BanCaseNotes.Add(entry.Note);added++;
        }
        return added;
    }

    public async Task<RegistryBackfillRun> BackfillAsync(DateTimeOffset now,CancellationToken token)
    {
        var count = 0;var restrictions = 0;var unresolved = 0;Guid? after = null;
        while (true)
        {
            var query = db.Sanctions.Where(s => s.Kind == SanctionKind.Termination && s.LiftedAt == null && s.StartsAt <= now);
            if (after is { } cursor) query = query.Where(s => s.Id.CompareTo(cursor) > 0);
            var page = await query.OrderBy(s => s.Id).Select(s => s.Id).Take(200).ToListAsync(token);
            if (page.Count == 0) break;
            foreach (var id in page)
            {
                await using var barrier = await Retention.PreservationLease.ExclusiveAsync(db,token);
                var sanction = await db.Sanctions.SingleOrDefaultAsync(s => s.Id == id && s.LiftedAt == null,token);
                if (sanction is null) continue;
                await db.Entry(sanction).ReloadAsync(token);
                await using var transaction = await db.Database.BeginTransactionAsync(token);
                var added = await StageAsync(sanction,now,token);
                await db.SaveChangesAsync(token);await transaction.CommitAsync(token);
                count++;restrictions+=added;
                if (!await db.BannedIdentifiers.AnyAsync(b => b.CaseId == sanction.CaseId,token)&&!await db.BanDispositions.AnyAsync(d=>d.CaseId==sanction.CaseId,token)) unresolved++;
            }
            after = page[^1];db.ChangeTracker.Clear();
        }
        return new(count,restrictions,unresolved);
    }
}
