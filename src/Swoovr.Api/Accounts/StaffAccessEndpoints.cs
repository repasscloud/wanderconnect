using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Accounts;

/// <summary>
/// Staff access controls (internal policy §11, §15; procedure §5; #88): time-limited elevated access with a recorded
/// purpose, the append-only sensitive-access log, conflict-of-interest exclusions, bulk export approvals and staff
/// revocation. Audit entries carry ids only.
/// </summary>
public static class StaffAccessEndpoints
{
    public static void Map(WebApplication app)
    {
        var access = app.MapGroup("/api/admin/access").RequireAuthorization();

        access.MapGet("/grants", async (ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AnyStaffAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var now = clock.GetUtcNow();
            var grants = await accounts.Db!.AccessGrants.Where(g => g.AccountId == current!.Account!.Id).OrderByDescending(g => g.GrantedAt).Take(50).ToListAsync(cancellationToken);
            return Results.Ok(grants.Select(g => View(g, now)).ToList());
        });

        access.MapPost("/grants", async (AccessGrantRequest request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AnyStaffAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, now, me) = (accounts.Db!, clock.GetUtcNow(), current!.Account!.Id);
            switch (StaffAccessPolicy.CheckRequest(request.Capability, request.Purpose, current.Allows))
            {
                case AccessRefusal.Unknown: return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Unknown capability.");
                case AccessRefusal.NotPermitted: return Results.Forbid();
                case AccessRefusal.PurposeRequired: return Problem(StatusCodes.Status400BadRequest, ProblemCodes.PurposeRequired, "Record the purpose (up to 500 characters).");
            }
            // One live grant per capability: a new request replaces the previous one.
            await db.AccessGrants.Where(g => g.AccountId == me && g.Capability == request.Capability && g.RevokedAt == null && g.ExpiresAt > now)
                .ExecuteUpdateAsync(u => u.SetProperty(g => g.RevokedAt, now).SetProperty(g => g.RevokedBy, me).SetProperty(g => g.RevokedReason, "superseded"), cancellationToken);
            var grant = StaffAccessPolicy.Grant(me, request.Capability!, request.Purpose!, now);
            db.AccessGrants.Add(grant);
            Audit.Record(db, now, me, "account", "access.granted", "access-grant", grant.Id.ToString(), grant.Capability);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/admin/access/grants/{grant.Id}", View(grant, now));
        });

        access.MapPost("/grants/{id:guid}/{step:regex(^(renew|revoke)$)}", async (Guid id, string step, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AnyStaffAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, now, me) = (accounts.Db!, clock.GetUtcNow(), current!.Account!.Id);
            var grant = await db.AccessGrants.FindAsync([id], cancellationToken);
            if (grant is null || grant.AccountId != me && !current.IsRootAdmin) return Results.NotFound();
            if (step == "renew")
            {
                switch (StaffAccessPolicy.CheckRenew(grant, me, now))
                {
                    case AccessRefusal.NotOwner: return Results.NotFound();
                    case AccessRefusal.Expired: return Problem(StatusCodes.Status409Conflict, ProblemCodes.GrantExpired, "This grant has ended; ask for a new one.");
                    case AccessRefusal.RenewalLimit: return Problem(StatusCodes.Status409Conflict, ProblemCodes.GrantRenewalLimit, "Renewed the maximum number of times; ask for a new one.");
                }
                (grant.ExpiresAt, grant.Renewals) = (now + StaffAccessPolicy.Duration, grant.Renewals + 1);
            }
            else if (grant.RevokedAt is null) (grant.RevokedAt, grant.RevokedBy, grant.RevokedReason) = (now, me, "revoked");
            Audit.Record(db, now, me, "account", "access." + (step == "renew" ? "renewed" : "revoked"), "access-grant", grant.Id.ToString(), grant.Capability);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(View(grant, now));
        });

        access.MapGet("/log", async (int? limit, ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AnyStaffAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            if (!current!.IsRootAdmin && !current.Allows(AccountRole.Legal)) return Results.Forbid();
            var take = Math.Clamp(limit ?? 200, 1, 1000);
            var entries = (await accounts.Db!.SensitiveAccessLog.OrderByDescending(e => e.Sequence).Take(take).ToListAsync(cancellationToken)).OrderBy(e => e.Sequence).ToList();
            var intact = StaffAccessPolicy.ChainIntact(entries);
            // #89: a broken chain is a critical security incident.
            if (!intact) await accounts.Services.GetRequiredService<Security.SecurityAlerts>().ChainBrokenAsync("access-log-chain-broken", "The sensitive-access log's hash chain is broken.", DateTimeOffset.UtcNow, cancellationToken);
            return Results.Ok(new SensitiveAccessLogView(entries.Select(e => new SensitiveAccessView(e.Sequence, e.At, e.ActorAccountId, e.Action, e.Resource, e.ResourceId, e.Status)).ToList(), intact));
        });

        access.MapGet("/exclusions", async (ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AnyStaffAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var me = current!.Account!.Id;
            var all = current.IsRootAdmin || current.Allows(AccountRole.Legal);
            var rows = await db.StaffExclusions.Where(e => all || e.StaffAccountId == me).OrderByDescending(e => e.CreatedAt).Take(500).ToListAsync(cancellationToken);
            return Results.Ok(rows.Select(e => new StaffExclusionView(e.StaffAccountId, e.SubjectAccountId, e.Reason, e.CreatedBy, e.CreatedAt)).ToList());
        });

        // §5: the root admin or the security and legal officer records a conflict; any staff member may declare their own.
        access.MapPost("/exclusions", async (StaffExclusionRequest request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AnyStaffAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, now, me) = (accounts.Db!, clock.GetUtcNow(), current!.Account!.Id);
            // An empty staff id is a self-declaration.
            if (request.StaffAccountId == Guid.Empty) request = request with { StaffAccountId = me };
            if (request.StaffAccountId != me && !current.IsRootAdmin && !current.Allows(AccountRole.Legal)) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > StaffAccessPolicy.PurposeMax)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.PurposeRequired, "Record why (up to 500 characters).");
            var realms = await db.Accounts.Where(a => a.Id == request.StaffAccountId || a.Id == request.SubjectAccountId).ToDictionaryAsync(a => a.Id, a => a.Realm, cancellationToken);
            if (realms.GetValueOrDefault(request.StaffAccountId) != AccountRealm.Staff || realms.GetValueOrDefault(request.SubjectAccountId) != AccountRealm.Members)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "A staff account and a member account.");
            if (!await db.StaffExclusions.AnyAsync(e => e.StaffAccountId == request.StaffAccountId && e.SubjectAccountId == request.SubjectAccountId, cancellationToken))
                db.StaffExclusions.Add(new StaffExclusion { StaffAccountId = request.StaffAccountId, SubjectAccountId = request.SubjectAccountId, Reason = request.Reason.Trim(), CreatedBy = me, CreatedAt = now });
            Audit.Record(db, now, me, "account", "access.excluded", "account", request.StaffAccountId.ToString(), request.SubjectAccountId.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok();
        });

        access.MapGet("/approvals", async (ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AnyStaffAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            if (!current!.Allows(AccountRole.Legal)) return Results.Forbid();
            var since = clock.GetUtcNow() - StaffAccessPolicy.ApprovalLifetime;
            var rows = await accounts.Db!.ExportApprovals.Where(a => a.UsedAt == null && a.RequestedAt > since).OrderBy(a => a.RequestedAt).ToListAsync(cancellationToken);
            return Results.Ok(rows.Select(ApprovalView).ToList());
        });

        access.MapPost("/approvals/{id:guid}/approve", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AnyStaffAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            if (!current!.Allows(AccountRole.Legal)) return Results.Forbid();
            var (db, now, me) = (accounts.Db!, clock.GetUtcNow(), current.Account!.Id);
            var approval = await db.ExportApprovals.FindAsync([id], cancellationToken);
            if (approval is null) return Results.NotFound();
            switch (StaffAccessPolicy.CheckApprove(approval, me, now))
            {
                case AccessRefusal.NotSeparated: return Problem(StatusCodes.Status403Forbidden, ProblemCodes.LeNotSeparated, "Another officer approves a bulk export.");
                case AccessRefusal.Used: return Problem(StatusCodes.Status409Conflict, ProblemCodes.ExportUnavailable, "Already approved, or older than 24 hours.");
            }
            if (await ExcludedAsync(db, me, await HoldAccountsAsync(db, approval.HoldId, cancellationToken), cancellationToken)) return Conflict();
            (approval.ApprovedBy, approval.ApprovedAt) = (me, now);
            Audit.Record(db, now, me, "account", "evidence.bulk-approved", "legal-hold", approval.HoldId.ToString(), approval.Id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(ApprovalView(approval));
        });

        // §11, §15: revoking a staff member removes their roles and grants and ends their sessions (the API refuses every token
        // authenticated before now). Root admin only; never the root admin themselves.
        app.MapPost("/api/admin/staff/{id:guid}/revoke", async (Guid id, StaffRevokeRequest request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AnyStaffAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            if (!current!.IsRootAdmin) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > StaffAccessPolicy.PurposeMax)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.PurposeRequired, "Record why (up to 500 characters).");
            var (db, now, me) = (accounts.Db!, clock.GetUtcNow(), current.Account!.Id);
            var target = await db.Accounts.FindAsync([id], cancellationToken);
            if (target is null || target.Realm != AccountRealm.Staff || target.IsRootAdmin || target.Id == me) return Results.NotFound();
            var roles = await db.RoleGrants.Where(g => g.AccountId == id).ExecuteDeleteAsync(cancellationToken);
            var grants = await db.AccessGrants.Where(g => g.AccountId == id && g.RevokedAt == null).ExecuteUpdateAsync(u => u.SetProperty(g => g.RevokedAt, now)
                .SetProperty(g => g.RevokedBy, me).SetProperty(g => g.RevokedReason, "staff-revoked"), cancellationToken);
            // Pending invitations to the same address can't bring the roles back.
            if (target.Email is { } email)
                await db.Invitations.Where(i => i.Email == email && i.Realm == AccountRealm.Staff && i.AcceptedAt == null && i.RevokedAt == null)
                    .ExecuteUpdateAsync(u => u.SetProperty(i => i.RevokedAt, now), cancellationToken);
            target.SessionsRevokedAt = now;
            Audit.Record(db, now, me, "account", "staff.revoked", "account", id.ToString(), $"roles:{roles};grants:{grants}");
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new StaffRevokeResult(roles, grants, false));
        });
    }

    /// <summary>The caller's live grant for a capability, or null (#88).</summary>
    public static async Task<AccessGrant?> GrantAsync(SwoovrDbContext db, Guid account, string capability, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.AccessGrants.Where(g => g.AccountId == account && g.Capability == capability && g.RevokedAt == null && g.ExpiresAt > now)
            .OrderByDescending(g => g.ExpiresAt).FirstOrDefaultAsync(cancellationToken);

    public static IResult NeedsGrant(string capability) =>
        Problem(StatusCodes.Status403Forbidden, ProblemCodes.ElevatedAccessRequired, $"Ask for time-limited access ({capability}) with a recorded purpose first.");

    /// <summary>Whether a conflict of interest keeps this staff member from any of these members (§5).</summary>
    public static async Task<bool> ExcludedAsync(SwoovrDbContext db, Guid staff, IReadOnlyCollection<Guid> subjects, CancellationToken cancellationToken)
    {
        if (subjects.Count == 0) return false;
        var exclusions = await db.StaffExclusions.Where(e => e.StaffAccountId == staff && subjects.Contains(e.SubjectAccountId)).ToListAsync(cancellationToken);
        var links = await db.StaffMemberLinks.Where(l => l.StaffAccountId == staff && subjects.Contains(l.MemberAccountId)).ToListAsync(cancellationToken);
        return StaffAccessPolicy.Excluded(staff, subjects, exclusions, links);
    }

    public static async Task<List<Guid>> HoldAccountsAsync(SwoovrDbContext db, Guid hold, CancellationToken cancellationToken) =>
        await db.LegalHoldScopes.Where(s => s.HoldId == hold && s.Kind == HoldScopeKind.Account).Select(s => s.TargetId).ToListAsync(cancellationToken);

    public static IResult Conflict() =>
        Problem(StatusCodes.Status403Forbidden, ProblemCodes.ConflictOfInterest, "A conflict of interest keeps you away from this member's records.");

    private static async Task<(CurrentAccount? Current, IResult? Denied)> AnyStaffAsync(ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken)
    {
        var current = await accounts.ResolveAsync(user, cancellationToken);
        if (current is null) return (null, Results.Unauthorized());
        if (current.Account is null) return (null, Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.PersistenceUnavailable, "Accounts are not available."));
        var staff = current.Facts.Realm == AccountRealm.Staff && (current.IsRootAdmin || current.Allows(AccountRole.Admin) || current.Allows(AccountRole.Support) || current.Allows(AccountRole.Legal));
        return staff ? (current, null) : (null, Results.Forbid());
    }

    private static AccessGrantView View(AccessGrant g, DateTimeOffset now) =>
        new(g.Id, g.Capability, g.Purpose, g.GrantedAt, g.ExpiresAt, g.Renewals, StaffAccessPolicy.Active(g, now), g.RevokedAt);

    private static ExportApprovalView ApprovalView(ExportApproval a) => new(a.Id, a.HoldId, a.RequestedBy, a.Purpose, a.RequestedAt, a.ApprovedBy, a.ApprovedAt, a.UsedAt);
}

/// <summary>
/// Writes the sensitive-access log (§11, §15) after every restricted admin request: who, the route kind, a path id, the
/// action and the status, chained under a transaction-scoped advisory lock so concurrent requests can't fork the chain.
/// A logging failure is logged and never changes the response already decided.
/// </summary>
public sealed class SensitiveAccessMiddleware(RequestDelegate next)
{
    private const long ChainLock = 0x5357_4F4F_5652_4C47; // "SWOOVRLG"

    public async Task InvokeAsync(HttpContext context, IServiceProvider services, TimeProvider clock, ILogger<SensitiveAccessMiddleware> log)
    {
        await next(context);
        if (StaffAccessPolicy.Classify(context.Request.Method, context.Request.Path.Value ?? "", context.Response.StatusCode) is not var (resource, id, action)) return;
        // Its own scope, so the request's tracked changes are never flushed here.
        await using var scope = services.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        if (scope.ServiceProvider.GetService<SwoovrDbContext>() is not { } db) return;
        try
        {
            Guid? actor = null;
            if (context.User.FindFirstValue("sub") is { } subject && context.User.FindFirstValue("iss") is { } issuer)
                actor = await db.Accounts.Where(a => a.Issuer == issuer && a.Subject == subject).Select(a => (Guid?)a.Id).SingleOrDefaultAsync(context.RequestAborted);
            await AppendAsync(db, clock.GetUtcNow(), actor, action, resource, id, context.Response.StatusCode, CancellationToken.None);
        }
        catch (Exception exception)
        {
            log.LogError(exception, "Sensitive-access log entry not written.");
        }
    }

    public static async Task AppendAsync(SwoovrDbContext db, DateTimeOffset now, Guid? actor, string action, string resource, string? id, int status, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync($"SELECT pg_advisory_xact_lock({ChainLock})", cancellationToken);
        var previous = await db.SensitiveAccessLog.OrderByDescending(e => e.Sequence).Select(e => e.Hash).FirstOrDefaultAsync(cancellationToken) ?? [];
        var at = DateTimeOffset.FromUnixTimeMilliseconds(now.ToUnixTimeMilliseconds());
        db.SensitiveAccessLog.Add(new SensitiveAccess
        {
            At = at, ActorAccountId = actor, Action = action, Resource = resource, ResourceId = id, Status = status, PreviousHash = previous,
            Hash = StaffAccessPolicy.Chain(previous, at, actor, action, resource, id, status),
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
