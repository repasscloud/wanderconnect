using Swoovr.Client;
using Swoovr.Contracts;
using Swoovr.Domain;

/// <summary>
/// Account deletion and export (Q43, D144; owner answers Q61–Q65, D169–D173): request, cancel, delete now, the 30-day
/// recovery period, the five-minute sign-in, the tombstone, two-year retention, the export link and its daily limit.
/// </summary>
static class DeletionChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Deletion check failed: " + what); checks++; }
        var now = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
        Account Member(AccountStatus status = AccountStatus.Active) => new()
        {
            Id = Guid.NewGuid(), Issuer = "http://localhost:8180/realms/swoovr-members-dev", Subject = Guid.NewGuid().ToString(), Realm = AccountRealm.Members,
            Email = "fictional.someone@example.test", Status = status, CreatedAt = now.AddDays(-100), LastSeenAt = now,
        };

        // Who may ask, and the fresh sign-in.
        Expect(AccountDeletionPolicy.CanRequest(AccountStatus.Active) && AccountDeletionPolicy.CanRequest(AccountStatus.Locked)
            && AccountDeletionPolicy.CanRequest(AccountStatus.Suspended), "active, locked and suspended accounts may leave");
        Expect(!AccountDeletionPolicy.CanRequest(AccountStatus.PendingDeletion) && !AccountDeletionPolicy.CanRequest(AccountStatus.Deleted), "pending and deleted ones can't ask again");
        var window = AccountDeletionPolicy.ReauthWindow;
        Expect(window == TimeSpan.FromMinutes(5), "the sign-in window is five minutes (D169)");
        Expect(AccountDeletionPolicy.Fresh(now.AddMinutes(-4), now, window), "a sign-in four minutes ago is fresh");
        Expect(!AccountDeletionPolicy.Fresh(now.AddMinutes(-6), now, window), "six minutes ago is stale");
        Expect(!AccountDeletionPolicy.Fresh(now.AddMinutes(2), now, window), "a sign-in from the future is refused");
        Expect(AccountDeletionPolicy.CheckRequest(Member(), fresh: false) == DeletionRequestError.StaleSession, "a stale session can't delete");
        Expect(AccountDeletionPolicy.CheckRequest(Member(), fresh: true) == DeletionRequestError.None, "a fresh one can");
        Expect(AccountDeletionPolicy.CheckRequest(Member(AccountStatus.PendingDeletion), fresh: true) == DeletionRequestError.AlreadyPending, "asking twice");
        Expect(AccountDeletionPolicy.CheckRequest(Member(AccountStatus.Deleted), fresh: true) == DeletionRequestError.NotAllowed, "a tombstone can't ask");

        // The 30-day recovery period, cancelling and deleting now.
        var a = Member();
        AccountDeletionPolicy.Request(a, now, immediately: false, deleteFounderToken: false);
        Expect(a.Status == AccountStatus.PendingDeletion && a.DeletionDueAt == now.AddDays(30) && a.DeletionRequestedAt == now, "30 days to change your mind");
        Expect(!AccountDeletionPolicy.Due(a, now.AddDays(29)) && AccountDeletionPolicy.Due(a, now.AddDays(30)), "due after exactly 30 days");
        Expect(AccountDeletionPolicy.Cancel(a) && a.Status == AccountStatus.Active && a.DeletionDueAt is null && a.DeletionRequestedAt is null, "cancel brings everything back");
        Expect(!AccountDeletionPolicy.Cancel(a), "nothing to cancel twice");
        var locked = Member(AccountStatus.Locked);
        AccountDeletionPolicy.Request(locked, now, false, false);
        AccountDeletionPolicy.Cancel(locked);
        Expect(locked.Status == AccountStatus.Locked, "cancelling restores a locked account as locked, not active");
        var b = Member();
        AccountDeletionPolicy.Request(b, now, immediately: true, deleteFounderToken: true);
        Expect(AccountDeletionPolicy.Due(b, now) && b.DeleteFounderToken, "delete now is due on the next purge run, with the founder choice kept");
        var c = Member();
        AccountDeletionPolicy.Request(c, now, false, false);
        Expect(AccountDeletionPolicy.DeleteNow(c, now.AddDays(3)) && AccountDeletionPolicy.Due(c, now.AddDays(3)), "delete now during the recovery period");
        Expect(!AccountDeletionPolicy.DeleteNow(Member(), now), "delete now needs a pending deletion");
        Expect(!AccountDeletionPolicy.Due(Member(), now.AddYears(1)), "an active account is never due");

        // The tombstone and retention.
        AccountDeletionPolicy.Tombstone(b, now);
        var subject = b.Subject;
        Expect(b.Status == AccountStatus.Deleted && b.Email is null && b.DeletedAt == now, "the tombstone keeps no email");
        Expect(b.Subject == subject, "it keeps the (deleted) Keycloak user id, so a token issued before deletion is refused, not given a new account");
        Expect(!AccountDeletionPolicy.Due(b, now.AddDays(1)), "a tombstone is never deleted twice");
        Expect(!AccountDeletionPolicy.RetentionOver(b, now.AddDays(729)) && AccountDeletionPolicy.RetentionOver(b, now.AddDays(730)), "records go after two years (D170)");
        Expect(!AccountDeletionPolicy.RetentionOver(Member(), now.AddYears(5)), "retention only ends for tombstones");
        Expect(AccountDeletionPolicy.TerminatedHold == TimeSpan.FromDays(730), "terminated-email holds last two years (D171)");
        Expect(!typeof(TerminatedEmailHold).GetProperties().Any(p => p.PropertyType == typeof(string)), "a hold stores no address, only a hash");

        // The export link (D172).
        Expect(AccountDeletionPolicy.ExportAllowed(null, now) && !AccountDeletionPolicy.ExportAllowed(now.AddHours(-23), now)
            && AccountDeletionPolicy.ExportAllowed(now.AddHours(-24), now), "one export a day");
        var export = new DataExport { Id = Guid.NewGuid(), AccountId = a.Id, CreatedAt = now, ExpiresAt = now + AccountDeletionPolicy.ExportLifetime };
        Expect(AccountDeletionPolicy.ExportLifetime == TimeSpan.FromMinutes(15), "the link lasts 15 minutes");
        Expect(AccountDeletionPolicy.ExportUsable(export, a.Id, now.AddMinutes(14)), "usable inside 15 minutes");
        Expect(!AccountDeletionPolicy.ExportUsable(export, a.Id, now.AddMinutes(15)), "expired at 15 minutes");
        Expect(!AccountDeletionPolicy.ExportUsable(export, Guid.NewGuid(), now), "someone else can't use it");
        Expect(!AccountDeletionPolicy.ExportUsable(null, a.Id, now), "an unknown link");
        export.DownloadedAt = now;
        Expect(!AccountDeletionPolicy.ExportUsable(export, a.Id, now), "it works once");
        Expect(!typeof(DataExport).GetProperties().Any(p => p.Name.Contains("Token") && p.PropertyType == typeof(string)), "only the link's hash is stored");

        // Shared wording for the web and the app.
        Expect(DeletionText.Refusal(ProblemCodes.StaleSession) is null, "a stale sign-in shows the sign-in-again step, not an error");
        Expect(DeletionText.Refusal(ProblemCodes.ExportTooSoon) == DeletionText.TooSoon, "the daily limit is explained");
        Expect(DeletionText.Refusal("anything-else") == DeletionText.Failed, "unknown refusals read as a failure");
        Expect(DeletionText.PendingTitle(new DeletionView("pending", now, now, false, false)) == "Your account is being deleted", "delete now reads as in progress");
        Expect(DeletionText.PendingTitle(new DeletionView("pending", now, now.AddDays(30), false, false)) == "Your account will be deleted", "the recovery period reads as scheduled");

        Console.WriteLine($"PASS: account deletion and export ({checks}).");
    }
}
