using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using Swoovr.Infrastructure.Verification;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Verification;

/// <summary>
/// Optional ID verification through Didit's hosted flow (D126–D129, D147, D152). The API creates the session with the
/// internal account id as vendor_data and hands the member Didit's link, which they open in their external browser.
/// Results arrive by the signed webhook and, when the member comes back, by re-reading the decision from Didit, so
/// verification also works without a webhook. Swoovr keeps only the outcome and operational metadata (D128). Every
/// change is audited with ids and machine values only.
/// </summary>
public static class VerificationEndpoints
{
    /// <summary>Webhook bodies above this are refused unread (DEFAULT; Didit decisions are a few kilobytes).</summary>
    public const int MaxWebhookBytes = 1024 * 1024;

    public static void Map(WebApplication app)
    {
        var me = app.MapGroup("/api/v1/me/verification").RequireAuthorization();
        me.MapGet("", async (ClaimsPrincipal user, AccountResolver accounts, IIdVerificationProvider provider, IConfiguration configuration,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken, ageGateExempt: true);
            if (denied is not null) return denied;
            var (db, profile, now) = (accounts.Db!, current!.Profile!, clock.GetUtcNow());
            // D152: coming back from Didit re-reads the session, so the result shows without waiting for a webhook. The
            // query string Didit appends to the return page is never trusted; only Didit's own answer is.
            var open = await OpenAttemptAsync(db, profile.AccountId, cancellationToken);
            if (open is not null && provider.Configured && await provider.GetDecisionAsync(open.SessionId, cancellationToken) is { } decision
                && decision.SessionId == open.SessionId && decision.VendorData == profile.AccountId.ToString())
                await ApplyAsync(db, open, decision, now, "return", cancellationToken);
            return Results.Ok(await ViewAsync(db, profile, provider, configuration, cancellationToken));
        });
        me.MapPost("", async (StartVerification request, ClaimsPrincipal user, AccountResolver accounts, IIdVerificationProvider provider,
            IConfiguration configuration, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken, ageGateExempt: true);
            if (denied is not null) return denied;
            var (db, profile, now) = (accounts.Db!, current!.Profile!, clock.GetUtcNow());
            if (!provider.Configured)
                return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.VerificationNotConfigured, "ID verification isn't available right now.");
            // The return address comes from configuration by client kind, never from the request (no open redirect).
            var callback = request.Client switch
            {
                "web" => configuration["Didit:Callbacks:Web"] ?? "http://localhost:5081/me/verification/return",
                "app" => configuration["Didit:Callbacks:App"] ?? "com.swoovr.app:/verification",
                _ => null,
            };
            if (callback is null) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Unknown client.");
            var attempts = await db.IdVerificationAttempts.Where(a => a.AccountId == profile.AccountId).ToListAsync(cancellationToken);
            switch (IdVerificationPolicy.CanStart(profile, attempts))
            {
                case IdStartRefusal.InProgress: return Results.Ok(await ViewAsync(db, profile, provider, configuration, cancellationToken));
                case IdStartRefusal.AlreadyVerified: return Problem(StatusCodes.Status409Conflict, ProblemCodes.AlreadyVerified, "Your ID is already verified.");
                case IdStartRefusal.AttemptUsed:
                    return Problem(StatusCodes.Status409Conflict, ProblemCodes.VerificationAttemptUsed, "You've used your ID check. Contact Swoovr support if you'd like another.");
                case not IdStartRefusal.None: return Problem(StatusCodes.Status403Forbidden, ProblemCodes.NotAvailable, "ID verification isn't available for this account.");
            }
            var session = await provider.CreateSessionAsync(profile.AccountId, callback, cancellationToken);
            if (session is null || IdVerificationPolicy.Map(session.Status) is not { } state || session.Url is null)
            {
                // A technical failure before any real attempt keeps the member's attempt (D129).
                Audit.Record(db, now, profile.AccountId, "account", "verification.provider-failed", "account", profile.AccountId.ToString());
                await db.SaveChangesAsync(cancellationToken);
                return Problem(StatusCodes.Status502BadGateway, ProblemCodes.VerificationProviderFailed, "We couldn't start the ID check. Try again shortly.");
            }
            var attempt = new IdVerificationAttempt
            {
                Id = Guid.NewGuid(), AccountId = profile.AccountId, SessionId = session.SessionId, WorkflowId = session.WorkflowId ?? provider.WorkflowId,
                State = state, ProviderStatus = Short(session.Status), Started = state is IdAttemptState.InProgress or IdAttemptState.InReview, SelfService = profile.CanRequestIdVerification,
                Url = session.Url, CreatedAt = now, UpdatedAt = now,
            };
            db.IdVerificationAttempts.Add(attempt);
            profile.CanRequestIdVerification = false;
            if (profile.IdStatus != VerificationStatus.Verified) profile.IdStatus = VerificationStatus.Pending;
            Audit.Record(db, now, profile.AccountId, "account", "verification.started", "id-verification", attempt.Id.ToString(), request.Client);
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException)
            {
                // A concurrent start won (one open attempt per member); show that one.
                db.ChangeTracker.Clear();
                profile = await db.MemberProfiles.SingleAsync(p => p.AccountId == profile.AccountId, cancellationToken);
            }
            return Results.Ok(await ViewAsync(db, profile, provider, configuration, cancellationToken));
        });

        // D152: Didit calls the API directly (never through the BFF). Signature first, then the signed timestamp, then
        // a durable dedupe on event_id in the same transaction as the change.
        app.MapPost("/api/v1/webhooks/didit", async (HttpRequest http, SwoovrDbContext db, DiditOptions options, TimeProvider clock,
            ILoggerFactory logs, CancellationToken cancellationToken) =>
        {
            var log = logs.CreateLogger("Swoovr.Api.Verification.DiditWebhook");
            if (http.ContentLength > MaxWebhookBytes) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            using var buffer = new MemoryStream();
            await http.Body.CopyToAsync(buffer, cancellationToken);
            if (buffer.Length > MaxWebhookBytes) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
            var now = clock.GetUtcNow();
            var check = DiditSignature.Check(buffer.ToArray(), http.Headers["X-Signature-V2"].FirstOrDefault(), http.Headers["X-Signature"].FirstOrDefault(),
                options.WebhookSecret ?? "", now, out var body);
            if (check != DiditWebhookCheck.Valid)
            {
                // Logged without the body: it may hold someone's decision data.
                log.LogWarning("Didit webhook refused: {Check}.", check);
                Audit.Record(db, now, null, "system", "verification.webhook-refused", null, null, check.ToString());
                await db.SaveChangesAsync(cancellationToken);
                return check == DiditWebhookCheck.BadSignature ? Results.Unauthorized() : Results.BadRequest();
            }
            using (body)
            {
                var root = body!.RootElement;
                var type = root.TryGetProperty("webhook_type", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString()! : "";
                if (!root.TryGetProperty("event_id", out var e) || e.ValueKind != JsonValueKind.String || !Guid.TryParse(e.GetString(), out var eventId))
                    return Results.BadRequest();
                if (await db.DiditWebhookEvents.AnyAsync(w => w.EventId == eventId, cancellationToken)) return Results.Ok(new { duplicate = true });
                var session = DiditClient.Read(root);
                var outcome = "ignored";
                IdVerificationAttempt? attempt = null;
                if (type is "status.updated" or "data.updated" && session is not null)
                {
                    attempt = await db.IdVerificationAttempts.SingleOrDefaultAsync(a => a.SessionId == session.SessionId, cancellationToken);
                    outcome = attempt is null ? "unknown-session"
                        : session.VendorData != attempt.AccountId.ToString() ? "vendor-mismatch"
                        : session.WorkflowId is { } workflow && workflow != attempt.WorkflowId ? "workflow-mismatch"
                        : "applied";
                }
                db.DiditWebhookEvents.Add(new DiditWebhookEvent { EventId = eventId, ReceivedAt = now, WebhookType = Short(type, 48), SessionId = session?.SessionId is { Length: <= 64 } s ? s : null, Outcome = outcome });
                if (outcome == "applied") await ApplyAsync(db, attempt!, session!, now, "webhook", cancellationToken, save: false);
                else if (outcome != "ignored")
                    Audit.Record(db, now, null, "system", "verification.webhook-" + outcome, "id-verification", attempt?.Id.ToString());
                try { await db.SaveChangesAsync(cancellationToken); }
                catch (DbUpdateException)
                {
                    // The same event raced in on another request; it was applied there.
                    return Results.Ok(new { duplicate = true });
                }
                return Results.Ok(new { outcome });
            }
        }).AllowAnonymous();

        var admin = app.MapGroup("/api/v1/admin/accounts/{id:guid}").RequireAuthorization();
        admin.MapGet("/verification", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (_, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var profile = await db.MemberProfiles.FindAsync([id], cancellationToken);
            return profile is null ? Results.NotFound() : Results.Ok(await AdminViewAsync(db, profile, cancellationToken));
        });
        // D129: admins decide case by case. "allow-again" gives one more self-service attempt; "revoke" removes ID
        // Verified. Both need a recent sign-in and are audited.
        admin.MapPost("/verification/{action:regex(^(allow-again|revoke)$)}", async (Guid id, string action, ClaimsPrincipal user, AccountResolver accounts,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var now = clock.GetUtcNow();
            if (!current!.RecentlyAuthenticated(now)) return Problem(StatusCodes.Status403Forbidden, ProblemCodes.StaleSession, "Sign in again to do this.");
            var db = accounts.Db!;
            var profile = await db.MemberProfiles.FindAsync([id], cancellationToken);
            if (profile is null) return Results.NotFound();
            if (action == "allow-again")
            {
                if (profile.IdStatus == VerificationStatus.Verified || await OpenAttemptAsync(db, id, cancellationToken) is not null)
                    return Problem(StatusCodes.Status409Conflict, ProblemCodes.InvalidRequest, "This member is verified or has a check open.");
                profile.CanRequestIdVerification = true;
            }
            else
            {
                if (profile.IdStatus != VerificationStatus.Verified) return Problem(StatusCodes.Status409Conflict, ProblemCodes.InvalidRequest, "This member isn't ID verified.");
                (profile.IdStatus, profile.IdCheckedAt) = (VerificationStatus.Rejected, now);
            }
            Audit.Record(db, now, current.Account!.Id, "account", "verification." + action, "account", id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(await AdminViewAsync(db, profile, cancellationToken));
        });
        // D127: an admin flags an account for age assurance, or clears the flag. Clearing it is only for a flag set in
        // error; an authorised adult resolution is the adult override.
        admin.MapPut("/age-assurance", async (Guid id, AgeAssuranceRequest request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var now = clock.GetUtcNow();
            if (!current!.RecentlyAuthenticated(now)) return Problem(StatusCodes.Status403Forbidden, ProblemCodes.StaleSession, "Sign in again to do this.");
            var db = accounts.Db!;
            var profile = await db.MemberProfiles.FindAsync([id], cancellationToken);
            if (profile is null) return Results.NotFound();
            if (profile.AgeStatus is AgeStatus.Underage) return Problem(StatusCodes.Status409Conflict, ProblemCodes.InvalidRequest, "This account is locked as under 18.");
            if (request.NeedsVerification && profile.AgeStatus != AgeStatus.NeedsVerification)
                (profile.AgeStatus, profile.AgeStatusAt) = (AgeStatus.NeedsVerification, now);
            else if (!request.NeedsVerification && profile.AgeStatus == AgeStatus.NeedsVerification)
                (profile.AgeStatus, profile.AgeStatusAt) = (profile.DateOfBirth is not null || profile.DeclaredAge is not null ? AgeStatus.SelfDeclaredAdult : AgeStatus.Attested, now);
            Audit.Record(db, now, current.Account!.Id, "account", request.NeedsVerification ? "age.needs-verification" : "age.needs-verification-cleared", "account", id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(await AdminViewAsync(db, profile, cancellationToken));
        });
    }

    private static string Short(string value, int max = 32) => value.Length <= max ? value : value[..max];

    private static Task<IdVerificationAttempt?> OpenAttemptAsync(SwoovrDbContext db, Guid accountId, CancellationToken cancellationToken) =>
        db.IdVerificationAttempts.Where(a => a.AccountId == accountId
            && (a.State == IdAttemptState.Created || a.State == IdAttemptState.InProgress || a.State == IdAttemptState.InReview))
            .OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Applies a reported status to an attempt and the member's flags. States only move forward (IdVerificationPolicy.Next),
    /// so a stale re-read or an out-of-order webhook never undoes a result.
    /// </summary>
    public static async Task ApplyAsync(SwoovrDbContext db, IdVerificationAttempt attempt, ProviderSession session, DateTimeOffset now, string source,
        CancellationToken cancellationToken, bool save = true)
    {
        if (IdVerificationPolicy.Next(attempt.State, session.Status) is not { } next) return;
        var profile = await db.MemberProfiles.SingleAsync(p => p.AccountId == attempt.AccountId, cancellationToken);
        var account = await db.Accounts.SingleAsync(a => a.Id == attempt.AccountId, cancellationToken);
        (attempt.State, attempt.ProviderStatus, attempt.UpdatedAt) = (next, Short(session.Status), now);
        // Expiry or abandonment alone isn't a real attempt (D129); progress or a decision is.
        attempt.Started |= next is IdAttemptState.InProgress or IdAttemptState.InReview or IdAttemptState.Approved or IdAttemptState.Declined;
        if (IdVerificationPolicy.Settled(next)) (attempt.CompletedAt, attempt.Url) = (now, null);
        if (next == IdAttemptState.Approved) attempt.Adult = session.Adult;
        if (profile.IdStatus != VerificationStatus.Verified || next == IdAttemptState.Approved)
            (profile.IdStatus, profile.IdCheckedAt) = (IdVerificationPolicy.StatusFor(next), IdVerificationPolicy.Settled(next) ? now : profile.IdCheckedAt);
        if (IdVerificationPolicy.ReturnsAttempt(attempt)) profile.CanRequestIdVerification = true;
        Audit.Record(db, now, null, "system", "verification." + next.ToString().ToLowerInvariant(), "id-verification", attempt.Id.ToString(), source);
        if (next == IdAttemptState.Approved)
        {
            var age = IdVerificationPolicy.AgeAfterApproval(profile.AgeStatus, session.Adult);
            if (age != profile.AgeStatus)
            {
                (profile.AgeStatus, profile.AgeStatusAt) = (age, now);
                Audit.Record(db, now, null, "system", "age.verified", "account", account.Id.ToString(), age.ToString());
            }
            if (age == AgeStatus.Underage)
            {
                // D127: confirmed under-18 accounts are not permitted; the account locks for review.
                (account.Status, account.StatusReason) = (AccountStatus.Locked, "underage");
                (profile.IdStatus, profile.IdCheckedAt) = (VerificationStatus.Rejected, now);
                Audit.Record(db, now, null, "system", "account.locked", "account", account.Id.ToString(), "underage");
            }
            // D158: compare the document's date of birth with the profile's; only an exact match marks it verified. The
            // document's date is dropped here; the audit says matched or not, never either date.
            var matches = IdVerificationPolicy.DateOfBirthMatches(profile.DateOfBirth, session.DateOfBirth);
            (profile.DateOfBirthVerified, profile.DateOfBirthVerifiedAt) = (matches, matches ? now : null);
            Audit.Record(db, now, null, "system", matches ? "dob.verified" : "dob.not-verified", "account", account.Id.ToString(),
                session.DateOfBirth is null ? "no-document-date" : profile.DateOfBirth is null ? "no-profile-date" : null);
            if (FounderEligibility.Stamp(profile, account.Status, now))
                Audit.Record(db, now, null, "system", "founder.eligible", "account", account.Id.ToString());
        }
        if (save) await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task<IdVerificationView> ViewAsync(SwoovrDbContext db, MemberProfile profile, IIdVerificationProvider provider,
        IConfiguration configuration, CancellationToken cancellationToken)
    {
        var attempts = await db.IdVerificationAttempts.Where(a => a.AccountId == profile.AccountId).OrderByDescending(a => a.CreatedAt).ToListAsync(cancellationToken);
        var latest = attempts.FirstOrDefault();
        var refusal = IdVerificationPolicy.CanStart(profile, attempts);
        return new(Wire(profile.IdStatus), profile.IdCheckedAt, latest is null ? null : Wire(latest.State), latest?.Url,
            refusal == IdStartRefusal.None, refusal == IdStartRefusal.None ? null : Wire(refusal), provider.Configured,
            configuration.GetValue("Didit:TestMode", true), AgeAssurance.Blocks(profile.AgeStatus));
    }

    private static async Task<AdminVerificationView> AdminViewAsync(SwoovrDbContext db, MemberProfile profile, CancellationToken cancellationToken)
    {
        var attempts = await db.IdVerificationAttempts.Where(a => a.AccountId == profile.AccountId).OrderByDescending(a => a.CreatedAt).ToListAsync(cancellationToken);
        return new(Wire(profile.IdStatus), profile.CanRequestIdVerification, Wire(profile.AgeStatus),
            attempts.Select(a => new IdAttemptAdminView(a.Id, Wire(a.State), a.ProviderStatus, a.Started, a.SelfService, a.Adult, a.CreatedAt, a.UpdatedAt, a.CompletedAt)).ToList());
    }
}
