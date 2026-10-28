using Swoovr.Domain;

static class MediaRecoveryChecks
{
    public static void Run()
    {
        var now = DateTimeOffset.Parse("2026-10-29T00:00:00Z");
        if (MediaRecoveryPolicy.NextAttempt(1, now) <= now || MediaRecoveryPolicy.NextAttempt(1000, now) > now.AddHours(1)) throw new Exception("Media recovery backoff must be bounded and future.");
        if (MediaRecoveryPolicy.Due(MediaStatus.Passed, null, now, now) || !MediaRecoveryPolicy.Due(MediaStatus.Blocked, null, now, now)) throw new Exception("Only unfinished blocked media is due for preservation.");
        if (MediaRecoveryPolicy.Due(MediaStatus.Blocked, now, now, now)) throw new Exception("Completed preservation must not repeat.");
        Console.WriteLine("PASS: blocked-media recovery scheduling policy.");
    }
}
