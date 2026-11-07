using System.Security.Claims;
using Swoovr.Api.Auth;
using Swoovr.Api.Connections;
using Swoovr.Contracts;
using Swoovr.Domain;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Accounts;

/// <summary>
/// #185 (Q126 A, D317, D341 DEFAULT): Account settings → Security &amp; access lists the member's own Keycloak sessions and linked
/// Apple/Google sign-ins through the existing `swoovr-api-accounts` service client (view-users, manage-users). Members realm only.
/// Never returns tokens, credentials, addresses, provider user ids or Keycloak session ids. Ending another session stops its
/// refreshes at once; an access token that device already holds lapses within its lifetime (5 minutes), the same guarantee as
/// the #87 number-change sign-out. Its open messaging connections close at once on this API instance and, on any instance,
/// when their access token expires (the hub's CloseOnAuthenticationExpiration).
/// </summary>
public static class SignInEndpoints
{
    public static void Map(WebApplication app)
    {
        var me = app.MapGroup("/api/me/sign-ins").RequireAuthorization();

        me.MapGet("", async (ClaimsPrincipal user, AccountResolver accounts, IIdentityAdmin identity, KeycloakOptions keycloak, CancellationToken token) =>
        {
            var (current, denied) = await MembersOnlyAsync(user, accounts, identity, token);
            if (denied is not null) return denied;
            var signIns = await identity.SignInsAsync(current!.Account!.Subject, token);
            return signIns is null ? Unavailable() : Results.Ok(View(signIns, Sid(user), keycloak));
        });

        me.MapPost("/sessions/{handle}/sign-out", async (string handle, ClaimsPrincipal user, AccountResolver accounts, IIdentityAdmin identity,
            KeycloakOptions keycloak, TimeProvider clock, MessagesConnections connections, CancellationToken token) =>
        {
            var (current, denied) = await MembersOnlyAsync(user, accounts, identity, token);
            if (denied is not null) return denied;
            if (!SignInPolicy.ValidHandle(handle)) return Results.NotFound();
            // Without this session's id the handle could be the caller's own session; refuse rather than guess.
            if (Sid(user) is not { } sid) return Problem(StatusCodes.Status409Conflict, ProblemCodes.CurrentSession, "Sign in again, then try this.");
            if (SignInPolicy.Handle(sid) == handle)
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.CurrentSession, "That's this device. Use Sign out instead.");
            var account = current!.Account!;
            if (await identity.SignInsAsync(account.Subject, token) is not { } signIns) return Unavailable();
            if (SignInPolicy.Find(signIns.Sessions, handle, sid) is not { } session) return Results.NotFound();
            if (!await identity.EndSessionAsync(account.Subject, session.Id, token)) return Unavailable();
            // Open messaging connections of that device stop receiving now (this instance) or at token expiry (any instance).
            connections.EndSessions(account.Id, [session.Id]);
            await AuditAsync(accounts, clock, account.Id, "sessions.ended", "one", token);
            return await RefreshedAsync(identity, account.Subject, sid, keycloak, signIns with { Sessions = signIns.Sessions.Where(s => s.Id != session.Id).ToList() }, token);
        });

        me.MapPost("/sessions/sign-out-others", async (ClaimsPrincipal user, AccountResolver accounts, IIdentityAdmin identity,
            KeycloakOptions keycloak, TimeProvider clock, MessagesConnections connections, CancellationToken token) =>
        {
            var (current, denied) = await MembersOnlyAsync(user, accounts, identity, token);
            if (denied is not null) return denied;
            // Without this session's id every session would look like "another device": refuse rather than sign the caller out too.
            if (Sid(user) is not { } sid) return Problem(StatusCodes.Status409Conflict, ProblemCodes.CurrentSession, "Sign in again, then try this.");
            var account = current!.Account!;
            if (await identity.SignInsAsync(account.Subject, token) is not { } signIns) return Unavailable();
            var others = SignInPolicy.Others(signIns.Sessions, sid);
            var ok = true;
            var ended = new List<string>();
            foreach (var other in others)
                if (await identity.EndSessionAsync(account.Subject, other, token)) ended.Add(other); else ok = false;
            connections.EndSessions(account.Id, ended);
            if (others.Count > 0) await AuditAsync(accounts, clock, account.Id, "sessions.ended", ok ? "others" : "others-partial", token);
            return ok ? await RefreshedAsync(identity, account.Subject, sid, keycloak, signIns with { Sessions = signIns.Sessions.Where(s => s.Id == sid).ToList() }, token)
                : Unavailable();
        });

        me.MapPost("/linked/{provider}/remove", async (string provider, ClaimsPrincipal user, AccountResolver accounts, IIdentityAdmin identity,
            KeycloakOptions keycloak, IConfiguration configuration, TimeProvider clock, CancellationToken token) =>
        {
            var (current, denied) = await MembersOnlyAsync(user, accounts, identity, token);
            if (denied is not null) return denied;
            if (!SignInPolicy.ValidProvider(provider)) return Results.NotFound();
            var (account, now) = (current!.Account!, clock.GetUtcNow());
            // Removing a way to sign in is a sensitive change: a fresh sign-in, like changing a number (#87) or deleting the account.
            if (!AccountDeletionPolicy.Fresh(current.Facts.AuthenticatedAt, now, TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("Accounts:ReauthSeconds", 300), 30, 900))))
                return Problem(StatusCodes.Status403Forbidden, ProblemCodes.StaleSession, "Sign in again to remove a linked sign-in.");
            if (await identity.SignInsAsync(account.Subject, token) is not { } before) return Unavailable();
            if (!before.LinkedProviders.Contains(provider)) return Results.NotFound();
            UnlinkOutcome outcome;
            // The lifecycle lease serialises removals on this account, so two at once can't both pass the last-method check.
            await using (await AccountLifecycleLease.AcquireAsync(accounts.Db!, account.Id, token))
                outcome = await identity.UnlinkProviderAsync(account.Subject, provider, token);
            switch (outcome)
            {
                case UnlinkOutcome.NotLinked: return Results.NotFound();
                case UnlinkOutcome.LastSignInMethod:
                    return Problem(StatusCodes.Status409Conflict, ProblemCodes.LastSignInMethod, "Add a password or passkey before removing your only way to sign in.");
                case UnlinkOutcome.Failed: return Unavailable();
            }
            await AuditAsync(accounts, clock, account.Id, "sign-in.unlinked", provider, token);
            return await RefreshedAsync(identity, account.Subject, Sid(user), keycloak,
                before with { LinkedProviders = before.LinkedProviders.Where(p => p != provider).ToList() }, token);
        });
    }

    /// <summary>Members only (AGENTS.md: staff and venue access never use these routes). Security stays reachable during setup gates.</summary>
    private static async Task<(CurrentAccount?, IResult?)> MembersOnlyAsync(ClaimsPrincipal user, AccountResolver accounts, IIdentityAdmin identity, CancellationToken token)
    {
        var (current, denied) = await MemberAsync(user, accounts, token, ageGateExempt: true, phoneExempt: true, policyExempt: true, registeredNamesExempt: true);
        if (denied is not null) return (null, denied);
        if (current!.Facts.Realm != AccountRealm.Members || current.Account!.Realm != AccountRealm.Members) return (null, Results.Forbid());
        if (!identity.Configured) return (null, Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.NotConfigured, "Session details aren't available right now."));
        return (current, null);
    }

    private static string? Sid(ClaimsPrincipal user) => user.FindFirstValue("sid") is { } sid && SignInPolicy.ValidSessionId(sid) ? sid : null;

    private static IResult Unavailable() =>
        Problem(StatusCodes.Status502BadGateway, ProblemCodes.IdentityUnavailable, "That didn't go through. Try again in a moment.");

    /// <summary>The view after a change; if Keycloak can't be re-read, what was known before minus what was just removed.</summary>
    private static async Task<IResult> RefreshedAsync(IIdentityAdmin identity, string subject, string? sid, KeycloakOptions keycloak, IdentitySignIns fallback,
        CancellationToken token) =>
        Results.Ok(View(await identity.SignInsAsync(subject, token) ?? fallback, sid, keycloak));

    public static SignInsView View(IdentitySignIns signIns, string? sid, KeycloakOptions keycloak) => new(
        signIns.Sessions.Select(s => new SignInSessionView(SignInPolicy.Handle(s.Id), SignInPolicy.Device(s.Clients), s.Started, s.LastActive, s.Id == sid))
            .OrderByDescending(s => s.Current).ThenByDescending(s => s.LastActive).ToList(),
        signIns.LinkedProviders.Select(p => new LinkedSignInView(p, Label(p, keycloak), SignInPolicy.CanUnlink(signIns, p))).ToList(),
        signIns.HasPassword, signIns.HasPasskey, sid is not null && signIns.Sessions.Any(s => s.Id == sid));

    private static string Label(string alias, KeycloakOptions keycloak) =>
        alias == keycloak.AppleAlias ? "Apple" : alias == keycloak.GoogleAlias ? "Google" : SignInPolicy.ProviderLabel(alias);

    private static async Task AuditAsync(AccountResolver accounts, TimeProvider clock, Guid accountId, string action, string detail, CancellationToken token)
    {
        Audit.Record(accounts.Db!, clock.GetUtcNow(), accountId, "account", action, "account", accountId.ToString(), detail);
        await accounts.Db!.SaveChangesAsync(token);
    }
}
