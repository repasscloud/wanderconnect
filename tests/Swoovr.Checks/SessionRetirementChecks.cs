using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Connections;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

/// <summary>
/// #198: open messaging connections close when their account's sessions are retired. #201: a retired web session ends and the
/// member signs in again (home instead for a sign-in under a minute old, so a refused fresh session can't loop).
/// </summary>
static class SessionRetirementChecks
{
    public static void Run()
    {
        var count = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception("session retirement: " + name); count++; }
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()); // token times are whole seconds
        string Iat(DateTimeOffset at) => at.ToUnixTimeSeconds().ToString();

        // #198 registry: the cutoff closes only connections opened with a sign-in before it.
        var connections = new MessagesConnections();
        var (alex, sam) = (Guid.NewGuid(), Guid.NewGuid());
        var aborted = new List<string>();
        void Open(string id, Guid account, DateTimeOffset signedIn) => connections.Add(id, new(account, signedIn, Iat(signedIn), () => aborted.Add(id)));
        Open("alex-phone", alex, now.AddHours(-2));
        Open("alex-web", alex, now.AddMinutes(-10));
        Open("sam-web", sam, now.AddHours(-1));
        Check(connections.Count == 3 && connections.Accounts().Count == 2, "connections are tracked per account");
        var cutoff = SessionRevocationPolicy.Cutoff(now);
        Check(connections.Retire(alex, cutoff, now) == 2 && aborted.Order().SequenceEqual(["alex-phone", "alex-web"]), "sign out everywhere closes every pre-cutoff connection");
        Check(connections.Count == 1 && !aborted.Contains("sam-web"), "another member's connection stays open");
        Check(connections.Retire(alex, cutoff, now) == 0 && aborted.Count == 2, "a closed connection is never closed twice");
        Open("alex-new", alex, now.AddMinutes(1));
        Check(connections.Retire(alex, cutoff, now.AddMinutes(1)) == 0, "a connection from a sign-in after the cutoff stays open");
        Check(connections.Retire(alex, null, now) == 0, "no cutoff closes nothing");
        Check(connections.End(alex) == 1 && aborted.Last() == "alex-new", "ending an account closes all its connections");
        connections.Remove("sam-web");
        Check(connections.Count == 0 && connections.End(sam) == 0, "a disconnected connection is forgotten");
        connections.Add("bad-iat", new(sam, now.AddHours(-1), "not-a-number", () => aborted.Add("bad-iat")));
        Check(connections.Retire(sam, now.AddHours(-2), now) == 1, "a malformed issue time never keeps a connection open after a cutoff");

        // #201: where a retired web session goes next.
        Check(RetiredSession.Next(Iat(now.AddMinutes(-20)), now) == RetiredSession.SignInPath, "an older session signs in again");
        Check(RetiredSession.Next(Iat(now.AddSeconds(-20)), now) == RetiredSession.HomePath, "a session signed in under a minute ago goes home (no loop)");
        Check(RetiredSession.Next(null, now) == RetiredSession.HomePath && RetiredSession.Next("-5", now) == RetiredSession.HomePath
            && RetiredSession.Next("x", now) == RetiredSession.HomePath, "a missing or malformed sign-in time goes home");
        Check(RetiredSession.Destination("/account/sign-in") == "/account/sign-in" && RetiredSession.Destination("/") == "/", "the two local destinations are followed");
        Check(RetiredSession.Destination("https://example.test/") is null && RetiredSession.Destination("//example.test") is null
            && RetiredSession.Destination(null) is null && RetiredSession.Destination("/explore") is null, "any other destination is ignored");
        Console.WriteLine($"Session retirement checks passed ({count}).");
    }

    /// <summary>#198 sweep against PostgreSQL: closes connections of inactive accounts and from before an account's cutoff.</summary>
    public static async Task Persistence(DbContextOptions<SwoovrDbContext> options, Action<string, bool> check)
    {
        await using var db = new SwoovrDbContext(options);
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds()); // token times are whole seconds
        var (kept, revoked, suspended, missing) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Account Member(Guid id, AccountStatus status, DateTimeOffset? cutoff) => new()
        {
            Id = id, Issuer = "https://issuer.example.test/realms/members", Subject = "fictional-sweep-" + id, Realm = AccountRealm.Members,
            Status = status, CreatedAt = now.AddDays(-30), LastSeenAt = now, SessionsRevokedAt = cutoff,
        };
        db.Accounts.AddRange(Member(kept, AccountStatus.Active, null), Member(revoked, AccountStatus.Active, now.AddMinutes(-5)),
            Member(suspended, AccountStatus.Suspended, null));
        await db.SaveChangesAsync();
        var connections = new MessagesConnections();
        var closed = new List<string>();
        void Open(string id, Guid account, DateTimeOffset signedIn) =>
            connections.Add(id, new(account, signedIn, signedIn.ToUnixTimeSeconds().ToString(), () => closed.Add(id)));
        Open("kept", kept, now.AddHours(-1));
        Open("revoked-old", revoked, now.AddHours(-1));
        Open("revoked-new", revoked, now.AddMinutes(-1));
        Open("suspended", suspended, now.AddMinutes(-1));
        Open("missing", missing, now.AddMinutes(-1));
        var count = await MessagesSessionSweep.SweepAsync(connections, db, now, CancellationToken.None);
        check("#198 sweep closes pre-cutoff, inactive and unknown-account connections",
            count == 3 && closed.Order().SequenceEqual(["missing", "revoked-old", "suspended"]));
        check("#198 sweep keeps current connections open", connections.Count == 2 && connections.Accounts().Order().SequenceEqual(new[] { kept, revoked }.Order()));
        check("#198 a second sweep closes nothing more", await MessagesSessionSweep.SweepAsync(connections, db, now, CancellationToken.None) == 0);
        await db.Accounts.Where(a => a.Id == kept || a.Id == revoked || a.Id == suspended).ExecuteDeleteAsync();
    }
}
