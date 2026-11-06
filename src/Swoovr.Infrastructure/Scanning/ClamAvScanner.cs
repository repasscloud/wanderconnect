using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;
using Swoovr.Domain;

namespace Swoovr.Infrastructure.Scanning;

/// <summary>
/// D336 (#221): clamd's TCP endpoint (never published outside the private network). <see cref="MaxStreamBytes"/> must not exceed
/// clamd's StreamMaxLength; anything longer is "unsupported" (scan-limit) without being sent.
/// </summary>
public sealed record ClamAvOptions(string Host, int Port = 3310, TimeSpan? Timeout = null, long MaxStreamBytes = 160L * 1024 * 1024)
{
    public TimeSpan EffectiveTimeout => Timeout ?? TimeSpan.FromMinutes(2);
}

/// <summary>
/// clamd INSTREAM client (zero-terminated commands: zVERSION, zPING, zINSTREAM with 4-byte big-endian chunk lengths).
/// It never throws for scanner trouble: an unreachable, slow or erroring scanner answers Failed, so files stay in quarantine.
/// Size-limit and Heuristics.Limits.Exceeded answers (clamd `AlertExceedsMax yes`) are Unsupported: never cleared.
/// </summary>
public sealed class ClamAvScanner(ClamAvOptions options) : IAttachmentMalwareScanner
{
    private const int ChunkBytes = 64 * 1024;
    public string Name => "clamav";

    public async Task<bool> HealthyAsync(CancellationToken cancellationToken = default)
    {
        try { return await CommandAsync("zPING\0", cancellationToken) == "PONG"; }
        catch (Exception e) when (Transient(e, cancellationToken)) { return false; }
    }

    public async Task<MalwareScanResult> ScanAsync(Stream content, CancellationToken cancellationToken = default)
    {
        string? engine = null, signatures = null;
        try
        {
            (engine, signatures) = Version(await CommandAsync("zVERSION\0", cancellationToken));
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            limit.CancelAfter(options.EffectiveTimeout);
            using var client = new TcpClient { NoDelay = true };
            await client.ConnectAsync(options.Host, options.Port, limit.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync(Encoding.ASCII.GetBytes("zINSTREAM\0"), limit.Token);
            var buffer = new byte[ChunkBytes + 4];
            long sent = 0;
            while (true)
            {
                var read = await content.ReadAsync(buffer.AsMemory(4, ChunkBytes), limit.Token);
                if (read == 0) break;
                sent += read;
                if (sent > options.MaxStreamBytes) return new(AttachmentScanState.Unsupported, Name, engine, signatures, Failure: "scan-limit");
                BinaryPrimitives.WriteUInt32BigEndian(buffer, (uint)read);
                await stream.WriteAsync(buffer.AsMemory(0, read + 4), limit.Token);
            }
            await stream.WriteAsync(new byte[4], limit.Token);
            return Interpret(await ReplyAsync(stream, limit.Token), engine, signatures);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return new(AttachmentScanState.Failed, Name, engine, signatures, Failure: "scan-timeout"); }
        catch (Exception e) when (Transient(e, cancellationToken))
        { return new(AttachmentScanState.Failed, Name, engine, signatures, Failure: "scanner-unavailable"); }
    }

    /// <summary>Maps clamd's INSTREAM reply. Public for the checks.</summary>
    public static MalwareScanResult Interpret(string reply, string? engine, string? signatures, string scanner = "clamav")
    {
        var text = reply.Trim();
        if (text.StartsWith("stream:", StringComparison.Ordinal)) text = text["stream:".Length..].Trim();
        if (text == "OK") return new(AttachmentScanState.Clean, scanner, engine, signatures);
        if (text.EndsWith(" FOUND", StringComparison.Ordinal))
        {
            var name = text[..^" FOUND".Length].Trim();
            if (name.Length is < 1 or > 200 || name.Any(char.IsControl)) return new(AttachmentScanState.Failed, scanner, engine, signatures, Failure: "scanner-error");
            return name.StartsWith("Heuristics.Limits.Exceeded", StringComparison.Ordinal)
                ? new(AttachmentScanState.Unsupported, scanner, engine, signatures, Detection: name, Failure: "scan-limit")
                : new(AttachmentScanState.Infected, scanner, engine, signatures, Detection: name);
        }
        if (text.Contains("size limit exceeded", StringComparison.OrdinalIgnoreCase)) return new(AttachmentScanState.Unsupported, scanner, engine, signatures, Failure: "scan-limit");
        return new(AttachmentScanState.Failed, scanner, engine, signatures, Failure: "scanner-error");
    }

    /// <summary>"ClamAV 1.4.3/27450/Thu Nov  6 08:31:10 2026" → engine "ClamAV 1.4.3", signatures "27450".</summary>
    public static (string? Engine, string? Signatures) Version(string reply)
    {
        var parts = reply.Trim().Split('/', 3);
        return parts.Length >= 2 && parts[0].StartsWith("ClamAV ", StringComparison.Ordinal) ? (parts[0], parts[1]) : (parts.Length == 1 && parts[0].Length is > 0 and <= 64 ? parts[0] : null, null);
    }

    private async Task<string> CommandAsync(string command, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(TimeSpan.FromSeconds(10));
        using var client = new TcpClient { NoDelay = true };
        await client.ConnectAsync(options.Host, options.Port, limit.Token);
        await using var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes(command), limit.Token);
        return await ReplyAsync(stream, limit.Token);
    }

    /// <summary>Reads one zero-terminated reply of at most 1 KB.</summary>
    private static async Task<string> ReplyAsync(Stream stream, CancellationToken cancellationToken)
    {
        var reply = new List<byte>();
        var one = new byte[256];
        while (reply.Count < 1024)
        {
            var read = await stream.ReadAsync(one, cancellationToken);
            if (read == 0) break;
            var end = Array.IndexOf(one, (byte)0, 0, read);
            reply.AddRange(one.Take(end < 0 ? read : end));
            if (end >= 0) break;
        }
        return Encoding.ASCII.GetString(reply.ToArray());
    }

    private static bool Transient(Exception e, CancellationToken cancellationToken) =>
        e is SocketException or IOException or ObjectDisposedException or InvalidOperationException
        || e is OperationCanceledException && !cancellationToken.IsCancellationRequested;
}

/// <summary>Development and tests only (D336 §7): scanning switched off by configuration. Every file is reported clean by "disabled".</summary>
public sealed class DisabledAttachmentScanner : IAttachmentMalwareScanner
{
    public string Name => "disabled";
    public Task<bool> HealthyAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<MalwareScanResult> ScanAsync(Stream content, CancellationToken cancellationToken = default) =>
        Task.FromResult(new MalwareScanResult(AttachmentScanState.Clean, Name, null, null));
}

/// <summary>Scanning is required but no scanner is configured: every file stays in quarantine (fail closed).</summary>
public sealed class UnconfiguredAttachmentScanner : IAttachmentMalwareScanner
{
    public string Name => "unconfigured";
    public Task<bool> HealthyAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
    public Task<MalwareScanResult> ScanAsync(Stream content, CancellationToken cancellationToken = default) =>
        Task.FromResult(new MalwareScanResult(AttachmentScanState.Failed, Name, null, null, Failure: "scanner-unconfigured"));
}
