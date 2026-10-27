using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Moderation;

/// <summary>
/// Reports, moderation, appeals and notices (D131, D133–D135), plus the admin account tools the admin app needs: the
/// staff-to-member link (D134, D146), the home-city move override (D150) and Plus grants (D151). Members report and
/// appeal; staff work cases only through these admin routes, which the member BFF never forwards (D136). Audit entries
/// carry ids and machine values only, never report text, reasons, names or evidence.
/// </summary>
public static class ModerationEndpoints
{
    private static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web);

    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization();
        // D190: `kind=serious` lists the Serious Safety Incident categories; otherwise the routine ones.
        api.MapGet("/report-categories", async (string? kind, ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (_, denied) = await MemberAsync(user, accounts, cancellationToken);
            var serious = kind == "serious";
            return denied ?? Results.Ok(await accounts.Db!.ReportCategories.Where(c => c.Available && c.Serious == serious).OrderBy(c => c.SortOrder)
                .Select(c => new ReportCategoryView(c.Id, c.Label)).ToListAsync(cancellationToken));
        });
        api.MapPost("/reports", ReportAsync);
        api.MapGet("/me/reports", async (ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            // D190: the general outcome once the case is closed: whether any action followed the report, never which.
            var mine = await (from r in db.Reports
                              join c in db.ModerationCases on r.CaseId equals c.Id
                              join k in db.ReportCategories on r.CategoryId equals k.Id
                              where r.ReporterAccountId == current!.Account!.Id
                              orderby r.CreatedAt descending
                              select new { r.Id, r.SubjectType, r.CategoryId, c.Status, r.CreatedAt, k.Serious,
                                  Action = db.Sanctions.Any(s => s.CaseId == c.Id && s.StartsAt >= r.CreatedAt) }).Take(100).ToListAsync(cancellationToken);
            return Results.Ok(mine.Select(r => new MyReportView(r.Id, Wire(r.SubjectType), r.CategoryId, ReportPolicy.ReporterStatus(r.Status), r.CreatedAt,
                ReportPolicy.Outcome(r.Status, r.Action), r.Serious)).ToList());
        });
        // D190 (policy §9): everyone the member has dealt with, so a report stays possible after a block, a disconnect or
        // the other account's deletion. One row per person: their conversation if there is one, else the latest request.
        api.MapGet("/me/safety/people", async (ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, me) = (accounts.Db!, current!.Account!.Id);
            var conversations = await db.Conversations.Where(c => c.PairLow == me || c.PairHigh == me).OrderByDescending(c => c.CreatedAt).Take(200).ToListAsync(cancellationToken);
            var requests = await db.ConnectionRequests.Where(r => r.FromAccountId == me || r.ToAccountId == me).OrderByDescending(r => r.CreatedAt).Take(200).ToListAsync(cancellationToken);
            var others = conversations.Select(c => c.PairLow == me ? c.PairHigh : c.PairLow).Concat(requests.Select(r => r.FromAccountId == me ? r.ToAccountId : r.FromAccountId)).Distinct().ToList();
            var names = await db.MemberProfiles.Where(p => others.Contains(p.AccountId)).ToDictionaryAsync(p => p.AccountId, p => p.DisplayName, cancellationToken);
            var blocked = (await db.Blocks.Where(b => b.BlockerAccountId == me && others.Contains(b.BlockedAccountId) || b.BlockedAccountId == me && others.Contains(b.BlockerAccountId))
                .Select(b => b.BlockerAccountId == me ? b.BlockedAccountId : b.BlockerAccountId).ToListAsync(cancellationToken)).ToHashSet();
            var people = new List<SafetyPerson>();
            foreach (var other in others)
            {
                var name = names.TryGetValue(other, out var n) ? n ?? "Someone" : "Deleted member";
                var deleted = !names.ContainsKey(other);
                if (conversations.FirstOrDefault(c => c.PairLow == other || c.PairHigh == other) is { } c)
                    people.Add(new("conversation", c.Id, name, deleted ? "deleted" : blocked.Contains(other) || c.ClosedReason == ClosedReasons.Blocked ? "blocked"
                        : c.ClosedReason == ClosedReasons.Disconnected ? "disconnected" : c.ClosedAt is null ? "connected" : "ended", c.CreatedAt));
                else if (requests.FirstOrDefault(r => r.FromAccountId == other || r.ToAccountId == other) is { } r)
                    people.Add(new("request", r.Id, name, blocked.Contains(other) ? "blocked" : r.Status switch
                    {
                        ConnectionRequestStatus.Accepted => "connected",
                        ConnectionRequestStatus.Pending => "pending",
                        ConnectionRequestStatus.Disconnected => "disconnected",
                        ConnectionRequestStatus.Declined => "declined",
                        _ => "ended",
                    }, r.CreatedAt));
            }
            return Results.Ok(people);
        });
        api.MapGet("/me/notices", async (ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken, ageGateExempt: true, barredExempt: true);
            if (denied is not null) return denied;
            return Results.Ok(await NoticesAsync(accounts.Db!, current!.Account!.Id, clock.GetUtcNow(), cancellationToken));
        });
        api.MapPost("/me/notices/{id:guid}/read", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken, ageGateExempt: true, barredExempt: true);
            if (denied is not null) return denied;
            var notice = await accounts.Db!.MemberNotices.SingleOrDefaultAsync(n => n.Id == id && n.AccountId == current!.Account!.Id, cancellationToken);
            if (notice is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotAvailable, "That notice isn't available.");
            notice.ReadAt ??= clock.GetUtcNow();
            await accounts.Db!.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });
        api.MapPost("/me/appeals", async (NewAppeal request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken, ageGateExempt: true, barredExempt: true);
            if (denied is not null) return denied;
            var (db, me, now) = (accounts.Db!, current!.Account!.Id, clock.GetUtcNow());
            if (request.Text is { Length: > ReportPolicy.TextMax }) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidReport, "Keep it under 2,000 characters.");
            var sanction = await db.Sanctions.FindAsync([request.SanctionId], cancellationToken);
            // Another member's sanction reads the same as none.
            if (sanction is null || sanction.AccountId != me) return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotAvailable, "That action isn't available.");
            if (!ModerationPolicy.MayAppeal(sanction, me, await db.Appeals.AnyAsync(a => a.SanctionId == sanction.Id, cancellationToken), now))
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.AppealNotAllowed, "This action can't be appealed.");
            var appeal = new Appeal { Id = Guid.NewGuid(), SanctionId = sanction.Id, AccountId = me, Text = Clean(request.Text), Status = AppealStatus.Pending, CreatedAt = now };
            db.Appeals.Add(appeal);
            Audit.Record(db, now, me, "account", "appeal.created", "appeal", appeal.Id.ToString(), sanction.Kind.ToString());
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException) { return Problem(StatusCodes.Status409Conflict, ProblemCodes.AppealNotAllowed, "This action can't be appealed."); }
            return Results.Created($"/api/v1/me/appeals/{appeal.Id}", new AppealView(appeal.Id, sanction.Id, Wire(appeal.Status), now));
        });
        api.MapGet("/me/plan", async (ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            return denied ?? Results.Ok(await Entitlements.PlanAsync(accounts.Db!, current!.Profile!, clock.GetUtcNow(), cancellationToken));
        });

        MapAdmin(app);
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>
    /// D131: a member reports a profile, a request between them, a bot, a conversation or a message. Optional text, an
    /// optional block in the same step, evidence preserved, reports about one account consolidated into its open case,
    /// urgent ones escalated at once. The reporter must be able to see the person or share a request or conversation with
    /// them; anything else reads as not available. D190: reporting stays open after a block, disconnect or the other
    /// account's deletion through the conversation or request they shared; a Serious Safety Incident category goes to the
    /// admin queue, is escalated every time and places a provisional legal hold over both accounts and their conversations.
    /// </summary>
    private static async Task<IResult> ReportAsync(NewReport request, ClaimsPrincipal user, AccountResolver accounts, IConfiguration configuration,
        IEmailSender email, Evidence.EvidencePreservation evidence, Evidence.EvidenceKeys evidenceKeys, IHubContext<Connections.MessagesHub> hub,
        TimeProvider clock, ILoggerFactory logs, CancellationToken cancellationToken)
    {
        var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
        if (denied is not null) return denied;
        var (db, me, now) = (accounts.Db!, current!.Account!.Id, clock.GetUtcNow());
        if (!Enum.TryParse<ReportSubject>(request.SubjectType, ignoreCase: true, out var subjectType) || !Enum.IsDefined(subjectType) || int.TryParse(request.SubjectType, out _))
            return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidReport, "Check what you're reporting.");
        if (!ReportPolicy.Supported(subjectType))
            return Problem(StatusCodes.Status400BadRequest, ProblemCodes.UnsupportedSubject, "That can't be reported yet.");
        if (request.Text is { Length: > ReportPolicy.TextMax })
            return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidReport, "Keep it under 2,000 characters.");
        if (request.Police is not null && !ReportPolicy.PoliceAnswers.Contains(request.Police) || request.PoliceReference is { Length: > ReportPolicy.PoliceReferenceMax }
            || request.MessageIds is { Count: > ReportPolicy.ChosenMessagesMax })
            return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidReport, "Check the police details and the messages you chose (up to 20).");
        var category = await db.ReportCategories.SingleOrDefaultAsync(c => c.Id == request.Category && c.Available, cancellationToken);
        if (category is null) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.UnknownCategory, "Pick a reason from the list.");

        // Resolve the subject account (its profile is gone after final deletion) and a snapshot of what the reporter saw.
        Guid? subjectId = null;
        MemberProfile? subject = null;
        object? snapshot = null;
        var pairConversations = new List<Guid>();
        if (subjectType is ReportSubject.Profile or ReportSubject.Bot && Guid.TryParse(request.SubjectId, out var publicId))
        {
            subject = await db.MemberProfiles.SingleOrDefaultAsync(p => p.PublicId == publicId, cancellationToken);
            if (subject is not null && (subject.Bot != (subjectType == ReportSubject.Bot) || !await MayReportAsync(db, configuration, me, subject, cancellationToken)))
                subject = null;
            if (subject is not null) snapshot = new { subject.DisplayName, subject.Area, subject.About, subject.Bot };
        }
        else if (subjectType == ReportSubject.Request && Guid.TryParse(request.SubjectId, out var requestId))
        {
            var found = await db.ConnectionRequests.FindAsync([requestId], cancellationToken);
            if (found is not null && (found.FromAccountId == me || found.ToAccountId == me))
            {
                subject = await db.MemberProfiles.FindAsync([found.FromAccountId == me ? found.ToAccountId : found.FromAccountId], cancellationToken);
                snapshot = new { found.ModeId, Status = found.Status.ToString(), found.CreatedAt, FromReporter = found.FromAccountId == me };
            }
        }
        else if (subjectType is ReportSubject.Message or ReportSubject.Conversation && Guid.TryParse(request.SubjectId, out var itemId))
        {
            // D143 rules 10, 18–21: the member's own conversation (even after a block, a disconnect or the other's
            // deletion); a message must be the other person's. The evidence is the reported message and up to twenty
            // before it, the messages the reporter chose (D190), or the conversation's latest twenty-one; deleted messages
            // are included while their text is still kept (D165). Never in the audit.
            var message = subjectType == ReportSubject.Message ? await db.Messages.FindAsync([itemId], cancellationToken) : null;
            var conversation = await db.Conversations.FindAsync([message?.ConversationId ?? itemId], cancellationToken);
            if (conversation is not null && (conversation.PairLow == me || conversation.PairHigh == me)
                && (subjectType == ReportSubject.Conversation || message is not null && message.SenderAccountId != me))
            {
                subjectId = conversation.PairLow == me ? conversation.PairHigh : conversation.PairLow;
                subject = await db.MemberProfiles.FindAsync([subjectId], cancellationToken);
                var all = await db.Messages.Where(m => m.ConversationId == conversation.Id).OrderByDescending(m => m.SentAt).Take(500).ToListAsync(cancellationToken);
                var chosen = request.MessageIds is { Count: > 0 } picks ? all.Where(m => picks.Contains(m.Id)).OrderBy(m => m.SentAt).ToList() : null;
                if (chosen is not null && chosen.Count != request.MessageIds!.Distinct().Count())
                    return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidReport, "Choose messages from this conversation.");
                var kept = chosen ?? (message is not null ? MessagingPolicy.Evidence(all, message) : all.Take(MessagingPolicy.EvidenceBefore + 1).OrderBy(m => m.SentAt).ToList());
                snapshot = new
                {
                    Conversation = conversation.Id, Reported = message?.Id, Chosen = chosen is not null, conversation.ClosedReason,
                    Messages = kept.Select(m => new { m.Id, FromSubject = m.SenderAccountId != me, m.SentAt, Deleted = m.DeletedAt is not null, Text = m.Body }).ToList(),
                };
            }
        }
        subjectId ??= subject?.AccountId;
        if (subjectId is not { } subjectAccount || subjectAccount == me
            || !await db.Accounts.AnyAsync(a => a.Id == subjectAccount && a.Realm == AccountRealm.Members, cancellationToken))
            return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotAvailable, "That isn't available.");

        var recent = await db.Reports.CountAsync(r => r.ReporterAccountId == me && r.Priority != ReportPriority.Urgent && r.CreatedAt > now - ReportPolicy.BurstWindow, cancellationToken);
        if (ReportPolicy.RateLimited(category.Priority, recent))
        {
            Audit.Record(db, now, me, "account", "report.rate-limited", "account", me.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Problem(StatusCodes.Status429TooManyRequests, ProblemCodes.TooManyRequests, "You've sent a lot of reports. Try again in a few minutes.");
        }
        var report = new Report
        {
            Id = Guid.NewGuid(), ReporterAccountId = me, SubjectAccountId = subjectAccount, SubjectType = subjectType, SubjectRef = request.SubjectId.Trim(),
            CategoryId = category.Id, Priority = category.Priority, Text = Clean(request.Text), CreatedAt = now, Police = category.Serious ? request.Police : null,
        };
        // D190: a police reference is kept only sealed with the evidence key, and only for serious incidents.
        if (category.Serious && Clean(request.PoliceReference) is { } reference && evidenceKeys.CurrentVersion is { } version)
            report.PoliceReference = EvidenceSeal.Seal(version, evidenceKeys.Encryption[version], reference);
        var item = await AttachAsync(db, report, subjectAccount, subject?.Employee == true, category.Serious, now, cancellationToken);
        db.CaseEvidence.Add(new CaseEvidence { Id = Guid.NewGuid(), CaseId = item.Id, ReportId = report.Id, Kind = Wire(subjectType),
            Snapshot = JsonSerializer.Serialize(snapshot, SnapshotJson), CapturedAt = now });
        Audit.Record(db, now, me, "account", category.Serious ? "report.serious" : "report.created", "moderation-case", item.Id.ToString(), $"{category.Id}:{category.Priority}");
        var closed = new List<Conversation>();
        if (request.Block) await Connections.RequestEndpoints.AddBlockAsync(db, me, subjectAccount, now, cancellationToken, closed);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            // A concurrent report opened the case first; join it.
            db.ChangeTracker.Clear();
            return await ReportAsync(request, user, accounts, configuration, email, evidence, evidenceKeys, hub, clock, logs, cancellationToken);
        }
        // #82: a block closes the conversation for both members' open chat windows at once.
        await Connections.MessagingEndpoints.PushClosedAsync(hub, closed, cancellationToken);
        if (category.Serious)
        {
            // A preservation failure never loses the member's report: it is logged and audited for staff to place the hold.
            try { await PreserveAsync(db, evidence, item, me, subjectAccount, category.Id, now, logs, cancellationToken); }
            catch (Exception error) when (error is DbUpdateException or InvalidOperationException)
            {
                logs.CreateLogger("Swoovr.Api.Moderation").LogError(error, "Serious case {Case}: preservation failed.", item.Id);
                db.ChangeTracker.Clear();
                item = (await db.ModerationCases.FindAsync([item.Id], cancellationToken))!;
                Audit.Record(db, now, null, "system", "hold.failed", "moderation-case", item.Id.ToString());
                await db.SaveChangesAsync(cancellationToken);
            }
            // Every serious report is escalated, even into a case already escalated and acknowledged.
            (item.EscalatedAt, item.AcknowledgedAt, item.AcknowledgedBy) = (null, null, null);
        }
        if (item.Priority == ReportPriority.Urgent && item.EscalatedAt is null)
            await EscalateAsync(db, item, category.Id, category.Serious, configuration, email, now, logs, cancellationToken);
        return Results.Created($"/api/v1/me/reports/{report.Id}", new MyReportView(report.Id, Wire(subjectType), category.Id, ReportPolicy.ReporterStatus(item.Status), now,
            null, category.Serious));
    }

    /// <summary>
    /// D190: a serious report places a provisional legal hold (Swoovr is the authoriser, `legal` confirms or releases it
    /// within 30 days) over the reporter, the reported account and their conversations; a later serious report on the
    /// same case and pair captures again instead of opening another hold. Without an evidence key nothing is held and
    /// the audit says so.
    /// </summary>
    private static async Task PreserveAsync(SwoovrDbContext db, Evidence.EvidencePreservation evidence, ModerationCase item, Guid reporter, Guid subject,
        string category, DateTimeOffset now, ILoggerFactory logs, CancellationToken cancellationToken)
    {
        if (!evidence.Configured)
        {
            Audit.Record(db, now, null, "system", "hold.not-placed", "moderation-case", item.Id.ToString(), "no-evidence-key");
            await db.SaveChangesAsync(cancellationToken);
            logs.CreateLogger("Swoovr.Api.Moderation").LogWarning("Serious case {Case}: no evidence key, nothing preserved.", item.Id);
            return;
        }
        var (low, high) = ConnectionRequest.Pair(reporter, subject);
        var conversations = await db.Conversations.Where(c => c.PairLow == low && c.PairHigh == high).Select(c => c.Id).ToListAsync(cancellationToken);
        var existing = await (from h in db.LegalHolds join s in db.LegalHoldScopes on h.Id equals s.HoldId
                              where h.CaseId == item.Id && h.Source == "serious-incident" && h.Status != LegalHoldStatus.Released
                                    && s.Kind == HoldScopeKind.Account && s.TargetId == reporter select h).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            await evidence.CaptureAsync(existing, null, now, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }
        // No conversation yet: hold the two accounts without narrowing to conversations.
        await Evidence.LegalHoldEndpoints.PlaceAsync(db, evidence, AccountRole.Unknown, null, $"Serious Safety Incident report ({category}), case {item.Id}",
            [reporter, subject], conversations, null, null, null, "Release when the case is resolved and no investigation or request needs the records.",
            item.Id, "serious-incident", now, cancellationToken);
    }

    /// <summary>A member may report someone they can see, someone they share any request with, or someone who blocked them after one.</summary>
    private static async Task<bool> MayReportAsync(SwoovrDbContext db, IConfiguration configuration, Guid me, MemberProfile subject, CancellationToken cancellationToken)
    {
        var (low, high) = ConnectionRequest.Pair(me, subject.AccountId);
        if (await db.ConnectionRequests.AnyAsync(r => r.PairLow == low && r.PairHigh == high, cancellationToken)) return true;
        if (await db.Conversations.AnyAsync(c => c.PairLow == low && c.PairHigh == high, cancellationToken)) return true;
        // Otherwise exactly when the profile would open for them (shared modes and the market rule, D125).
        var row = await Profiles.ProfileEndpoints.Candidates(db, configuration, me).SingleOrDefaultAsync(c => c.Profile.AccountId == subject.AccountId, cancellationToken);
        if (row is null) return false;
        var shared = await Profiles.ProfileEndpoints.SharedModesAsync(db, me, row, cancellationToken);
        if (shared.Count == 0) return false;
        var viewer = await db.MemberProfiles.SingleAsync(p => p.AccountId == me, cancellationToken);
        var reach = await Profiles.ProfileEndpoints.ReachAsync(db, configuration, viewer, row, shared, [], DateTimeOffset.UtcNow, cancellationToken);
        return MarketAccess.ProfileOpens(reach.SameMarket, false, reach.VisitingModes.Count > 0);
    }

    /// <summary>Joins the subject's open case or opens one; staff-affiliated subjects and serious incidents go to the admin queue (D134, D190).</summary>
    private static async Task<ModerationCase> AttachAsync(SwoovrDbContext db, Report report, Guid subject, bool employee, bool serious, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var item = await db.ModerationCases.SingleOrDefaultAsync(c => c.SubjectAccountId == subject
            && (c.Status == CaseStatus.Open || c.Status == CaseStatus.UnderReview), cancellationToken);
        if (item is null)
        {
            item = new ModerationCase { Id = Guid.NewGuid(), SubjectAccountId = subject, Status = CaseStatus.Open, Priority = report.Priority, CreatedAt = now };
            db.ModerationCases.Add(item);
        }
        item.Priority = ReportPolicy.Max(item.Priority, report.Priority);
        item.Queue = ReportPolicy.QueueFor(employee || item.Queue == CaseQueue.Admin, serious);
        item.UpdatedAt = now;
        report.CaseId = item.Id;
        db.Reports.Add(report);
        return item;
    }

    /// <summary>
    /// D135: urgent reports go at once to SWOOVR_ESCALATION_EMAIL (configuration `Moderation:EscalationEmail`, never
    /// committed) and the case stays awaiting acknowledgement. The email carries ids and the category only.
    /// </summary>
    private static async Task EscalateAsync(SwoovrDbContext db, ModerationCase item, string category, bool serious, IConfiguration configuration, IEmailSender email,
        DateTimeOffset now, ILoggerFactory logs, CancellationToken cancellationToken)
    {
        // D159: the primary and backup contacts (Moderation:EscalationEmail and Moderation:EscalationBackupEmail, from
        // SWOOVR_ESCALATION_EMAIL and SWOOVR_ESCALATION_BACKUP_EMAIL); the same address twice is sent once.
        var to = configuration["Moderation:EscalationEmail"];
        var sent = false;
        foreach (var address in new[] { to, configuration["Moderation:EscalationBackupEmail"] }.Where(a => !string.IsNullOrWhiteSpace(a))
                     .Select(a => a!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
            sent |= await email.SendAsync(new EmailMessage(address, serious ? "Swoovr serious safety incident: acknowledgement needed" : "Swoovr urgent report: acknowledgement needed",
                $"""
                {(serious ? "A Serious Safety Incident report needs review now. If anyone may be in immediate danger, act before routine work. Evidence is preserved under a provisional legal hold." : "An urgent report needs review.")}

                Case: {item.Id}
                Category: {category}
                Queue: {item.Queue}

                Open the Swoovr admin app and acknowledge the case. It stays open until someone does.
                """), cancellationToken);
        item.EscalatedAt = now;
        Audit.Record(db, now, null, "system", sent ? "case.escalated" : "case.escalation-failed", "moderation-case", item.Id.ToString(),
            string.IsNullOrWhiteSpace(to) ? "unconfigured" : null);
        await db.SaveChangesAsync(cancellationToken);
        if (!sent) logs.CreateLogger("Swoovr.Api.Moderation").LogWarning("Urgent case {Case} was not emailed.", item.Id);
    }

    private static async Task<List<NoticeView>> NoticesAsync(SwoovrDbContext db, Guid account, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var notices = await db.MemberNotices.Where(n => n.AccountId == account).OrderByDescending(n => n.CreatedAt).Take(50).ToListAsync(cancellationToken);
        var ids = notices.Where(n => n.SanctionId is not null).Select(n => n.SanctionId!.Value).Distinct().ToList();
        var sanctions = await db.Sanctions.Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, cancellationToken);
        var appeals = await db.Appeals.Where(a => ids.Contains(a.SanctionId)).ToDictionaryAsync(a => a.SanctionId, cancellationToken);
        return notices.Select(n => new NoticeView(n.Id, Wire(n.Kind), n.CreatedAt, n.ReadAt is not null,
            n.SanctionId is { } sid && sanctions.TryGetValue(sid, out var s)
                ? new SanctionNoticeView(s.Id, Wire(s.Kind), s.Reason, s.StartsAt, s.EndsAt, Enforcement.Live(s, now), s.HideFromDiscovery, s.NoNewRequests, s.NoMessages,
                    ModerationPolicy.MayAppeal(s, account, appeals.ContainsKey(s.Id), now), appeals.TryGetValue(s.Id, out var a) ? Wire(a.Status) : null)
                : null)).ToList();
    }

    /// <summary>D135: an in-app notice, and a short email that says only that there is one (Mailpit locally).</summary>
    private static async Task NotifyAsync(SwoovrDbContext db, IEmailSender email, Guid account, NoticeKind kind, Guid? sanction, DateTimeOffset now, CancellationToken cancellationToken)
    {
        db.MemberNotices.Add(new MemberNotice { Id = Guid.NewGuid(), AccountId = account, Kind = kind, SanctionId = sanction, CreatedAt = now });
        await db.SaveChangesAsync(cancellationToken);
        if (await db.Accounts.Where(a => a.Id == account).Select(a => a.Email).SingleOrDefaultAsync(cancellationToken) is { } address)
            await email.SendAsync(new EmailMessage(address, "A notice about your Swoovr account",
                """
                There's a new notice about your Swoovr account. Sign in to Swoovr and open Me to read it.

                — Swoovr (RePass Cloud Pty Ltd)
                """), cancellationToken);
    }

    private static void MapAdmin(WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/admin").RequireAuthorization();

        admin.MapGet("/cases", async (string? queue, string? status, ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var query = db.ModerationCases.AsQueryable();
            // Support never sees the admin queue (D134).
            if (current!.StaffRole != AccountRole.Admin) query = query.Where(c => c.Queue == CaseQueue.Support);
            else if (Enum.TryParse<CaseQueue>(queue, true, out var q)) query = query.Where(c => c.Queue == q);
            query = status == "all" ? query : Enum.TryParse<CaseStatus>(status, true, out var s) ? query.Where(c => c.Status == s)
                : query.Where(c => c.Status == CaseStatus.Open || c.Status == CaseStatus.UnderReview);
            var cases = await query.OrderByDescending(c => c.Priority).ThenBy(c => c.CreatedAt).Take(200).ToListAsync(cancellationToken);
            return Results.Ok(await SummariesAsync(db, cases, cancellationToken));
        });
        admin.MapGet("/cases/{id:guid}", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, item, denied) = await CaseAsync(user, accounts, id, cancellationToken);
            return denied ?? Results.Ok(await DetailAsync(accounts.Db!, item!, clock.GetUtcNow(), cancellationToken));
        });
        admin.MapPost("/cases/{id:guid}/{step:regex(^(review|acknowledge|close)$)}", async (Guid id, string step, ClaimsPrincipal user, AccountResolver accounts,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, item, denied) = await CaseAsync(user, accounts, id, cancellationToken);
            if (denied is not null) return denied;
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            var subject = await db.MemberProfiles.FindAsync([item!.SubjectAccountId], cancellationToken);
            var refusal = ModerationPolicy.Check(ModerationAction.Acknowledge, current!.StaffRole, subject?.Employee == true || item.Queue == CaseQueue.Admin,
                await OwnAccountAsync(db, current.Account!.Id, item.SubjectAccountId, cancellationToken));
            if (refusal != ModerationRefusal.None) return Refused(refusal);
            switch (step)
            {
                case "review" when item.Status == CaseStatus.Open: item.Status = CaseStatus.UnderReview; break;
                case "acknowledge" when item.EscalatedAt is not null && item.AcknowledgedAt is null: (item.AcknowledgedAt, item.AcknowledgedBy) = (now, current.Account.Id); break;
                case "close" when item.Status != CaseStatus.Closed:
                    if (item.EscalatedAt is not null && item.AcknowledgedAt is null)
                        return Problem(StatusCodes.Status409Conflict, ProblemCodes.InvalidRequest, "Acknowledge the urgent report first.");
                    (item.Status, item.ClosedAt, item.ClosedBy) = (CaseStatus.Closed, now, current.Account.Id);
                    break;
                default: return Problem(StatusCodes.Status409Conflict, ProblemCodes.InvalidRequest, "That step doesn't apply to this case now.");
            }
            item.UpdatedAt = now;
            Audit.Record(db, now, current.Account.Id, "account", "case." + step, "moderation-case", item.Id.ToString(), current.StaffRole.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(await DetailAsync(db, item, now, cancellationToken));
        });
        admin.MapPost("/cases/{id:guid}/actions", async (Guid id, ModerationActionRequest request, ClaimsPrincipal user, AccountResolver accounts, IEmailSender email,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, item, denied) = await CaseAsync(user, accounts, id, cancellationToken);
            if (denied is not null) return denied;
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            if (item!.Status == CaseStatus.Closed) return Problem(StatusCodes.Status409Conflict, ProblemCodes.InvalidRequest, "This case is closed.");
            var result = await ActAsync(db, email, current!, item.SubjectAccountId, item, request, now, cancellationToken);
            if (result is not null) return result;
            return Results.Ok(await DetailAsync(db, item, now, cancellationToken));
        });
        // Actions on an account without a report (for example after a support contact) follow the same rules.
        admin.MapPost("/accounts/{id:guid}/sanctions", async (Guid id, ModerationActionRequest request, ClaimsPrincipal user, AccountResolver accounts, IEmailSender email,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            if (await db.MemberProfiles.FindAsync([id], cancellationToken) is null) return Results.NotFound();
            if (request.Action == "dismiss") return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Nothing to dismiss.");
            return await ActAsync(db, email, current!, id, null, request, clock.GetUtcNow(), cancellationToken) ?? Results.Ok(await SummaryAsync(db, id, clock.GetUtcNow(), cancellationToken));
        });
        admin.MapPost("/sanctions/{id:guid}/lift", async (Guid id, LiftRequest request, ClaimsPrincipal user, AccountResolver accounts, IEmailSender email,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, cancellationToken);
            if (denied is not null) return denied;
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            var sanction = await db.Sanctions.FindAsync([id], cancellationToken);
            if (sanction is null) return Results.NotFound();
            if (!Enforcement.Live(sanction, now)) return Problem(StatusCodes.Status409Conflict, ProblemCodes.InvalidRequest, "This action has already ended.");
            var subject = await db.MemberProfiles.FindAsync([sanction.AccountId], cancellationToken);
            var refusal = ModerationPolicy.Check(ModerationAction.Lift, current!.StaffRole, subject?.Employee == true,
                await OwnAccountAsync(db, current.Account!.Id, sanction.AccountId, cancellationToken), reason: request.Reason, lifting: sanction);
            if (refusal != ModerationRefusal.None) return Refused(refusal);
            (sanction.LiftedAt, sanction.LiftedBy) = (now, current.Account.Id);
            Audit.Record(db, now, current.Account.Id, "account", "sanction.lifted", "sanction", sanction.Id.ToString(), current.StaffRole.ToString());
            await db.SaveChangesAsync(cancellationToken);
            await NotifyAsync(db, email, sanction.AccountId, NoticeKind.SanctionLifted, sanction.Id, now, cancellationToken);
            return Results.Ok(View(sanction, now));
        });

        admin.MapGet("/appeals", async (string? status, ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var wanted = Enum.TryParse<AppealStatus>(status, true, out var s) ? s : AppealStatus.Pending;
            var rows = await (from a in db.Appeals
                              join sanction in db.Sanctions on a.SanctionId equals sanction.Id
                              join p in db.MemberProfiles on a.AccountId equals p.AccountId
                              where a.Status == wanted && (current!.StaffRole == AccountRole.Admin || !p.Employee && sanction.CreatedByRole != AccountRole.Admin)
                              orderby a.CreatedAt
                              select new AppealAdminView(a.Id, a.SanctionId, a.AccountId, sanction.Kind.ToString(), a.Text, a.Status.ToString(), a.CreatedAt, a.DecidedBy, a.DecidedAt))
                .Take(200).ToListAsync(cancellationToken);
            return Results.Ok(rows.Select(r => r with { SanctionKind = JsonNamingPolicy.CamelCase.ConvertName(r.SanctionKind), Status = JsonNamingPolicy.CamelCase.ConvertName(r.Status) }).ToList());
        });
        admin.MapPost("/appeals/{id:guid}/decide", async (Guid id, AppealDecision request, ClaimsPrincipal user, AccountResolver accounts, IEmailSender email,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, cancellationToken);
            if (denied is not null) return denied;
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            var appeal = await db.Appeals.FindAsync([id], cancellationToken);
            if (appeal is null) return Results.NotFound();
            if (appeal.Status != AppealStatus.Pending) return Problem(StatusCodes.Status409Conflict, ProblemCodes.InvalidRequest, "This appeal has been decided.");
            if (request.Outcome is not ("uphold" or "overturn")) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Uphold or overturn.");
            var sanction = await db.Sanctions.SingleAsync(s => s.Id == appeal.SanctionId, cancellationToken);
            var subject = await db.MemberProfiles.FindAsync([sanction.AccountId], cancellationToken);
            var refusal = ModerationPolicy.MayDecideAppeal(sanction, current!.Account!.Id, current.StaffRole, subject?.Employee == true,
                await OwnAccountAsync(db, current.Account.Id, sanction.AccountId, cancellationToken));
            if (refusal != ModerationRefusal.None) return Refused(refusal);
            (appeal.Status, appeal.DecidedBy, appeal.DecidedAt) = (request.Outcome == "overturn" ? AppealStatus.Overturned : AppealStatus.Upheld, current.Account.Id, now);
            if (appeal.Status == AppealStatus.Overturned && sanction.LiftedAt is null) (sanction.LiftedAt, sanction.LiftedBy) = (now, current.Account.Id);
            Audit.Record(db, now, current.Account.Id, "account", "appeal." + request.Outcome, "appeal", appeal.Id.ToString(), current.StaffRole.ToString());
            await db.SaveChangesAsync(cancellationToken);
            await NotifyAsync(db, email, appeal.AccountId, appeal.Status == AppealStatus.Overturned ? NoticeKind.AppealOverturned : NoticeKind.AppealUpheld, sanction.Id, now, cancellationToken);
            return Results.Ok(new AppealView(appeal.Id, sanction.Id, Wire(appeal.Status), appeal.CreatedAt));
        });

        // D134, D146: an admin links a staff-realm account to the one member account its owner uses personally.
        admin.MapPut("/staff-links/{staffId:guid}", async (Guid staffId, StaffLinkRequest request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            var staff = await db.Accounts.FindAsync([staffId], cancellationToken);
            if (staff is not { Realm: AccountRealm.Staff }) return Results.NotFound();
            var existing = await db.StaffMemberLinks.FindAsync([staffId], cancellationToken);
            if (existing is not null) db.StaffMemberLinks.Remove(existing);
            if (request.MemberAccountId is { } member)
            {
                if (await db.Accounts.FindAsync([member], cancellationToken) is not { Realm: AccountRealm.Members }) return Results.NotFound();
                db.StaffMemberLinks.Add(new StaffMemberLink { StaffAccountId = staffId, MemberAccountId = member, LinkedAt = now, LinkedBy = current!.Account!.Id });
            }
            Audit.Record(db, now, current!.Account!.Id, "account", request.MemberAccountId is null ? "staff-link.removed" : "staff-link.set", "account", staffId.ToString(),
                request.MemberAccountId?.ToString());
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException) { return Problem(StatusCodes.Status409Conflict, ProblemCodes.InvalidRequest, "That member account is linked to another staff account."); }
            return Results.NoContent();
        });

        // D150: an admin lifts the 30-day wait for a genuine move or a mistake; the next home-city change may enter another market.
        admin.MapPost("/accounts/{id:guid}/home-move-override", async (Guid id, HomeMoveOverride request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > ModerationPolicy.ReasonMax)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Give a reason.");
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            var profile = await db.MemberProfiles.FindAsync([id], cancellationToken);
            if (profile is null) return Results.NotFound();
            (profile.HomeMarketMovedTo, profile.HomeMarketMovedAt) = (null, null);
            Audit.Record(db, now, current!.Account!.Id, "account", "home-market.override", "account", id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(await SummaryAsync(db, id, now, cancellationToken));
        });

        admin.MapGet("/accounts/{id:guid}/summary", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (_, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, cancellationToken);
            if (denied is not null) return denied;
            return await accounts.Db!.Accounts.FindAsync([id], cancellationToken) is null ? Results.NotFound()
                : Results.Ok(await SummaryAsync(accounts.Db!, id, clock.GetUtcNow(), cancellationToken));
        });

        // D151: complimentary Plus for beta testers; admins grant, change, extend and revoke, all audited.
        admin.MapPost("/accounts/{id:guid}/entitlements", async (Guid id, EntitlementGrant request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            if (await db.MemberProfiles.FindAsync([id], cancellationToken) is null) return Results.NotFound();
            var starts = request.StartsAt ?? now;
            if (!Enum.TryParse<PlanTier>(request.Tier, true, out var tier) || !Enum.IsDefined(tier) || int.TryParse(request.Tier, out _)
                || !EntitlementPolicy.ValidGrant(tier, starts, request.EndsAt, request.Reason?.Trim()))
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidEntitlement, "A grant needs Plus or Premium, an end within a year and a reason.");
            var grant = new Entitlement { Id = Guid.NewGuid(), AccountId = id, Tier = tier, Source = EntitlementSource.AdminGrant, StartsAt = starts, EndsAt = request.EndsAt,
                Reason = request.Reason!.Trim(), GrantedBy = current!.Account!.Id, CreatedAt = now };
            db.Entitlements.Add(grant);
            Audit.Record(db, now, current.Account.Id, "account", "entitlement.granted", "account", id.ToString(), $"{tier}:{grant.Id}");
            await db.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/v1/admin/entitlements/{grant.Id}", Entitlements.View(grant, now));
        });
        admin.MapPut("/entitlements/{id:guid}", async (Guid id, EntitlementChange request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            var grant = await db.Entitlements.FindAsync([id], cancellationToken);
            if (grant is not { Source: EntitlementSource.AdminGrant } || grant.RevokedAt is not null) return Results.NotFound();
            var tier = grant.Tier;
            if (request.Tier is not null && (!Enum.TryParse(request.Tier, true, out tier) || !Enum.IsDefined(tier) || int.TryParse(request.Tier, out _)))
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidEntitlement, "Unknown tier.");
            var ends = request.EndsAt ?? grant.EndsAt;
            var reason = request.Reason?.Trim() ?? grant.Reason;
            // An extension is measured from now, so a grant can run on in yearly steps.
            if (!EntitlementPolicy.ValidGrant(tier, grant.StartsAt > now ? grant.StartsAt : now, ends, reason))
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidEntitlement, "A grant needs Plus or Premium, an end within a year and a reason.");
            (grant.Tier, grant.EndsAt, grant.Reason) = (tier, ends, reason);
            Audit.Record(db, now, current!.Account!.Id, "account", "entitlement.changed", "account", grant.AccountId.ToString(), $"{tier}:{grant.Id}");
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(Entitlements.View(grant, now));
        });
        admin.MapPost("/entitlements/{id:guid}/revoke", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            var grant = await db.Entitlements.FindAsync([id], cancellationToken);
            if (grant is not { Source: EntitlementSource.AdminGrant } || grant.RevokedAt is not null) return Results.NotFound();
            (grant.RevokedAt, grant.RevokedBy) = (now, current!.Account!.Id);
            Audit.Record(db, now, current.Account.Id, "account", "entitlement.revoked", "account", grant.AccountId.ToString(), grant.Id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(Entitlements.View(grant, now));
        });
    }

    private static IResult Refused(ModerationRefusal refusal) => refusal switch
    {
        ModerationRefusal.TooLong => Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Restrictions last up to 30 days and suspensions up to 90."),
        ModerationRefusal.ReasonRequired => Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Give a reason (up to 1,000 characters)."),
        _ => Problem(StatusCodes.Status403Forbidden, ProblemCodes.ModerationNotPermitted, refusal switch
        {
            ModerationRefusal.OwnAccount => "You can't decide a case about your own member account.",
            ModerationRefusal.StaffAffiliatedCase => "Cases about staff-affiliated accounts are for admins.",
            ModerationRefusal.AdminSanction => "An admin applied this; only an admin can change it.",
            _ => "Your role can't do this.",
        }),
    };

    private static async Task<bool> OwnAccountAsync(SwoovrDbContext db, Guid staff, Guid member, CancellationToken cancellationToken) =>
        staff == member || await db.StaffMemberLinks.AnyAsync(l => l.StaffAccountId == staff && l.MemberAccountId == member, cancellationToken);

    /// <summary>A case the caller's role may open: support never sees the admin queue (404, like an unknown id).</summary>
    private static async Task<(CurrentAccount? Current, ModerationCase? Case, IResult? Denied)> CaseAsync(ClaimsPrincipal user, AccountResolver accounts, Guid id,
        CancellationToken cancellationToken)
    {
        var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, cancellationToken);
        if (denied is not null) return (null, null, denied);
        var item = await accounts.Db!.ModerationCases.FindAsync([id], cancellationToken);
        if (item is null || item.Queue == CaseQueue.Admin && current!.StaffRole != AccountRole.Admin) return (null, null, Results.NotFound());
        return (current, item, null);
    }

    /// <summary>Applies one action (D133) and notifies the member; returns a refusal, or null when done.</summary>
    private static async Task<IResult?> ActAsync(SwoovrDbContext db, IEmailSender email, CurrentAccount current, Guid subjectId, ModerationCase? item,
        ModerationActionRequest request, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var action = request.Action switch
        {
            "dismiss" => ModerationAction.Dismiss, "warn" => ModerationAction.Warn, "restrict" => ModerationAction.Restrict,
            "suspend" => ModerationAction.Suspend, "terminate" => ModerationAction.Terminate, _ => (ModerationAction?)null,
        };
        if (action is null) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Unknown action.");
        var subject = await db.MemberProfiles.FindAsync([subjectId], cancellationToken);
        var staffAffiliated = subject?.Employee == true || item?.Queue == CaseQueue.Admin;
        var duration = request.Days is { } days ? TimeSpan.FromDays(days) : (TimeSpan?)null;
        var refusal = ModerationPolicy.Check(action.Value, current.StaffRole, staffAffiliated, await OwnAccountAsync(db, current.Account!.Id, subjectId, cancellationToken),
            duration, request.Reason?.Trim());
        if (refusal != ModerationRefusal.None) return Refused(refusal);
        // DEFAULT: suspension and termination need a sign-in within the last 15 minutes, like other sensitive admin actions.
        if (action is ModerationAction.Suspend or ModerationAction.Terminate && !current.RecentlyAuthenticated(now))
            return Problem(StatusCodes.Status403Forbidden, ProblemCodes.StaleSession, "Sign in again to do this.");
        Sanction? sanction = null;
        if (action != ModerationAction.Dismiss)
        {
            var kind = action switch
            {
                ModerationAction.Warn => SanctionKind.Warning, ModerationAction.Restrict => SanctionKind.Restriction,
                ModerationAction.Suspend => SanctionKind.Suspension, _ => SanctionKind.Termination,
            };
            sanction = new Sanction
            {
                Id = Guid.NewGuid(), AccountId = subjectId, CaseId = item?.Id, Kind = kind, Reason = request.Reason!.Trim(), StartsAt = now,
                EndsAt = kind is SanctionKind.Restriction or SanctionKind.Suspension ? now + duration : null,
                HideFromDiscovery = kind == SanctionKind.Restriction && request.HideFromDiscovery,
                NoNewRequests = kind == SanctionKind.Restriction && request.NoNewRequests,
                NoMessages = kind == SanctionKind.Restriction && request.NoMessages,
                CreatedBy = current.Account.Id, CreatedByRole = current.StaffRole,
            };
            db.Sanctions.Add(sanction);
        }
        if (item is not null) (item.Status, item.UpdatedAt) = (CaseStatus.Reviewed, now);
        Audit.Record(db, now, current.Account.Id, "account", "moderation." + request.Action, item is null ? "account" : "moderation-case",
            (item?.Id ?? subjectId).ToString(), $"{current.StaffRole}:{sanction?.Id}");
        await db.SaveChangesAsync(cancellationToken);
        if (sanction is not null)
            await NotifyAsync(db, email, subjectId, sanction.Kind switch
            {
                SanctionKind.Warning => NoticeKind.Warning, SanctionKind.Restriction => NoticeKind.Restriction,
                SanctionKind.Suspension => NoticeKind.Suspension, _ => NoticeKind.Termination,
            }, sanction.Id, now, cancellationToken);
        return null;
    }

    private static SanctionView View(Sanction s, DateTimeOffset now) => new(s.Id, s.AccountId, s.CaseId, Wire(s.Kind), s.Reason, s.StartsAt, s.EndsAt,
        s.HideFromDiscovery, s.NoNewRequests, s.NoMessages, s.CreatedBy, Wire(s.CreatedByRole), s.LiftedAt, Enforcement.Live(s, now));

    private static async Task<List<CaseSummaryView>> SummariesAsync(SwoovrDbContext db, List<ModerationCase> cases, CancellationToken cancellationToken)
    {
        var ids = cases.Select(c => c.Id).ToList();
        var subjects = cases.Select(c => c.SubjectAccountId).Distinct().ToList();
        var counts = await db.Reports.Where(r => ids.Contains(r.CaseId)).GroupBy(r => r.CaseId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
        var names = await db.MemberProfiles.Where(p => subjects.Contains(p.AccountId)).ToDictionaryAsync(p => p.AccountId, p => p.DisplayName, cancellationToken);
        return cases.Select(c => new CaseSummaryView(c.Id, c.SubjectAccountId, names.GetValueOrDefault(c.SubjectAccountId), Wire(c.Status), Wire(c.Queue), Wire(c.Priority),
            counts.GetValueOrDefault(c.Id), c.CreatedAt, c.UpdatedAt, c.EscalatedAt is not null && c.AcknowledgedAt is null, c.SystemFlag)).ToList();
    }

    private static async Task<CaseDetailView> DetailAsync(SwoovrDbContext db, ModerationCase item, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var reports = await db.Reports.Where(r => r.CaseId == item.Id).OrderBy(r => r.CreatedAt).ToListAsync(cancellationToken);
        var evidence = await db.CaseEvidence.Where(e => e.CaseId == item.Id).OrderBy(e => e.CapturedAt).ToListAsync(cancellationToken);
        var sanctions = await db.Sanctions.Where(s => s.AccountId == item.SubjectAccountId).OrderByDescending(s => s.StartsAt).ToListAsync(cancellationToken);
        var subject = await db.MemberProfiles.FindAsync([item.SubjectAccountId], cancellationToken);
        return new((await SummariesAsync(db, [item], cancellationToken))[0], subject?.Employee == true,
            reports.Select(r => new CaseReportView(r.Id, r.ReporterAccountId, Wire(r.SubjectType), r.SubjectRef, r.CategoryId, Wire(r.Priority), r.Text, r.CreatedAt, r.Police)).ToList(),
            evidence.Select(e => new CaseEvidenceView(e.ReportId, e.Kind, e.Snapshot, e.CapturedAt)).ToList(),
            sanctions.Select(s => View(s, now)).ToList());
    }

    public static async Task<AccountAdminSummary> SummaryAsync(SwoovrDbContext db, Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.SingleAsync(a => a.Id == id, cancellationToken);
        var profile = await db.MemberProfiles.FindAsync([id], cancellationToken);
        var sanctions = await db.Sanctions.Where(s => s.AccountId == id).OrderByDescending(s => s.StartsAt).ToListAsync(cancellationToken);
        var grants = await db.Entitlements.Where(e => e.AccountId == id).OrderByDescending(e => e.CreatedAt).ToListAsync(cancellationToken);
        var plan = profile is null ? PlanTier.Free : EntitlementPolicy.Effective(grants, profile.FoundingMember, profile.FounderEntitlementFrom, now);
        return new(id, Wire(account.Realm), Wire(account.Status), profile?.DisplayName, profile is null ? null : Wire(profile.AgeStatus),
            profile is null ? null : Wire(profile.IdStatus), profile?.CanRequestIdVerification ?? false, profile?.Bot ?? false, profile?.Employee ?? false,
            profile?.FoundingMember ?? false, profile?.HomeMarketId, profile?.HomeMarketMovedAt, Wire(plan), sanctions.Select(s => View(s, now)).ToList(),
            grants.Select(g => Entitlements.View(g, now)).ToList(),
            await db.ModerationCases.CountAsync(c => c.SubjectAccountId == id && (c.Status == CaseStatus.Open || c.Status == CaseStatus.UnderReview), cancellationToken),
            profile?.DateOfBirthVerified ?? false, profile?.HomeReviewFlaggedAt,
            // D150: counts only, never where; the last 30 days' device and IP checks that disagreed with the home city.
            await db.HomeLocationChecks.CountAsync(c => c.AccountId == id && c.Outcome == HomeConsistency.Inconsistent && c.CheckedAt > now - HomeLocationPolicy.Window, cancellationToken),
            profile is null ? "none" : Wire(profile.DobCheck));
    }
}

/// <summary>
/// The entitlement service (D151): every feature asks this for the member's effective tier, never a payment
/// provider. Admin grants, later promotions and paid plans are rows in `entitlements`; founding membership is read from
/// the profile.
/// </summary>
public static class Entitlements
{
    public static async Task<PlanTier> TierAsync(SwoovrDbContext db, MemberProfile profile, DateTimeOffset now, CancellationToken cancellationToken) =>
        EntitlementPolicy.Effective(await db.Entitlements.Where(e => e.AccountId == profile.AccountId && e.RevokedAt == null).ToListAsync(cancellationToken),
            profile.FoundingMember, profile.FounderEntitlementFrom, now);

    public static async Task<PlanView> PlanAsync(SwoovrDbContext db, MemberProfile profile, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var valid = (await db.Entitlements.Where(e => e.AccountId == profile.AccountId && e.RevokedAt == null).ToListAsync(cancellationToken))
            .Where(e => EntitlementPolicy.Valid(e, now)).OrderByDescending(e => e.Tier).ThenByDescending(e => e.EndsAt).FirstOrDefault();
        var tier = await TierAsync(db, profile, now, cancellationToken);
        if (tier == PlanTier.Free) return new("free", null, null, false);
        if (valid is not null && valid.Tier == tier)
            return new(AccountEndpoints.Wire(tier), AccountEndpoints.Wire(valid.Source), valid.EndsAt, valid.Source == EntitlementSource.AdminGrant);
        return new(AccountEndpoints.Wire(tier), "founding", null, true);
    }

    public static EntitlementView View(Entitlement e, DateTimeOffset now) => new(e.Id, AccountEndpoints.Wire(e.Tier), AccountEndpoints.Wire(e.Source), e.StartsAt, e.EndsAt,
        e.Reason, e.GrantedBy, e.CreatedAt, e.RevokedAt, EntitlementPolicy.Valid(e, now));
}
