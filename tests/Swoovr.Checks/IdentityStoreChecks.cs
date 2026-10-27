using System.Text.Json;
using Swoovr.Domain;
using Swoovr.Infrastructure.Verification;

/// <summary>
/// The verified identity store (policy §2; owner Q78, D191): the fields read from an approved Didit decision and nothing
/// else, sealing, ban-matching keys, who may decrypt, and retention at deletion.
/// </summary>
static class IdentityStoreChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Identity store check failed: " + what); checks++; }
        var now = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

        // Read from a fictional decision: the approved document check's minimum fields; never the number or address.
        using var decision = JsonDocument.Parse("""
            { "session_id": "11111111-1111-1111-1111-111111111111", "status": "Approved", "vendor_data": "x",
              "id_verifications": [
                { "status": "Declined", "first_name": "Wrong", "last_name": "Person", "date_of_birth": "1970-01-01" },
                { "status": "Approved", "document_type": "Passport", "document_number": "FICT12345", "first_name": "Zoë", "last_name": "Fictional-Smith",
                  "date_of_birth": "1990-02-03", "expiration_date": "2031-04-05", "issuing_state": "AUS", "address": "1 Fictional St", "verification_method": "document" } ] }
            """);
        var session = DiditClient.Read(decision.RootElement)!;
        var fields = session.Identity!;
        Expect(fields.FirstName == "Zoë" && fields.LastName == "Fictional-Smith", "the approved check's names");
        Expect(fields.DateOfBirth == new DateOnly(1990, 2, 3) && fields.ExpiryDate == new DateOnly(2031, 4, 5), "dates");
        Expect(fields.DocumentType == "Passport" && fields.IssuingCountry == "AUS" && fields.Method == "document", "document type, country and method");
        var serialised = JsonSerializer.Serialize(fields);
        Expect(!serialised.Contains("FICT12345") && !serialised.Contains("Fictional St"), "never the document number or address");
        using (var none = JsonDocument.Parse("""{ "session_id": "s", "status": "Declined", "id_verifications": [ { "status": "Declined", "first_name": "A" } ] }"""))
            Expect(DiditClient.Read(none.RootElement)!.Identity is null, "nothing without an approved check");

        // Ban matching keys: folded names and the date; nothing without both.
        Expect(VerifiedIdentityPolicy.MatchKey(fields) == "zoe|fictionalsmith|1990-02-03", "accents, case and punctuation folded");
        Expect(VerifiedIdentityPolicy.MatchKey(fields with { FirstName = "ZOE", LastName = "fictional smith" }) == VerifiedIdentityPolicy.MatchKey(fields), "the same person written differently matches");
        Expect(VerifiedIdentityPolicy.MatchKey(fields with { DateOfBirth = null }) is null && VerifiedIdentityPolicy.MatchKey(fields with { LastName = " " }) is null, "no key without a full name and date");
        var hmacKey = new byte[32]; hmacKey[0] = 5;
        Expect(!VerifiedIdentityPolicy.Match(hmacKey, fields)!.SequenceEqual(VerifiedIdentityPolicy.Match(new byte[32], fields)!), "keyed: another key, another value");
        Expect(!VerifiedIdentityPolicy.Match(hmacKey, fields)!.SequenceEqual(VerifiedIdentityPolicy.Match(hmacKey, fields with { DateOfBirth = new DateOnly(1990, 2, 4) })!), "a different date doesn't match");

        // Sealed record.
        var key = new byte[32]; key[7] = 3;
        var attempt = new IdVerificationAttempt { Id = Guid.NewGuid(), SessionId = "11111111-1111-1111-1111-111111111111" };
        var record = VerifiedIdentityPolicy.Record(Guid.NewGuid(), attempt, fields, now, 1, key, hmacKey);
        Expect(!System.Text.Encoding.UTF8.GetString(record.Sealed).Contains("Fictional"), "fields never in clear");
        Expect(record.IssuingCountry == "AUS" && record.SessionId == attempt.SessionId && record.RetainUntil is null, "country, session and no end date while active");
        Expect(VerifiedIdentityPolicy.Open(new Dictionary<byte, byte[]> { [1] = key }, record.Sealed) == fields, "opens to the same fields");
        var wrong = false;
        try { VerifiedIdentityPolicy.Open(new Dictionary<byte, byte[]> { [1] = hmacKey }, record.Sealed); } catch (System.Security.Cryptography.CryptographicException) { wrong = true; }
        Expect(wrong, "the wrong key reads nothing");

        // Who decrypts; retention.
        Expect(VerifiedIdentityPolicy.MayDecrypt(AccountRole.Legal, "Fictional police request REF-1"), "legal with a purpose");
        Expect(!VerifiedIdentityPolicy.MayDecrypt(AccountRole.Legal, " ") && !VerifiedIdentityPolicy.MayDecrypt(AccountRole.Legal, new string('x', 501)), "a purpose is required and bounded");
        foreach (var role in new[] { AccountRole.Admin, AccountRole.Support, AccountRole.Member, AccountRole.Unknown })
            Expect(!VerifiedIdentityPolicy.MayDecrypt(role, "purpose"), $"{role} never decrypts");
        Expect(VerifiedIdentityPolicy.RetainAfterDeletion(terminated: false, now) is null, "an ordinary deletion erases it");
        Expect(VerifiedIdentityPolicy.RetainAfterDeletion(terminated: true, now) == now.AddDays(730), "a terminated member's is kept two years");

        Console.WriteLine($"PASS: verified identity store ({checks}).");
    }
}
