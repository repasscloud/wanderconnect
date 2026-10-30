using System.Text.Json;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Evidence;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Retention;

public sealed record EvidenceRetentionDate(DateTimeOffset? Until, string Reason);
public sealed record SafetyRetentionRun(int CasesDisposed, int ContentDisposed, int CopiesDisposed, int ReviewRequired, bool HoldStatusUnavailable = false);

/// <summary>D216: independent case content and released copies. No user content or identifiers enter disposal logs.</summary>
public sealed class SafetyRetention(SwoovrDbContext db, EvidencePreservation evidence)
{
    public static async Task<List<Guid>> CaseAccountsAsync(SwoovrDbContext db, Guid id, CancellationToken token) =>
        (await db.ModerationCases.Where(c => c.Id == id).Select(c => c.SubjectAccountId).ToListAsync(token))
        .Concat(await db.Reports.Where(r => r.CaseId == id).Select(r => r.ReporterAccountId).ToListAsync(token)).Distinct().ToList();

    public static async Task<List<Guid>> HoldAccountsAsync(SwoovrDbContext db, LegalHold hold, CancellationToken token)
    {
        var accounts = await db.LegalHoldScopes.Where(s => s.HoldId == hold.Id && s.Kind == HoldScopeKind.Account).Select(s => s.TargetId).ToListAsync(token);
        accounts.AddRange(hold.RelatedAccountIds ?? []);
        accounts.AddRange(await db.EvidenceItems.Where(e => e.HoldId == hold.Id && e.AccountId != null).Select(e => e.AccountId!.Value).ToListAsync(token));
        var cases = (hold.RetentionCaseIds ?? []).Concat(hold.CaseId is { } own ? [own] : Array.Empty<Guid>()).Distinct();
        foreach (var id in cases) accounts.AddRange(await CaseAccountsAsync(db, id, token));
        return accounts.Distinct().ToList();
    }

    public async Task<SafetyRetentionWindow> CaseWindowAsync(ModerationCase item, CancellationToken token)
    {
        if (await (from a in db.Appeals join s in db.Sanctions on a.SanctionId equals s.Id
                   where s.CaseId == item.Id && a.Status == AppealStatus.Pending select a.Id).AnyAsync(token)) return new(null, null);
        var serious = item.SystemFlag == "media-known-content"
            || await db.AuditEntries.AnyAsync(a => a.Action == "report.serious" && a.TargetId == item.Id.ToString(), token)
            || await db.MediaItems.AnyAsync(i => i.CaseId == item.Id && i.Status == MediaStatus.Blocked, token)
            || await db.LegalHolds.AnyAsync(h => h.CaseId == item.Id && (h.Source == "serious-incident" || h.Source == "media-screening"), token)
            || await (from r in db.Reports join c in db.ReportCategories on r.CategoryId equals c.Id where r.CaseId == item.Id && c.Serious select r.Id).AnyAsync(token);
        var disclosed = await (from d in db.Disclosures join h in db.LegalHolds on d.HoldId equals h.Id
                               where h.CaseId == item.Id || h.RetentionCaseIds != null && h.RetentionCaseIds.Contains(item.Id)
                               select (DateTimeOffset?)d.DisclosedAt).MaxAsync(token);
        var appeal = await (from a in db.Appeals join s in db.Sanctions on a.SanctionId equals s.Id
                            where s.CaseId == item.Id select a.DecidedAt).MaxAsync(token);
        var exported = await (from e in db.EvidenceExports join h in db.LegalHolds on e.HoldId equals h.Id
                              where h.CaseId == item.Id || h.RetentionCaseIds != null && h.RetentionCaseIds.Contains(item.Id)
                              select (DateTimeOffset?)e.CreatedAt).MaxAsync(token);
        var reinstated=await db.BanTransitions.Where(t=>t.CaseId==item.Id&&t.Action=="account-reinstated").Select(t=>(DateTimeOffset?)t.At).MaxAsync(token);
        var dispositionReview=await db.BanDispositionReviews.Where(r=>r.CaseId==item.Id).Select(r=>(DateTimeOffset?)r.At).MaxAsync(token);
        var latest = new[] { item.EscalatedAt, disclosed, appeal, exported, reinstated, dispositionReview }.Where(d => d is not null).Max();
        return SafetyRetentionPolicy.ForCase(item, serious, latest);
    }

    /// <summary>Resolve legacy case provenance internally; absent/undecipherable provenance remains unknown.</summary>
    public async Task ResolveProvenanceAsync(LegalHold hold, CancellationToken token)
    {
        if (hold.RetentionCaseIds is not null && hold.RelatedAccountIds is not null) return;
        await using var barrier = await PreservationLease.ExclusiveAsync(db, token);
        await using var holdMutex = await PreservationLease.HoldAsync(db, hold.Id, token);
        await db.Entry(hold).ReloadAsync(token);
        if (hold.RetentionCaseIds is not null && hold.RelatedAccountIds is not null) return;
        var cases = (hold.RetentionCaseIds ?? []).ToHashSet();
        var accounts = (await db.LegalHoldScopes.Where(s => s.HoldId == hold.Id && s.Kind == HoldScopeKind.Account).Select(s => s.TargetId).ToListAsync(token)).ToHashSet();
        if (hold.CaseId is { } own) cases.Add(own);
        foreach (var copy in await db.EvidenceItems.Where(e => e.HoldId == hold.Id).ToListAsync(token))
        {
            if (copy.AccountId is { } owner) accounts.Add(owner);
            try
            {
                using var document = JsonDocument.Parse(evidence.Open(copy));
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return;
                bool Member(string name)
                {
                    if (!root.TryGetProperty(name, out var field) || !field.TryGetGuid(out var id)) return false;
                    accounts.Add(id); return true;
                }
                bool Case(string name)
                {
                    if (!root.TryGetProperty(name, out var field)) return false;
                    if (field.ValueKind == JsonValueKind.Null) return true;
                    if (!field.TryGetGuid(out var id)) return false;
                    cases.Add(id); return true;
                }
                switch (copy.Kind)
                {
                    case "case": if (!Guid.TryParse(copy.RecordId, out var id) || !Member("subjectAccountId")) return; cases.Add(id); break;
                    case "report": if (!Case("caseId") || !Member("reporterAccountId") || !Member("subjectAccountId")) return; break;
                    case "case-evidence":
                        if (!Case("caseId") || !root.TryGetProperty("reportId", out var reportField) || !reportField.TryGetGuid(out var reportId)) return;
                        var sourceReport = await db.Reports.AsNoTracking().SingleOrDefaultAsync(r => r.Id == reportId, token);
                        if (sourceReport is null) return; // Missing participant provenance requires independent review.
                        accounts.UnionWith([sourceReport.ReporterAccountId, sourceReport.SubjectAccountId]);
                        break;
                    case "sanction": if (!Case("caseId")) return; break;
                    case "conversation": if (!Member("pairLow") || !Member("pairHigh")) return; break;
                    case "request": if (!Member("fromAccountId") || !Member("toAccountId")) return; break;
                    case "block": if (!Member("blockerAccountId") || !Member("blockedAccountId")) return; break;
                    case "message":
                    {
                        if (!root.TryGetProperty("message", out var message) || !message.TryGetProperty("conversationId", out var conversationField)
                            || !conversationField.TryGetGuid(out var conversationId)) return;
                        var conversation = await db.Conversations.AsNoTracking().SingleOrDefaultAsync(c => c.Id == conversationId, token);
                        if (conversation is not null) accounts.UnionWith([conversation.PairLow, conversation.PairHigh]);
                        else
                        {
                            var relationship = await db.EvidenceItems.AsNoTracking().FirstOrDefaultAsync(e => e.HoldId == hold.Id && e.Kind == "conversation" && e.RecordId == conversationId.ToString(), token);
                            if (relationship is null) return;
                            using var relationshipDocument = JsonDocument.Parse(evidence.Open(relationship));
                            if (!relationshipDocument.RootElement.TryGetProperty("pairLow", out var low) || !low.TryGetGuid(out var lowId)
                                || !relationshipDocument.RootElement.TryGetProperty("pairHigh", out var high) || !high.TryGetGuid(out var highId)) return;
                            accounts.UnionWith([lowId, highId]);
                        }
                        break;
                    }
                    case "account": case "audit": case "contact-history": case "identity": case "identity-record": case "media-item":
                    case "mode": case "phone": case "profile": case "profile-version": case "verification": break;
                    default: return; // Unknown schema/provenance requires legal review, never an empty-reference inference.
                }
            }
            catch (Exception error) when (error is System.Security.Cryptography.CryptographicException or JsonException or FormatException or KeyNotFoundException or InvalidOperationException) { return; }
        }
        foreach (var id in cases)
        {
            // Missing review clocks remain non-disposable; copied relationship metadata can still establish conflicts.
            accounts.UnionWith(await CaseAccountsAsync(db, id, token));
        }
        hold.RetentionCaseIds = cases.Order().ToArray();
        hold.RelatedAccountIds = accounts.Order().ToArray();
        await db.SaveChangesAsync(token);
    }

    public async Task<EvidenceRetentionDate> ForHoldAsync(LegalHold hold, CancellationToken token)
    {
        if (hold.Status != LegalHoldStatus.Released) return new(null, "live-hold");
        if (hold.ReleasedAt is null) return new(null, "missing-release-provenance");
        if (hold.RetentionCaseIds is null || hold.RelatedAccountIds is null) return new(null, "retention-review-required");
        DateTimeOffset? until = hold.ReleasedAt;
        foreach (var id in hold.RetentionCaseIds)
        {
            var item = await db.ModerationCases.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, token);
            if (item is null) return new(null, "missing-review-provenance");
            var window = await CaseWindowAsync(item, token);
            if (window.ContentUntil is null) return new(null, "review-open");
            if (window.ContentUntil > until) until = window.ContentUntil;
        }
        var policeIds = await db.EvidenceExports.Where(e => e.HoldId == hold.Id && e.LeCaseId != null).Select(e => e.LeCaseId!.Value).ToListAsync(token);
        if (hold.LeCaseId is { } ownPolice) policeIds.Add(ownPolice);
        foreach (var police in policeIds.Distinct())
        {
            var item = await db.LeCases.AsNoTracking().SingleOrDefaultAsync(c => c.Id == police, token);
            if (item is not { Status: LeCaseStatus.Closed, ClosedAt: not null, RetainUntil: not null }) return new(null, "police-case-open-or-unknown");
            if (item.RetainUntil > until) until = item.RetainUntil;
        }
        if (policeIds.Count == 0 && hold.RetentionCaseIds.Length == 0 && hold.NoReviewRetentionConfirmedAt is null)
            return new(null, "retention-review-required");
        return new(until, policeIds.Count > 0 ? "police-schedule" : hold.RetentionCaseIds.Length > 0 ? "review-schedule" : "legal-no-review-disposition");
    }

    public async Task<bool> RetainedAccessibleAsync(LegalHold hold, DateTimeOffset now, CancellationToken token)
    {
        if (hold.Status != LegalHoldStatus.Released || !await db.EvidenceItems.AnyAsync(i => i.HoldId == hold.Id, token)) return false;
        var date = await ForHoldAsync(hold, token);
        if (date.Until is null || date.Until > now) return true; // Includes elevated legal review of unresolved provenance.
        var held = await evidence.TryHeldAccountsAsync(token);
        return held is not null && (await HoldAccountsAsync(db, hold, token)).Any(held.Contains);
    }

    public async Task<bool> ConfirmNoReviewAsync(LegalHold hold, Guid actor, string? reason, bool confirmed, DateTimeOffset now, CancellationToken token)
    {
        await using var barrier = await PreservationLease.ExclusiveAsync(db, token);
        await using var holdMutex = await PreservationLease.HoldAsync(db, hold.Id, token);
        await db.Entry(hold).ReloadAsync(token);
        if (!confirmed || reason?.Trim() is not { Length: >= 20 and <= LegalHoldPolicy.TextMax } note
            || hold.Status != LegalHoldStatus.Released || hold.Source != "staff" || hold.CaseId is not null || hold.LeCaseId is not null
            || !LawEnforcementPolicy.IndependentRelease(hold, actor)) return false;
        if (hold.NoReviewRetentionConfirmedAt is not null) return true;
        var scopeAccounts = await HoldAccountsAsync(db, hold, token);
        var live = await evidence.TryHeldAccountsAsync(token);
        if (live is null || scopeAccounts.Any(live.Contains)) return false;
        await ResolveProvenanceAsync(hold, token);
        if (hold.RetentionCaseIds is null || hold.RelatedAccountIds is null || hold.RetentionCaseIds.Length > 0 || await db.Disclosures.AnyAsync(d => d.HoldId == hold.Id, token)
            || await db.EvidenceExports.AnyAsync(e => e.HoldId == hold.Id && e.LeCaseId != null, token)) return false;
        // The independent officer attests after reviewing provenance; missing links alone never authorise destruction.
        foreach (var copy in await db.EvidenceItems.Where(e => e.HoldId == hold.Id).ToListAsync(token))
        {
            if (copy.Kind is "case" or "report" or "case-evidence") return false;
            try { using var document = JsonDocument.Parse(evidence.Open(copy)); }
            catch (Exception error) when (error is System.Security.Cryptography.CryptographicException or JsonException or FormatException or KeyNotFoundException or InvalidOperationException) { return false; }
        }
        hold.RetentionCaseIds = [];
        hold.NoReviewRetentionConfirmedAt = now;
        hold.NoReviewRetentionConfirmedBy = actor;
        hold.NoReviewRetentionReason = (hold.NoReviewRetentionReason is { Length: > 0 } history ? history + "\n" : "")
            + JsonSerializer.Serialize(new { actor, at = now, reason = note });
        await evidence.CustodyAsync(hold.Id, actor, "retention-no-review", "history-sha256:" + Convert.ToHexStringLower(EvidenceSeal.Hash(hold.NoReviewRetentionReason)), now, token);
        Audit.Record(db, now, actor, "account", "hold.retention-disposition", "legal-hold", hold.Id.ToString(), "no-review");
        await db.SaveChangesAsync(token);
        var held = await evidence.TryHeldAccountsAsync(token);
        if (held is not null && !(await HoldAccountsAsync(db, hold, token)).Any(held.Contains)) await DisposeCopiesAsync(hold, now, token);
        return true;
    }

    public async Task<bool> MediaRequiredAsync(MediaItem item, CancellationToken token)
    {
        var live = await evidence.TryLiveAsync(token);
        if (live is null) return true;
        if (item.Purpose == AttachmentPolicy.Purpose && item.ConversationId is { } conversationId)
        {
            var conversation = await db.Conversations.AsNoTracking().SingleOrDefaultAsync(c => c.Id == conversationId, token);
            if (conversation is not null)
                foreach (var message in await db.Messages.AsNoTracking().Where(m => m.MediaId == item.Id).ToListAsync(token))
                    if (EvidencePreservation.MessageHeld(message, conversation, live.Value.Holds, live.Value.Scopes)) return true;
        }
        if (item.CaseId is { } own && await db.ModerationCases.AnyAsync(c => c.Id == own && c.EvidencePurgedAt == null, token)) return true;
        // Released but still-required copies reference the actual bytes too, not merely an object key.
        if (await db.EvidenceItems.AnyAsync(e => e.Kind == "media-item" && e.RecordId == item.Id.ToString(), token)) return true;
        if (await db.CaseEvidence.AnyAsync(e => e.MediaIds != null && e.MediaIds.Contains(item.Id), token)) return true;
        var cases = db.ModerationCases.Where(c => c.SubjectAccountId == item.AccountId
            || db.Reports.Any(r => r.CaseId == c.Id && r.ReporterAccountId == item.AccountId)).Select(c => c.Id);
        foreach (var legacy in await db.CaseEvidence.AsNoTracking().Where(e => e.MediaIds == null && cases.Contains(e.CaseId)).ToListAsync(token))
        {
            var references = ReviewMediaReferences.Read(legacy.Kind, legacy.Snapshot);
            if (!references.Known || references.Ids.Contains(item.Id)) return true;
        }
        return false;
    }

    public async Task<bool> AccountRecordsRequiredAsync(Guid account, CancellationToken token) =>
        await db.ModerationCases.AnyAsync(c => c.SubjectAccountId == account, token)
        || await db.Reports.AnyAsync(r => r.ReporterAccountId == account || r.SubjectAccountId == account, token)
        || await db.EvidenceItems.AnyAsync(e => e.AccountId == account, token)
        || await db.MediaItems.AnyAsync(i => i.AccountId == account && i.ObjectKey != null, token);

    private async Task<bool> UnresolvedCopyObligationAsync(Guid review, IReadOnlyList<Guid> accounts, CancellationToken token) =>
        await db.LegalHolds.AnyAsync(h => (h.RetentionCaseIds == null || h.RelatedAccountIds == null)
            && db.EvidenceItems.Any(e => e.HoldId == h.Id)
            && (h.CaseId == review || db.LegalHoldScopes.Any(s => s.HoldId == h.Id && s.Kind == HoldScopeKind.Account && accounts.Contains(s.TargetId))), token);

    public async Task<SafetyRetentionRun> PurgeAsync(DateTimeOffset now, CancellationToken token, Guid? onlyCase = null, Guid? onlyAccount = null)
    {
        if (await evidence.TryHeldAccountsAsync(token) is null) return new(0, 0, 0, 0, true);
        var (contents, copies, records, reviews) = (0, 0, 0, 0);
        // Establish legacy references before any case/report source row can be disposed.
        foreach (var hold in await db.LegalHolds.Where(h => (h.RetentionCaseIds == null || h.RelatedAccountIds == null) && db.EvidenceItems.Any(e => e.HoldId == h.Id)).ToListAsync(token))
            await ResolveProvenanceAsync(hold, token);
        await foreach (var id in ClosedCaseIdsAsync(onlyCase, onlyAccount, token))
        {
            await using var barrier = await PreservationLease.SharedAsync(db, token);
            await using var caseMutex = await PreservationLease.CaseAsync(db, id, token);
            var item = await db.ModerationCases.SingleOrDefaultAsync(c => c.Id == id, token);
            if (item is null) continue;
            await db.Entry(item).ReloadAsync(token);
            var accounts = await CaseAccountsAsync(db, id, token);
            var held = await evidence.TryHeldAccountsAsync(token);
            if (held is null) return new(records, contents, copies, reviews, true);
            if (accounts.Any(held.Contains) || await db.LegalHolds.AnyAsync(h => h.Status != LegalHoldStatus.Released
                && (h.CaseId == id || h.RetentionCaseIds != null && h.RetentionCaseIds.Contains(id)), token)) continue;
            if (await UnresolvedCopyObligationAsync(id, accounts, token)) { reviews++; continue; }
            var window = await CaseWindowAsync(item, token);
            if (item.EvidencePurgedAt is null && SafetyRetentionPolicy.MayDispose(window.ContentUntil, now, false))
            {
                var legacy = await db.CaseEvidence.Where(e => e.CaseId == id && e.MediaIds == null).ToListAsync(token);
                if (legacy.Any(e => !ReviewMediaReferences.Read(e.Kind, e.Snapshot).Known)) { reviews++; continue; }
                await using var transaction = await db.Database.BeginTransactionAsync(token);
                await db.CaseEvidence.Where(e => e.CaseId == id).ExecuteDeleteAsync(token);
                await db.Reports.Where(r => r.CaseId == id).ExecuteUpdateAsync(u => u.SetProperty(r => r.Text, (string?)null).SetProperty(r => r.PoliceReference, (byte[]?)null), token);
                var sanctions = db.Sanctions.Where(s => s.CaseId == id).Select(s => s.Id);
                await db.Appeals.Where(a => sanctions.Contains(a.SanctionId)).ExecuteUpdateAsync(u => u.SetProperty(a => a.Text, (string?)null), token);
                await db.Sanctions.Where(s => s.CaseId == id).ExecuteUpdateAsync(u => u.SetProperty(s => s.Reason, "Historical enforcement decision; detailed record retention ended."), token);
                item.EvidencePurgedAt = now;
                Audit.Record(db, now, null, "retention", "case.content-expired", "moderation-case", id.ToString());
                await db.SaveChangesAsync(token);
                await transaction.CommitAsync(token);
                contents++;
            }
        }
        // A released copy may still be needed by its review or police-case schedule, or by an overlapping live hold.
        var released = await db.LegalHolds.Where(h => h.Status == LegalHoldStatus.Released && db.EvidenceItems.Any(e => e.HoldId == h.Id)
            && (onlyCase == null || h.CaseId == onlyCase || h.RetentionCaseIds != null && h.RetentionCaseIds.Contains(onlyCase.Value))
            && (onlyAccount == null || h.RelatedAccountIds != null && h.RelatedAccountIds.Contains(onlyAccount.Value))).ToListAsync(token);
        foreach (var hold in released)
        {
            await using var barrier = await PreservationLease.SharedAsync(db, token);
            await using var holdMutex = await PreservationLease.HoldAsync(db, hold.Id, token);
            await db.Entry(hold).ReloadAsync(token);
            var accounts = await HoldAccountsAsync(db, hold, token);
            var held = await evidence.TryHeldAccountsAsync(token);
            if (held is null) return new(records, contents, copies, reviews, true);
            if (accounts.Any(held.Contains)) continue;
            var date = await ForHoldAsync(hold, token);
            if (!SafetyRetentionPolicy.MayDispose(date.Until, now, false)) { if (date.Reason.Contains("unknown") || date.Reason.Contains("required")) reviews++; continue; }
            copies += await DisposeCopiesAsync(hold, now, token);
        }
        await foreach (var id in ClosedCaseIdsAsync(onlyCase, onlyAccount, token))
        {
            await using var barrier = await PreservationLease.SharedAsync(db, token);
            await using var caseMutex = await PreservationLease.CaseAsync(db, id, token);
            var item = await db.ModerationCases.SingleOrDefaultAsync(c => c.Id == id, token);
            if (item is null) continue;
            await db.Entry(item).ReloadAsync(token);
            var held = await evidence.TryHeldAccountsAsync(token);
            if (held is null) return new(records, contents, copies, reviews, true);
            var accounts = await CaseAccountsAsync(db, id, token);
            if (await UnresolvedCopyObligationAsync(id, accounts, token)) { reviews++; continue; }
            if (accounts.Any(held.Contains) || item.EvidencePurgedAt is null
                || !SafetyRetentionPolicy.MayDispose((await CaseWindowAsync(item, token)).RecordsUntil, now, false)) continue;
            // Retain positive case provenance until the operational hold/export/custody group has expired too.
            if (await db.LegalHolds.AnyAsync(h => h.CaseId == id || h.RetentionCaseIds != null && h.RetentionCaseIds.Contains(id), token)) continue;
            // D217: preserve the minimal case reference while registry/history rows still require it. Content above
            // follows its own clock; active anti-abuse necessity does not retain original evidence indefinitely.
            if (await db.BannedIdentifiers.AnyAsync(b => b.CaseId == id,token) || await db.BanDispositions.AnyAsync(d=>d.CaseId==id,token)
                || await db.EmailRetirementOperations.AnyAsync(o=>o.CaseId==id,token)) continue;
            await db.ModerationCases.Where(c => c.Id == id).ExecuteDeleteAsync(token);
            Audit.Record(db, now, null, "retention", "case.records-expired", "moderation-case", id.ToString());
            await db.SaveChangesAsync(token);
            records++;
        }
        return new(records, contents, copies, reviews);
    }

    // Keyset paging makes progress past arbitrarily many held/unresolved cases. Re-query on the record pass;
    // disposal can delete earlier rows, so offset paging would skip records.
    private async IAsyncEnumerable<Guid> ClosedCaseIdsAsync(Guid? onlyCase, Guid? onlyAccount, [EnumeratorCancellation] CancellationToken token)
    {
        Guid? after = null;
        while (true)
        {
            var page = await db.Database.SqlQuery<Guid>($"""
                SELECT c.id AS "Value" FROM moderation_cases c
                WHERE c.status = 'Closed' AND ({onlyCase}::uuid IS NULL OR c.id = {onlyCase})
                  AND ({onlyAccount}::uuid IS NULL OR c.subject_account_id = {onlyAccount}
                       OR EXISTS (SELECT 1 FROM reports r WHERE r.case_id = c.id AND r.reporter_account_id = {onlyAccount}))
                  AND ({after}::uuid IS NULL OR c.id > {after}) ORDER BY c.id LIMIT 500
                """).ToListAsync(token);
            if (page.Count == 0) yield break;
            foreach (var id in page) yield return id;
            after = page[^1];
        }
    }

    public async Task<int> DisposeCopiesAsync(LegalHold hold, DateTimeOffset now, CancellationToken token)
    {
        await using var barrier = await PreservationLease.SharedAsync(db, token);
        await using var holdMutex = await PreservationLease.HoldAsync(db, hold.Id, token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var count = await db.EvidenceItems.Where(e => e.HoldId == hold.Id).ExecuteDeleteAsync(token);
        if (count > 0)
        {
            await evidence.CustodyAsync(hold.Id, null, "retention-expired", "items-erased:" + count, now, token);
            Audit.Record(db, now, null, "retention", "hold.copies-expired", "legal-hold", hold.Id.ToString(), "items:" + count);
            await db.SaveChangesAsync(token);
        }
        await transaction.CommitAsync(token);
        return count;
    }
}
