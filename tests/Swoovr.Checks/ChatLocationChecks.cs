using Swoovr.Client;
using Swoovr.Contracts;
using Swoovr.Domain;

/// <summary>
/// Issue 544 (D431, D455): sharing a location in a chat. The point is checked and kept to 5 decimal places in the message's
/// own text, so the text rules (delete for both, 30-day purge, holds) cover it; pasted places are read without any map
/// provider; the card's links carry the coordinates only.
/// </summary>
static class ChatLocationChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Chat location check failed: " + what); checks++; }

        // The point and the label.
        Expect(ChatLocationPolicy.Check(-33.8688, 151.2093, null) == ChatLocationRefusal.None, "a valid point");
        Expect(ChatLocationPolicy.Check(90, 180, "x") == ChatLocationRefusal.None && ChatLocationPolicy.Check(-90, -180, null) == ChatLocationRefusal.None, "the edges are valid");
        foreach (var (lat, lon) in new (double?, double?)[] { (null, 1), (1, null), (90.1, 0), (-90.1, 5), (5, 180.1), (5, -180.1), (double.NaN, 1), (1, double.PositiveInfinity), (0, 0), (0.000001, -0.000002) })
            Expect(ChatLocationPolicy.Check(lat, lon, null) == ChatLocationRefusal.InvalidPoint, $"refused: {lat},{lon}");
        Expect(ChatLocationPolicy.Check(1, 1, new string('a', 80)) == ChatLocationRefusal.None, "80-character label");
        Expect(ChatLocationPolicy.Check(1, 1, new string('a', 81)) == ChatLocationRefusal.InvalidLabel, "81 characters refused");
        Expect(ChatLocationPolicy.Check(1, 1, "two\nlines") == ChatLocationRefusal.InvalidLabel && ChatLocationPolicy.Check(1, 1, "tab\there") == ChatLocationRefusal.InvalidLabel, "one line, no control characters");
        Expect(ChatLocationPolicy.Check(1, 1, "   ") == ChatLocationRefusal.None && ChatLocationPolicy.CleanLabel("   ") is null, "a blank label is no label");

        // Stored in the message's text: geo line, then the label; read back exactly; rounded to 5 places.
        var body = ChatLocationPolicy.Format(-33.868812345, 151.209312345, "  Café entrance ");
        Expect(body == "geo:-33.86881,151.20931\nCafé entrance", "format: " + body);
        Expect(ChatLocationPolicy.Read(body) == new ChatLocation(-33.86881, 151.20931, "Café entrance"), "read back");
        Expect(ChatLocationPolicy.Format(1.5, -2.25, null) == "geo:1.5,-2.25" && ChatLocationPolicy.Read("geo:1.5,-2.25") == new ChatLocation(1.5, -2.25, null), "without a label");
        foreach (var bad in new[] { null, "", "hello", "geo:", "geo:1", "geo:a,b", "geo:91,1", "geo:0,0", "geo:1,1,1" })
            Expect(ChatLocationPolicy.Read(bad) is null, "not a location: " + bad);
        Expect(MessagingPolicy.CleanText(body) is not null, "the text passes the ordinary message text rule");

        // Message rules: it counts like a message, is deleted for both within five minutes, and its place is purged with the text.
        var now = DateTimeOffset.Parse("2026-11-28T12:00:00Z");
        var me = Guid.NewGuid();
        var message = new Message { Id = Guid.NewGuid(), SenderAccountId = me, Kind = MessageKind.Location, Body = body, SentAt = now.AddMinutes(-4) };
        Expect(MessagingPolicy.CountsAsUnanswered(MessageKind.Location), "counts towards the unanswered limit");
        Expect(MessagingPolicy.MayDeleteForBoth(message, me, now) && !MessagingPolicy.MayDeleteForBoth(message, Guid.NewGuid(), now), "the sender deletes it for both");
        Expect(!MessagingPolicy.MayDeleteForBoth(message, me, now.AddMinutes(2)), "within five minutes only");
        message.DeletedAt = now;
        Expect(MessagingPolicy.VisibleText(message) == "" && !MessagingPolicy.Purge(message, now.AddDays(29)) && MessagingPolicy.Purge(message, now.AddDays(31)), "deleted: hidden at once, purged after 30 days like text");

        // Pasted places (no provider is asked).
        (double, double)? P(string s) => ChatLocationText.Parse(s);
        Expect(P("-33.8688, 151.2093") == (-33.8688, 151.2093), "lat, lon");
        Expect(P("geo:-33.8688,151.2093") == (-33.8688, 151.2093), "geo: link");
        Expect(P("https://www.google.com/maps/place/Opera/@-33.8567844,151.2152967,17z/data=x") == (-33.8567844, 151.2152967), "Google Maps @");
        Expect(P("https://www.google.com/maps/search/?api=1&query=-33.85,151.21") == (-33.85, 151.21), "Google Maps query=");
        Expect(P("https://maps.apple.com/?ll=-33.85%2C151.21&q=Opera") == (-33.85, 151.21), "Apple Maps ll= (escaped comma)");
        Expect(P("https://www.openstreetmap.org/?mlat=-33.85&mlon=151.21#map=17/-33.85/151.21") == (-33.85, 151.21), "OpenStreetMap mlat/mlon");
        Expect(P("https://www.openstreetmap.org/#map=16/-34.9285/138.6007") == (-34.9285, 138.6007), "OpenStreetMap #map");
        foreach (var bad in new[] { "", "   ", "Sydney Opera House", "95, 10", "10, 190", "1234567", new string('1', 3000) })
            Expect(P(bad) is null, "no place: " + bad[..Math.Min(bad.Length, 20)]);

        // The card's links: coordinates only, never the label.
        var place = new ChatLocationView(-33.86881, 151.20931, "Café entrance");
        foreach (var link in new[] { ChatLocationText.AppleMaps(place), ChatLocationText.GoogleMaps(place), ChatLocationText.OpenStreetMap(place) })
            Expect(link.StartsWith("https://", StringComparison.Ordinal) && link.Contains("-33.86881") && link.Contains("151.20931") && !link.Contains("Caf"), "link: " + link);
        Expect(ChatLocationText.Title(place) == "Café entrance" && ChatLocationText.Title(place with { Label = null }) == "Shared location", "title");
        Expect(ChatLocationText.LabelMax == ChatLocationPolicy.LabelMax, "the web's label limit is the API's");

        Console.WriteLine($"PASS: chat location (#544, D455) ({checks}).");
    }
}
