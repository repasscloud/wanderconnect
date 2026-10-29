using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Evidence;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Accounts;

public sealed record ConflictRetentionRun(int Expired, bool HoldStatusUnavailable = false);

/// <summary>Minimal conflict provenance from explicit staff-member associations; never inferred from email.</summary>
public sealed class StaffConflictHistory(SwoovrDbContext db, EvidencePreservation evidence)
{
    public const string PersonalReason = "Explicit personal staff-member association (retained conflict record).";

    /// <summary>Caller saves atomically with link replacement/removal. Original actor/time identify the source assertion.</summary>
    public async Task<bool> PreserveAsync(StaffMemberLink link, DateTimeOffset now, CancellationToken token)
    {
        if (db.ChangeTracker.Entries<StaffExclusion>().Any(e => e.State == EntityState.Added
            && e.Entity.StaffAccountId == link.StaffAccountId && e.Entity.SubjectAccountId == link.MemberAccountId)
            || await db.StaffExclusions.AnyAsync(e => e.StaffAccountId == link.StaffAccountId && e.SubjectAccountId == link.MemberAccountId, token)) return false;
        db.StaffExclusions.Add(new StaffExclusion { StaffAccountId = link.StaffAccountId, SubjectAccountId = link.MemberAccountId,
            Reason = PersonalReason, CreatedBy = link.LinkedBy, CreatedAt = link.LinkedAt });
        Audit.Record(db, now, null, "system", "staff-conflict.preserved", "account", link.StaffAccountId.ToString(), link.MemberAccountId.ToString());
        return true;
    }

    /// <summary>Historical authenticated admin link events remain authoritative after old code removed their current link.</summary>
    public async Task<int> ReconcileAsync(DateTimeOffset now, CancellationToken token)
    {
        await using var barrier = await PreservationLease.ExclusiveAsync(db, token);
        var added = 0;
        foreach (var link in await db.StaffMemberLinks.AsNoTracking().ToListAsync(token))
            if (await PreserveAsync(link, now, token)) added++;
        await db.SaveChangesAsync(token);
        long after = 0;
        while (true)
        {
            var page = await db.AuditEntries.AsNoTracking().Where(a => a.Id > after && a.Action == "staff-link.set" && a.ActorAccountId != null)
                .OrderBy(a => a.Id).Take(500).ToListAsync(token);
            if (page.Count == 0) break;
            foreach (var source in page)
                if (Guid.TryParse(source.TargetId, out var staff) && Guid.TryParse(source.Detail, out var member)
                    && await PreserveAsync(new StaffMemberLink { StaffAccountId = staff, MemberAccountId = member,
                        LinkedBy = source.ActorAccountId!.Value, LinkedAt = source.At }, now, token)) added++;
            await db.SaveChangesAsync(token);
            after = page[^1].Id;
        }
        return added;
    }

    /// <summary>Opaque association expires only after its minimum and every related source/retention obligation ends.</summary>
    public async Task<ConflictRetentionRun> PurgeAsync(DateTimeOffset now, CancellationToken token)
    {
        await using var barrier = await PreservationLease.ExclusiveAsync(db, token);
        if (await evidence.TryHeldAccountsAsync(token) is null) return new(0, true);
        var cutoff = now.ToUniversalTime().AddMonths(-24);
        var expired = 0;
        foreach (var row in await db.StaffExclusions.AsNoTracking().Where(e => e.CreatedAt <= cutoff).ToListAsync(token))
        {
            var staff = row.StaffAccountId.ToString();
            var member = row.SubjectAccountId.ToString();
            if (await db.StaffMemberLinks.AnyAsync(l => l.StaffAccountId == row.StaffAccountId && l.MemberAccountId == row.SubjectAccountId, token)
                || await db.AuditEntries.AnyAsync(a => (a.Action == "staff-link.set" || a.Action == "access.excluded") && a.TargetId == staff && a.Detail == member && a.At > cutoff, token)
                || await RequiredAsync(row.SubjectAccountId, token)) continue;
            await using var transaction = await db.Database.BeginTransactionAsync(token);
            // Retire the expired source assertions too; a later reconciliation must not resurrect an unnecessary association.
            await db.AuditEntries.Where(a => (a.Action == "staff-link.set" || a.Action == "access.excluded" || a.Action == "staff-conflict.preserved") && a.TargetId == staff && a.Detail == member).ExecuteDeleteAsync(token);
            await db.StaffExclusions.Where(e => e.StaffAccountId == row.StaffAccountId && e.SubjectAccountId == row.SubjectAccountId).ExecuteDeleteAsync(token);
            foreach (var tracked in db.StaffExclusions.Local.Where(e => e.StaffAccountId == row.StaffAccountId && e.SubjectAccountId == row.SubjectAccountId).ToList()) db.Entry(tracked).State = EntityState.Detached;
            // Count only: do not create another lasting member/staff association while expiring it.
            Audit.Record(db, now, null, "retention", "staff-conflict.expired");
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            expired++;
        }
        return new(expired);
    }

    public async Task<bool> RequiredAsync(Guid member, CancellationToken token) =>
        await db.Accounts.AnyAsync(a => a.Id == member, token)
        || await db.ModerationCases.AnyAsync(c => c.SubjectAccountId == member, token)
        || await db.Reports.AnyAsync(r => r.ReporterAccountId == member || r.SubjectAccountId == member, token)
        || await db.LegalHoldScopes.AnyAsync(s => s.Kind == HoldScopeKind.Account && s.TargetId == member, token)
        || await db.LegalHolds.AnyAsync(h => h.RelatedAccountIds != null && h.RelatedAccountIds.Contains(member), token)
        || await db.EvidenceItems.AnyAsync(e => e.AccountId == member, token)
        || await db.LeCasePeople.AnyAsync(p => p.AccountId == member, token)
        || await db.VerifiedIdentities.AnyAsync(i => i.AccountId == member, token)
        || await db.ContactHistories.AnyAsync(h => h.AccountId == member, token)
        || await db.IdentityAccesses.AnyAsync(a => a.AccountId == member, token)
        // Undeciphered legacy membership cannot establish that a subject is absent.
        || await db.LegalHolds.AnyAsync(h => h.RelatedAccountIds == null && db.EvidenceItems.Any(e => e.HoldId == h.Id), token);
}
