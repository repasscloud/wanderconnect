using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Swoovr.Infrastructure.Email;

static class EmailOutcomeChecks
{
    public static async Task Run()
    {
        var message = new EmailMessage("fictional-recipient@example.test", "Fictional transport test", "Synthetic text only.");
        IEmailSender unknown = new LegacyFalseSender();
        Need((await unknown.SendOutcomeAsync(message, default)).Status == "unknown", "legacy false cannot establish non-acceptance");
        Need((await new UnconfiguredEmailSender().SendOutcomeAsync(message, default)).Status == "retry", "unconfigured is a known no-send");
        foreach (var expected in new[] { "accepted", "retry", "rejected", "unknown" })
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var server = ServeAsync(listener, expected, deadline.Token);
            try
            {
                var sender = new SmtpEmailSender("127.0.0.1", ((IPEndPoint)listener.LocalEndpoint).Port, "no-reply@example.test", NullLogger<SmtpEmailSender>.Instance);
                Need((await sender.SendOutcomeAsync(message, deadline.Token)).Status == expected, "SMTP " + expected);
                await server;
            }
            finally { listener.Stop(); }
        }
        var silent = new TcpListener(IPAddress.Loopback, 0); silent.Start();
        try
        {
            var connected = silent.AcceptTcpClientAsync();
            var bounded = new SmtpEmailSender("127.0.0.1", ((IPEndPoint)silent.LocalEndpoint).Port, "no-reply@example.test", NullLogger<SmtpEmailSender>.Instance, TimeSpan.FromMilliseconds(100));
            var result = await bounded.SendOutcomeAsync(message, default).WaitAsync(TimeSpan.FromSeconds(5));
            using var peer = await connected.WaitAsync(TimeSpan.FromSeconds(5));
            Need(result.Status == "unknown", "silent SMTP peer is bounded and never a known non-acceptance");
        }
        finally { silent.Stop(); }
        Console.WriteLine("PASS: email acceptance/rejection/uncertainty and bounded silent-peer timeout against fictional loopback SMTP (no external delivery).");
    }
    static void Need(bool ok, string name) { if (!ok) throw new Exception("Email outcome: " + name); }
    static async Task ServeAsync(TcpListener listener, string outcome, CancellationToken token)
    {
        using var client = await listener.AcceptTcpClientAsync(token);
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        await using var writer = new StreamWriter(stream, Encoding.ASCII, leaveOpen: true) { AutoFlush = true, NewLine = "\r\n" };
        await writer.WriteLineAsync("220 fictional.test ESMTP");
        var data = false;
        while (await reader.ReadLineAsync(token) is { } line)
        {
            if (data)
            {
                if (line != ".") continue;
                if (outcome == "unknown") return; // DATA received, final acknowledgement lost: never assume retry is safe.
                await writer.WriteLineAsync("250 queued"); data = false;
            }
            else if (line.StartsWith("EHLO") || line.StartsWith("HELO")) await writer.WriteLineAsync("250 fictional.test");
            else if (line.StartsWith("MAIL") && outcome is "retry" or "rejected")
            { await writer.WriteLineAsync(outcome == "retry" ? "451 temporary refusal" : "550 permanent refusal"); return; }
            else if (line.StartsWith("MAIL") || line.StartsWith("RCPT")) await writer.WriteLineAsync("250 OK");
            else if (line.StartsWith("DATA")) { await writer.WriteLineAsync("354 continue"); data = true; }
            else if (line.StartsWith("QUIT")) { await writer.WriteLineAsync("221 bye"); return; }
            else await writer.WriteLineAsync("250 OK");
        }
    }
    sealed class LegacyFalseSender : IEmailSender { public Task<bool> SendAsync(EmailMessage message, CancellationToken token) => Task.FromResult(false); }
}
