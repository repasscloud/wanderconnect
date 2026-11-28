using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Swoovr.Domain;
using Swoovr.Contracts;
using Swoovr.Infrastructure.Verification;

/// <summary>Issue 488 (D413): ID images read live from Didit for staff; synthetic bytes only.</summary>
static class IdImageChecks
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly byte[] Webp = [.. "RIFF"u8, 1, 2, 3, 4, .. "WEBP"u8, 1];

    public static async Task Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("ID image check failed: " + what); checks++; }
        var (session, account) = (Guid.NewGuid(), Guid.NewGuid());
        string Decision(string documents, string? vendor = null, string? id = null) =>
            $$"""{"session_id":"{{id ?? session.ToString()}}","status":"Approved","vendor_data":"{{vendor ?? account.ToString()}}","id_verifications":{{documents}}}""";
        var front = """[{"status":"In Review","front_image":"https://media.example/front.png?signature=secret","back_image":null}]""";

        async Task<(ProviderImages Result, List<HttpRequestMessage> Sent)> Read(string documents, Func<Uri, HttpResponseMessage>? media = null, string hosts = "media.example",
            string? vendor = null, string? id = null, HttpStatusCode code = HttpStatusCode.OK)
        {
            var sent = new List<HttpRequestMessage>();
            var http = new HttpClient(new Fake(r =>
            {
                sent.Add(r);
                return r.RequestUri!.Host == "didit.example" ? new(code) { Content = new StringContent(Decision(documents, vendor, id)) }
                    : media?.Invoke(r.RequestUri) ?? new(HttpStatusCode.OK) { Content = new ByteArrayContent(Png) };
            }));
            var source = new DiditIdImages(http, new DiditOptions("synthetic-key", "w", null, new Uri("https://didit.example/")), new DiditImageOptions(true, DiditImageOptions.Parse(hosts)), NullLogger<DiditIdImages>.Instance);
            return (await source.ReadAsync(session.ToString(), account, CancellationToken.None), sent);
        }

        var (ok, requests) = await Read(front);
        Expect(ok is { Result: ProviderImageResult.Available, Images: [{ Side: "Front", ContentType: "image/png" }] } && ok.Images[0].Data.SequenceEqual(Png), "the front image");
        Expect(requests.Count == 2 && requests[0].Headers.Contains("x-api-key") && !requests[1].Headers.Contains("x-api-key") && requests[1].Headers.Authorization is null, "the API key never goes to the image host");
        var both = """[{"status":"Approved","front_image":"https://media.example/f","back_image":"https://media.example/b","portrait_image":"https://media.example/p","full_front_image":"https://media.example/x"}]""";
        var (two, asked) = await Read(both, u => new(HttpStatusCode.OK) { Content = new ByteArrayContent(u.AbsolutePath == "/f" ? Jpeg : Webp) });
        Expect(two.Images.Select(i => i.Side + i.ContentType).SequenceEqual(["Frontimage/jpeg", "Backimage/webp"]) && asked.Count == 3, "front and back only, never the portrait or full frame");
        async Task Is(string what, ProviderImageResult expected, Task<(ProviderImages Result, List<HttpRequestMessage> Sent)> read, int? calls = null)
        {
            var (r, s) = await read;
            Expect(r.Result == expected && r.Images.Count == 0 && (calls is null || s.Count == calls), $"{what}: {r.Result}, {s.Count} calls");
        }
        await Is("another member's session", ProviderImageResult.Unavailable, Read(front, vendor: Guid.NewGuid().ToString()), 1);
        await Is("another session", ProviderImageResult.Unavailable, Read(front, id: Guid.NewGuid().ToString()), 1);
        await Is("a deleted session", ProviderImageResult.Gone, Read(front, code: HttpStatusCode.NotFound), 1);
        await Is("Didit failing", ProviderImageResult.Unavailable, Read(front, code: HttpStatusCode.ServiceUnavailable), 1);
        await Is("no document check", ProviderImageResult.None, Read("[]"), 1);
        await Is("no image (a key without media, or a wallet)", ProviderImageResult.None, Read("""[{"status":"Approved","front_image":null}]"""), 1);
        await Is("a host not listed", ProviderImageResult.HostNotAllowed, Read(front, hosts: "other.example"), 1);
        await Is("no host listed", ProviderImageResult.HostNotAllowed, Read(front, hosts: ""), 1);
        foreach (var bad in new[] { "http://media.example/f", "https://media.example.evil.test/f", "https://user@media.example/f", "https://media.example:8443/f", "https://sub.media.example/f", "file:///etc/passwd", "/relative" })
            await Is("host not allowed: " + bad, ProviderImageResult.HostNotAllowed, Read($$"""[{"front_image":"{{bad}}"}]"""), 1);
        await Is("a redirect", ProviderImageResult.Unavailable, Read(front, _ => new(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://other.example/") } }), 2);
        await Is("an expired link", ProviderImageResult.Unavailable, Read(front, _ => new(HttpStatusCode.Forbidden)));
        await Is("HTML labelled as a PNG", ProviderImageResult.Refused, Read(front, _ => new(HttpStatusCode.OK) { Content = new StringContent("<script>1</script>", Encoding.UTF8, "image/png") }));
        await Is("SVG", ProviderImageResult.Refused, Read(front, _ => new(HttpStatusCode.OK) { Content = new StringContent("<svg xmlns='http://www.w3.org/2000/svg'/>") }));
        await Is("too large, declared", ProviderImageResult.Refused, Read(front, _ => new(HttpStatusCode.OK) { Content = new ByteArrayContent([.. Png, .. new byte[DiditIdImages.MaxBytes]]) }));
        await Is("too large, undeclared", ProviderImageResult.Refused, Read(front, _ => new(HttpStatusCode.OK) { Content = new Endless() }));
        await Is("one bad side refuses both", ProviderImageResult.Refused, Read(both, u => new(HttpStatusCode.OK) { Content = new ByteArrayContent(u.AbsolutePath == "/f" ? Png : "nope"u8.ToArray()) }));
        var off = new DiditIdImages(new HttpClient(new Fake(_ => throw new Exception("called"))), new DiditOptions("k", "w", null, new Uri("https://didit.example/")), new DiditImageOptions(false, ["media.example"]), NullLogger<DiditIdImages>.Instance);
        Expect((await off.ReadAsync(session.ToString(), account, default)).Result == ProviderImageResult.Unavailable, "off: nothing is asked");

        // The log names only a refused host: never a path, signature, session or image.
        var log = new Lines();
        var leaky = new DiditIdImages(new HttpClient(new Fake(r => new(HttpStatusCode.OK) { Content = new StringContent(Decision(front)) })), new DiditOptions("k", "w", null, new Uri("https://didit.example/")), new DiditImageOptions(true, []), log);
        await leaky.ReadAsync(session.ToString(), account, default);
        Expect(log.All.Contains("media.example") && !log.All.Contains("secret") && !log.All.Contains("front") && !log.All.Contains(session.ToString()), "log: " + log.All);

        // Release gate: the notice in force covers it from 0.21, on or after its date.
        Expect(!PrivacyNoticeVersion.CoversIdReview("0.20, 17 November 2026", new(2027, 1, 1)) && !PrivacyNoticeVersion.CoversIdReview("0.21, 3 December 2026", new(2026, 12, 2))
            && PrivacyNoticeVersion.CoversIdReview("0.21, 3 December 2026", new(2026, 12, 3)) && PrivacyNoticeVersion.CoversIdReview("1.0, 3 December 2026", new(2027, 1, 1)) && !PrivacyNoticeVersion.CoversIdReview("soon", new(2027, 1, 1)), "notice version");
        // D432/D453: 0.21 took effect on 26 November 2026; the notice in force now (0.22, from 28 November 2026) still covers it.
        Expect(PrivacyNoticeVersion.CoversIdReview(PrivacyNoticeVersion.Current, new(2026, 11, 28)) && !PrivacyNoticeVersion.CoversIdReview(PrivacyNoticeVersion.Current, new(2026, 11, 27))
            && Swoovr.Client.PrivacyNotice.Version == PrivacyNoticeVersion.Current, "the notice in force covers it from its date");
        bool Can(string capability, AccountRole role) => StaffAccessPolicy.CheckRequest(capability, "Fictional", r => r == role) == AccessRefusal.None;
        Expect(Can("id-image-open", AccountRole.Admin) && !new[] { AccountRole.Support, AccountRole.Legal, AccountRole.TrustSafety }.Any(r => Can("id-image-open", r)), "Admin only");
        Expect(StaffAccessPolicy.Classify("POST", $"/api/admin/accounts/{account}/id-image", 200) == ("identity", account.ToString(), "view")
            && StaffAccessPolicy.Classify("POST", $"/api/admin/accounts/{account}/id-image", 403)?.Action == "refused", "an identity view in the access log");
        Console.WriteLine($"PASS: ID images for staff ({checks} checks; synthetic bytes, no live provider).");
    }

    private sealed class Fake(Func<HttpRequestMessage, HttpResponseMessage> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(send(request));
    }
    private sealed class Endless : HttpContent
    {
        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            await stream.WriteAsync(Png);
            for (var i = 0; i < 200; i++) await stream.WriteAsync(new byte[65536]);
        }
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
    private sealed class Lines : Microsoft.Extensions.Logging.ILogger<DiditIdImages>
    {
        public string All = "";
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel level) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel level, Microsoft.Extensions.Logging.EventId id, TState state, Exception? error, Func<TState, Exception?, string> format) => All += format(state, error) + error + "\n";
    }
}
