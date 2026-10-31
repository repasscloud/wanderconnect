using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Swoovr.Infrastructure.Messaging;

static class PushProviderChecks
{
    public static async Task Run()
    {
        var encryption = new Dictionary<int, byte[]> { [1] = RandomNumberGenerator.GetBytes(32), [2] = RandomNumberGenerator.GetBytes(32) };
        var protector = new PushTokenProtector(encryption, RandomNumberGenerator.GetBytes(32));
        var deviceId = Guid.NewGuid(); var accountId = Guid.NewGuid();
        var cipher = protector.Protect(deviceId, accountId, "ios", "fictional-provider-token");
        Need(protector.Unprotect(deviceId, accountId, "ios", 2, cipher) == "fictional-provider-token", "token encrypted roundtrip");
        Need(!Encoding.UTF8.GetString(cipher).Contains("fictional-provider-token"), "no plaintext token storage");
        try { protector.Unprotect(deviceId, Guid.NewGuid(), "ios", 2, cipher); throw new Exception("Cross-account token decrypt allowed"); } catch (CryptographicException) { }
        try { protector.Unprotect(deviceId, accountId, "android", 2, cipher); throw new Exception("Cross-platform token decrypt allowed"); } catch (CryptographicException) { }
        Need(protector.Hash("ios", "token") != protector.Hash("android", "token"), "platform-separated token lookup");
        using var ec = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var apple = new ApnsOptions("KEYTEST123", "TEAMTEST12", "com.swoovr.app", ec.ExportPkcs8PrivateKeyPem(), true);
        var message = new PushMessage(Guid.NewGuid(), "ios", new string('a', 64), "Swoovr", "Neutral body", "/chats/" + Guid.NewGuid());
        var appleHandler = new PushFake(async request =>
        {
            Need(request.Version.Major == 2 && request.RequestUri!.Host == "api.sandbox.push.apple.com", "APNs HTTP2 sandbox");
            Need(request.Headers.GetValues("apns-topic").Single() == "com.swoovr.app", "APNs topic");
            var jwt = request.Headers.Authorization!.Parameter!.Split('.');
            Need(ec.VerifyData(Encoding.ASCII.GetBytes(jwt[0] + "." + jwt[1]), Decode(jwt[2]), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation), "APNs ES256 signature");
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Need(payload.RootElement.GetProperty("swoovr_path").GetString() == message.Link, "safe deep link");
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var apns = new ApnsPushSender(new HttpClient(appleHandler), apple, TimeProvider.System);
        Need((await apns.SendAsync(message, default)).Status == "accepted", "acceptance not delivery");
        var invalid = new ApnsPushSender(new HttpClient(new PushFake(_ => Task.FromResult(Json(HttpStatusCode.Gone, "{\"reason\":\"Unregistered\"}")))), apple, TimeProvider.System);
        Need((await invalid.SendAsync(message, default)).InvalidToken, "APNs invalidates unregistered");
        var auth = new ApnsPushSender(new HttpClient(new PushFake(_ => Task.FromResult(Json(HttpStatusCode.Forbidden, "{\"reason\":\"InvalidProviderToken\"}")))), apple, TimeProvider.System);
        Need(!(await auth.SendAsync(message, default)).InvalidToken, "provider auth never invalidates device");
        var busy = new ApnsPushSender(new HttpClient(new PushFake(_ =>
        {
            var response = Json(HttpStatusCode.ServiceUnavailable, "{}");
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(123));
            return Task.FromResult(response);
        })), apple, TimeProvider.System);
        var retry = await busy.SendAsync(message, default);
        Need(retry.Status == "retry" && retry.RetryAfter == TimeSpan.FromSeconds(123), "provider Retry-After preserved");
        var uncertain = new ApnsPushSender(new HttpClient(new PushFake(_ => throw new HttpRequestException("fictional network failure"))), apple, TimeProvider.System);
        Need((await uncertain.SendAsync(message, default)).Status == "unknown", "network uncertainty is not failed delivery");
        Need((await apns.SendAsync(message with { Link = "//outside.invalid" }, default)).Status == "invalid-request", "external deep link refused");
        using var rsa = RSA.Create(2048);
        var fcmOptions = new FcmOptions("fictional-project", "sender@fictional-project.iam.gserviceaccount.com", "fictional-key", rsa.ExportPkcs8PrivateKeyPem());
        int oauth = 0, sends = 0;
        var google = new PushFake(async request =>
        {
            if (request.RequestUri!.Host == "oauth2.googleapis.com")
            {
                oauth++;
                var form = await request.Content!.ReadAsStringAsync();
                var jwt = Uri.UnescapeDataString(form.Split("assertion=")[1]).Split('.');
                Need(rsa.VerifyData(Encoding.ASCII.GetBytes(jwt[0] + "." + jwt[1]), Decode(jwt[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1), "FCM OAuth assertion signature");
                using var claims = JsonDocument.Parse(Decode(jwt[1]));
                Need(claims.RootElement.GetProperty("aud").GetString() == "https://oauth2.googleapis.com/token", "OAuth audience");
                Need(claims.RootElement.GetProperty("exp").GetInt64() - claims.RootElement.GetProperty("iat").GetInt64() == 3600, "OAuth assertion lifetime");
                Need(claims.RootElement.GetProperty("scope").GetString() == "https://www.googleapis.com/auth/firebase.messaging", "least OAuth scope");
                return Json(HttpStatusCode.OK, "{\"access_token\":\"fictional-access\",\"expires_in\":3600,\"token_type\":\"Bearer\"}");
            }
            sends++;
            Need(request.RequestUri.AbsolutePath == "/v1/projects/fictional-project/messages:send", "provider-owned v1");
            Need(request.Headers.Authorization!.Parameter == "fictional-access", "FCM OAuth bearer");
            return sends == 1 ? Json(HttpStatusCode.OK, "{\"name\":\"projects/fictional-project/messages/fictional\"}")
                : Json(HttpStatusCode.NotFound, "{\"error\":{\"details\":[{\"@type\":\"type.googleapis.com/google.firebase.fcm.v1.FcmError\",\"errorCode\":\"UNREGISTERED\"}]}}");
        });
        var fcm = new FcmPushSender(new HttpClient(google), fcmOptions, TimeProvider.System);
        Need((await fcm.SendAsync(message with { Platform = "android", Token = "fictional-fcm-token" }, default)).Status == "accepted", "FCM acceptance");
        Need((await fcm.SendAsync(message with { Platform = "android" }, default)).InvalidToken && oauth == 1, "FCM invalidation/cached OAuth");
        Console.WriteLine("PASS: push provider HTTP/signature checks (fictional generated keys, fake HTTP; no device delivery).");
    }
    static void Need(bool ok, string name) { if (!ok) throw new Exception("Push: " + name); }
    static byte[] Decode(string text) => Convert.FromBase64String(text.Replace('-', '+').Replace('_', '/').PadRight((text.Length + 3) / 4 * 4, '='));
    static HttpResponseMessage Json(HttpStatusCode code, string body) => new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    sealed class PushFake(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request); }
}
