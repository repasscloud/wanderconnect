using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Swoovr.Client;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Messaging;

/// <summary>
/// Mobile number verification (Q45, D148, D153, D154; details D181): numbers, codes (six digits, five minutes, five wrong
/// tries, single use, bound to account and number), limits and cooldown, and the senders: Twilio's request shape against a
/// fake handler (nothing leaves the machine, D174) and the simulated sender, which never claims delivery outside the local sink.
/// </summary>
static class PhoneChecks
{
    public static async Task Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Phone check failed: " + what); checks++; }
        var now = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

        // Numbers.
        Expect(PhoneVerificationPolicy.Normalise("0412 345 678") == "+61412345678", "an Australian mobile written locally");
        Expect(PhoneVerificationPolicy.Normalise("+61 (412) 345-678") == "+61412345678", "international form with punctuation");
        Expect(PhoneVerificationPolicy.Normalise("+1 555 010 9999") == "+15550109999", "another country");
        foreach (var bad in new[] { null, "", "12345", "0312345678", "+0412345678", "+1234567", "+1234567890123456", "0412 345 67x", "04123456789", "<script>" })
            Expect(PhoneVerificationPolicy.Normalise(bad) is null, "refused: " + bad);
        Expect(PhoneVerificationPolicy.Masked("+61412345678") == "+61••••••678", "masked to the country code and last three digits");

        // Codes.
        var codes = Enumerable.Range(0, 200).Select(_ => PhoneVerificationPolicy.NewCode()).ToList();
        Expect(codes.All(c => c.Length == 6 && c.All(char.IsAsciiDigit)), "six digits, leading zeros kept");
        Expect(codes.Distinct().Count() > 150, "random");
        var key = new byte[32]; key[0] = 7;
        Guid me = Guid.NewGuid(), other = Guid.NewGuid();
        const string number = "+61412345678";
        PhoneCode Sent(string code) => new() { Id = Guid.NewGuid(), AccountId = me, Number = number, CodeHash = PhoneVerificationPolicy.Hash(key, me, number, code), CreatedAt = now, ExpiresAt = now + PhoneVerificationPolicy.CodeLifetime };
        Expect(PhoneVerificationPolicy.Hash(key, me, number, "123456").SequenceEqual(PhoneVerificationPolicy.Hash(key, me, number, "123456")), "the hash is stable");
        Expect(!PhoneVerificationPolicy.Hash(key, me, number, "123456").SequenceEqual(PhoneVerificationPolicy.Hash(key, other, number, "123456")), "bound to the account");
        Expect(!PhoneVerificationPolicy.Hash(key, me, number, "123456").SequenceEqual(PhoneVerificationPolicy.Hash(key, me, "+61400000000", "123456")), "bound to the number");
        var ok = Sent("042137");
        Expect(PhoneVerificationPolicy.Check(ok, key, me, number, " 042137 ", now.AddMinutes(4)) == PhoneCheckResult.Verified && ok.UsedAt is not null, "the right code within five minutes");
        Expect(PhoneVerificationPolicy.Check(ok, key, me, number, "042137", now.AddMinutes(4)) == PhoneCheckResult.NoCode, "single use");
        Expect(PhoneVerificationPolicy.Check(Sent("042137"), key, me, number, "042137", now.AddMinutes(5)) == PhoneCheckResult.Expired, "expired at five minutes");
        Expect(PhoneVerificationPolicy.Check(Sent("042137"), key, other, number, "042137", now) == PhoneCheckResult.Wrong, "another account's code is wrong for this one");
        Expect(PhoneVerificationPolicy.Check(null, key, me, number, "042137", now) == PhoneCheckResult.NoCode, "nothing sent");
        var tries = Sent("042137");
        for (var i = 1; i <= 4; i++) Expect(PhoneVerificationPolicy.Check(tries, key, me, number, "000000", now) == PhoneCheckResult.Wrong, $"wrong try {i}");
        Expect(PhoneVerificationPolicy.Check(tries, key, me, number, "000000", now) == PhoneCheckResult.TooManyAttempts, "the fifth wrong try ends the code");
        Expect(PhoneVerificationPolicy.Check(tries, key, me, number, "042137", now) == PhoneCheckResult.TooManyAttempts, "even the right code no longer works");
        foreach (var junk in new[] { null, "", "12345", "1234567", "12a456", "１２３４５６" })
            Expect(PhoneVerificationPolicy.Check(Sent("042137"), key, me, number, junk, now) == PhoneCheckResult.Wrong, "malformed: " + junk);
        Expect(!typeof(PhoneCode).GetProperties().Any(p => p.Name == "Code" || p.Name == "IpAddress"), "no code or address stored in clear");
        Expect(!typeof(MemberProfile).GetProperties().Any(p => p.Name.Contains("Phone")), "nothing on the profile (D148)");

        // Limits and cooldown.
        PhoneSendFacts F(DateTimeOffset? last = null, int account = 0, int toNumber = 0, int address = 0, bool verified = false) => new(last, account, toNumber, address, verified);
        Expect(PhoneVerificationPolicy.CanSend(F(), now) == PhoneSendRefusal.None, "the first code");
        Expect(PhoneVerificationPolicy.CanSend(F(last: now.AddSeconds(-59)), now) == PhoneSendRefusal.Cooldown, "a minute between codes");
        Expect(PhoneVerificationPolicy.CanSend(F(last: now.AddSeconds(-60)), now) == PhoneSendRefusal.None, "after a minute");
        Expect(PhoneVerificationPolicy.CanSend(F(account: 5), now) == PhoneSendRefusal.AccountLimit, "five an hour per account");
        Expect(PhoneVerificationPolicy.CanSend(F(toNumber: 5), now) == PhoneSendRefusal.NumberLimit, "five an hour per number");
        Expect(PhoneVerificationPolicy.CanSend(F(address: 10), now) == PhoneSendRefusal.AddressLimit, "ten an hour per address");
        Expect(PhoneVerificationPolicy.CanSend(F(verified: true), now) == PhoneSendRefusal.AlreadyVerified, "the same number isn't verified twice");

        // Twilio's request, against a fake handler: nothing is sent anywhere.
        var fake = new FakeHandler(HttpStatusCode.Created);
        var options = new TwilioOptions("ACfictional0000", "fictional-token", "+15005550006", new Uri("https://twilio.invalid/"));
        var twilio = new TwilioMessageSender(new HttpClient(fake), options, NullLogger<TwilioMessageSender>.Instance);
        Expect(twilio.Kind == "twilio" && await twilio.SendCodeAsync("+61412345678", "042137", default), "201 means queued");
        Expect(fake.Last!.Method == HttpMethod.Post && fake.Last.RequestUri!.ToString() == "https://twilio.invalid/2010-04-01/Accounts/ACfictional0000/Messages.json", "Programmable Messaging, not Verify");
        Expect(fake.Last.Headers.Authorization is { Scheme: "Basic" } auth && System.Text.Encoding.ASCII.GetString(Convert.FromBase64String(auth.Parameter!)) == "ACfictional0000:fictional-token", "basic auth with the SID and token");
        Expect(fake.Body.Contains("To=%2B61412345678") && fake.Body.Contains("From=%2B15005550006") && fake.Body.Contains("042137"), "To, From and the code in the body");
        Expect(!await new TwilioMessageSender(new HttpClient(new FakeHandler(HttpStatusCode.BadRequest)), options, NullLogger<TwilioMessageSender>.Instance).SendCodeAsync(number, "042137", default), "a refusal is not delivery");
        Expect(!await new TwilioMessageSender(new HttpClient(fake), options with { AuthToken = null }, NullLogger<TwilioMessageSender>.Instance).SendCodeAsync(number, "042137", default), "unconfigured sends nothing");

        // The simulated sender (D174): only into the local sink, always labelled.
        var sink = new SinkEmail();
        Expect(await new SimulatedMessageSender(sink, localSink: true).SendCodeAsync(number, "042137", default), "into the local sink");
        Expect(sink.Sent!.To == "sms-61412345678@simulated.swoovr.invalid" && sink.Sent.Subject.StartsWith("SIMULATED") && sink.Sent.Text.StartsWith("SIMULATED"), "labelled simulated");
        sink.Sent = null;
        Expect(!await new SimulatedMessageSender(sink, localSink: false).SendCodeAsync(number, "042137", default) && sink.Sent is null, "anywhere else: nothing, and never claimed");
        Expect(MemberText.PhoneProblem(ProblemCodes.PhoneInUse) == "That number can't be used. Try another.", "an in-use number says nothing about whose");

        // Contact history (policy §4, D187): encrypted value, keyed lookup, versioned key, tamper-evident.
        var encKey = new byte[32]; encKey[1] = 9;
        var record = ContactHistoryPolicy.Record(me, ContactType.Mobile, number, now.AddDays(-40), now, me, "changed", 1, encKey, key);
        Expect(!System.Text.Encoding.UTF8.GetString(record.EncryptedValue).Contains("412345678"), "never stored in clear");
        Expect(ContactHistoryPolicy.Decrypt(new Dictionary<byte, byte[]> { [1] = encKey }, record.EncryptedValue) == number, "decrypts with the right key");
        Expect(record.RetainUntil == now + TimeSpan.FromDays(730) && record.ValidUntil == now, "kept two years from the change");
        Expect(record.LookupHmac.SequenceEqual(ContactHistoryPolicy.Lookup(key, ContactType.Mobile, number)), "found by exact keyed lookup");
        Expect(!record.LookupHmac.SequenceEqual(ContactHistoryPolicy.Lookup(key, ContactType.Email, number)), "lookups are per contact type");
        Expect(ContactHistoryPolicy.Lookup(key, ContactType.Email, " Someone@Example.TEST ").SequenceEqual(ContactHistoryPolicy.Lookup(key, ContactType.Email, "someone@example.test")), "emails compared normalised");
        var altered = (byte[])record.EncryptedValue.Clone(); altered[^1] ^= 1;
        var tampered = false;
        try { ContactHistoryPolicy.Decrypt(new Dictionary<byte, byte[]> { [1] = encKey }, altered); } catch (System.Security.Cryptography.CryptographicException) { tampered = true; }
        Expect(tampered, "a changed value fails authentication");
        var wrongKey = false;
        try { ContactHistoryPolicy.Decrypt(new Dictionary<byte, byte[]> { [1] = key }, record.EncryptedValue); } catch (System.Security.Cryptography.CryptographicException) { wrongKey = true; }
        Expect(wrongKey, "the wrong key reads nothing");
        Expect(!ContactHistoryPolicy.Encrypt(1, encKey, number).SequenceEqual(ContactHistoryPolicy.Encrypt(1, encKey, number)), "a fresh nonce each time");

        var claimAt=now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        Expect(!EmailClaimPolicy.CanAdopt(claimAt,null,now),"unknown acceptance clock refuses contact adoption");
        Expect(EmailClaimPolicy.CanAdopt(claimAt,now.AddSeconds(-1),now),"newer email evidence can advance accepted contact");
        Expect(!EmailClaimPolicy.CanAdopt(claimAt,now,now),"same-second ambiguous claim cannot reverse a change");
        Expect(!EmailClaimPolicy.CanAdopt(claimAt,now.AddSeconds(1),now),"older claim cannot roll contact back");
        foreach(var invalid in new string?[]{null,"bad","-1","999999999999999999999",now.AddSeconds(6).ToUnixTimeSeconds().ToString()})
            Expect(!EmailClaimPolicy.CanAdopt(invalid,now.AddDays(-1),now),"unknown/future email issuance cannot adopt a changed contact");
        var skewedNew=now.AddSeconds(2).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var skewedOld=now.AddSeconds(1).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var skewedAcceptance=EmailClaimPolicy.AcceptanceClock(skewedNew,now);
        Expect(skewedAcceptance==now.AddSeconds(2),"accepted clock retains tolerated issuer-ahead evidence");
        Expect(!EmailClaimPolicy.CanAdopt(skewedOld,skewedAcceptance,now),"older tolerated-skew token cannot reverse newer acceptance");
        Expect(EmailClaimPolicy.AcceptanceClock(claimAt,now)==now,"server acceptance time is the minimum watermark");
        Expect(EmailClaimPolicy.AcceptanceClock(null,now)==now.AddSeconds(5),"unknown legacy issuance uses conservative skew boundary");
        Console.WriteLine($"PASS: mobile number verification ({checks}).");
    }

    private sealed class FakeHandler(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Last;
        public string Body = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Last = request;
            Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status){Content=new StringContent("{\"sid\":\"SM"+new string('a',32)+"\",\"status\":\"queued\"}",System.Text.Encoding.UTF8,"application/json")};
        }
    }

    private sealed class SinkEmail : IEmailSender
    {
        public EmailMessage? Sent;
        public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken) { Sent = message; return Task.FromResult(true); }
    }
}
