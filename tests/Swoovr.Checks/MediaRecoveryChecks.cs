using Swoovr.Domain;
using Swoovr.Infrastructure.Media;

static class MediaRecoveryChecks
{
    public static void Run()
    {
        var now = DateTimeOffset.Parse("2026-10-29T00:00:00Z");
        if (MediaRecoveryPolicy.NextAttempt(1, now) <= now || MediaRecoveryPolicy.NextAttempt(1000, now) > now.AddHours(1)) throw new Exception("Media recovery backoff must be bounded and future.");
        if (MediaRecoveryPolicy.Due(MediaStatus.Passed, null, now, now) || !MediaRecoveryPolicy.Due(MediaStatus.Blocked, null, now, now)) throw new Exception("Only unfinished blocked media is due for preservation.");
        if (MediaRecoveryPolicy.Due(MediaStatus.Blocked, now, now, now)) throw new Exception("Completed preservation must not repeat.");
        var absentRoot = Path.Combine(Path.GetTempPath(), "swoovr-absent-media-" + Guid.NewGuid().ToString("N"));
        var store = new LocalMediaStore(absentRoot);
        if (!store.DeleteAsync(MediaKeys.Photo(Guid.NewGuid()), CancellationToken.None).GetAwaiter().GetResult())
            throw new Exception("Deleting an absent local clear copy must be idempotent, including an absent parent directory.");
        Console.WriteLine("PASS: blocked-media recovery scheduling policy.");
    }
}
