using System.Security.Cryptography;
using Swoovr.Domain;

static class MeetingSafetyContactAccessChecks
{
    public static void Run()
    {
        var checks = 0;
        void Check(bool condition) { if (!condition) throw new Exception("Meeting safety contact access policy failed."); checks++; }
        var now = new DateTimeOffset(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);
        var key = RandomNumberGenerator.GetBytes(32);
        var recipient = Guid.NewGuid();
        MeetingSafetyContactChallenge New(string code)
        {
            var row = new MeetingSafetyContactChallenge { Id = Guid.NewGuid(), RecipientId = recipient, CreatedAt = now, ExpiresAt = now.AddMinutes(5) };
            row.CodeHash = MeetingSafetyContactAccessPolicy.CodeHash(key, row.RecipientId, row.Id, code);
            return row;
        }
        Check(MeetingSafetyContactAccessPolicy.CodeShape(MeetingSafetyContactAccessPolicy.Code()));
        Check(!MeetingSafetyContactAccessPolicy.CodeShape("１２３４５６"));
        Check(!MeetingSafetyContactAccessPolicy.CodeShape(" 123456"));
        var valid = New("000001");
        Check(valid.CodeHash.Length == 32);
        Check(!CryptographicOperations.FixedTimeEquals(valid.CodeHash, MeetingSafetyContactAccessPolicy.CodeHash(key, Guid.NewGuid(), valid.Id, "000001")));
        Check(!CryptographicOperations.FixedTimeEquals(valid.CodeHash, MeetingSafetyContactAccessPolicy.CodeHash(key, recipient, Guid.NewGuid(), "000001")));
        Check(MeetingSafetyContactAccessPolicy.Check(valid, key, "000001", now));
        Check(valid.UsedAt == now);
        Check(!MeetingSafetyContactAccessPolicy.Check(valid, key, "000001", now));
        var expired = New("123456");
        Check(!MeetingSafetyContactAccessPolicy.Check(expired, key, "123456", expired.ExpiresAt));
        var replaced = New("123456"); replaced.SupersededAt = now;
        Check(!MeetingSafetyContactAccessPolicy.Check(replaced, key, "123456", now));
        var guesses = New("123456");
        for (var i = 0; i < 5; i++) Check(!MeetingSafetyContactAccessPolicy.Check(guesses, key, i == 0 ? null : "654321", now));
        Check(guesses.FailedAttempts == 5);
        Check(!MeetingSafetyContactAccessPolicy.Check(guesses, key, "123456", now));
        Check(guesses.UsedAt == null);
        var email = MeetingSafetyContactAccessPolicy.DestinationHash(key, "email", "fictional@example.test");
        Check(email.Length == 32);
        Check(!CryptographicOperations.FixedTimeEquals(email, MeetingSafetyContactAccessPolicy.DestinationHash(key, "sms", "fictional@example.test")));
        Console.WriteLine($"PASS: meeting safety contact access policy ({checks} checks; no persistence/provider proof).");
    }
}
