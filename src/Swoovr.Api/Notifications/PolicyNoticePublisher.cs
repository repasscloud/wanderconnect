using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Notifications;

/// <summary>
/// #121 DEFAULT: explicit operator-staged, centre-only notices. No external delivery and no automatic publication.
/// Call from the existing restricted operator CLI only; do not map this service to a member/public route.
/// </summary>
public sealed class PolicyNoticePublisher(SwoovrDbContext db, IConfiguration configuration, TimeProvider clock)
{
    private const int PublicationMutex = 0x504E4F54;
    public bool Enabled => configuration.GetValue("PolicyNotices:Enabled", false);

    public async Task<Guid> StageAsync(string version, string title, string link, Guid operatorId, CancellationToken token)
    {
        if (operatorId == Guid.Empty || !PolicyNoticePolicy.ValidVersion(version) || !PolicyNoticePolicy.ValidTitle(title) || !PolicyNoticePolicy.AllowedLink(link))
            throw new InvalidOperationException("Invalid policy notice source or metadata.");
        await using var owner = await AccountLifecycleLease.AcquireAsync(db, operatorId, token);
        await using var preservation = await PreservationLease.SharedAsync(db, token);
        await RequireOperatorAsync(operatorId,token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({PublicationMutex}, {0})", token);
        var prior = await db.Set<PolicyNoticePublication>().AsNoTracking().SingleOrDefaultAsync(p => p.Version == version, token);
        if (prior != null)
        {
            if(prior.DisposedAt!=null)throw new InvalidOperationException("Publication version has expired and cannot be reused.");
            if (prior.Title != title || prior.Link != link) throw new InvalidOperationException("Version is already staged with different metadata.");
            return prior.Id;
        }
        var row = new PolicyNoticePublication { Id = Guid.NewGuid(), Version = version, Title = title, Link = link, StagedAt = clock.GetUtcNow(), StagedBy = operatorId };
        db.Add(row);Audit.Record(db,clock.GetUtcNow(),operatorId,"cli","policy-notice.staged","policy-publication",row.Id.ToString(),version); await db.SaveChangesAsync(token); await transaction.CommitAsync(token);
        return row.Id;
    }

    private async Task RequireOperatorAsync(Guid id,CancellationToken token)
    {
        if(!await db.Accounts.AnyAsync(a=>a.Id==id&&a.Realm==AccountRealm.Staff&&a.Status==AccountStatus.Active&&a.IsRootAdmin,token))throw new InvalidOperationException("A current named root operator is required.");
    }

    /// <param name="sourceIsPublished">Explicit operator confirmation of independently published source, never inferred from existence of this record.</param>
    public async Task<bool> PublishAsync(Guid id, Guid operatorId, bool sourceIsPublished, CancellationToken token)
    {
        if (!Enabled || operatorId == Guid.Empty || !sourceIsPublished) return false;
        await using var owner = await AccountLifecycleLease.AcquireAsync(db, operatorId, token);
        await using var preservation = await PreservationLease.SharedAsync(db, token);
        await RequireOperatorAsync(operatorId,token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({PublicationMutex}, {0})", token);
        var row = await db.Set<PolicyNoticePublication>().AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, token);
        if (row == null||row.DisposedAt!=null) return false;
        if (row.PublishedAt != null) return true; // Repeat explicit command never snapshots newly registered members.
        if (!PolicyNoticePolicy.CanPublish(row, sourceIsPublished)) return false;
        var now = clock.GetUtcNow();
        row.PublishedAt = now; row.PublishedBy = operatorId; row.SendUntil = now.AddDays(30); row.PurgeAfter = now.AddDays(60);
        await db.Set<PolicyNoticePublication>().Where(p => p.Id == id && p.PublishedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.PublishedAt, now).SetProperty(p => p.PublishedBy, operatorId)
                .SetProperty(p => p.SendUntil, now.AddDays(30)).SetProperty(p => p.PurgeAfter, now.AddDays(60)), token);
        // Snapshot once in the same transaction as activation. Destination/member content never enters this table.
        // Explicit table names must be preserved by the root model configuration.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO policy_notice_recipients (publication_id, account_id, status, created_at)
            SELECT {id}, a.id, 'queued', {now} FROM accounts a
            WHERE a.realm='Members' AND a.status='Active' AND a.created_at<={now}
            ON CONFLICT (publication_id, account_id) DO NOTHING
            """, token);
        Audit.Record(db,now,operatorId,"cli","policy-notice.published","policy-publication",id.ToString(),row.Version);await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return true;
    }

    /// <summary>Bounded resumable centre delivery. Restarting never republishes, resnapshots or sends push/email.</summary>
    public async Task<int> RunBatchAsync(Guid publicationId, CancellationToken token)
    {
        if (!Enabled) return 0;
        var ids = await db.Set<PolicyNoticeRecipient>().AsNoTracking().Where(r => r.PublicationId == publicationId && r.Status == "queued")
            .OrderBy(r => r.AccountId).Select(r => r.AccountId).Take(PolicyNoticePolicy.BatchSize).ToListAsync(token);
        var completed = 0;
        foreach (var member in ids)
        {
            await using var owner = await AccountLifecycleLease.AcquireAsync(db, member, token);
            await using var preservation = await PreservationLease.SharedAsync(db, token);
            await using var transaction = await db.Database.BeginTransactionAsync(token);
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({PublicationMutex}, {0})", token);
            // A prior operator invocation might share this DbContext. Read current transaction-owned state afresh.
            var target = await db.Set<PolicyNoticeRecipient>().AsNoTracking().SingleOrDefaultAsync(r => r.PublicationId == publicationId && r.AccountId == member, token);
            if (target is not { Status: "queued" }) continue;
            var publication = await db.Set<PolicyNoticePublication>().AsNoTracking().SingleOrDefaultAsync(p => p.Id == publicationId, token);
            var now = clock.GetUtcNow();
            var live = await db.Accounts.AnyAsync(a => a.Id == member && a.Realm == AccountRealm.Members && a.Status == AccountStatus.Active, token);
            var barred = Enforcement.For(await db.Sanctions.Where(s => s.AccountId == member && s.LiftedAt == null).ToListAsync(token), now).Barred;
            var eligible = PolicyNoticePolicy.CanDisplay(publication, now) && publication!.SendUntil > now && live && !barred;
            if (eligible)
            {
                var dedupe = PolicyNoticePolicy.Dedupe(publicationId);
                if (!await db.Notifications.AnyAsync(n => n.AccountId == member && n.DedupeKey == dedupe, token))
                    db.Notifications.Add(new() { Id = Guid.NewGuid(), AccountId = member, Kind = NotificationKind.PolicyChange,
                        SubjectId = publicationId, DedupeKey = dedupe, CreatedAt = publication!.PublishedAt!.Value, UpdatedAt = publication.PublishedAt.Value });
            }
            await db.SaveChangesAsync(token);
            await db.Set<PolicyNoticeRecipient>().Where(r => r.PublicationId == publicationId && r.AccountId == member && r.Status == "queued")
                .ExecuteUpdateAsync(set => set.SetProperty(r => r.Status, eligible ? "centre-recorded" : "suppressed").SetProperty(r => r.CompletedAt, now), token);
            await transaction.CommitAsync(token); completed++;
        }
        return completed;
    }
}
