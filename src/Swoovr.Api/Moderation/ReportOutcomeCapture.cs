using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Moderation;

/// <summary>Prepared producer only. Caller owns preservation/case authority and an active transaction.
/// Save real moderation decisions first, then capture before committing. Never sends externally.</summary>
public static class ReportOutcomeCapture
{
    public static async Task<int> InTransactionAsync(SwoovrDbContext db, Guid caseId, DateTimeOffset now, CancellationToken token)
    {
        if (db.Database.CurrentTransaction is null || db.ChangeTracker.HasChanges())
            throw new InvalidOperationException("Report outcome capture requires a transaction with source changes already saved.");
        var review = await db.ModerationCases.AsNoTracking().SingleOrDefaultAsync(item => item.Id == caseId, token);
        if (review is null) return 0;
        var reports = await db.Reports.AsNoTracking().Where(item => item.CaseId == caseId).OrderBy(item => item.Id).ToListAsync(token);
        var sanctions = await db.Sanctions.AsNoTracking().Where(item => item.CaseId == caseId).ToListAsync(token);
        var sanctionIds = sanctions.Select(item => item.Id).ToArray();
        var appeals = await db.Appeals.AsNoTracking().Where(item => sanctionIds.Contains(item.SanctionId) && item.DecidedAt != null && item.Status != AppealStatus.Pending).ToListAsync(token);
        var removals = await db.Set<ContentRemoval>().AsNoTracking().Where(item => item.CaseId == caseId).Select(item => new { item.Id, item.RemovedAt }).ToListAsync(token);
        var recipients=reports.Select(r=>r.ReporterAccountId).Distinct().ToArray();
        var active=await db.Accounts.Where(a=>recipients.Contains(a.Id)&&a.Status==AccountStatus.Active).Select(a=>a.Id).ToListAsync(token);
        var count = 0;
        foreach (var report in reports)
        {
            // No subject text, reason, category, reporter note or staff identity enters the hash.
            var relevantSanctions = sanctions.Where(item => item.StartsAt >= report.CreatedAt).ToArray();
            var relevantIds = relevantSanctions.Select(item => item.Id).ToHashSet();
            var decisions = relevantSanctions.Select(item => new ReportOutcomeDecision(item.Id, "sanction", item.StartsAt, item.EndsAt, item.LiftedAt,
                    item.Kind.ToString(), item.HideFromDiscovery, item.NoNewRequests, item.NoMessages))
                .Concat(removals.Where(item => item.RemovedAt >= report.CreatedAt).Select(item => new ReportOutcomeDecision(item.Id, "removal", item.RemovedAt)))
                .Concat(appeals.Where(item => relevantIds.Contains(item.SanctionId)).Select(item => new ReportOutcomeDecision(item.Id, "appeal", item.DecidedAt!.Value, State: item.Status.ToString())));
            var observed = ReportOutcomePolicy.Observe(review.Status, review.ClosedAt, decisions);
            var checkpoint = await db.Set<ReportOutcomeState>().SingleOrDefaultAsync(item => item.ReportId == report.Id, token);
            if (checkpoint is null)
            {
                checkpoint = new() { ReportId = report.Id, SourceCaseId = caseId, SourceAccountId = report.ReporterAccountId };
                db.Set<ReportOutcomeState>().Add(checkpoint);
            }
            if (checkpoint.SourceCaseId != caseId || checkpoint.SourceAccountId != report.ReporterAccountId)
                throw new InvalidOperationException("Report checkpoint provenance does not match its source.");
            var next = ReportOutcomePolicy.Advance(checkpoint.Fingerprint, checkpoint.Revision, observed);
            checkpoint.Fingerprint = next.Fingerprint;
            checkpoint.Revision = next.Revision;
            if (!next.Notify||!active.Contains(report.ReporterAccountId)) continue;
            // Centre-only: no external dispatch intent exists. No actor, source text or case identity is exposed.
            db.Notifications.Add(new Notification
            {
                Id = Guid.NewGuid(), AccountId = report.ReporterAccountId, Kind = NotificationKind.ReportOutcome,
                ActorAccountId = null, ModeId = null, SubjectId = report.Id, CreatedAt = now, UpdatedAt = now,
                DedupeKey = ReportOutcomePolicy.Dedupe(report.Id, next.Revision)
            });
            count++;
        }
        await db.SaveChangesAsync(token);
        return count;
    }
}
