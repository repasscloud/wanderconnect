using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Swoovr.Api.Accounts;
using Swoovr.Api.Auth;
using Swoovr.Client;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Identity;

/// <summary>
/// #185 (D341): Security &amp; access → Active sessions and Linked sign-ins. Pure policy, the members-realm adapter against a fake
/// Keycloak (fictional ids only; no real provider or authorization claim), the API's view projection and the shared wording.
/// </summary>
static class SignInChecks
{
    public static async Task Run()
    {
        var count = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception("sign-ins: " + name); count++; }
        var now = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        // Policy.
        IdentitySession S(string id, params string[] clients) => new(id, now.AddHours(-1), now, clients);
        var sessions = new List<IdentitySession> { S("this-web", "swoovr-web"), S("phone-app", "swoovr-mobile"), S("old_web-2", "swoovr-web"), S("bad id!") };
        Check(SignInPolicy.Others(sessions, "this-web").SequenceEqual(["phone-app", "old_web-2"]), "sign out others keeps this session and skips malformed ids");
        var handle = SignInPolicy.Handle("phone-app");
        Check(SignInPolicy.ValidHandle(handle) && handle != "phone-app" && !handle.Contains('+') && !handle.Contains('/'), "handle is opaque base64url, never the session id");
        Check(SignInPolicy.Find(sessions, handle, "this-web")?.Id == "phone-app", "a handle finds the caller's own other session");
        Check(SignInPolicy.Find(sessions, SignInPolicy.Handle("this-web"), "this-web") is null, "the current session is never matched for per-device sign-out");
        Check(SignInPolicy.Find(sessions, SignInPolicy.Handle("someone-else"), "this-web") is null, "a handle for a session that isn't the caller's matches nothing");
        Check(!SignInPolicy.ValidHandle("short") && !SignInPolicy.ValidHandle(null) && !SignInPolicy.ValidHandle(new string('a', 21) + "/"), "malformed handles are refused");
        Check(SignInPolicy.Device(["swoovr-web"]) == "web" && SignInPolicy.Device(["swoovr-mobile", "swoovr-web"]) == "app" && SignInPolicy.Device([]) == "other", "device comes from the signed-in client only");
        Check(SignInPolicy.ValidProvider("google") && SignInPolicy.ValidProvider("apple") && !SignInPolicy.ValidProvider("../users") && !SignInPolicy.ValidProvider("Google") && !SignInPolicy.ValidProvider(""), "provider aliases are plain lowercase realm aliases");
        var socialOnly = new IdentitySignIns([], ["google"], HasPassword: false, HasPasskey: false);
        Check(!SignInPolicy.CanUnlink(socialOnly, "google"), "the only sign-in method can't be removed");
        Check(SignInPolicy.CanUnlink(socialOnly with { HasPassword = true }, "google"), "a password keeps a way to sign in");
        Check(SignInPolicy.CanUnlink(socialOnly with { HasPasskey = true }, "google"), "a passkey keeps a way to sign in");
        Check(SignInPolicy.CanUnlink(socialOnly with { LinkedProviders = ["google", "apple"] }, "google"), "another linked provider keeps a way to sign in");
        Check(!SignInPolicy.CanUnlink(socialOnly with { HasPassword = true }, "apple"), "a provider that isn't linked can't be removed");

        // Members-realm adapter against a fake Keycloak.
        var keycloak = new FakeKeycloak();
        using var http = new HttpClient(keycloak);
        IIdentityAdmin admin = new KeycloakIdentityAdmin(http, new("https://auth.example.test", "swoovr-members-test", "swoovr-api-accounts", "fictional-service-secret"),
            TimeProvider.System, NullLogger<KeycloakIdentityAdmin>.Instance);
        var subject = keycloak.Subject.ToString();
        var read = await admin.SignInsAsync(subject, CancellationToken.None);
        Check(read is { Sessions.Count: 2 } && read.Sessions[0].Id == "kc-session-web" && read.Sessions[0].Clients.SequenceEqual(["swoovr-web"]), "sessions are read newest first with their client ids");
        Check(read!.Sessions[1].Started == DateTimeOffset.FromUnixTimeMilliseconds(FakeKeycloak.Start), "session times come from Keycloak's milliseconds");
        Check(read.LinkedProviders.SequenceEqual(["google"]) && read.HasPassword && !read.HasPasskey, "linked providers and credential types are read");
        Check(keycloak.Paths.All(p => !p.Contains("fictional.member")), "provider user names are never requested by path");
        Check(await admin.EndSessionAsync(subject, "kc-session-app", CancellationToken.None) && keycloak.Deleted.Contains("/admin/realms/swoovr-members-test/sessions/kc-session-app"), "one session is ended by id");
        Check(!await admin.EndSessionAsync(subject, "../users", CancellationToken.None), "a malformed session id is never sent");
        Check(await admin.UnlinkProviderAsync(subject, "apple", CancellationToken.None) == UnlinkOutcome.NotLinked, "a provider that isn't linked isn't removed");
        keycloak.HasPassword = false;
        Check(await admin.UnlinkProviderAsync(subject, "google", CancellationToken.None) == UnlinkOutcome.LastSignInMethod
            && !keycloak.Deleted.Any(p => p.Contains("federated-identity")), "the adapter re-reads and refuses removing the last sign-in method");
        keycloak.HasPassword = true;
        Check(await admin.UnlinkProviderAsync(subject, "google", CancellationToken.None) == UnlinkOutcome.Unlinked
            && keycloak.Deleted.Contains($"/admin/realms/swoovr-members-test/users/{subject}/federated-identity/google"), "a linked provider is removed when a password remains");
        keycloak.FailSessions = true;
        Check(await admin.SignInsAsync(subject, CancellationToken.None) is null, "an unreadable realm is reported, never an empty list");
        Check(await admin.UnlinkProviderAsync(subject, "google", CancellationToken.None) == UnlinkOutcome.Failed, "unlink fails closed when the user can't be re-read");
        keycloak.FailSessions = false; keycloak.Oversize = true;
        Check(await admin.SignInsAsync(subject, CancellationToken.None) is null, "oversized responses are refused");
        keycloak.Oversize = false;
        Check(await admin.SignInsAsync("not-a-guid", CancellationToken.None) is null, "a subject that isn't a Keycloak id is never sent");
        using var unconfiguredHttp = new HttpClient(keycloak);
        var before = keycloak.Paths.Count;
        IIdentityAdmin unconfigured = new KeycloakIdentityAdmin(unconfiguredHttp, new("https://auth.example.test", "swoovr-members-test", "swoovr-api-accounts", null),
            TimeProvider.System, NullLogger<KeycloakIdentityAdmin>.Instance);
        Check(await unconfigured.SignInsAsync(subject, CancellationToken.None) is null && keycloak.Paths.Count == before, "no secret, no calls");

        // API view projection.
        var options = new KeycloakOptions { AppleAlias = "apple-members", GoogleAlias = "google" };
        var view = SignInEndpoints.View(new([S("phone-app", "swoovr-mobile"), S("this-web", "swoovr-web")], ["apple-members", "google"], false, false), "this-web", options);
        Check(view.CurrentKnown && view.Sessions[0].Current && view.Sessions[0].Device == "web" && view.Sessions[1].Device == "app", "this device is listed first and marked");
        Check(view.Sessions.All(s => s.Handle != "this-web" && s.Handle != "phone-app"), "Keycloak session ids never leave the API");
        Check(view.Linked.Select(l => l.Label).SequenceEqual(["Apple", "Google"]) && view.Linked.All(l => l.CanUnlink), "configured aliases get their labels; two providers can each be removed");
        var unknown = SignInEndpoints.View(new([S("this-web", "swoovr-web")], [], true, false), null, options);
        Check(!unknown.CurrentKnown && !unknown.Sessions[0].Current, "without a session id nothing is marked as this device");

        // Shared wording.
        Check(SignInText.Device("web") == "Web browser" && SignInText.Device("app") == "Swoovr app" && SignInText.Device("x") == "Other sign-in", "device labels");
        Check(SignInText.LastActive(now.AddMinutes(-2), now) == "Active now" && SignInText.LastActive(now.AddMinutes(-30), now) == "Active 30 minutes ago"
            && SignInText.LastActive(now.AddHours(-5), now) == "Active 5 hours ago" && SignInText.LastActive(now.AddHours(-30), now) == "Active yesterday", "last active wording");
        Check(SignInText.Problem(ProblemCodes.LastSignInMethod) == SignInText.OnlyMethod, "last-method refusal explains what to add");
        Console.WriteLine($"PASS: sessions and linked sign-ins ({count} checks; fake Keycloak, no real provider claim).");
    }

    private sealed class FakeKeycloak : HttpMessageHandler
    {
        public const long Start = 1_793_000_000_000;
        public Guid Subject = Guid.NewGuid();
        public bool HasPassword = true, FailSessions, Oversize;
        public List<string> Paths = [], Deleted = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            var path = request.RequestUri!.AbsolutePath; Paths.Add(path);
            HttpResponseMessage Json(object body) => new(HttpStatusCode.OK) { Content = JsonContent.Create(body) };
            if (path.EndsWith("/token")) return Task.FromResult(Json(new { access_token = "fictional-service-access-token", expires_in = 300 }));
            if (request.Headers.Authorization?.Parameter != "fictional-service-access-token") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            if (request.Method == HttpMethod.Delete) { Deleted.Add(path); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)); }
            if (path.EndsWith("/sessions"))
            {
                if (FailSessions) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                if (Oversize) return Task.FromResult(Json(new[] { new { id = new string('x', 300_000) } }));
                return Task.FromResult(Json(new object[]
                {
                    new { id = "kc-session-app", ipAddress = "192.0.2.10", start = Start, lastAccess = Start + 60_000, clients = new Dictionary<string, string> { ["uuid-m"] = "swoovr-mobile" } },
                    new { id = "kc-session-web", ipAddress = "192.0.2.11", start = Start + 1000, lastAccess = Start + 120_000, clients = new Dictionary<string, string> { ["uuid-w"] = "swoovr-web" } },
                }));
            }
            if (path.EndsWith("/federated-identity")) return Task.FromResult(Json(new[] { new { identityProvider = "google", userId = "fictional-google-id", userName = "fictional.member@example.test" } }));
            if (path.EndsWith("/credentials"))
                return Task.FromResult(Json(HasPassword ? new object[] { new { type = "password", id = "c1" } } : []));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
