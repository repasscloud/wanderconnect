using Swoovr.Client;
using Swoovr.Domain;

/// <summary>
/// Cross-mode privacy (#37, D175) and the per-mode connection lifecycle (#38, D176): which modes a profile view shows,
/// the per-mode about, disconnecting without blocking, the 30-day wait for the member who was disconnected only, and the
/// labels each mode uses.
/// </summary>
static class ModePrivacyChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Mode privacy check failed: " + what); checks++; }
        var now = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
        IReadOnlyList<string> shared = ["dating", "friends", "business"];
        HashSet<string> none = [], friends = ["friends"];

        // D175: one mode at a time, plus connected modes; never the person's other modes.
        Expect(ModePrivacy.Shown("business", shared, none).SequenceEqual(["business"]), "opened from Business: only Business");
        Expect(ModePrivacy.Shown("business", shared, friends).SequenceEqual(["friends", "business"]), "plus the modes already connected, in order");
        Expect(!ModePrivacy.Shown("business", shared, friends).Contains("dating"), "a Business contact never sees Dating");
        Expect(ModePrivacy.Shown("activities", shared, friends).Count == 0, "an unshared requested mode reads as not found");
        Expect(ModePrivacy.Shown("made-up", shared, none).Count == 0, "an unknown mode reads as not found");
        Expect(ModePrivacy.Shown(null, shared, friends).SequenceEqual(["friends"]), "no mode given: the connected modes");
        Expect(ModePrivacy.Shown(null, shared, none).SequenceEqual(["dating"]), "no mode and no connection: only the first shared mode");
        Expect(ModePrivacy.Shown("friends", shared, new HashSet<string> { "business" }).SequenceEqual(["friends", "business"]), "a pending request's mode shows too (D182)");
        Expect(ModePrivacy.Shown(null, [], none).Count == 0, "nothing shared: nothing");
        Expect(ModePrivacy.About("Here for coffee chats", "Main about", null) == "Here for coffee chats", "the mode's own about wins");
        // #91 (policy §6): the core about crosses into a mode only with the member's consent.
        Expect(ModePrivacy.About(null, "Main about", true) == "Main about", "no mode about, consent given: the core about");
        Expect(ModePrivacy.About(null, "Main about", null) is null && ModePrivacy.About(null, "Main about", false) is null, "no consent: nothing crosses");
        Expect(ModePrivacy.About("Mode about", "Main about", false) == "Mode about", "the mode's own about never needs consent");

        // D176: disconnecting without blocking.
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), stranger = Guid.NewGuid();
        ConnectionRequest Accepted() => new() { Id = Guid.NewGuid(), FromAccountId = a, ToAccountId = b, ModeId = "friends", Status = ConnectionRequestStatus.Accepted, CreatedAt = now.AddDays(-5) };
        var link = Accepted();
        Expect(!ConnectionRequestPolicy.Disconnect(link, stranger, now) && link.Status == ConnectionRequestStatus.Accepted, "nobody else can disconnect");
        Expect(ConnectionRequestPolicy.Disconnect(link, b, now), "either member may disconnect (the recipient)");
        Expect(link.Status == ConnectionRequestStatus.Disconnected && link.EndedBy == b && link.EndedAt == now, "who and when are kept");
        Expect(!ConnectionRequestPolicy.Disconnect(link, a, now), "only an accepted connection can be disconnected");
        var other = Accepted();
        Expect(ConnectionRequestPolicy.Disconnect(other, a, now) && other.EndedBy == a, "the sender may disconnect too");
        var pending = Accepted(); pending.Status = ConnectionRequestStatus.Pending;
        Expect(!ConnectionRequestPolicy.Disconnect(pending, a, now), "a pending request is withdrawn or declined, not disconnected");
        Expect(Enum.GetNames<ConnectionRequestStatus>().Last() == "Disconnected", "a new stored name, appended");

        // D176: the one who was disconnected waits 30 days in that mode; the one who disconnected doesn't.
        RequestFacts Facts(DateTimeOffset? disconnectedBy) => new(false, false, false, true, false, false, null, 0, disconnectedBy);
        Expect(ConnectionRequestPolicy.CanSend(Facts(now.AddDays(-29)), now) == RequestRefusal.RecentlyDisconnected, "29 days after being disconnected: wait");
        Expect(ConnectionRequestPolicy.CanSend(Facts(now.AddDays(-30)), now) == RequestRefusal.None, "after 30 days: may ask again");
        Expect(ConnectionRequestPolicy.CanSend(Facts(null), now) == RequestRefusal.None, "the member who disconnected may ask any time");
        Expect(ConnectionRequestPolicy.CanSend(Facts(now) with { RecipientShownToSender = false }, now) == RequestRefusal.NotAvailable, "not available still wins, so nothing leaks");

        // Labels per mode (D176) and closed reasons (D143, D170, D176).
        Expect(MemberText.ConnectionLabel("dating") == "Match" && MemberText.ConnectionLabel("friends") == "Friend"
            && MemberText.ConnectionLabel("activities") == "Activity partner" && MemberText.ConnectionLabel("business") == "Contact", "the four labels");
        Expect(MemberText.ConnectionLabel("unknown") == "Connection", "a neutral fallback");
        Expect(new[] { ClosedReasons.Blocked, ClosedReasons.Disconnected, ClosedReasons.Deleted }.All(r => r.Length <= 16), "closed reasons fit the column");

        Console.WriteLine($"PASS: cross-mode privacy and connection lifecycle ({checks}).");
    }
}
