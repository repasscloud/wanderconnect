using System.Net;
using System.Text;
using System.Text.Json;
using Swoovr.Client;
using Swoovr.Contracts;

/// <summary>
/// The app's code + PKCE client against an in-memory stand-in for the members realm: no provider is contacted.
/// Negative cases follow AUTHENTICATION.md (callback replay, wrong issuer/audience, revoked session).
/// </summary>
static class NativeSignInChecks
{
    const string Authority = "https://id.example.invalid/realms/swoovr-members-dev";
    static readonly NativeSignInOptions Options = new(new(Authority), "swoovr-mobile", new("com.swoovr.app:/oauth2redirect"), new("com.swoovr.app:/signedout"));

    public static async Task Run()
    {
        var checks = 0;
        void Pass() => checks++;

        // RFC 7636 appendix B test vector.
        Expect(NativeSignIn.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk") == "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM", "S256 challenge"); Pass();

        var realm = new FakeRealm();
        var signIn = new NativeSignIn(new HttpClient(realm), Options);
        var request = await signIn.BeginAsync();
        var query = Parse(request.AuthorizeUri.Query);
        Expect(request.AuthorizeUri.GetLeftPart(UriPartial.Path) == Authority + "/protocol/openid-connect/auth", "authorize endpoint from discovery"); Pass();
        Expect(query["response_type"] == "code" && query["code_challenge_method"] == "S256" && query["client_id"] == "swoovr-mobile", "code + PKCE S256 only"); Pass();
        Expect(query["code_challenge"] == NativeSignIn.Challenge(request.CodeVerifier) && !query.ContainsKey("code_verifier"), "verifier never leaves the app"); Pass();
        Expect(!query.ContainsKey("client_secret"), "no client secret"); Pass();
        var second = await signIn.BeginAsync();
        Expect(second.State != request.State && second.Nonce != request.Nonce && second.CodeVerifier != request.CodeVerifier, "fresh values per attempt"); Pass();

        realm.Nonce = request.Nonce;
        realm.Verifier = request.CodeVerifier;
        await Refused(() => signIn.CompleteAsync(request, new Dictionary<string, string> { ["state"] = second.State, ["code"] = "c1" }), "state from another attempt"); Pass();
        await Refused(() => signIn.CompleteAsync(request, new Dictionary<string, string> { ["code"] = "c1" }), "missing state"); Pass();
        await Refused(() => signIn.CompleteAsync(request, new Dictionary<string, string> { ["state"] = request.State, ["error"] = "access_denied" }), "error callback"); Pass();
        await Refused(() => signIn.CompleteAsync(request, new Dictionary<string, string> { ["state"] = request.State }), "missing code"); Pass();
        Expect(realm.TokenCalls == 0, "nothing redeemed before the callback checks pass"); Pass();

        var tokens = await signIn.CompleteAsync(request, new Dictionary<string, string> { ["state"] = request.State, ["code"] = "c1" });
        Expect(tokens.AccessToken == "access-1" && tokens.RefreshToken == "refresh-1", "code redeemed with the verifier"); Pass();
        await Refused(() => signIn.CompleteAsync(request, new Dictionary<string, string> { ["state"] = request.State, ["code"] = "c1" }), "code replay"); Pass();
        realm.Verifier = "someone-else";
        await Refused(() => signIn.CompleteAsync(request, new Dictionary<string, string> { ["state"] = request.State, ["code"] = "c2" }), "wrong verifier"); Pass();
        realm.Verifier = request.CodeVerifier;

        foreach (var (label, change) in new (string, Action<FakeRealm>)[] {
            ("wrong ID token issuer", r => r.IdIssuer = "https://evil.example.invalid/realms/swoovr-members-dev"),
            ("wrong ID token audience", r => r.IdAudience = "swoovr-web"),
            ("wrong nonce", r => r.Nonce = "replayed"),
        })
        {
            var bad = new FakeRealm();
            var client = new NativeSignIn(new HttpClient(bad), Options);
            var attempt = await client.BeginAsync();
            (bad.Verifier, bad.Nonce) = (attempt.CodeVerifier, attempt.Nonce);
            change(bad);
            await Refused(() => client.CompleteAsync(attempt, new Dictionary<string, string> { ["state"] = attempt.State, ["code"] = "c9" }), label); Pass();
        }
        await Refused(() => new NativeSignIn(new HttpClient(new FakeRealm { DiscoveryIssuer = "https://evil.example.invalid/realms/x" }), Options).BeginAsync(), "discovery issuer mismatch"); Pass();

        // Session: stored, refreshed before expiry with rotation, cleared when the realm refuses (revoked session).
        var clock = new Clock(DateTimeOffset.Parse("2026-10-23T00:00:00Z"));
        var store = new MemoryStore();
        var session = new NativeSession(new NativeSignIn(new HttpClient(realm), Options, clock), store, clock);
        Expect(!await session.IsSignedInAsync() && await session.AccessTokenAsync() is null, "signed out by default"); Pass();
        await session.SaveAsync(tokens with { ExpiresAt = clock.Now.AddMinutes(5) });
        Expect(store.Value!.Contains("access-1"), "tokens written to the secure store"); Pass();
        var restored = new NativeSession(new NativeSignIn(new HttpClient(realm), Options, clock), store, clock);
        Expect(await restored.AccessTokenAsync() == "access-1", "restored from the store after a restart"); Pass();
        clock.Now = clock.Now.AddMinutes(5);
        Expect(await restored.AccessTokenAsync() == "access-2" && store.Value!.Contains("refresh-2") && realm.LastRefresh == "refresh-1", "refreshed with rotation"); Pass();
        realm.RevokeAll = true;
        clock.Now = clock.Now.AddMinutes(10);
        Expect(await restored.AccessTokenAsync() is null && store.Value is null, "revoked session signs the app out"); Pass();
        store.Value = "{not json";
        Expect(await new NativeSession(new NativeSignIn(new HttpClient(realm), Options, clock), store, clock).AccessTokenAsync() is null && store.Value is null, "unreadable store is cleared"); Pass();

        // Bearer handler: no token means no request; a 401 is retried once for bodiless calls only.
        realm.RevokeAll = false;
        var api = new FakeApi();
        var handler = new BearerHandler(restored) { InnerHandler = api };
        var members = new MemberClient(new HttpClient(handler) { BaseAddress = new("https://api.example.invalid/") });
        var none = await members.SessionAsync();
        Expect(none.Status == 401 && api.Calls == 0, "no token: nothing sent to the API"); Pass();
        await restored.SaveAsync(new NativeTokens("stale", "refresh-9", "id", clock.Now.AddMinutes(5)));
        var ok = await members.SessionAsync();
        Expect(ok.Ok && api.Calls == 2 && api.Seen[0] == "stale" && api.Seen[1].StartsWith("access-"), "401 then one retry with a refreshed token"); Pass();
        await restored.SaveAsync(new NativeTokens("stale", "refresh-9", "id", clock.Now.AddMinutes(5)));
        api.Calls = 0;
        var post = await members.RedeemAsync("FOUNDER-V1-x");
        Expect(post.Status == 401 && api.Calls == 1, "a request with a body is never replayed"); Pass();

        var end = Parse((await signIn.EndSessionUriAsync(tokens)).Query);
        Expect(end["post_logout_redirect_uri"] == "com.swoovr.app:/signedout" && end["client_id"] == "swoovr-mobile", "end session returns to the app"); Pass();

        // Shared account wording (web and app).
        Expect(AccountText.BetaBlocked(Summary("notEnrolled")) && AccountText.BetaBlocked(Summary("unavailable")) && !AccountText.BetaBlocked(Summary("enrolled")), "beta notice"); Pass();
        Expect(AccountText.Coupon(new(429, null, ProblemCodes.TooManyAttempts)).Result == "limited" && AccountText.Coupon(new(400, null, ProblemCodes.CouponInvalid)).Result == "invalid", "coupon refusals"); Pass();
        Expect(AccountText.MethodLabel("emailPassword") == "Password or passkey" && AccountText.MethodLabel("unknown") == "New account (just created)", "method labels"); Pass();

        Console.WriteLine($"PASS: native sign-in client, {checks} checks (PKCE S256, callback state/replay, ID token issuer/audience/nonce, rotation, revoked session, bearer retry, shared account wording).");
    }

    static SessionSummary Summary(string beta) => new("members", "emailPassword", false, true, true, beta == "enrolled", false, false, beta);

    static void Expect(bool condition, string what) { if (!condition) throw new Exception("Native sign-in check failed: " + what); }

    static async Task Refused(Func<Task> action, string what)
    {
        try { await action(); }
        catch (NativeSignInException) { return; }
        throw new Exception("Native sign-in accepted " + what);
    }

    static Dictionary<string, string> Parse(string query) => query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
        .Select(p => p.Split('=', 2)).ToDictionary(p => Uri.UnescapeDataString(p[0]), p => Uri.UnescapeDataString(p[1]));

    sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    sealed class MemoryStore : ITokenStore
    {
        public string? Value;
        public Task<string?> ReadAsync() => Task.FromResult(Value);
        public Task WriteAsync(string value) { Value = value; return Task.CompletedTask; }
        public void Clear() => Value = null;
    }

    sealed class FakeApi : HttpMessageHandler
    {
        public int Calls;
        public List<string> Seen = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var token = request.Headers.Authorization?.Parameter ?? "";
            Seen.Add(token);
            if (token == "stale") return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(
                JsonSerializer.Serialize(Summary("enrolled"), new JsonSerializerOptions(JsonSerializerDefaults.Web)), Encoding.UTF8, "application/json") });
        }
    }

    /// <summary>Discovery, token (code and refresh) and nothing else.</summary>
    sealed class FakeRealm : HttpMessageHandler
    {
        public string DiscoveryIssuer = Authority, IdIssuer = Authority, IdAudience = "swoovr-mobile";
        public string Nonce = "", Verifier = "";
        public string? LastRefresh;
        public bool RevokeAll;
        public int TokenCalls;
        private readonly HashSet<string> redeemed = [];
        private int issued = 1;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsoluteUri;
            if (request.Method == HttpMethod.Get && path == Authority + "/.well-known/openid-configuration")
                return Json(new Dictionary<string, string> {
                    ["issuer"] = DiscoveryIssuer, ["authorization_endpoint"] = Authority + "/protocol/openid-connect/auth",
                    ["token_endpoint"] = Authority + "/protocol/openid-connect/token", ["end_session_endpoint"] = Authority + "/protocol/openid-connect/logout" });
            if (request.Method != HttpMethod.Post || path != Authority + "/protocol/openid-connect/token") throw new Exception("Unexpected realm request " + path);
            TokenCalls++;
            var form = Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (form["client_id"] != "swoovr-mobile" || form.ContainsKey("client_secret")) return new(HttpStatusCode.Unauthorized);
            if (RevokeAll) return new(HttpStatusCode.BadRequest);
            if (form["grant_type"] == "authorization_code")
            {
                if (!redeemed.Add(form["code"]) || NativeSignIn.Challenge(form["code_verifier"]) != NativeSignIn.Challenge(Verifier)
                    || form["redirect_uri"] != "com.swoovr.app:/oauth2redirect") return new(HttpStatusCode.BadRequest);
            }
            else if (form["grant_type"] == "refresh_token") LastRefresh = form["refresh_token"];
            else return new(HttpStatusCode.BadRequest);
            var n = issued++;
            return Json(new Dictionary<string, object> {
                ["access_token"] = "access-" + n, ["token_type"] = "Bearer", ["expires_in"] = 300, ["refresh_token"] = "refresh-" + n,
                ["id_token"] = IdToken(),
            });
        }

        private string IdToken()
        {
            static string Part(object value) => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return Part(new { alg = "RS256" }) + "." + Part(new { iss = IdIssuer, aud = IdAudience, nonce = Nonce, sub = "fictional" }) + ".sig";
        }

        private static HttpResponseMessage Json(object value) =>
            new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
    }
}
