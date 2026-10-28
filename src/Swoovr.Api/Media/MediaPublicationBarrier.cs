namespace Swoovr.Api.Media;

public sealed partial class MediaPipeline
{
    // Deterministic local race tests only. No content in marker files; rejected at startup in hosted environments.
    private async Task PublicationBarrierAsync(Guid id, string stage, CancellationToken cancellationToken)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var root = configuration["Media:Publication:BarrierDirectory"];
        if (!services.GetRequiredService<IHostEnvironment>().IsDevelopment() || string.IsNullOrWhiteSpace(root)
            || (configuration["Media:Publication:BarrierStage"] ?? "after-read") != stage) return;
        Directory.CreateDirectory(root);
        await File.WriteAllTextAsync(Path.Combine(root, id + ".entered"), "", cancellationToken);
        var release = Path.Combine(root, id + ".release");
        for (var i = 0; i < 400; i++)
        {
            if (File.Exists(release)) return;
            await Task.Delay(50, cancellationToken);
        }
        throw new InvalidOperationException("Development publication barrier timed out.");
    }
}
