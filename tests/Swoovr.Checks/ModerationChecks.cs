using Swoovr.Domain;

/// <summary>
/// Slice Q rules: who may do what in moderation (D133, D134), sanction limits and enforcement with automatic expiry,
/// appeals (D135), what reporters see and the abuse limit (D131), and the entitlement service (D151).
/// </summary>
static class ModerationChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Moderation check failed: " + what); checks++; }
        var (admin, support) = (AccountRole.Admin, AccountRole.Support);
        var day = TimeSpan.FromDays(1);

        // Roles (D133): support reviews, dismisses, warns and restricts; only admins suspend, terminate and revoke affiliation.
        Expect(ModerationPolicy.Check(ModerationAction.Dismiss, support, false, false) == ModerationRefusal.None, "support dismisses");
        Expect(ModerationPolicy.Check(ModerationAction.Warn, support, false, false, reason: "r") == ModerationRefusal.None, "support warns");
        Expect(ModerationPolicy.Check(ModerationAction.Restrict, support, false, false, 30 * day, "r") == ModerationRefusal.None, "support restricts 30 days");
        Expect(ModerationPolicy.Check(ModerationAction.Suspend, support, false, false, day, "r") == ModerationRefusal.NotPermitted, "support can't suspend");
        Expect(ModerationPolicy.Check(ModerationAction.Terminate, support, false, false, null, "r") == ModerationRefusal.NotPermitted, "support can't terminate");
        Expect(ModerationPolicy.Check(ModerationAction.RevokeStaffAffiliation, support, false, false) == ModerationRefusal.NotPermitted, "support can't revoke affiliation");
        Expect(ModerationPolicy.Check(ModerationAction.Suspend, admin, false, false, 90 * day, "r") == ModerationRefusal.None, "admin suspends 90 days");
        Expect(ModerationPolicy.Check(ModerationAction.Terminate, admin, false, false, null, "r") == ModerationRefusal.None, "admin terminates");
        Expect(ModerationPolicy.Check(ModerationAction.Warn, AccountRole.Member, false, false, reason: "r") == ModerationRefusal.NotPermitted, "members can't moderate");
        Expect(ModerationPolicy.Check(ModerationAction.Warn, AccountRole.VenuePartner, false, false, reason: "r") == ModerationRefusal.NotPermitted, "venue partners can't moderate");
        // Staff-affiliated cases (D134) and one's own account.
        Expect(ModerationPolicy.Check(ModerationAction.Warn, support, true, false, reason: "r") == ModerationRefusal.StaffAffiliatedCase, "support can't act on staff");
        Expect(ModerationPolicy.Check(ModerationAction.Dismiss, support, true, false) == ModerationRefusal.StaffAffiliatedCase, "support can't even dismiss a staff case");
        Expect(ModerationPolicy.Check(ModerationAction.Warn, admin, true, false, reason: "r") == ModerationRefusal.None, "admins work staff cases");
        Expect(ModerationPolicy.Check(ModerationAction.Warn, admin, true, true, reason: "r") == ModerationRefusal.OwnAccount, "never one's own member account");
        Expect(ModerationPolicy.Check(ModerationAction.Acknowledge, admin, false, true) == ModerationRefusal.OwnAccount, "not even acknowledging");
        // Limits and reasons.
        Expect(ModerationPolicy.Check(ModerationAction.Restrict, admin, false, false, 31 * day, "r") == ModerationRefusal.TooLong, "restriction over 30 days");
        Expect(ModerationPolicy.Check(ModerationAction.Suspend, admin, false, false, 91 * day, "r") == ModerationRefusal.TooLong, "suspension over 90 days");
        Expect(ModerationPolicy.Check(ModerationAction.Restrict, admin, false, false, null, "r") == ModerationRefusal.TooLong, "restriction without a length");
        Expect(ModerationPolicy.Check(ModerationAction.Restrict, admin, false, false, TimeSpan.Zero, "r") == ModerationRefusal.TooLong, "zero days");
        Expect(ModerationPolicy.Check(ModerationAction.Warn, admin, false, false, reason: " ") == ModerationRefusal.ReasonRequired, "a reason is required");
        Expect(ModerationPolicy.Check(ModerationAction.Warn, admin, false, false, reason: new string('x', 1001)) == ModerationRefusal.ReasonRequired, "reason over 1,000");
        Expect(ModerationPolicy.Check(ModerationAction.Restrict, support, false, false, day, "r") == ModerationRefusal.None, "a restriction needs no scope since Q55");
        // Lifting: support can't lift what an admin applied, nor a suspension.
        var byAdmin = new Sanction { Kind = SanctionKind.Restriction, CreatedByRole = admin };
        var bySupport = new Sanction { Kind = SanctionKind.Restriction, CreatedByRole = support };
        Expect(ModerationPolicy.Check(ModerationAction.Lift, support, false, false, reason: "r", lifting: byAdmin) == ModerationRefusal.AdminSanction, "support can't override admin");
        Expect(ModerationPolicy.Check(ModerationAction.Lift, support, false, false, reason: "r", lifting: bySupport) == ModerationRefusal.None, "support lifts its own kind");
        Expect(ModerationPolicy.Check(ModerationAction.Lift, admin, false, false, reason: "r", lifting: byAdmin) == ModerationRefusal.None, "admins lift anything");

        // D136: local password staff only when the API allows it; never for the root admin's rule or venues.
        var supportByPassword = new AccessSession(AccountRole.Support, AccountRealm.Staff, SignInMethod.EmailPassword, false, true, true, true, false, new HashSet<Guid>());
        Expect(!new AccountAccessPolicy().Allows(supportByPassword, AccountRole.Support), "password staff refused by default (Entra only)");
        Expect(new AccountAccessPolicy(localPasswordStaff: true).Allows(supportByPassword, AccountRole.Support), "local password staff allowed when switched on");
        Expect(!new AccountAccessPolicy(true).Allows(supportByPassword with { Method = SignInMethod.Google }, AccountRole.Support), "never social sign-in");
        Expect(!new AccountAccessPolicy(true).Allows(supportByPassword with { IsFresh = false }, AccountRole.Support), "needs a fresh session");
        Expect(!new AccountAccessPolicy(true).Allows(supportByPassword with { EmailVerified = false }, AccountRole.Support), "needs a verified email");
        Expect(!new AccountAccessPolicy(true).Allows(supportByPassword with { Realm = AccountRealm.Members }, AccountRole.Support), "staff realm only");
        Expect(!new AccountAccessPolicy(true).Allows(supportByPassword with { Role = AccountRole.Admin, IsRootAdmin = true }, AccountRole.Admin), "the root admin still needs OTP");

        // Enforcement and automatic expiry (D133).
        var now = DateTimeOffset.Parse("2026-10-25T00:00:00Z");
        Sanction S(SanctionKind kind, int? days, bool hide = false, bool noRequests = false, DateTimeOffset? lifted = null) =>
            new() { Kind = kind, StartsAt = now.AddDays(-1), EndsAt = days is { } d ? now.AddDays(-1 + d) : null, HideFromDiscovery = hide, NoNewRequests = noRequests, LiftedAt = lifted };
        Expect(Enforcement.For([], now) == Enforcement.None, "nothing");
        Expect(Enforcement.For([S(SanctionKind.Warning, null)], now) == Enforcement.None, "a warning restricts nothing");
        // Q55 (D160): any restriction bars the member, whatever options were ticked.
        var restricted = Enforcement.For([S(SanctionKind.Restriction, 7, noRequests: true)], now);
        Expect(restricted.Barred && restricted.Restricted && restricted.HiddenFromDiscovery && restricted.NoNewRequests && restricted.NoMessages, "a restriction bars everything");
        Expect(restricted.State == "restricted" && Enforcement.None.State == "active", "restricted state");
        Expect(Enforcement.For([S(SanctionKind.Restriction, 7), S(SanctionKind.Suspension, 7)], now).State == "suspended", "suspension outranks restriction");
        Expect(!Enforcement.For([S(SanctionKind.Restriction, 1)], now).Barred, "an expired restriction lifts by itself");
        var suspended = Enforcement.For([S(SanctionKind.Suspension, 30)], now);
        Expect(suspended.Barred && suspended.HiddenFromDiscovery && suspended.NoNewRequests && suspended.NoMessages, "a suspension bars everything");
        Expect(Enforcement.For([S(SanctionKind.Termination, null)], now).Terminated, "termination is permanent");
        Expect(!Enforcement.For([S(SanctionKind.Suspension, 1)], now).Barred, "an expired suspension lifts by itself");
        Expect(!Enforcement.For([S(SanctionKind.Suspension, 30, lifted: now)], now).Barred, "a lifted suspension");
        Expect(!Enforcement.For([new Sanction { Kind = SanctionKind.Suspension, StartsAt = now.AddDays(1), EndsAt = now.AddDays(5) }], now).Barred, "not started yet");

        // Appeals (D135).
        var member = Guid.NewGuid();
        var decider = Guid.NewGuid();
        var sanction = new Sanction { AccountId = member, StartsAt = now.AddDays(-3), CreatedBy = decider, CreatedByRole = support };
        Expect(ModerationPolicy.MayAppeal(sanction, member, false, now), "within 30 days");
        Expect(!ModerationPolicy.MayAppeal(sanction, member, true, now), "one appeal per action");
        Expect(!ModerationPolicy.MayAppeal(sanction, Guid.NewGuid(), false, now), "only the member");
        Expect(!ModerationPolicy.MayAppeal(sanction, member, false, now.AddDays(28)), "not after 30 days");
        Expect(ModerationPolicy.MayDecideAppeal(sanction, decider, support, false, false) == ModerationRefusal.NotPermitted, "not by the original decider");
        Expect(ModerationPolicy.MayDecideAppeal(sanction, Guid.NewGuid(), support, false, false) == ModerationRefusal.None, "another support member");
        Expect(ModerationPolicy.MayDecideAppeal(sanction, Guid.NewGuid(), support, true, false) == ModerationRefusal.StaffAffiliatedCase, "admins only for staff");
        Expect(ModerationPolicy.MayDecideAppeal(new Sanction { CreatedBy = decider, CreatedByRole = admin }, Guid.NewGuid(), support, false, false) == ModerationRefusal.AdminSanction, "support can't overturn an admin");
        Expect(ModerationPolicy.MayDecideAppeal(sanction, Guid.NewGuid(), admin, false, true) == ModerationRefusal.OwnAccount, "never one's own account");

        // Reports (D131).
        Expect(ReportPolicy.Supported(ReportSubject.Profile) && ReportPolicy.Supported(ReportSubject.Request) && ReportPolicy.Supported(ReportSubject.Bot), "subjects today");
        Expect(!ReportPolicy.Supported(ReportSubject.Message) && !ReportPolicy.Supported(ReportSubject.Photo), "later subjects wait for their features");
        Expect(ReportPolicy.ReporterStatus(CaseStatus.Open) == "submitted" && ReportPolicy.ReporterStatus(CaseStatus.Reviewed) == "reviewed", "reporter statuses");
        Expect(ReportPolicy.RateLimited(ReportPriority.Normal, 10) && !ReportPolicy.RateLimited(ReportPriority.Normal, 9), "burst limit");
        Expect(!ReportPolicy.RateLimited(ReportPriority.Urgent, 1000), "urgent reports always accepted");
        Expect(ReportPolicy.QueueFor(true) == CaseQueue.Admin && ReportPolicy.QueueFor(false) == CaseQueue.Support, "staff cases go to admins");
        Expect(ReportPolicy.Max(ReportPriority.Normal, ReportPriority.Urgent) == ReportPriority.Urgent, "case priority is the highest");

        // Entitlements (D151).
        Entitlement E(PlanTier tier, int fromDays, int? toDays, bool revoked = false) =>
            new() { Tier = tier, StartsAt = now.AddDays(fromDays), EndsAt = toDays is { } t ? now.AddDays(t) : null, RevokedAt = revoked ? now : null };
        Expect(EntitlementPolicy.Effective([], false, null, now) == PlanTier.Free, "free by default");
        Expect(EntitlementPolicy.Effective([E(PlanTier.Plus, -1, 30)], false, null, now) == PlanTier.Plus, "a valid grant");
        Expect(EntitlementPolicy.Effective([E(PlanTier.Plus, -40, -1)], false, null, now) == PlanTier.Free, "a lapsed grant falls back to Free");
        Expect(EntitlementPolicy.Effective([E(PlanTier.Plus, -1, 30, revoked: true)], false, null, now) == PlanTier.Free, "a revoked grant");
        Expect(EntitlementPolicy.Effective([E(PlanTier.Plus, 2, 30)], false, null, now) == PlanTier.Free, "a future grant");
        Expect(EntitlementPolicy.Effective([E(PlanTier.Plus, -1, 30), E(PlanTier.Premium, -1, 5)], false, null, now) == PlanTier.Premium, "the highest valid tier");
        Expect(EntitlementPolicy.Effective([], true, now.AddDays(-1), now) == PlanTier.Plus, "founders get Plus");
        Expect(EntitlementPolicy.Effective([], true, now.AddDays(1), now) == PlanTier.Free, "founding Plus from its start");
        Expect(EntitlementPolicy.ValidGrant(PlanTier.Plus, now, now.AddDays(90), "beta tester"), "a valid grant request");
        Expect(!EntitlementPolicy.ValidGrant(PlanTier.Free, now, now.AddDays(90), "x"), "granting Free");
        Expect(!EntitlementPolicy.ValidGrant(PlanTier.Plus, now, null, "x"), "an open-ended grant");
        Expect(!EntitlementPolicy.ValidGrant(PlanTier.Plus, now, now.AddDays(400), "x"), "over a year");
        Expect(!EntitlementPolicy.ValidGrant(PlanTier.Plus, now, now.AddDays(-1), "x"), "ends before it starts");
        Expect(!EntitlementPolicy.ValidGrant(PlanTier.Plus, now, now.AddDays(9), " "), "no reason");

        Console.WriteLine($"PASS: {checks} Moderation cases (support/admin limits, staff-affiliated cases admin-only, never one's own account, 30/90-day limits and reasons, support can't override admins, enforcement with automatic expiry, appeals once within 30 days by someone else, reporter statuses, urgent reports never limited, entitlements: highest valid tier, lapsed/revoked/future grants, founders, grant limits).");
    }
}
