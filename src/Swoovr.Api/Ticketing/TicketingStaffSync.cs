using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Ticketing;

/// <summary>Wakes the staff synchronisation early after a change made in Swoovr (staff revoked, conflict recorded).</summary>
public sealed class TicketingStaffSignal
{
    private readonly Channel<bool> channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });
    public void Nudge() => channel.Writer.TryWrite(true);
    public async Task WaitAsync(TimeSpan timeout, TimeProvider clock, CancellationToken ct)
    {
        using var timer = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timer.CancelAfter(timeout);
        try { await channel.Reader.ReadAsync(timer.Token); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
    }
}

public sealed record TicketingStaffSyncResult(int Checked, int Changed, int Failed, int Drifted);

/// <summary>
/// Mirrors Swoovr staff permissions into Zammad (owner decision, 5 November 2026, Q133/#204). Swoovr is the only source:
/// for each staff member it computes two identities from <see cref="TicketingPolicy"/> and pushes them.
/// <list type="bullet">
/// <item>The API identity (token only, never signs in) gets every queue the roles allow; Swoovr Admin adds the elevated
/// capability, 15-minute sign-in and conflict-of-interest checks on every request.</item>
/// <item>The web identity (central staff sign-in) gets only what Zammad can enforce by itself: ordinary queues and hand-off.
/// It is switched off while the staff member has any conflict-of-interest record, because Zammad can't hide one member's
/// requests from one agent.</item>
/// </list>
/// A failure is logged, recorded and retried with backoff; it never widens access. Changes made in Zammad are reverted at
/// the next verification pass.
/// </summary>
public sealed class TicketingStaffSync(SwoovrDbContext db, TicketingActorFactory actors, ContactHistoryKeys keys, TimeProvider clock, ILogger<TicketingStaffSync> log)
{
    private sealed record Desired(bool Active, IReadOnlyDictionary<string, string> Api, bool WebActive, IReadOnlyDictionary<string, string> Web, string? Email, DateTimeOffset? SessionsRevokedAt)
    {
        public string Digest => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Active, Api, WebActive, Web, Email, SessionsRevokedAt }))));
    }

    /// <summary>Synchronises every staff member who is due (or all of them when <paramref name="verify"/>, comparing against Zammad).</summary>
    public async Task<TicketingStaffSyncResult> RunAsync(bool verify, CancellationToken ct, Guid? only = null)
    {
        var directory = actors.StaffDirectory();
        var groups = (await directory.GroupsAsync(ct)).Where(g => TicketingPolicy.Groups.Contains(g.Name)).ToDictionary(g => g.Name, g => g.Id);
        var staff = await db.Accounts.AsNoTracking().Where(a => a.Realm == AccountRealm.Staff).Select(a => a.Id).ToListAsync(ct);
        var known = await db.Set<TicketingStaff>().AsNoTracking().Select(s => s.AccountId).ToListAsync(ct);
        var ids = staff.Concat(known).Distinct().Where(id => only is null || id == only).ToList();
        int changed = 0, failed = 0, drifted = 0;
        foreach (var id in ids)
        {
            try
            {
                var outcome = await SyncAsync(directory, groups, id, verify || only is not null, ct);
                if (outcome.Changed) changed++;
                if (outcome.Drifted) drifted++;
            }
            catch (Exception error) when (error is TicketingProviderException or HttpRequestException or TaskCanceledException or DbUpdateException or InvalidOperationException && !ct.IsCancellationRequested)
            {
                failed++;
                db.ChangeTracker.Clear();
                var code = error switch { TicketingProviderException p => p.Code, DbUpdateException => "database-failed", _ => "provider-unreachable" };
                await RecordFailureAsync(id, code, ct);
            }
        }
        return new(ids.Count, changed, failed, drifted);
    }

    private async Task<Desired> DesiredAsync(Guid id, CancellationToken ct)
    {
        var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id && a.Realm == AccountRealm.Staff, ct);
        if (account is null) return new(false, new Dictionary<string, string>(), false, new Dictionary<string, string>(), null, null);
        var granted = await db.RoleGrants.AsNoTracking().Where(r => r.AccountId == id).Select(r => r.Role).ToListAsync(ct);
        var roles = TicketingPolicy.EffectiveRoles(granted, account.IsRootAdmin);
        var active = account.Status == AccountStatus.Active && account.DeletedAt is null && roles.Count > 0;
        if (!active) return new(false, new Dictionary<string, string>(), false, new Dictionary<string, string>(), account.Email, account.SessionsRevokedAt);
        var web = TicketingPolicy.StaffGrants(roles, web: true);
        var webActive = account.Email is not null && web.Values.Contains("full") && !await HasConflictsAsync(id, ct);
        return new(true, TicketingPolicy.StaffGrants(roles, web: false), webActive, webActive ? web : new Dictionary<string, string>(), account.Email, account.SessionsRevokedAt);
    }

    /// <summary>Any conflict-of-interest record at all (exclusions, a linked member account, or a recorded staff link).</summary>
    private async Task<bool> HasConflictsAsync(Guid staff, CancellationToken ct)
    {
        var key = staff.ToString();
        return await db.StaffExclusions.AnyAsync(e => e.StaffAccountId == staff, ct)
            || await db.StaffMemberLinks.AnyAsync(l => l.StaffAccountId == staff, ct)
            || await db.AuditEntries.AnyAsync(a => a.Action == "staff-link.set" && a.ActorAccountId != null && a.TargetId == key, ct);
    }

    private async Task<(bool Changed, bool Drifted)> SyncAsync(ITicketingStaffDirectory directory, IReadOnlyDictionary<string, long> groups, Guid id, bool verify, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        // One writer per staff member across API replicas: the row is created (committed, so a failure can be recorded on it),
        // then locked for the remote work.
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ticketing_staff (account_id, token_protected, web_active, attempts, next_attempt_at) VALUES ({id}, {Array.Empty<byte>()}, false, 0, {now}) ON CONFLICT (account_id) DO NOTHING", ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var row = await db.Set<TicketingStaff>().FromSqlInterpolated($"SELECT * FROM ticketing_staff WHERE account_id = {id} FOR UPDATE").SingleAsync(ct);
        var desired = await DesiredAsync(id, ct);
        if (!verify && row.NextAttemptAt > now) { await transaction.CommitAsync(ct); return (false, false); }
        if (!desired.Active && row.ProviderUserId is null && row.WebProviderUserId is null)
        {
            // Never provisioned and nothing to grant: nothing exists in Zammad to revoke.
            db.Remove(row); await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return (false, false);
        }
        var digest = desired.Digest;
        if (!verify && row.AppliedDigest == digest && row.FailureCode is null) { await transaction.CommitAsync(ct); return (false, false); }

        var changed = row.AppliedDigest != digest;
        var drifted = false;
        string Name(string? email, string suffix) => (email ?? "Staff member") + suffix;
        IReadOnlyDictionary<long, string> Ids(IReadOnlyDictionary<string, string> grants) =>
            grants.ToDictionary(g => groups.TryGetValue(g.Key, out var groupId) ? groupId : throw new TicketingProviderException("provider-group-missing"), g => g.Value);

        // API identity: never has an email or password. Deactivated (token refused by Zammad) when the person has no staff role.
        var login = TicketingPolicy.StaffLogin(id);
        var api = row.ProviderUserId is long apiId ? await directory.GetAsync(apiId, ct) : await directory.FindAsync(login, ct);
        if (api is null && desired.Active) api = await directory.CreateAgentAsync(login, Name(desired.Email, ""), "(Swoovr Admin)", null, ct);
        if (api is not null)
        {
            if (api.Login != login) throw new TicketingProviderException("provider-identity-mismatch");
            var apiGroups = Ids(desired.Api);
            if (!Matches(api, desired.Active, apiGroups, null))
            {
                drifted |= !changed;
                api = await directory.UpdateAgentAsync(api.Id, desired.Active, apiGroups, Name(desired.Email, ""), "(Swoovr Admin)", null, ct);
                if (!Matches(api, desired.Active, apiGroups, null)) throw new TicketingProviderException("provider-staff-not-applied");
            }
            row.ProviderUserId = api.Id;
            var issued = false;
            if (desired.Active && row.TokenProtected.Length == 0)
            {
                var version = keys.CurrentVersion ?? throw new InvalidOperationException("No encryption key is configured.");
                row.TokenProtected = ContactHistoryPolicy.Encrypt(version, keys.Encryption[version], await directory.IssueTokenAsync(api.Id, ct));
                issued = true;
            }
            // Only the token Swoovr issued survives (an older or hand-made one is deleted).
            // A switched-off identity's tokens are refused by Zammad, and acting on its behalf isn't possible, so only active ones.
            if (desired.Active && (issued || verify || changed)) Revoked(await directory.RevokeTokensAsync(api.Id, keepIssued: true, ct), id, now);
        }

        // Web identity: created only when there is something Zammad can safely let them do; otherwise kept switched off.
        var webLogin = TicketingPolicy.StaffWebLogin(id);
        var webUser = row.WebProviderUserId is long webId ? await directory.GetAsync(webId, ct) : await directory.FindAsync(webLogin, ct);
        if (webUser is null && desired.WebActive) webUser = await directory.CreateAgentAsync(webLogin, Name(desired.Email, ""), "", desired.Email, ct);
        if (webUser is not null)
        {
            if (webUser.Login != webLogin) throw new TicketingProviderException("provider-identity-mismatch");
            var webGroups = Ids(desired.Web);
            // An inactive web identity keeps no email, so the central sign-in can't link to it and the address stays free.
            var email = desired.WebActive ? desired.Email : null;
            // Losing web access, or a Swoovr session revocation, ends every Zammad web session: once before switching the
            // identity off (Zammad drops an inactive user's session data on its next request, after which it can't be
            // listed) and once after, for a sign-in that raced the change. Zammad also refuses any request from an inactive user.
            var revokedNow = desired.SessionsRevokedAt is { } cutoff && (row.SessionsRevokedApplied is null || cutoff > row.SessionsRevokedApplied);
            var endSessions = (webUser.Active || row.WebActive) && !desired.WebActive || revokedNow;
            var ended = endSessions ? await directory.EndSessionsAsync(webUser.Id, ct) : 0;
            if (!Matches(webUser, desired.WebActive, webGroups, email))
            {
                drifted |= !changed;
                webUser = await directory.UpdateAgentAsync(webUser.Id, desired.WebActive, webGroups, Name(desired.Email, ""), "", email, ct);
                if (!Matches(webUser, desired.WebActive, webGroups, email)) throw new TicketingProviderException("provider-staff-not-applied");
            }
            if (endSessions) ended += await directory.EndSessionsAsync(webUser.Id, ct);
            if (ended > 0) Audit.Record(db, now, null, "system", "ticketing.staff-sessions-ended", "account", id.ToString(), $"sessions:{ended}");
            row.WebProviderUserId = webUser.Id;
            // The web sign-in never holds an API token: one would outlive its web sessions and the central sign-in.
            if (desired.WebActive && (verify || changed)) Revoked(await directory.RevokeTokensAsync(webUser.Id, keepIssued: false, ct), id, now);
            if (revokedNow) row.SessionsRevokedApplied = desired.SessionsRevokedAt;
        }

        if (changed || drifted)
        {
            var detail = $"active:{desired.Active};queues:{desired.Api.Count(g => g.Value == "full")};web:{desired.WebActive};webQueues:{desired.Web.Count(g => g.Value == "full")}";
            Audit.Record(db, now, null, "system", drifted ? "ticketing.staff-drift-reverted" : "ticketing.staff-synced", "account", id.ToString(), detail);
            log.LogInformation("Helpdesk staff permissions applied for {Account} ({Detail}).", id, detail);
        }
        if (drifted) log.LogWarning("Helpdesk staff permissions for {Account} had been changed in Zammad and were reverted.", id);
        (row.AppliedDigest, row.WebActive, row.SyncedAt, row.FailureCode, row.Attempts, row.NextAttemptAt) = (digest, desired.WebActive, now, null, 0, now);
        if (verify) row.VerifiedAt = now;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (changed, drifted);
    }

    private void Revoked(int count, Guid id, DateTimeOffset now)
    {
        if (count == 0) return;
        Audit.Record(db, now, null, "system", "ticketing.staff-tokens-revoked", "account", id.ToString(), $"tokens:{count}");
        log.LogWarning("Helpdesk tokens not issued by Swoovr were revoked for {Account} ({Count}).", id, count);
    }

    private static bool Matches(StaffDirectoryUser user, bool active, IReadOnlyDictionary<long, string> groups, string? email) =>
        user.Active == active && user.Roles.Count == 1 && user.Roles[0] == "Agent"
        && string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase)
        && user.Groups.Count(g => g.Value.Count > 0) == groups.Count
        && groups.All(g => user.Groups.TryGetValue(g.Key, out var held) && held.Count == 1 && held[0] == g.Value);

    private async Task RecordFailureAsync(Guid id, string code, CancellationToken ct)
    {
        var row = await db.Set<TicketingStaff>().SingleOrDefaultAsync(s => s.AccountId == id, ct);
        if (row is null) return;
        var now = clock.GetUtcNow();
        row.Attempts++;
        (row.FailureCode, row.NextAttemptAt) = (code, now + TimeSpan.FromSeconds(Math.Min(3600, 30 * Math.Pow(2, Math.Min(row.Attempts, 7)))));
        Audit.Record(db, now, null, "system", "ticketing.staff-sync-failed", "account", id.ToString(), code);
        log.LogWarning("Helpdesk staff permissions for {Account} not applied (attempt {Attempt}): {Code}.", id, row.Attempts, code);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The API identity's token for Swoovr Admin, or null while it isn't provisioned.</summary>
    public static async Task<(long ProviderUserId, string Token)?> TokenAsync(SwoovrDbContext db, ContactHistoryKeys keys, Guid id, CancellationToken ct)
    {
        var row = await db.Set<TicketingStaff>().AsNoTracking().SingleOrDefaultAsync(s => s.AccountId == id, ct);
        return row is { ProviderUserId: long user, TokenProtected.Length: > 0 } ? (user, ContactHistoryPolicy.Decrypt(keys.Encryption, row.TokenProtected)) : null;
    }
}

/// <summary>Runs the synchronisation every minute (or sooner when nudged) and a full verification against Zammad every ten.</summary>
public sealed class TicketingStaffSyncWorker(IServiceScopeFactory scopes, TicketingStaffSignal signal, IConfiguration configuration, TimeProvider clock, ILogger<TicketingStaffSyncWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("Ticketing:Staff:SyncSeconds", 60), 5, 3600));
        var verifyEvery = TimeSpan.FromMinutes(Math.Clamp(configuration.GetValue("Ticketing:Staff:VerifyMinutes", 10), 1, 1440));
        var lastVerify = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var verify = clock.GetUtcNow() - lastVerify >= verifyEvery;
                await scope.ServiceProvider.GetRequiredService<TicketingStaffSync>().RunAsync(verify, stoppingToken);
                if (verify) lastVerify = clock.GetUtcNow();
            }
            catch (Exception error) when (error is TicketingProviderException or TicketingAccessException or HttpRequestException or DbUpdateException or InvalidOperationException or Npgsql.NpgsqlException && !stoppingToken.IsCancellationRequested)
            { log.LogWarning("Helpdesk staff synchronisation pass failed: {Error}.", error.GetType().Name); }
            try { await signal.WaitAsync(interval, clock, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
