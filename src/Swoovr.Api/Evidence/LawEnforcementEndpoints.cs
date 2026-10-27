using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Evidence;

/// <summary>
/// Restricted law-enforcement case management (police-contact procedure §2–§14, D195): cases with their requests,
/// requester details and independent verification, the legal assessment and APP 6.5 note, deadline timers, separated
/// approvals with break-glass review, notification decisions, transfers with receipts, the closure checklist, the annual
/// transparency aggregate and the quarterly audit export. `legal` only; staff implicated in a case are excluded from it.
/// Audit entries carry ids and machine values only, never case content.
/// </summary>
public static class LawEnforcementEndpoints
{
    public static void Map(WebApplication app)
    {
        var le = app.MapGroup("/api/v1/admin/law-enforcement").RequireAuthorization();

        le.MapGet("/overview", async (ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await LegalAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            var open = await VisibleAsync(db, current!.Account!.Id).Where(c => c.Status == LeCaseStatus.Open).ToListAsync(cancellationToken);
            var ids = open.Select(c => c.Id).ToList();
            var reconsider = await db.LeNotifications.CountAsync(n => ids.Contains(n.CaseId) && n.ReconsiderAt != null && n.ReconsiderAt <= now, cancellationToken);
            var retrospective = await db.LeRequests.CountAsync(r => ids.Contains(r.CaseId) && r.BreakGlass && r.RetrospectiveAt == null, cancellationToken);
            return Results.Ok(new LeOverview(open.Count, open.Count(c => LawEnforcementPolicy.Timer(c, now).State.EndsWith("overdue")), reconsider, retrospective));
        });

        le.MapGet("/cases", async (string? status, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await LegalAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            var wanted = status == "closed" ? LeCaseStatus.Closed : LeCaseStatus.Open;
            var cases = await VisibleAsync(db, current!.Account!.Id).Where(c => c.Status == wanted).OrderBy(c => c.ReceivedAt).Take(200).ToListAsync(cancellationToken);
            var ids = cases.Select(c => c.Id).ToList();
            var open = await db.LeRequests.Where(r => ids.Contains(r.CaseId) && r.Status == "open").GroupBy(r => r.CaseId).Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
            var holds = await db.LegalHolds.Where(h => h.LeCaseId != null && ids.Contains(h.LeCaseId.Value) && h.Status != LegalHoldStatus.Released)
                .GroupBy(h => h.LeCaseId!.Value).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
            Audit.Record(db, now, current.Account.Id, "account", "le.cases-listed", null, null, $"results:{cases.Count}");
            await db.SaveChangesAsync(cancellationToken);
            // Overdue first, then the soonest due.
            return Results.Ok(cases.Select(c => Summary(c, now, open.GetValueOrDefault(c.Id), holds.GetValueOrDefault(c.Id)))
                .OrderBy(s => s.Timer.EndsWith("overdue") ? 0 : 1).ThenBy(s => s.Due ?? DateTimeOffset.MaxValue).ToList());
        });

        le.MapPost("/cases", async (NewLeCase request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await LegalAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, now, me) = (accounts.Db!, clock.GetUtcNow(), current!.Account!.Id);
            var subjects = (request.Subjects ?? []).Distinct().ToList();
            if (request.Channel is null || !LawEnforcementPolicy.Channels.Contains(request.Channel) || !Enum.TryParse<LeUrgency>(request.Urgency, true, out var urgency)
                || !LawEnforcementPolicy.Text(request.Agency, LawEnforcementPolicy.ShortMax) || !LawEnforcementPolicy.Text(request.OfficerName, LawEnforcementPolicy.ShortMax)
                || new[] { request.AgencyReference, request.OfficerRank, request.OfficerServiceNumber, request.OfficerEmail, request.OfficerPhone }.Any(v => !LawEnforcementPolicy.Optional(v))
                || request.AgencyCountry is { } country && (country.Length != 2 || !country.All(char.IsAsciiLetter))
                || request.ReceivedAt is { } received && received > now.AddMinutes(5)
                || subjects.Count > LawEnforcementPolicy.SubjectMax
                || request.Request is not { } first || !ValidRequest(first))
                return Invalid("Give the channel, urgency, agency, officer and the first request (kind and description); up to 20 subjects.");
            if (await db.Accounts.CountAsync(a => subjects.Contains(a.Id) && a.Realm == AccountRealm.Members, cancellationToken) != subjects.Count)
                return Invalid("Subjects must be member accounts.");
            var id = Guid.NewGuid();
            var item = new LeCase
            {
                Id = id, Reference = LawEnforcementPolicy.Reference(now, id), Status = LeCaseStatus.Open, Channel = request.Channel, ReceivedAt = request.ReceivedAt ?? now,
                Urgency = urgency, Agency = request.Agency!.Trim(), AgencyCountry = (request.AgencyCountry ?? "AU").ToUpperInvariant(), AgencyReference = Trim(request.AgencyReference),
                OfficerName = request.OfficerName!.Trim(), OfficerRank = Trim(request.OfficerRank), OfficerServiceNumber = Trim(request.OfficerServiceNumber),
                OfficerEmail = Trim(request.OfficerEmail), OfficerPhone = Trim(request.OfficerPhone), OwnerId = me, CreatedBy = me, CreatedAt = now,
                StatutoryDeadline = request.StatutoryDeadline, Privileged = request.Privileged, Confidential = request.Confidential,
            };
            db.LeCases.Add(item);
            db.LeRequests.Add(NewRequest(id, first, now));
            db.LeCasePeople.AddRange(subjects.Select(s => new LeCasePerson { CaseId = id, AccountId = s, Kind = "subject", AddedBy = me, AddedAt = now }));
            Audit.Record(db, now, me, "account", "le.case-opened", "le-case", id.ToString(), $"{Wire(urgency)}:{request.Channel}");
            await db.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/v1/admin/law-enforcement/cases/{id}", await DetailAsync(db, item, now, cancellationToken));
        });

        le.MapGet("/cases/{id:guid}", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, item, denied) = await CaseAsync(user, accounts, id, cancellationToken);
            if (denied is not null) return denied;
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            Audit.Record(db, now, current!.Account!.Id, "account", "le.case-viewed", "le-case", id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(await DetailAsync(db, item!, now, cancellationToken));
        });

        // Every change below follows one shape: load (legal, not excluded, open), validate, apply, audit with ids, return the case.
        le.MapPost("/cases/{id:guid}/verify", (Guid id, LeVerification body, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken ct) =>
            ChangeAsync(user, accounts, clock, id, "le.verified", ct, (item, me, now) =>
            {
                if (LawEnforcementPolicy.CheckVerify(body.Source, body.Evidence) != LeRefusal.None)
                    return Task.FromResult<IResult?>(Invalid("Verify through an official directory, the agency switchboard, its portal or in person, and record how. A government email alone isn't enough."));
                (item.VerificationSource, item.VerificationEvidence, item.VerifiedBy, item.VerifiedAt) = (body.Source, body.Evidence!.Trim(), me, now);
                return Task.FromResult<IResult?>(null);
            }, detail: body.Source));

        le.MapPost("/cases/{id:guid}/assessment", (Guid id, LeAssessment body, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken ct) =>
            ChangeAsync(user, accounts, clock, id, "le.assessed", ct, (item, me, now) =>
            {
                if (!LawEnforcementPolicy.Text(body.Assessment) || body.Instrument is null || !LawEnforcementPolicy.Instruments.Contains(body.Instrument)
                    || body.ConfidentialityAssessment is { Length: > LawEnforcementPolicy.TextMax })
                    return Task.FromResult<IResult?>(Invalid("Record the assessment and the instrument relied on."));
                (item.LegalAssessment, item.LegalInstrument, item.Mandatory, item.AssessedBy, item.AssessedAt) = (body.Assessment!.Trim(), body.Instrument, body.Mandatory, me, now);
                if (body.ConfidentialityAssessment is { } c) item.ConfidentialityAssessment = Trim(c);
                return Task.FromResult<IResult?>(null);
            }, detail: body.Instrument));

        le.MapPost("/cases/{id:guid}/app65", (Guid id, LeNote body, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken ct) =>
            ChangeAsync(user, accounts, clock, id, "le.app65-noted", ct, (item, me, now) =>
            {
                if (!LawEnforcementPolicy.Text(body.Note)) return Task.FromResult<IResult?>(Invalid("Write the APP 6.5 note: the factual basis, the extent, the recipients and the date."));
                (item.App65Note, item.App65By, item.App65At) = (body.Note!.Trim(), me, now);
                return Task.FromResult<IResult?>(null);
            }));

        le.MapPut("/cases/{id:guid}/flags", (Guid id, LeFlags body, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken ct) =>
            ChangeAsync(user, accounts, clock, id, "le.flags", ct, (item, _, _) =>
            {
                (item.Privileged, item.Confidential) = (body.Privileged, body.Confidential);
                return Task.FromResult<IResult?>(null);
            }, detail: $"privileged:{body.Privileged};confidential:{body.Confidential}"));

        le.MapPost("/cases/{id:guid}/responses", (Guid id, LeResponse body, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken ct) =>
            ChangeAsync(user, accounts, clock, id, "le.responded", ct, (item, _, now) =>
            {
                if (body.Kind is not ("acknowledgement" or "update")) return Task.FromResult<IResult?>(Invalid("Record an acknowledgement or an update."));
                if (item.FirstResponseAt is null) item.FirstResponseAt = now; else item.LastUpdateAt = now;
                return Task.FromResult<IResult?>(null);
            }, detail: body.Kind));

        le.MapPost("/cases/{id:guid}/requests", (Guid id, NewLeRequest body, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken ct) =>
            ChangeAsync(user, accounts, clock, id, "le.request-added", ct, (item, _, now) =>
            {
                if (!ValidRequest(body)) return Task.FromResult<IResult?>(Invalid("Give the request's kind and description; the period runs forwards."));
                accounts.Db!.LeRequests.Add(NewRequest(item.Id, body, now));
                return Task.FromResult<IResult?>(null);
            }, detail: body.Kind));

        le.MapPost("/cases/{id:guid}/requests/{requestId:guid}/decision", (Guid id, Guid requestId, LeDecision body, ClaimsPrincipal user, AccountResolver accounts,
            TimeProvider clock, CancellationToken ct) =>
            ChangeAsync(user, accounts, clock, id, body.BreakGlass ? "le.decided-break-glass" : "le.decided", ct, async (item, me, now) =>
            {
                var request = await accounts.Db!.LeRequests.SingleOrDefaultAsync(r => r.Id == requestId && r.CaseId == item.Id, ct);
                if (request is null) return Results.NotFound();
                var refusal = LawEnforcementPolicy.CheckDecide(item, request, me, body.Outcome, body.Reason, body.BreakGlass);
                if (refusal != LeRefusal.None) return Refused(refusal);
                (request.Status, request.DecisionReason, request.DecidedBy, request.DecidedAt, request.BreakGlass) = (body.Outcome!, body.Reason!.Trim(), me, now, body.BreakGlass);
                // §4, §5: an emergency approval is reviewed afterwards by another officer.
                if (body.BreakGlass) request.RetrospectiveDue = LawEnforcementPolicy.AddBusinessDays(now, LawEnforcementPolicy.RetrospectiveBusinessDays);
                return null;
            }, detail: body.Outcome, target: requestId));

        le.MapPost("/cases/{id:guid}/requests/{requestId:guid}/retrospective", (Guid id, Guid requestId, LeNote body, ClaimsPrincipal user, AccountResolver accounts,
            TimeProvider clock, CancellationToken ct) =>
            ChangeAsync(user, accounts, clock, id, "le.retrospective", ct, async (item, me, now) =>
            {
                var request = await accounts.Db!.LeRequests.SingleOrDefaultAsync(r => r.Id == requestId && r.CaseId == item.Id, ct);
                if (request is null) return Results.NotFound();
                var refusal = LawEnforcementPolicy.CheckRetrospective(request, me, body.Note);
                if (refusal != LeRefusal.None) return Refused(refusal);
                (request.RetrospectiveBy, request.RetrospectiveAt, request.RetrospectiveNote) = (me, now, body.Note!.Trim());
                return null;
            }, target: requestId, allowClosed: true));

        le.MapPost("/cases/{id:guid}/people", (Guid id, LePerson body, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken ct) =>
            ChangeAsync(user, accounts, clock, id, "le.person-added", ct, async (item, me, now) =>
            {
                var db = accounts.Db!;
                var realm = await db.Accounts.Where(a => a.Id == body.AccountId).Select(a => (AccountRealm?)a.Realm).SingleOrDefaultAsync(ct);
                if (body.Kind == "subject" ? realm != AccountRealm.Members : body.Kind == "implicated-staff" ? realm != AccountRealm.Staff : true)
                    return Invalid("A subject is a member account; implicated staff is a staff account.");
                if (await db.LeCasePeople.AnyAsync(p => p.CaseId == item.Id && p.AccountId == body.AccountId, ct)) return Invalid("Already on this case.");
                if (body.Kind == "subject" && await db.LeCasePeople.CountAsync(p => p.CaseId == item.Id && p.Kind == "subject", ct) >= LawEnforcementPolicy.SubjectMax)
                    return Invalid("At most 20 subjects.");
                // §5: an implicated owner hands the case over first.
                if (body.Kind == "implicated-staff" && body.AccountId == item.OwnerId) return Invalid("Reassign the case before naming its owner as implicated.");
                db.LeCasePeople.Add(new LeCasePerson { CaseId = item.Id, AccountId = body.AccountId, Kind = body.Kind!, AddedBy = me, AddedAt = now });
                return null;
            }, detail: body.Kind, target: body.AccountId));

        le.MapPost("/cases/{id:guid}/owner", (Guid id, LeOwner body, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken ct) =>
            ChangeAsync(user, accounts, clock, id, "le.owner-changed", ct, async (item, _, _) =>
            {
                var db = accounts.Db!;
                var legal = await db.RoleGrants.AnyAsync(g => g.AccountId == body.OwnerId && g.Role == AccountRole.Legal, ct);
                if (!legal || await db.LeCasePeople.AnyAsync(p => p.CaseId == item.Id && p.AccountId == body.OwnerId && p.Kind == "implicated-staff", ct))
                    return Invalid("The owner is an uninvolved security and legal officer.");
                item.OwnerId = body.OwnerId;
                return null;
            }, target: body.OwnerId));

        le.MapPost("/cases/{id:guid}/notifications", (Guid id, LeNotificationRequest body, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken ct) =>
            ChangeAsync(user, accounts, clock, id, "le.notification", ct, async (item, me, now) =>
            {
                var db = accounts.Db!;
                if (!await db.LeCasePeople.AnyAsync(p => p.CaseId == item.Id && p.AccountId == body.AccountId && p.Kind == "subject", ct))
                    return Invalid("Notification decisions are for the case's subjects.");
                switch (LawEnforcementPolicy.CheckNotification(body.Decision, body.Reason, body.ReconsiderAt, now))
                {
                    case LeRefusal.Invalid: return Invalid("Record notified, delay or withhold with the precise reason.");
                    case LeRefusal.ReviewTooFar: return Problem(StatusCodes.Status400BadRequest, ProblemCodes.LeDateInvalid, "Delaying or withholding needs a reconsideration date within six months.");
                }
                db.LeNotifications.Add(new LeNotification
                {
                    Id = Guid.NewGuid(), CaseId = item.Id, AccountId = body.AccountId, Decision = body.Decision!, Reason = body.Reason!.Trim(),
                    ReconsiderAt = body.Decision == "notified" ? null : body.ReconsiderAt, DecidedBy = me, DecidedAt = now,
                });
                // A newer decision supersedes the reconsideration of earlier ones for the same member.
                await db.LeNotifications.Where(n => n.CaseId == item.Id && n.AccountId == body.AccountId && n.ReconsiderAt != null)
                    .ExecuteUpdateAsync(u => u.SetProperty(n => n.ReconsiderAt, (DateTimeOffset?)null), ct);
                return null;
            }, detail: body.Decision, target: body.AccountId, allowClosed: true));

        le.MapPost("/cases/{id:guid}/transfers", (Guid id, LeTransferRequest body, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken ct) =>
            ChangeAsync(user, accounts, clock, id, "le.transferred", ct, async (item, me, now) =>
            {
                var db = accounts.Db!;
                var export = await db.EvidenceExports.FindAsync([body.ExportId], ct);
                var hold = export is null ? null : await db.LegalHolds.FindAsync([export.HoldId], ct);
                if (export?.PackageSha256 is null || hold?.LeCaseId != item.Id)
                    return Invalid("Transfer a downloaded export of a hold that serves this case.");
                if (!LawEnforcementPolicy.Text(body.Recipient, LawEnforcementPolicy.ShortMax) || body.Method is null || !LawEnforcementPolicy.TransferMethods.Contains(body.Method)
                    || body.SentAt is { } sent && (sent > now.AddMinutes(5) || sent < export.DownloadedAt))
                    return Invalid("Record the verified recipient and the transfer method.");
                var transfer = new LeTransfer
                {
                    Id = Guid.NewGuid(), CaseId = item.Id, ExportId = export.Id, PackageSha256 = export.PackageSha256, Recipient = body.Recipient!.Trim(),
                    Method = body.Method, SentBy = me, SentAt = body.SentAt ?? now,
                };
                db.LeTransfers.Add(transfer);
                await accounts.Services.GetRequiredService<EvidencePreservation>().CustodyAsync(hold.Id, me, "transferred",
                    $"transfer:{transfer.Id};export:{export.Id};method:{body.Method}", now, ct);
                return null;
            }, detail: body.Method, target: body.ExportId));

        le.MapPost("/cases/{id:guid}/transfers/{transferId:guid}/receipt", (Guid id, Guid transferId, LeReceipt body, ClaimsPrincipal user, AccountResolver accounts,
            TimeProvider clock, CancellationToken ct) =>
            ChangeAsync(user, accounts, clock, id, "le.receipt", ct, async (item, me, now) =>
            {
                var db = accounts.Db!;
                var transfer = await db.LeTransfers.SingleOrDefaultAsync(t => t.Id == transferId && t.CaseId == item.Id, ct);
                if (transfer is null) return Results.NotFound();
                if (transfer.ReceiptAt is not null || !LawEnforcementPolicy.Text(body.Reference, LawEnforcementPolicy.ShortMax)
                    || body.ReceivedAt is { } at && (at < transfer.SentAt || at > now.AddMinutes(5)))
                    return Invalid("Record the recipient's acknowledgement once.");
                (transfer.ReceiptAt, transfer.ReceiptReference) = (body.ReceivedAt ?? now, body.Reference!.Trim());
                if (await db.EvidenceExports.FindAsync([transfer.ExportId], ct) is { } export)
                    await accounts.Services.GetRequiredService<EvidencePreservation>().CustodyAsync(export.HoldId, me, "receipt", $"transfer:{transfer.Id}", now, ct);
                return null;
            }, target: transferId, allowClosed: true));

        le.MapPost("/cases/{id:guid}/close", async (Guid id, LeClose body, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, item, denied) = await CaseAsync(user, accounts, id, cancellationToken);
            if (denied is not null) return denied;
            var (db, now, me) = (accounts.Db!, clock.GetUtcNow(), current!.Account!.Id);
            if (item!.Status == LeCaseStatus.Closed) return Refused(LeRefusal.Closed);
            if (!body.ConfirmObligations || !LawEnforcementPolicy.Text(body.Note)) return Invalid("Confirm outstanding obligations were checked and write a closure note.");
            var detail = await DetailAsync(db, item, now, cancellationToken);
            if (detail.Closure.Any(c => !c.Done))
                return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "The closure checklist isn't complete.",
                    extensions: new Dictionary<string, object?> { ["code"] = ProblemCodes.LeClosureIncomplete, ["missing"] = detail.Closure.Where(c => !c.Done).Select(c => c.Item).ToList() });
            // §13: closing never releases a hold or destroys evidence; it starts the seven-year retention.
            (item.Status, item.ClosedAt, item.ClosedBy, item.ClosureNote, item.RetainUntil) = (LeCaseStatus.Closed, now, me, body.Note!.Trim(), LawEnforcementPolicy.RetainUntil(now));
            Audit.Record(db, now, me, "account", "le.closed", "le-case", id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(await DetailAsync(db, item, now, cancellationToken));
        });

        le.MapGet("/transparency", async (int? year, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await LegalAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            var y = year ?? now.Year;
            var (from, until) = (new DateTimeOffset(y, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(y + 1, 1, 1, 0, 0, 0, TimeSpan.Zero));
            // §13: confidential cases are left out entirely; small counts are suppressed.
            var cases = await db.LeCases.Where(c => c.ReceivedAt >= from && c.ReceivedAt < until && !c.Confidential).ToListAsync(cancellationToken);
            var ids = cases.Select(c => c.Id).ToList();
            var requests = await db.LeRequests.Where(r => ids.Contains(r.CaseId)).ToListAsync(cancellationToken);
            var holds = await db.LegalHolds.Where(h => h.LeCaseId != null && ids.Contains(h.LeCaseId.Value)).Select(h => h.Id).ToListAsync(cancellationToken);
            var affected = await db.LeCasePeople.Where(p => ids.Contains(p.CaseId) && p.Kind == "subject").Select(p => p.AccountId).Distinct().CountAsync(cancellationToken);
            Dictionary<string, int?> Counts(IEnumerable<string> keys, IEnumerable<string> values) =>
                keys.ToDictionary(k => k, k => LawEnforcementPolicy.Suppress(values.Count(v => v == k)));
            Audit.Record(db, now, current!.Account!.Id, "account", "le.transparency", null, null, y.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new TransparencyReport(y, LawEnforcementPolicy.Suppress(cases.Count),
                LawEnforcementPolicy.Suppress(cases.Count(c => c.Urgency == LeUrgency.Critical || requests.Any(r => r.CaseId == c.Id && r.Kind == "emergency-disclosure"))),
                Counts(LawEnforcementPolicy.RequestKinds, requests.Select(r => r.Kind)),
                Counts(["open", .. LawEnforcementPolicy.Outcomes], requests.Select(r => r.Status)),
                Counts(cases.Select(c => c.AgencyCountry).Distinct().Order(), cases.Select(c => c.AgencyCountry)),
                LawEnforcementPolicy.Suppress(holds.Count), LawEnforcementPolicy.Suppress(affected), LawEnforcementPolicy.SuppressBelow));
        });

        // §13, §14: the quarterly internal review export: ids, times, states, timers and integrity flags, never case content.
        le.MapGet("/audit-export", async (string? quarter, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await LegalAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            if (LawEnforcementPolicy.Quarter(quarter) is not { } period) return Invalid("Name a quarter such as 2026-Q4.");
            var (from, until) = period;
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            var cases = await db.LeCases.Where(c => c.ReceivedAt < until && (c.ClosedAt == null || c.ClosedAt >= from)).OrderBy(c => c.ReceivedAt).ToListAsync(cancellationToken);
            var ids = cases.Select(c => c.Id).ToList();
            var requests = await db.LeRequests.Where(r => ids.Contains(r.CaseId)).ToListAsync(cancellationToken);
            var notifications = await db.LeNotifications.Where(n => ids.Contains(n.CaseId)).ToListAsync(cancellationToken);
            var transfers = await db.LeTransfers.Where(t => ids.Contains(t.CaseId)).ToListAsync(cancellationToken);
            var holds = await db.LegalHolds.Where(h => h.LeCaseId != null && ids.Contains(h.LeCaseId.Value)).ToListAsync(cancellationToken);
            var holdIds = holds.Select(h => h.Id).ToList();
            var custody = (await db.CustodyEvents.Where(c => holdIds.Contains(c.HoldId)).OrderBy(c => c.Sequence).ToListAsync(cancellationToken)).ToLookup(c => c.HoldId);
            var export = new
            {
                Format = "swoovr-le-audit/1",
                Quarter = quarter,
                GeneratedAt = now,
                GeneratedBy = current!.Account!.Id,
                Note = "Ids, times, states and integrity flags only. No agency, officer, subject, assessment, note or evidence content.",
                Cases = cases.Select(c =>
                {
                    var timer = LawEnforcementPolicy.Timer(c, now);
                    var responseDue = LawEnforcementPolicy.ResponseDue(c.Urgency, c.ReceivedAt, c.StatutoryDeadline);
                    var mine = requests.Where(r => r.CaseId == c.Id).ToList();
                    return new
                    {
                        c.Id, Status = Wire(c.Status), Urgency = Wire(c.Urgency), c.Channel, c.AgencyCountry, c.ReceivedAt, c.ClosedAt, c.RetainUntil,
                        Verified = c.VerifiedAt is not null, c.VerificationSource, Assessed = c.AssessedAt is not null, c.LegalInstrument, App65Note = c.App65Note is not null,
                        c.Privileged, c.Confidential, ResponseDue = responseDue, c.FirstResponseAt, ResponseMet = c.FirstResponseAt is { } f && f <= responseDue, Timer = timer.State,
                        Requests = mine.Select(r => new
                        {
                            r.Id, r.Kind, r.Status, r.DecidedAt, r.BreakGlass, r.RetrospectiveDue, RetrospectiveDone = r.RetrospectiveAt is not null,
                            Separated = r.DecidedBy is null || r.DecidedBy != c.OwnerId,
                        }),
                        Notifications = notifications.Where(n => n.CaseId == c.Id).Select(n => new { n.Id, n.Decision, n.DecidedAt, n.ReconsiderAt }),
                        Transfers = transfers.Where(t => t.CaseId == c.Id).Select(t => new { t.Id, t.Method, t.SentAt, t.ReceiptAt, PackageSha256 = Convert.ToHexStringLower(t.PackageSha256) }),
                        Holds = holds.Where(h => h.LeCaseId == c.Id).Select(h => new
                        {
                            h.Id, Status = Wire(h.Status), h.Kind, h.PlacedAt, h.ReviewDue, Overdue = LegalHoldPolicy.Overdue(h, now), h.ReleasedAt,
                            IndependentRelease = h.ReleasedBy is not { } r || LawEnforcementPolicy.IndependentRelease(h, r),
                            CustodyIntact = EvidenceSeal.ChainIntact(custody[h.Id].ToList()), CustodyEvents = custody[h.Id].Count(),
                        }),
                    };
                }),
            };
            Audit.Record(db, now, current.Account.Id, "account", "le.audit-exported", null, null, quarter);
            await db.SaveChangesAsync(cancellationToken);
            return Results.File(JsonSerializer.SerializeToUtf8Bytes(export, EvidencePreservation.Json), "application/json", $"swoovr-le-audit-{quarter}.json");
        });
    }

    private static bool ValidRequest(NewLeRequest request) =>
        request.Kind is not null && LawEnforcementPolicy.RequestKinds.Contains(request.Kind) && LawEnforcementPolicy.Text(request.Description)
        && !(request.From is { } f && request.Until is { } u && u <= f);

    private static LeRequest NewRequest(Guid caseId, NewLeRequest request, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), CaseId = caseId, Kind = request.Kind!, Description = request.Description!.Trim(), ReceivedAt = now, From = request.From, Until = request.Until,
    };

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Cases the caller may see: every case except those naming them as implicated staff (§5).</summary>
    private static IQueryable<LeCase> VisibleAsync(SwoovrDbContext db, Guid me) =>
        db.LeCases.Where(c => !db.LeCasePeople.Any(p => p.CaseId == c.Id && p.AccountId == me && p.Kind == "implicated-staff"));

    private static async Task<(CurrentAccount? Current, IResult? Denied)> LegalAsync(ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken)
    {
        var current = await accounts.ResolveAsync(user, cancellationToken);
        if (current is null) return (null, Results.Unauthorized());
        if (current.Account is null) return (null, Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.PersistenceUnavailable, "Accounts are not available."));
        return current.Allows(AccountRole.Legal) ? (current, null) : (null, Results.Forbid());
    }

    private static async Task<(CurrentAccount? Current, LeCase? Case, IResult? Denied)> CaseAsync(ClaimsPrincipal user, AccountResolver accounts, Guid id,
        CancellationToken cancellationToken)
    {
        var (current, denied) = await LegalAsync(user, accounts, cancellationToken);
        if (denied is not null) return (null, null, denied);
        var db = accounts.Db!;
        var item = await db.LeCases.FindAsync([id], cancellationToken);
        if (item is null) return (null, null, Results.NotFound());
        // §5: implicated staff are refused, and the refusal is recorded (ids only).
        if (await db.LeCasePeople.AnyAsync(p => p.CaseId == id && p.AccountId == current!.Account!.Id && p.Kind == "implicated-staff", cancellationToken))
        {
            Audit.Record(db, DateTimeOffset.UtcNow, current!.Account!.Id, "account", "le.refused-excluded", "le-case", id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return (null, null, Results.Forbid());
        }
        return (current, item, null);
    }

    private static async Task<IResult> ChangeAsync(ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, Guid id, string action,
        CancellationToken cancellationToken, Func<LeCase, Guid, DateTimeOffset, Task<IResult?>> apply, string? detail = null, Guid? target = null, bool allowClosed = false)
    {
        var (current, item, denied) = await CaseAsync(user, accounts, id, cancellationToken);
        if (denied is not null) return denied;
        var (db, now, me) = (accounts.Db!, clock.GetUtcNow(), current!.Account!.Id);
        if (item!.Status == LeCaseStatus.Closed && !allowClosed) return Refused(LeRefusal.Closed);
        if (await apply(item, me, now) is { } refused) return refused;
        Audit.Record(db, now, me, "account", action, "le-case", id.ToString(), target is { } t ? $"{detail}:{t}" : detail);
        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await DetailAsync(db, item, now, cancellationToken));
    }

    private static LeCaseSummary Summary(LeCase c, DateTimeOffset now, int openRequests, int holds)
    {
        var (due, state) = LawEnforcementPolicy.Timer(c, now);
        return new(c.Id, c.Reference, Wire(c.Status), Wire(c.Urgency), c.Agency, c.AgencyCountry, c.ReceivedAt, c.OwnerId, c.VerifiedAt is not null, c.Privileged,
            c.Confidential, state, due, openRequests, holds);
    }

    public static async Task<LeCaseDetail> DetailAsync(SwoovrDbContext db, LeCase c, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var requests = await db.LeRequests.Where(r => r.CaseId == c.Id).OrderBy(r => r.ReceivedAt).ToListAsync(cancellationToken);
        var people = await db.LeCasePeople.Where(p => p.CaseId == c.Id).OrderBy(p => p.AddedAt).ToListAsync(cancellationToken);
        var notifications = await db.LeNotifications.Where(n => n.CaseId == c.Id).OrderBy(n => n.DecidedAt).ToListAsync(cancellationToken);
        var transfers = await db.LeTransfers.Where(t => t.CaseId == c.Id).OrderBy(t => t.SentAt).ToListAsync(cancellationToken);
        var holds = await db.LegalHolds.Where(h => h.LeCaseId == c.Id).OrderBy(h => h.PlacedAt).ToListAsync(cancellationToken);
        var holdIds = holds.Select(h => h.Id).ToList();
        var scopes = await db.LegalHoldScopes.Where(s => holdIds.Contains(s.HoldId) && s.Kind == HoldScopeKind.Account).GroupBy(s => s.HoldId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        var items = await db.EvidenceItems.Where(i => holdIds.Contains(i.HoldId)).GroupBy(i => i.HoldId)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        var disclosures = await db.Disclosures.Where(d => holdIds.Contains(d.HoldId)).OrderBy(d => d.DisclosedAt).ToListAsync(cancellationToken);
        var (due, state) = LawEnforcementPolicy.Timer(c, now);
        return new LeCaseDetail(c.Id, c.Reference, Wire(c.Status), c.Channel, c.ReceivedAt, Wire(c.Urgency), c.Agency, c.AgencyCountry, c.AgencyReference, c.OfficerName,
            c.OfficerRank, c.OfficerServiceNumber, c.OfficerEmail, c.OfficerPhone, c.OwnerId, c.CreatedAt, c.VerificationSource, c.VerificationEvidence, c.VerifiedBy,
            c.VerifiedAt, c.LegalAssessment, c.LegalInstrument, c.Mandatory, c.AssessedAt, c.App65Note, c.App65At, c.Privileged, c.Confidential,
            c.ConfidentialityAssessment, c.StatutoryDeadline, c.FirstResponseAt, c.LastUpdateAt, state, due, c.ClosedAt, c.ClosureNote, c.RetainUntil,
            requests.Select(r => new LeRequestView(r.Id, r.Kind, r.Description, r.ReceivedAt, r.From, r.Until, r.Status, r.DecisionReason, r.DecidedBy, r.DecidedAt,
                r.BreakGlass, r.RetrospectiveDue, r.RetrospectiveBy, r.RetrospectiveAt)).ToList(),
            people.Select(p => new LeCasePersonView(p.AccountId, p.Kind, p.AddedAt)).ToList(),
            notifications.Select(n => new LeNotificationView(n.Id, n.AccountId, n.Decision, n.Reason, n.ReconsiderAt, n.DecidedBy, n.DecidedAt,
                n.ReconsiderAt is { } r && r <= now)).ToList(),
            transfers.Select(t => new LeTransferView(t.Id, t.ExportId, Convert.ToHexStringLower(t.PackageSha256), t.Recipient, t.Method, t.SentBy, t.SentAt,
                t.ReceiptAt, t.ReceiptReference)).ToList(),
            holds.Select(h => new LegalHoldSummary(h.Id, Wire(h.Status), h.Source, h.CaseId, h.PlacedAt, h.ReviewDue, LegalHoldPolicy.Overdue(h, now),
                scopes.GetValueOrDefault(h.Id), items.GetValueOrDefault(h.Id))).ToList(),
            LawEnforcementPolicy.Closure(c, requests, people, notifications, transfers, holds, now).Select(i => new LeChecklistItem(i.Item, i.Done)).ToList(),
            disclosures.Select(d => new DisclosureView(d.Id, d.HoldId, d.Agency, d.Officer, d.Reference, d.LegalBasis, d.VerifiedHow, d.DisclosedBy, d.DisclosedAt)).ToList());
    }

    private static IResult Invalid(string title) => Problem(StatusCodes.Status400BadRequest, ProblemCodes.LeCaseInvalid, title);

    private static IResult Refused(LeRefusal refusal) => refusal switch
    {
        LeRefusal.NotPermitted or LeRefusal.Excluded => Results.Forbid(),
        LeRefusal.NotVerified => Problem(StatusCodes.Status409Conflict, ProblemCodes.LeNotVerified, "Verify the requester independently first."),
        LeRefusal.NotAssessed => Problem(StatusCodes.Status409Conflict, ProblemCodes.LeNotAssessed, "Record the legal assessment first."),
        LeRefusal.App65Required => Problem(StatusCodes.Status409Conflict, ProblemCodes.LeApp65Required, "Relying on APP 6.2(e) needs the APP 6.5 note first."),
        LeRefusal.NotSeparated => Problem(StatusCodes.Status403Forbidden, ProblemCodes.LeNotSeparated, "Another officer approves (or reviews) this."),
        LeRefusal.Closed => Problem(StatusCodes.Status409Conflict, ProblemCodes.LeCaseClosed, "The case is closed."),
        LeRefusal.AlreadyDecided => Problem(StatusCodes.Status409Conflict, ProblemCodes.LeAlreadyDecided, "That request was already decided."),
        LeRefusal.ReviewTooFar => Problem(StatusCodes.Status400BadRequest, ProblemCodes.LeDateInvalid, "Check the date."),
        _ => Invalid("Check the request."),
    };
}
