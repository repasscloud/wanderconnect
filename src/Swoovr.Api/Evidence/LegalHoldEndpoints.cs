using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Evidence;

/// <summary>
/// Legal holds, evidence exports and the disclosure register (policy §3, §11, §13; owner Q76, D189). Admin API only
/// (D136). Only the `legal` role (security / legal officer) reads, confirms, reviews, captures, releases and exports;
/// an admin may open a provisional hold from a case. Audit entries carry ids only; the custody chain records every step.
/// </summary>
public static class LegalHoldEndpoints
{
    public static void Map(WebApplication app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization();

        admin.MapGet("/holds/overview", async (ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await StaffAsync(user, accounts, legalOnly: false, cancellationToken);
            if (denied is not null) return denied;
            var now = clock.GetUtcNow();
            var live = await accounts.Db!.LegalHolds.Where(h => h.Status != LegalHoldStatus.Released).Select(h => h.ReviewDue).ToListAsync(cancellationToken);
            return Results.Ok(new HoldOverview(live.Count, live.Count(d => d <= now)));
        });

        admin.MapGet("/holds", async (string? status, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (_, denied) = await StaffAsync(user, accounts, legalOnly: true, cancellationToken);
            if (denied is not null) return denied;
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            var query = status == "released" ? db.LegalHolds.Where(h => h.Status == LegalHoldStatus.Released) : db.LegalHolds.Where(h => h.Status != LegalHoldStatus.Released);
            // Overdue reviews first, then the soonest due.
            var holds = await query.OrderBy(h => h.ReviewDue).Take(200).ToListAsync(cancellationToken);
            var ids = holds.Select(h => h.Id).ToList();
            var scopes = await db.LegalHoldScopes.Where(s => ids.Contains(s.HoldId) && s.Kind == HoldScopeKind.Account).GroupBy(s => s.HoldId)
                .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
            var items = await db.EvidenceItems.Where(i => ids.Contains(i.HoldId)).GroupBy(i => i.HoldId)
                .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
            return Results.Ok(holds.Select(h => new LegalHoldSummary(h.Id, Wire(h.Status), h.Source, h.CaseId, h.PlacedAt, h.ReviewDue, LegalHoldPolicy.Overdue(h, now),
                scopes.GetValueOrDefault(h.Id), items.GetValueOrDefault(h.Id))).ToList());
        });

        admin.MapPost("/holds", async (NewLegalHold request, ClaimsPrincipal user, AccountResolver accounts, EvidencePreservation evidence, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await StaffAsync(user, accounts, legalOnly: false, cancellationToken);
            if (denied is not null) return denied;
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            var role = Role(current!);
            var accountIds = (request.AccountIds ?? []).Distinct().ToList();
            var conversationIds = (request.ConversationIds ?? []).Distinct().ToList();
            var refusal = LegalHoldPolicy.CheckPlace(role, request.Reason, accountIds.Count, request.CaseId, request.From, request.Until, request.ReviewDue, now);
            if (refusal != HoldRefusal.None) return Refused(refusal);
            if (!evidence.Configured) return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.EvidenceNotConfigured, "No evidence key is configured.");
            if (await db.Accounts.CountAsync(a => accountIds.Contains(a.Id) && a.Realm == AccountRealm.Members, cancellationToken) != accountIds.Count)
                return Refused(HoldRefusal.ScopeRequired);
            if (conversationIds.Count > 0 && (conversationIds.Count > LegalHoldPolicy.ScopeMax || await db.Conversations.CountAsync(c => conversationIds.Contains(c.Id)
                    && (accountIds.Contains(c.PairLow) || accountIds.Contains(c.PairHigh)), cancellationToken) != conversationIds.Count))
                return Refused(HoldRefusal.ScopeRequired);
            // D195 (§7): a hold serving a law-enforcement case records it and the requesting body; a temporary police
            // preservation hold needs a verified case, covers only its subjects and is reviewed within 30 days.
            LeCase? leCase = null;
            if (request.Kind is not (null or LawEnforcementPolicy.PolicePreservation)) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.LeCaseInvalid, "Unknown hold kind.");
            if (request.LeCaseId is { } leCaseId)
            {
                if (role != AccountRole.Legal) return Results.Forbid();
                leCase = await db.LeCases.FindAsync([leCaseId], cancellationToken);
                if (leCase is null) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.LeCaseInvalid, "No such law-enforcement case.");
                var people = await db.LeCasePeople.Where(p => p.CaseId == leCaseId).ToListAsync(cancellationToken);
                if (LawEnforcementPolicy.Excluded(people, current!.Account!.Id)) return Results.Forbid();
                if (request.Kind == LawEnforcementPolicy.PolicePreservation)
                {
                    switch (LawEnforcementPolicy.CheckTemporaryHold(leCase, request.ReviewDue, now))
                    {
                        case LeRefusal.Closed: return Problem(StatusCodes.Status409Conflict, ProblemCodes.LeCaseClosed, "The case is closed.");
                        case LeRefusal.NotVerified: return Problem(StatusCodes.Status409Conflict, ProblemCodes.LeNotVerified, "Verify the requester independently first.");
                        case LeRefusal.ReviewTooFar: return Problem(StatusCodes.Status400BadRequest, ProblemCodes.LeDateInvalid, "A temporary police preservation hold is reviewed within 30 days.");
                    }
                    var subjects = people.Where(p => p.Kind == "subject").Select(p => p.AccountId).ToHashSet();
                    if (accountIds.Any(a => !subjects.Contains(a))) return Refused(HoldRefusal.ScopeRequired);
                }
            }
            else if (request.Kind == LawEnforcementPolicy.PolicePreservation)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.LeCaseInvalid, "A temporary police preservation hold belongs to a law-enforcement case.");
            if (request.LawfulBasis is { Length: > LawEnforcementPolicy.ShortMax }) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.LeCaseInvalid, "Shorten the lawful basis.");
            if (request.CaseId is { } caseId)
            {
                var item = await db.ModerationCases.FindAsync([caseId], cancellationToken);
                if (item is null) return Refused(HoldRefusal.CaseRequired);
                // An admin's provisional hold covers only the case's own people (subject and reporters): narrowly scoped (§3).
                if (role == AccountRole.Admin)
                {
                    var people = await db.Reports.Where(r => r.CaseId == caseId).Select(r => r.ReporterAccountId).ToListAsync(cancellationToken);
                    people.Add(item.SubjectAccountId);
                    if (accountIds.Any(a => !people.Contains(a))) return Refused(HoldRefusal.ScopeRequired);
                }
            }
            var review = request.Kind == LawEnforcementPolicy.PolicePreservation ? request.ReviewDue ?? now + LawEnforcementPolicy.TemporaryHoldMax : request.ReviewDue;
            var hold = await PlaceAsync(db, evidence, role, current!.Account!.Id, request.Reason!.Trim(), accountIds, conversationIds, request.From, request.Until,
                review, request.ReleaseConditions, request.CaseId, "staff", now, cancellationToken, leCase, request.Kind, request.LawfulBasis?.Trim());
            // A summary only: an admin who opened a provisional hold doesn't get to read it.
            return Results.Created($"/api/admin/holds/{hold.Id}", new LegalHoldSummary(hold.Id, Wire(hold.Status), hold.Source, hold.CaseId, hold.PlacedAt,
                hold.ReviewDue, false, accountIds.Count, await db.EvidenceItems.CountAsync(i => i.HoldId == hold.Id, cancellationToken)));
        });

        admin.MapGet("/holds/{id:guid}", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, EvidencePreservation evidence, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, hold, denied) = await HoldAsync(user, accounts, id, cancellationToken);
            if (denied is not null) return denied;
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            await evidence.CustodyAsync(hold!.Id, current!.Account!.Id, "viewed", null, now, cancellationToken);
            Audit.Record(db, now, current.Account.Id, "account", "hold.viewed", "legal-hold", hold.Id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            var detail = await DetailAsync(db, hold, now, cancellationToken);
            // #89: a broken custody chain is a critical security incident.
            if (!detail.ChainIntact)
                await accounts.Services.GetRequiredService<Security.SecurityAlerts>().ChainBrokenAsync("custody-chain-broken", $"The custody chain of hold {hold.Id} is broken.", now, cancellationToken);
            return Results.Ok(detail);
        });

        admin.MapPost("/holds/{id:guid}/{step:regex(^(confirm|capture)$)}", async (Guid id, string step, ClaimsPrincipal user, AccountResolver accounts,
            EvidencePreservation evidence, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, hold, denied) = await HoldAsync(user, accounts, id, cancellationToken);
            if (denied is not null) return denied;
            var (db, now, me) = (accounts.Db!, clock.GetUtcNow(), current!.Account!.Id);
            var refusal = LegalHoldPolicy.CheckManage(AccountRole.Legal, hold!);
            if (refusal != HoldRefusal.None) return Refused(refusal);
            if (step == "confirm")
            {
                if (hold!.Status != LegalHoldStatus.Provisional) return Refused(HoldRefusal.NotProvisional);
                (hold.Status, hold.ConfirmedBy, hold.ConfirmedAt, hold.ReviewDue) = (LegalHoldStatus.Active, me, now, now + LegalHoldPolicy.DefaultReview);
                await evidence.CustodyAsync(hold.Id, me, "confirmed", null, now, cancellationToken);
            }
            else
            {
                if (!evidence.Configured) return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.EvidenceNotConfigured, "No evidence key is configured.");
                await evidence.CaptureAsync(hold!, me, now, cancellationToken);
            }
            Audit.Record(db, now, me, "account", step == "confirm" ? "hold.confirmed" : "hold.captured", "legal-hold", hold!.Id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(await DetailAsync(db, hold, now, cancellationToken));
        });

        admin.MapPost("/holds/{id:guid}/review", async (Guid id, HoldReviewRequest request, ClaimsPrincipal user, AccountResolver accounts, EvidencePreservation evidence,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, hold, denied) = await HoldAsync(user, accounts, id, cancellationToken);
            if (denied is not null) return denied;
            var (db, now, me) = (accounts.Db!, clock.GetUtcNow(), current!.Account!.Id);
            var due = request.ReviewDue ?? now + LegalHoldPolicy.DefaultReview;
            var refusal = LegalHoldPolicy.CheckReview(AccountRole.Legal, hold!, due, now);
            if (refusal != HoldRefusal.None) return Refused(refusal);
            hold!.ReviewDue = due;
            await evidence.CustodyAsync(hold.Id, me, "reviewed", "due:" + due.ToString("yyyy-MM-dd"), now, cancellationToken);
            Audit.Record(db, now, me, "account", "hold.reviewed", "legal-hold", hold.Id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(await DetailAsync(db, hold, now, cancellationToken));
        });

        admin.MapPost("/holds/{id:guid}/release", async (Guid id, HoldReleaseRequest request, ClaimsPrincipal user, AccountResolver accounts, EvidencePreservation evidence,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, hold, denied) = await HoldAsync(user, accounts, id, cancellationToken);
            if (denied is not null) return denied;
            var (db, now, me) = (accounts.Db!, clock.GetUtcNow(), current!.Account!.Id);
            // D195 (§7): an independent officer, after checking every overlapping hold.
            var (live, scopes) = await evidence.LiveAsync(cancellationToken);
            var overlaps = LegalHoldPolicy.Overlaps(hold!, live, scopes);
            var refusal = LegalHoldPolicy.CheckRelease(AccountRole.Legal, hold!, request.Reason, me, overlaps, request.OverlapsChecked);
            if (refusal != HoldRefusal.None)
            {
                if (refusal == HoldRefusal.NotIndependent)
                {
                    await evidence.CustodyAsync(hold!.Id, me, "release-refused", "not-independent", now, cancellationToken);
                    Audit.Record(db, now, me, "account", "hold.release-refused", "legal-hold", hold.Id.ToString(), "not-independent");
                    await db.SaveChangesAsync(cancellationToken);
                }
                return Refused(refusal);
            }
            await evidence.ReleaseAsync(hold!, me, request.Reason!, now, cancellationToken, overlaps);
            Audit.Record(db, now, me, "account", "hold.released", "legal-hold", hold!.Id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(await DetailAsync(db, hold, now, cancellationToken));
        });

        admin.MapPost("/holds/{id:guid}/exports", async (Guid id, EvidenceExportRequest request, ClaimsPrincipal user, AccountResolver accounts,
            EvidencePreservation evidence, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, hold, denied) = await HoldAsync(user, accounts, id, cancellationToken);
            if (denied is not null) return denied;
            var (db, now, me) = (accounts.Db!, clock.GetUtcNow(), current!.Account!.Id);
            var refusal = LegalHoldPolicy.CheckManage(AccountRole.Legal, hold!);
            if (refusal != HoldRefusal.None) return Refused(refusal);
            if (string.IsNullOrWhiteSpace(request.Purpose) || request.Purpose.Length > LegalHoldPolicy.TextMax)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.DisclosureInvalid, "Record the purpose of this export.");
            // #88 (policy §11, §15): exporting needs live time-limited access; a bulk export also needs a second officer's approval.
            if (await StaffAccessEndpoints.GrantAsync(db, me, StaffAccessPolicy.EvidenceExport, now, cancellationToken) is null)
                return StaffAccessEndpoints.NeedsGrant(StaffAccessPolicy.EvidenceExport);
            ExportApproval? approval = null;
            var scopeCount = await db.LegalHoldScopes.CountAsync(s => s.HoldId == hold!.Id && s.Kind == HoldScopeKind.Account, cancellationToken);
            if (StaffAccessPolicy.Bulk(scopeCount, await db.EvidenceItems.CountAsync(i => i.HoldId == hold!.Id, cancellationToken)))
            {
                approval = request.ApprovalId is { } approvalId ? await db.ExportApprovals.FindAsync([approvalId], cancellationToken) : null;
                if (!StaffAccessPolicy.ApprovalUsable(approval, me, hold!.Id, now))
                {
                    var pending = await db.ExportApprovals.Where(a => a.HoldId == hold.Id && a.RequestedBy == me && a.UsedAt == null && a.RequestedAt > now - StaffAccessPolicy.ApprovalLifetime)
                        .OrderByDescending(a => a.RequestedAt).FirstOrDefaultAsync(cancellationToken);
                    if (pending is null)
                    {
                        pending = new ExportApproval { Id = Guid.NewGuid(), HoldId = hold.Id, RequestedBy = me, Purpose = request.Purpose.Trim(), RequestedAt = now };
                        db.ExportApprovals.Add(pending);
                        Audit.Record(db, now, me, "account", "evidence.bulk-requested", "legal-hold", hold.Id.ToString(), pending.Id.ToString());
                        await db.SaveChangesAsync(cancellationToken);
                        // #89: a bulk export alerts the escalation contacts (ids only).
                        await accounts.Services.GetRequiredService<Security.SecurityAlerts>().BulkExportAsync(me, hold.Id, pending.Id, now, cancellationToken);
                    }
                    return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "A bulk export needs another officer's approval.",
                        extensions: new Dictionary<string, object?> { ["code"] = ProblemCodes.BulkExportApprovalRequired, ["approvalId"] = pending.Id });
                }
            }
            // D195 (§8): an export for a case must be from a hold serving that case; a correction names the downloaded package
            // it supplements and why, and never replaces it; redactions apply to the disclosure copy only (§6).
            if (request.LeCaseId is { } exportCase && hold!.LeCaseId != exportCase)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.LeCaseInvalid, "This hold doesn't serve that case.");
            if (request.SupplementsExportId is { } supplements)
            {
                var original = await db.EvidenceExports.FindAsync([supplements], cancellationToken);
                if (original is null || original.HoldId != hold!.Id || original.PackageSha256 is null || string.IsNullOrWhiteSpace(request.CorrectionReason)
                    || request.CorrectionReason.Length > LegalHoldPolicy.TextMax)
                    return Problem(StatusCodes.Status400BadRequest, ProblemCodes.LeCaseInvalid, "A correction names a downloaded package of this hold and says why.");
            }
            else if (request.CorrectionReason is not null) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.LeCaseInvalid, "Name the package this corrects.");
            var redactions = request.Redactions ?? [];
            if (redactions.Count > 0)
            {
                var itemIds = redactions.Select(r => r.ItemId).Distinct().ToList();
                if (redactions.Count > 200 || await db.EvidenceItems.CountAsync(i => i.HoldId == hold!.Id && itemIds.Contains(i.Id), cancellationToken) != itemIds.Count
                    || redactions.Any(r => r.Fields is not { Count: > 0 and <= 20 } || r.Fields.Any(f => string.IsNullOrWhiteSpace(f) || f.Length > 100)
                        || string.Join(',', r.Fields).Length > 500 || string.IsNullOrWhiteSpace(r.Reason) || r.Reason.Length > LegalHoldPolicy.TextMax))
                    return Problem(StatusCodes.Status400BadRequest, ProblemCodes.LeCaseInvalid, "Each redaction names an item of this hold, its fields and a reason.");
            }
            Disclosure? disclosure = null;
            if (request.Disclosure is { } d)
            {
                if (!LegalHoldPolicy.ValidDisclosure(d.Agency, d.Officer, d.Reference, d.LegalBasis, d.VerifiedHow))
                    return Problem(StatusCodes.Status400BadRequest, ProblemCodes.DisclosureInvalid, "A disclosure needs the agency, officer, reference, legal basis and how the request was verified.");
                disclosure = new Disclosure
                {
                    Id = Guid.NewGuid(), HoldId = hold!.Id, Agency = d.Agency!.Trim(), Officer = d.Officer!.Trim(), Reference = d.Reference!.Trim(),
                    LegalBasis = d.LegalBasis!, VerifiedHow = d.VerifiedHow!.Trim(), DisclosedBy = me, DisclosedAt = now,
                };
                db.Disclosures.Add(disclosure);
                await evidence.CustodyAsync(hold.Id, me, "disclosed", $"disclosure:{disclosure.Id};basis:{disclosure.LegalBasis}", now, cancellationToken);
            }
            var secret = RandomNumberGenerator.GetBytes(32);
            var export = new EvidenceExport
            {
                Id = Guid.NewGuid(), HoldId = hold!.Id, TokenHash = SHA256.HashData(secret), CreatedBy = me, CreatedAt = now,
                ExpiresAt = now + LegalHoldPolicy.ExportLifetime, Purpose = request.Purpose.Trim(), DisclosureId = disclosure?.Id,
                LeCaseId = request.LeCaseId, SupplementsExportId = request.SupplementsExportId, CorrectionReason = request.CorrectionReason?.Trim(),
            };
            db.EvidenceExports.Add(export);
            if (approval is not null) approval.UsedAt = now;
            db.EvidenceRedactions.AddRange(redactions.Select(r => new EvidenceRedaction
            {
                Id = Guid.NewGuid(), ExportId = export.Id, ItemId = r.ItemId, Fields = string.Join(',', r.Fields!.Select(f => f.Trim())), Reason = r.Reason!.Trim(),
                RedactedBy = me, At = now,
            }));
            await evidence.CustodyAsync(hold.Id, me, "export-created", $"export:{export.Id}" + (redactions.Count > 0 ? $";redactions:{redactions.Count}" : "")
                + (export.SupplementsExportId is { } s ? $";supplements:{s}" : ""), now, cancellationToken);
            Audit.Record(db, now, me, "account", "evidence.export-created", "legal-hold", hold.Id.ToString(), disclosure is null ? null : "disclosure");
            await db.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/admin/evidence-exports/{Base64Url(secret)}", new EvidenceExportLink(Base64Url(secret), export.ExpiresAt));
        });

        admin.MapGet("/evidence-exports/{token}", async (string token, ClaimsPrincipal user, AccountResolver accounts, EvidencePreservation evidence,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await StaffAsync(user, accounts, legalOnly: true, cancellationToken);
            if (denied is not null) return denied;
            var (db, now, me) = (accounts.Db!, clock.GetUtcNow(), current!.Account!.Id);
            byte[] hash;
            try { hash = SHA256.HashData(Convert.FromBase64String(token.Replace('-', '+').Replace('_', '/').PadRight((token.Length + 3) / 4 * 4, '='))); }
            catch (FormatException) { return Unavailable(); }
            var export = await db.EvidenceExports.SingleOrDefaultAsync(e => e.TokenHash == hash, cancellationToken);
            var hold = export is null ? null : await db.LegalHolds.FindAsync([export.HoldId], cancellationToken);
            // Only the officer who created it, once, within 15 minutes, while the hold is live.
            if (!LegalHoldPolicy.ExportUsable(export, hold, now) || export!.CreatedBy != me) return Unavailable();
            // #88: the download needs the officer's access to still be live.
            if (await StaffAccessEndpoints.GrantAsync(db, me, StaffAccessPolicy.EvidenceExport, now, cancellationToken) is null)
                return StaffAccessEndpoints.NeedsGrant(StaffAccessPolicy.EvidenceExport);
            export.DownloadedAt = now;
            var items = await db.EvidenceItems.Where(i => i.HoldId == hold!.Id).OrderBy(i => i.Capture).ThenBy(i => i.Kind).ThenBy(i => i.RecordId).ToListAsync(cancellationToken);
            // D195 (§6): redactions change the disclosure copy only; the preserved original and its hash never change.
            var redactions = (await db.EvidenceRedactions.Where(r => r.ExportId == export.Id).ToListAsync(cancellationToken)).ToLookup(r => r.ItemId);
            var copies = items.Select(i =>
            {
                var original = evidence.Open(i);
                var applied = redactions[i.Id].ToList();
                if (applied.Count == 0) return (Item: i, Json: original, Hash: i.Sha256, Redactions: new List<object>());
                var (json, missing) = LawEnforcementPolicy.Redact(original, applied.SelectMany(r => r.Fields.Split(',')));
                return (Item: i, Json: json, Hash: EvidenceSeal.Hash(json),
                    Redactions: applied.Select(r => (object)new { r.Id, Fields = r.Fields.Split(','), r.Reason, r.RedactedBy, r.At, Missing = missing.Where(m => r.Fields.Split(',').Contains(m)) }).ToList());
            }).ToList();
            export.PackageSha256 = EvidenceSeal.PackageHash(copies.Select(c => c.Hash));
            await evidence.CustodyAsync(hold!.Id, me, "exported", $"export:{export.Id};items:{items.Count};redacted:{copies.Count(c => c.Redactions.Count > 0)}", now, cancellationToken);
            Audit.Record(db, now, me, "account", "evidence.exported", "legal-hold", hold.Id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            var scopes = await db.LegalHoldScopes.Where(s => s.HoldId == hold.Id).ToListAsync(cancellationToken);
            var custody = await db.CustodyEvents.Where(c => c.HoldId == hold.Id).OrderBy(c => c.Sequence).ToListAsync(cancellationToken);
            var disclosure = export.DisclosureId is { } did ? await db.Disclosures.FindAsync([did], cancellationToken) : null;
            var supplemented = export.SupplementsExportId is { } sid ? await db.EvidenceExports.FindAsync([sid], cancellationToken) : null;
            var leCase = export.LeCaseId is { } lid ? await db.LeCases.FindAsync([lid], cancellationToken) : null;
            var package = new
            {
                Format = "swoovr-evidence/1",
                GeneratedAt = now,
                GeneratedBy = me,
                export.Id,
                export.Purpose,
                Case = leCase is null ? null : new { leCase.Id, leCase.Reference },
                // D195 (§8): a correction is a new package that names the one it supplements; the original is never replaced.
                Supplements = supplemented is null ? null : new { supplemented.Id, PackageSha256 = Convert.ToHexStringLower(supplemented.PackageSha256!), export.CorrectionReason },
                Disclosure = disclosure is null ? null : View(disclosure),
                Hold = new
                {
                    hold.Id, Status = Wire(hold.Status), hold.Source, hold.Reason, hold.ReleaseConditions, hold.CaseId, hold.PlacedBy, hold.PlacedAt,
                    hold.ConfirmedBy, hold.ConfirmedAt, hold.ReviewDue, hold.From, hold.Until, hold.LeCaseId, hold.Kind, hold.LawfulBasis, hold.RequestingBody,
                    Accounts = scopes.Where(s => s.Kind == HoldScopeKind.Account).Select(s => s.TargetId),
                    Conversations = scopes.Where(s => s.Kind == HoldScopeKind.Conversation).Select(s => s.TargetId),
                },
                Items = copies.Select(c => new
                {
                    c.Item.Id, c.Item.Capture, c.Item.Kind, c.Item.Source, c.Item.RecordId, c.Item.AccountId, c.Item.CapturedAt, c.Item.CapturedBy,
                    // Sha256 is the preserved original's; DisclosedSha256 is of ContentJson as handed over (the same unless redacted).
                    Sha256 = Convert.ToHexStringLower(c.Item.Sha256),
                    DisclosedSha256 = Convert.ToHexStringLower(c.Hash),
                    Redacted = c.Redactions.Count > 0,
                    Redactions = c.Redactions,
                    ContentJson = c.Json,
                }),
                Manifest = new
                {
                    ItemCount = items.Count,
                    Algorithm = "DisclosedSha256 is SHA-256 of the UTF-8 ContentJson string (equal to Sha256, the preserved original's, unless the item was redacted); "
                        + "PackageSha256 is SHA-256 over the DisclosedSha256 values (raw bytes) in this order; OriginalPackageSha256 the same over Sha256",
                    PackageSha256 = Convert.ToHexStringLower(export.PackageSha256),
                    OriginalPackageSha256 = Convert.ToHexStringLower(EvidenceSeal.PackageHash(items.Select(i => i.Sha256))),
                    RedactedItems = copies.Count(c => c.Redactions.Count > 0),
                },
                Custody = new
                {
                    Intact = EvidenceSeal.ChainIntact(custody),
                    Events = custody.Select(c => new CustodyEventView(c.Sequence, c.At, c.ActorAccountId, c.Action, c.Detail, Convert.ToHexStringLower(c.Hash))),
                },
            };
            return Results.File(JsonSerializer.SerializeToUtf8Bytes(package, EvidencePreservation.Json), "application/json", $"swoovr-evidence-{hold.Id}.json");
        });

        admin.MapGet("/disclosures", async (ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (_, denied) = await StaffAsync(user, accounts, legalOnly: true, cancellationToken);
            if (denied is not null) return denied;
            var rows = await accounts.Db!.Disclosures.OrderByDescending(d => d.DisclosedAt).Take(500).ToListAsync(cancellationToken);
            return Results.Ok(rows.Select(View).ToList());
        });
    }

    /// <summary>
    /// Places a hold and captures what it covers at once. Used by staff and by a serious-incident report (Z), where
    /// Swoovr itself is the authoriser and the hold is provisional.
    /// </summary>
    public static async Task<LegalHold> PlaceAsync(SwoovrDbContext db, EvidencePreservation evidence, AccountRole role, Guid? actor, string reason,
        IReadOnlyCollection<Guid> accountIds, IReadOnlyCollection<Guid> conversationIds, DateTimeOffset? from, DateTimeOffset? until, DateTimeOffset? reviewDue,
        string? releaseConditions, Guid? caseId, string source, DateTimeOffset now, CancellationToken cancellationToken,
        LeCase? leCase = null, string? kind = null, string? lawfulBasis = null, bool deferCapture = false, Guid? sourceMediaId = null)
    {
        var status = LegalHoldPolicy.StatusFor(role);
        var hold = new LegalHold
        {
            Id = Guid.NewGuid(), Status = status, Reason = reason, ReleaseConditions = string.IsNullOrWhiteSpace(releaseConditions) ? null : releaseConditions.Trim(),
            Source = source, SourceMediaId = sourceMediaId, CaseId = caseId, PlacedBy = actor, PlacedByRole = role, PlacedAt = now, ReviewDue = LegalHoldPolicy.ReviewFor(status, reviewDue, now),
            From = from, Until = until, LeCaseId = leCase?.Id, RequestingBody = leCase?.Agency, Kind = kind,
            LawfulBasis = string.IsNullOrWhiteSpace(lawfulBasis) ? leCase?.LegalInstrument : lawfulBasis,
        };
        if (status == LegalHoldStatus.Active) (hold.ConfirmedBy, hold.ConfirmedAt) = (actor, now);
        db.LegalHolds.Add(hold);
        db.LegalHoldScopes.AddRange(accountIds.Select(a => new LegalHoldScope { HoldId = hold.Id, Kind = HoldScopeKind.Account, TargetId = a }));
        db.LegalHoldScopes.AddRange(conversationIds.Select(c => new LegalHoldScope { HoldId = hold.Id, Kind = HoldScopeKind.Conversation, TargetId = c }));
        await db.SaveChangesAsync(cancellationToken);
        await evidence.CustodyAsync(hold.Id, actor, "placed", $"{Wire(status)};{source};accounts:{accountIds.Count};conversations:{conversationIds.Count}", now, cancellationToken);
        if (!deferCapture) await evidence.CaptureAsync(hold, actor, now, cancellationToken);
        Audit.Record(db, now, actor, actor is null ? "system" : "account", "hold.placed", "legal-hold", hold.Id.ToString(), $"{Wire(status)}:{source}");
        await db.SaveChangesAsync(cancellationToken);
        return hold;
    }

    private static AccountRole Role(CurrentAccount current) =>
        current.Allows(AccountRole.Legal) ? AccountRole.Legal : current.Allows(AccountRole.Admin) ? AccountRole.Admin : AccountRole.Unknown;

    /// <summary>`legal` only, or (for placing and the overview) `legal` or an admin. Support never reaches holds.</summary>
    private static async Task<(CurrentAccount? Current, IResult? Denied)> StaffAsync(ClaimsPrincipal user, AccountResolver accounts, bool legalOnly,
        CancellationToken cancellationToken)
    {
        var current = await accounts.ResolveAsync(user, cancellationToken);
        if (current is null) return (null, Results.Unauthorized());
        if (current.Account is null) return (null, Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.PersistenceUnavailable, "Accounts are not available."));
        var role = Role(current);
        return role == AccountRole.Legal || !legalOnly && role == AccountRole.Admin ? (current, null) : (null, Results.Forbid());
    }

    private static async Task<(CurrentAccount? Current, LegalHold? Hold, IResult? Denied)> HoldAsync(ClaimsPrincipal user, AccountResolver accounts, Guid id,
        CancellationToken cancellationToken)
    {
        var (current, denied) = await StaffAsync(user, accounts, legalOnly: true, cancellationToken);
        if (denied is not null) return (null, null, denied);
        var hold = await accounts.Db!.LegalHolds.FindAsync([id], cancellationToken);
        if (hold is null) return (null, null, Results.NotFound());
        // D195 (§5): staff implicated in the hold's law-enforcement case never reach it or its evidence.
        if (hold.LeCaseId is { } leCase && await accounts.Db.LeCasePeople.AnyAsync(p => p.CaseId == leCase && p.Kind == "implicated-staff" && p.AccountId == current!.Account!.Id, cancellationToken))
            return (null, null, Results.Forbid());
        // #88 (§5): nor does an officer with a conflict of interest over anyone the hold names.
        if (await StaffAccessEndpoints.ExcludedAsync(accounts.Db, current!.Account!.Id, await StaffAccessEndpoints.HoldAccountsAsync(accounts.Db, hold.Id, cancellationToken), cancellationToken))
            return (null, null, StaffAccessEndpoints.Conflict());
        return (current, hold, null);
    }

    private static async Task<LegalHoldDetail> DetailAsync(SwoovrDbContext db, LegalHold hold, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var scopes = await db.LegalHoldScopes.Where(s => s.HoldId == hold.Id).ToListAsync(cancellationToken);
        var items = await db.EvidenceItems.Where(i => i.HoldId == hold.Id).OrderBy(i => i.Capture).ThenBy(i => i.Kind).ThenBy(i => i.RecordId).ToListAsync(cancellationToken);
        var custody = await db.CustodyEvents.Where(c => c.HoldId == hold.Id).OrderBy(c => c.Sequence).ToListAsync(cancellationToken);
        var disclosures = await db.Disclosures.Where(d => d.HoldId == hold.Id).OrderBy(d => d.DisclosedAt).ToListAsync(cancellationToken);
        var exports = await db.EvidenceExports.Where(e => e.HoldId == hold.Id).OrderBy(e => e.CreatedAt).ToListAsync(cancellationToken);
        var exportIds = exports.Select(e => e.Id).ToList();
        var redactions = await db.EvidenceRedactions.Where(r => exportIds.Contains(r.ExportId)).GroupBy(r => r.ExportId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        var live = await db.LegalHolds.Where(h => h.Status != LegalHoldStatus.Released).ToListAsync(cancellationToken);
        var liveIds = live.Select(h => h.Id).ToList();
        var liveScopes = await db.LegalHoldScopes.Where(s => liveIds.Contains(s.HoldId) || s.HoldId == hold.Id).ToListAsync(cancellationToken);
        return new LegalHoldDetail(hold.Id, Wire(hold.Status), hold.Source, hold.Reason, hold.ReleaseConditions, hold.CaseId, hold.PlacedBy, Wire(hold.PlacedByRole),
            hold.PlacedAt, hold.ConfirmedBy, hold.ConfirmedAt, hold.ReviewDue, LegalHoldPolicy.Overdue(hold, now), hold.From, hold.Until,
            scopes.Where(s => s.Kind == HoldScopeKind.Account).Select(s => s.TargetId).ToList(),
            scopes.Where(s => s.Kind == HoldScopeKind.Conversation).Select(s => s.TargetId).ToList(),
            items.Select(i => new EvidenceItemView(i.Id, i.Capture, i.Kind, i.Source, i.RecordId, i.AccountId, i.CapturedAt, Convert.ToHexStringLower(i.Sha256))).ToList(),
            custody.Select(c => new CustodyEventView(c.Sequence, c.At, c.ActorAccountId, c.Action, c.Detail, Convert.ToHexStringLower(c.Hash))).ToList(),
            EvidenceSeal.ChainIntact(custody), disclosures.Select(View).ToList(), hold.ReleasedAt, hold.ReleaseReason,
            hold.LeCaseId, hold.Kind, hold.LawfulBasis, hold.RequestingBody, LegalHoldPolicy.Live(hold) ? LegalHoldPolicy.Overlaps(hold, live, liveScopes) : 0, hold.ReleasedBy,
            exports.Select(e => new EvidenceExportView(e.Id, e.CreatedBy, e.CreatedAt, e.DownloadedAt, e.PackageSha256 is null ? null : Convert.ToHexStringLower(e.PackageSha256),
                e.LeCaseId, e.SupplementsExportId, redactions.GetValueOrDefault(e.Id))).ToList());
    }

    private static DisclosureView View(Disclosure d) => new(d.Id, d.HoldId, d.Agency, d.Officer, d.Reference, d.LegalBasis, d.VerifiedHow, d.DisclosedBy, d.DisclosedAt);

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static IResult Unavailable() => Problem(StatusCodes.Status404NotFound, ProblemCodes.ExportUnavailable, "That export link has expired or was already used.");

    private static IResult Refused(HoldRefusal refusal) => refusal switch
    {
        HoldRefusal.NotPermitted => Results.Forbid(),
        HoldRefusal.ReasonRequired => Problem(StatusCodes.Status400BadRequest, ProblemCodes.HoldReasonRequired, "Give a reason (up to 1,000 characters)."),
        HoldRefusal.ScopeRequired => Problem(StatusCodes.Status400BadRequest, ProblemCodes.HoldScopeInvalid, "Name 1–20 accounts (and only their conversations)."),
        HoldRefusal.BadDates or HoldRefusal.ReviewTooFar => Problem(StatusCodes.Status400BadRequest, ProblemCodes.HoldDatesInvalid, "Check the period and the review date (within a year)."),
        HoldRefusal.CaseRequired => Problem(StatusCodes.Status400BadRequest, ProblemCodes.HoldCaseRequired, "Admins open a provisional hold from a case."),
        HoldRefusal.Released => Problem(StatusCodes.Status409Conflict, ProblemCodes.HoldReleased, "This hold has been released."),
        HoldRefusal.NotProvisional => Problem(StatusCodes.Status409Conflict, ProblemCodes.HoldNotProvisional, "Only a provisional hold can be confirmed."),
        HoldRefusal.NotIndependent => Problem(StatusCodes.Status403Forbidden, ProblemCodes.HoldReleaseNotIndependent,
            "Release needs a security and legal officer who neither placed nor confirmed this hold."),
        HoldRefusal.OverlapsUnchecked => Problem(StatusCodes.Status409Conflict, ProblemCodes.HoldOverlapsUnchecked,
            "Other live holds cover the same accounts. Check them, then confirm you have."),
        _ => Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Check the request."),
    };
}
