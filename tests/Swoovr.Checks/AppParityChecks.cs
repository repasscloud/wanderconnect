using System.Text.Json;
using System.Text.RegularExpressions;
using Swoovr.Client;
using Swoovr.Contracts;

/// <summary>
/// Slice S (D141): the web and the app share tabs, badge artwork and wording. Badge shapes come from BadgeArtwork and
/// their colours and labels from docs/design/brand-colours.json; both clients must draw from them, not their own copies.
/// The member wording for refusals is MemberText, so the two clients say the same thing about the same API answer.
/// </summary>
static class AppParityChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("App parity check failed: " + what); checks++; }
        var root = Root();
        string Read(string path) => File.ReadAllText(Path.Combine(root, path));

        // Badge artwork: every badge the API can return has a shape, a label and a fixed colour token.
        using var brand = JsonDocument.Parse(Read("docs/design/brand-colours.json"));
        var badges = brand.RootElement.GetProperty("badges");
        var colours = Read("src/Swoovr.Mobile/Resources/Styles/Colors.xaml");
        foreach (var apiBadge in new[] { "founder", "photoVerified", "idVerified", "staff", "bot" })
            Expect(BadgeArtwork.Kinds.Contains(AccountText.BadgeKind(apiBadge)), $"API badge {apiBadge} has artwork");
        foreach (var kind in BadgeArtwork.Kinds)
        {
            var shape = BadgeArtwork.Shape(kind);
            var token = BadgeArtwork.ColourToken(kind);
            Expect(badges.TryGetProperty(token, out var entry), $"{kind} colour is a brand token");
            Expect(entry.GetProperty("label").GetString() == BadgeArtwork.Label(kind), $"{kind} label matches brand-colours.json");
            Expect(colours.Contains($"x:Key=\"Badge{char.ToUpperInvariant(token[0])}{token[1..]}\">{entry.GetProperty("hex").GetString()}<"), $"{kind} colour generated into Colors.xaml");
            Expect(shape.Width > 0 && shape.Height > 0 && shape.Strokes.Count > 0, $"{kind} has a view box and strokes");
            foreach (var stroke in shape.Strokes)
            {
                // Absolute commands only, so SVG and MAUI's path markup read the data the same way.
                Expect(Regex.IsMatch(stroke.Data, @"^M[-0-9. MLHVCAZ]+$") && !Regex.IsMatch(stroke.Data, "[a-z]"), $"{kind} path data uses absolute commands");
                Expect(stroke.Part is "line" or "disc" or "tick" or "bird", $"{kind} stroke part is known");
            }
        }
        Expect(BadgeArtwork.Shape("founding") == BadgeArtwork.Shape("staff") && BadgeArtwork.Shape("staff") == BadgeArtwork.Shape("bot"), "the three birds share one shape");
        Expect(BadgeArtwork.Shape("photo").Strokes.Any(s => s.Part == "tick") && BadgeArtwork.Shape("id").Strokes.Any(s => s.Part == "disc"), "verification badges carry the check disc");
        Expect(Throws(() => BadgeArtwork.Shape("admin")) && Throws(() => BadgeArtwork.ColourToken("")), "an unknown badge has no artwork (admin verified has none, D58)");
        Expect(BadgeArtwork.SvgTransform(BadgeArtwork.Shape("photo").Strokes[0]) is null && BadgeArtwork.SvgTransform(BadgeArtwork.Shape("bot").Strokes[0])!.StartsWith("translate("), "transforms only where the artwork has one");
        // The app draws untransformed paths, so it places the data itself (MAUI clips transformed paths).
        Expect(BadgeArtwork.Transform("M0 0H10V4L2 3Z", 2, 1, 1) == "M 1 1 H 21 V 9 L 5 7 Z", "move, lines and close are mapped");
        Expect(BadgeArtwork.Transform("M0 0C1 2 3 4 5 6", 1, 0, 0) == "M 0 0 C 1 2 3 4 5 6", "the identity keeps a curve");
        Expect(BadgeArtwork.Transform("M2 2A5 5 0 1 0 12 2", 2, 0, 0) == "M 4 4 A 10 10 0 1 0 24 4", "arcs scale radii and end, not flags");
        Expect(BadgeArtwork.Transform("M0 0L1 1 2 2", 1, 1, 0) == "M 1 0 L 2 1 3 2", "a repeated command keeps going");
        Expect(Throws<FormatException>(() => BadgeArtwork.Transform("M0 0l1 1", 1, 0, 0)), "relative commands are refused");
        foreach (var kind in BadgeArtwork.Kinds)
        {
            var shape = BadgeArtwork.Shape(kind);
            var numbers = shape.Strokes.SelectMany(s => Regex.Matches(BadgeArtwork.Placed(shape, s, 16), @"-?\d+(\.\d+)?").Select(m => double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture))).ToList();
            // Flags and radii are small; coordinates must land inside the 16 px square, with a little room for the stroke.
            Expect(numbers.All(n => n is >= -0.5 and <= 16.5), $"{kind} placed inside a 16 px square");
        }
        var web = Read("src/Swoovr.Web.Client/UI/Badge.razor");
        Expect(web.Contains("BadgeArtwork.Shape") && !web.Contains("d=\"M"), "the web badge draws only the shared artwork");
        Expect(Read("src/Swoovr.Mobile/Ui.cs").Contains("BadgeArtwork.Shape"), "the app badge draws the shared artwork");

        // Tabs (D141): Explore, Connections, Chats, Me, in that order, on both.
        var appTabs = Regex.Matches(Read("src/Swoovr.Mobile/AppShell.xaml"), "ShellContent Title=\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray();
        var webTabs = Regex.Matches(Read("src/Swoovr.Web/Components/Layout/MainLayout.razor"), "@Tab\\(\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToArray();
        Expect(appTabs.SequenceEqual(["Explore", "Connections", "Chats", "Me"]), "app tabs are Explore, Connections, Chats, Me");
        Expect(webTabs.SequenceEqual(appTabs), "web and app tabs match");

        // Whole-screen refusals: each barred state has words; an ordinary refusal doesn't bar the screen.
        foreach (var code in new[] { ProblemCodes.AccountRestricted, ProblemCodes.AccountSuspended, ProblemCodes.AgeVerificationRequired, ProblemCodes.BetaNotEnrolled, ProblemCodes.AccountLocked })
            Expect(MemberText.Barred(code) is { Length: > 0 }, $"{code} bars the screen");
        foreach (var code in new[] { null, ProblemCodes.ModeNotEnabled, ProblemCodes.DateOfBirthRequired, ProblemCodes.NotAvailable, ProblemCodes.Restricted })
            Expect(MemberText.Barred(code) is null, $"{code ?? "success"} doesn't bar the screen");
        Expect(MemberText.Barred(ProblemCodes.AccountRestricted) != MemberText.Barred(ProblemCodes.AccountSuspended), "restricted and suspended read differently");

        // D160: the restricted screen for every state but active.
        Expect(MemberText.RestrictedTitle("active") is null, "an active account isn't restricted");
        Expect(new[] { "restricted", "suspended", "terminated" }.Select(MemberText.RestrictedTitle).Distinct().Count() == 3, "each barred state has its own heading");
        Expect(MemberText.RestrictedTitle("unknown-state") is not null, "an unknown state fails closed to the restricted screen");

        // Explore's notices (D125, D139, D157), the same as the web's.
        foreach (var code in new[] { ProblemCodes.ModeNotEnabled, ProblemCodes.DateOfBirthRequired, ProblemCodes.DatingDetailsRequired, ProblemCodes.HomeCityRequired, ProblemCodes.MarketNotOpen })
            Expect(MemberText.ExploreNotice(code, "Dating") is { Length: > 0 }, $"Explore explains {code}");
        Expect(MemberText.ExploreNotice(null, "Dating") is null && MemberText.ExploreNotice(ProblemCodes.AccountRestricted, "Dating") is null, "people or a barred screen, not a notice");
        Expect(MemberText.ExploreNotice(ProblemCodes.ModeNotEnabled, "Friends")!.StartsWith("Friends is off"), "the notice names the mode");
        Expect(MemberText.ExploreNotice(ProblemCodes.DateOfBirthRequired, "Dating")!.Contains("never shown"), "the date of birth is never shown (D139)");
        Expect(Read("src/Swoovr.Web.Client/Profiles/ExplorePanel.razor").Contains("Swoovr isn't available in your area yet. We're expanding to new cities.")
            && MemberText.ExploreNotice(ProblemCodes.MarketNotOpen, "")!.Equals("Swoovr isn't available in your area yet. We're expanding to new cities."), "the D125 sentence is the owner's, on both");

        // Requests, answers and notices.
        Expect(MemberText.RequestProblem(ProblemCodes.ArrivalRequired) != MemberText.RequestProblem("anything-else"), "arrival has its own words (D149)");
        Expect(MemberText.RequestProblem(null) == "That request couldn't be sent.", "an unknown failure says nothing more");
        Expect(MemberText.Answered("accept", true) == "Connected." && MemberText.Answered("accept", false) == MemberText.Answered("withdraw", false), "answers and their failure");
        var sanction = new SanctionNoticeView(Guid.NewGuid(), "restriction", "Reason", DateTimeOffset.UnixEpoch, null, true, true, true, true, CanAppeal: true, AppealStatus: null);
        Expect(MemberText.Appealable(new NoticeView(Guid.NewGuid(), "restriction", DateTimeOffset.UnixEpoch, false, sanction)), "a live action may be appealed");
        Expect(!MemberText.Appealable(new NoticeView(Guid.NewGuid(), "restriction", DateTimeOffset.UnixEpoch, false, sanction with { AppealStatus = "pending" })), "only once");
        Expect(!MemberText.Appealable(new NoticeView(Guid.NewGuid(), "sanctionLifted", DateTimeOffset.UnixEpoch, false, sanction)), "not from a lifted notice");
        Expect(!MemberText.Appealable(new NoticeView(Guid.NewGuid(), "restriction", DateTimeOffset.UnixEpoch, false, sanction with { CanAppeal = false })), "not after 30 days or when not allowed");
        Expect(!MemberText.Appealable(new NoticeView(Guid.NewGuid(), "notice", DateTimeOffset.UnixEpoch, false, null)), "a notice without an action has nothing to appeal");
        Expect(MemberText.NoticeTitle("warning") != MemberText.NoticeTitle("suspension") && MemberText.NoticeTitle("zzz") == "Notice", "notice headings");
        var view = new IdVerificationView("none", null, null, null, true, null, true, true, false);
        Expect(MemberText.VerificationState(view) == "You haven't verified your ID." && MemberText.VerificationState(view with { IdStatus = "verified" }).StartsWith("Your ID is verified"), "verification states");
        var panel = Read("src/Swoovr.Web.Client/Account/VerificationPanel.razor") + Read("src/Swoovr.Web.Client/Moderation/NoticesPanel.razor");
        Expect(panel.Contains("MemberText.VerificationState") && panel.Contains("MemberText.NoticeTitle") && panel.Contains("MemberText.Appealable"), "the web panels use the shared wording");
        // The app starts Didit with the "app" client kind only; the API picks the return address (D155).
        Expect(Read("src/Swoovr.Mobile/VerificationPage.cs").Contains("StartVerificationAsync(\"app\")"), "the app asks for the app return address");

        Console.WriteLine($"PASS: app parity checks ({checks}).");
    }

    static bool Throws(Action action) => Throws<ArgumentOutOfRangeException>(action);
    static bool Throws<T>(Action action) where T : Exception { try { action(); return false; } catch (T) { return true; } }

    static string Root()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Swoovr.sln"))) return dir.FullName;
        throw new Exception("App parity checks need the repository (Swoovr.sln not found above the checks).");
    }
}
