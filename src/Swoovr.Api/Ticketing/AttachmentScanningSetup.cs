using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using Swoovr.Infrastructure.Scanning;

namespace Swoovr.Api.Ticketing;

/// <summary>
/// D336 (#221): ClamAV beside the API (`Attachments:Scanning:ClamAv:Host`/`Port`). Scanning can be switched off only in
/// Development (`Attachments:Scanning:Enabled=false`); hosted environments refuse to start without a scanner
/// (HostingPolicy). Required but unconfigured: every file stays in quarantine.
/// </summary>
public static class AttachmentScanningSetup
{
    public static IServiceCollection AddSwoovrAttachmentScanning(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment, bool persistence)
    {
        if (!configuration.GetValue("Attachments:Scanning:Enabled", true))
        {
            if (!environment.IsDevelopment()) throw new InvalidOperationException("Attachments:Scanning:Enabled may be false in Development only.");
            services.AddSingleton<IAttachmentMalwareScanner, DisabledAttachmentScanner>();
        }
        else if (configuration["Attachments:Scanning:ClamAv:Host"] is { Length: > 0 } host)
        {
            var port = configuration.GetValue("Attachments:Scanning:ClamAv:Port", 3310);
            var seconds = configuration.GetValue("Attachments:Scanning:ClamAv:TimeoutSeconds", 120);
            var maximum = configuration.GetValue("Attachments:Scanning:ClamAv:MaxStreamBytes", 160L * 1024 * 1024);
            if (port is < 1 or > 65535 || seconds is < 5 or > 900 || maximum < Swoovr.Contracts.SupportAttachmentPolicy.MaxVideoBytes)
                throw new InvalidOperationException("Invalid Attachments:Scanning:ClamAv settings (the stream limit must cover the largest allowed file).");
            services.AddSingleton<IAttachmentMalwareScanner>(new ClamAvScanner(new(host, port, TimeSpan.FromSeconds(seconds), maximum)));
        }
        else services.AddSingleton<IAttachmentMalwareScanner, UnconfiguredAttachmentScanner>();
        services.AddSingleton<SupportAttachmentSignal>();
        if (persistence)
        {
            services.AddScoped<SupportAttachmentQuarantine>();
            if (configuration.GetValue("Attachments:Scanning:Worker", true)) services.AddHostedService<SupportAttachmentWorker>();
        }
        return services;
    }

    /// <summary>For monitoring and alerts: 200 when the scanner answers, else 503. Readiness doesn't depend on it.</summary>
    public static void Map(WebApplication app) => app.MapGet("/health/attachments", async (HttpContext context, IAttachmentMalwareScanner scanner, IServiceProvider services) =>
    {
        context.Response.Headers.CacheControl = "no-store";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var healthy = await scanner.HealthyAsync(timeout.Token);
        long? waiting = null;
        if (services.GetService<SwoovrDbContext>() is { } db)
            try { waiting = await db.Set<SupportAttachmentHold>().LongCountAsync(h => h.ReleasedAt == null && (h.State == AttachmentScanState.Pending || h.State == AttachmentScanState.Scanning || h.State == AttachmentScanState.Failed), timeout.Token); }
            catch (Exception e) when (e is System.Data.Common.DbException or InvalidOperationException or OperationCanceledException) { }
        return Results.Json(new { status = healthy ? "healthy" : "unhealthy", scanner = scanner.Name, waiting }, statusCode: healthy ? 200 : 503);
    });
}
