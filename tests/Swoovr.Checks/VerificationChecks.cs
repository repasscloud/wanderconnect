using System.Text;
using System.Text.Json;
using Swoovr.Domain;
using Swoovr.Infrastructure.Verification;

/// <summary>
/// Slice P rules: Didit status mapping that only moves forward (D128, D129), one self-service attempt, the age-assurance
/// gate (D127), founder eligibility and allocation order (D130), and Didit webhook authentication (D152): signature
/// over the raw body first (canonical V2 checked against Didit's documented Python rules), then the signed timestamp.
/// </summary>
static class VerificationChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Verification check failed: " + what); checks++; }

        // Status mapping (Didit's statuses, case-insensitive; both spellings of KYC Expired).
        Expect(IdVerificationPolicy.Map("Approved") == IdAttemptState.Approved && IdVerificationPolicy.Map("approved") == IdAttemptState.Approved, "approved either case");
        Expect(IdVerificationPolicy.Map("Kyc Expired") == IdAttemptState.Expired && IdVerificationPolicy.Map("KYC Expired") == IdAttemptState.Expired, "kyc expired");
        Expect(IdVerificationPolicy.Map("Awaiting User") == IdAttemptState.InProgress, "awaiting user is in progress");
        Expect(IdVerificationPolicy.Map("Bogus") is null && IdVerificationPolicy.Map(null) is null, "unknown statuses ignored");

        // Forward only; settled attempts never change; Resubmitted reopens only a review.
        Expect(IdVerificationPolicy.Next(IdAttemptState.Created, "In Progress") == IdAttemptState.InProgress, "created to in progress");
        Expect(IdVerificationPolicy.Next(IdAttemptState.InProgress, "Approved") == IdAttemptState.Approved, "in progress to approved");
        Expect(IdVerificationPolicy.Next(IdAttemptState.Created, "Approved") == IdAttemptState.Approved, "a skipped step is fine");
        Expect(IdVerificationPolicy.Next(IdAttemptState.InReview, "Not Started") is null, "a stale re-read can't move back");
        Expect(IdVerificationPolicy.Next(IdAttemptState.Approved, "Declined") is null, "approved stays approved");
        Expect(IdVerificationPolicy.Next(IdAttemptState.Declined, "Approved") is null, "a replayed approval can't overturn a decline");
        Expect(IdVerificationPolicy.Next(IdAttemptState.InReview, "Resubmitted") == IdAttemptState.InProgress, "resubmitted reopens a review");
        Expect(IdVerificationPolicy.Next(IdAttemptState.Approved, "Resubmitted") is null, "resubmitted can't reopen a settled check");
        Expect(IdVerificationPolicy.Next(IdAttemptState.InProgress, "In Progress") is null, "same state is a no-op");

        // One self-service attempt (D128, D129).
        var fresh = new MemberProfile();
        Expect(fresh.CanRequestIdVerification, "a member starts with one attempt");
        Expect(IdVerificationPolicy.CanStart(fresh, []) == IdStartRefusal.None, "first attempt allowed");
        Expect(IdVerificationPolicy.CanStart(new MemberProfile { CanRequestIdVerification = false }, []) == IdStartRefusal.AttemptUsed, "second refused");
        Expect(IdVerificationPolicy.CanStart(fresh, [new IdVerificationAttempt { State = IdAttemptState.InReview }]) == IdStartRefusal.InProgress, "an open attempt is shown, not repeated");
        Expect(IdVerificationPolicy.CanStart(new MemberProfile { IdStatus = VerificationStatus.Verified }, []) == IdStartRefusal.AlreadyVerified, "verified members don't re-check");
        Expect(IdVerificationPolicy.CanStart(new MemberProfile { Bot = true }, []) == IdStartRefusal.Bot, "bots never get an ID badge");
        Expect(IdVerificationPolicy.ReturnsAttempt(new IdVerificationAttempt { State = IdAttemptState.Expired, Started = false }), "an unused expired session isn't an attempt");
        Expect(!IdVerificationPolicy.ReturnsAttempt(new IdVerificationAttempt { State = IdAttemptState.Expired, Started = true }), "an expired real attempt counts");
        Expect(!IdVerificationPolicy.ReturnsAttempt(new IdVerificationAttempt { State = IdAttemptState.Declined, Started = true }), "a decline counts");
        Expect(IdVerificationPolicy.StatusFor(IdAttemptState.Approved) == VerificationStatus.Verified && IdVerificationPolicy.StatusFor(IdAttemptState.InReview) == VerificationStatus.Pending, "public status");
        Expect(BadgePolicy.For(new MemberProfile { IdStatus = VerificationStatus.Pending }).Count == 0, "a pending check shows no badge");

        // Age after approval and the gate (D127).
        Expect(IdVerificationPolicy.AgeAfterApproval(AgeStatus.NeedsVerification, true) == AgeStatus.Verified, "an adult result clears the flag");
        Expect(IdVerificationPolicy.AgeAfterApproval(AgeStatus.Attested, false) == AgeStatus.Underage, "an under-18 document locks");
        Expect(IdVerificationPolicy.AgeAfterApproval(AgeStatus.NeedsVerification, null) == AgeStatus.NeedsVerification, "no age result keeps the flag");
        Expect(AgeAssurance.Blocks(AgeStatus.NeedsVerification) && !AgeAssurance.Blocks(AgeStatus.Verified), "the gate");
        Expect(AgeAssurance.Inherits([AgeStatus.Attested, AgeStatus.NeedsVerification]) && AgeAssurance.Inherits([AgeStatus.Underage]), "a flag follows the email");
        Expect(!AgeAssurance.Inherits([AgeStatus.Attested, AgeStatus.Verified]) && !AgeAssurance.Inherits([]), "nothing to inherit");

        // Adult from a decision: age, else the date of birth, never returned; any under-18 approved document decides.
        JsonElement Decision(string json) => JsonDocument.Parse(json).RootElement;
        Expect(DiditClient.Adult(Decision("""{"id_verifications":[{"status":"Approved","age":30}]}""")) == true, "adult by age");
        Expect(DiditClient.Adult(Decision("""{"id_verifications":[{"status":"Approved","date_of_birth":"2015-01-01"}]}""")) == false, "minor by date of birth");
        Expect(DiditClient.Adult(Decision("""{"id_verifications":[{"status":"Declined","age":30}]}""")) is null, "declined documents don't count");
        Expect(DiditClient.Adult(Decision("""{"id_verifications":[{"status":"Approved","age":30},{"status":"Approved","age":16}]}""")) == false, "the safer answer wins");
        Expect(DiditClient.Adult(Decision("""{}""")) is null, "no document check");
        var read = DiditClient.Read(Decision("""{"session_id":"s","status":"Approved","vendor_data":"v","workflow_id":"w","decision":{"id_verifications":[{"status":"Approved","age":40,"document_number":"X"}]}}"""))!;
        Expect(read is { SessionId: "s", VendorData: "v", WorkflowId: "w", Adult: true }, "webhook envelope read");
        // Q53 (D158): the document's date of birth is compared with the profile's; only an exact match verifies it.
        Expect(DiditClient.DateOfBirth(Decision("""{"id_verifications":[{"status":"Declined","date_of_birth":"1990-01-01"},{"status":"Approved","date_of_birth":"1991-02-03"}]}""")) == new DateOnly(1991, 2, 3), "date from the approved document");
        Expect(DiditClient.DateOfBirth(Decision("""{"id_verifications":[{"status":"Approved","date_of_birth":"03/02/1991"}]}""")) is null, "only ISO dates");
        Expect(IdVerificationPolicy.DateOfBirthMatches(new DateOnly(1991, 2, 3), new DateOnly(1991, 2, 3)), "same date verifies");
        Expect(!IdVerificationPolicy.DateOfBirthMatches(new DateOnly(1991, 2, 3), new DateOnly(1991, 3, 2)), "a different date doesn't");
        Expect(!IdVerificationPolicy.DateOfBirthMatches(null, new DateOnly(1991, 2, 3)) && !IdVerificationPolicy.DateOfBirthMatches(new DateOnly(1991, 2, 3), null), "missing dates don't");

        // Founders (D130).
        var eligible = new MemberProfile { DisplayName = "A", HomeGeoNameId = 1, IdStatus = VerificationStatus.Verified, AgeStatus = AgeStatus.Verified };
        Expect(FounderEligibility.Meets(eligible, AccountStatus.Active), "profile set up and ID verified");
        Expect(FounderEligibility.Meets(new MemberProfile { DisplayName = "A", HomeGeoNameId = 1, PhotoStatus = VerificationStatus.Verified }, AccountStatus.Active), "photo verified also counts");
        Expect(!FounderEligibility.Meets(new MemberProfile { DisplayName = "A", HomeGeoNameId = 1 }, AccountStatus.Active), "no verification");
        Expect(!FounderEligibility.Meets(new MemberProfile { DisplayName = "A", IdStatus = VerificationStatus.Verified }, AccountStatus.Active), "no home city");
        Expect(!FounderEligibility.Meets(eligible, AccountStatus.Locked), "locked accounts");
        Expect(!FounderEligibility.Meets(new MemberProfile { DisplayName = "A", HomeGeoNameId = 1, IdStatus = VerificationStatus.Verified, Bot = true }, AccountStatus.Active), "bots");
        var t0 = DateTimeOffset.Parse("2026-10-25T00:00:00Z");
        var stamp = new MemberProfile { DisplayName = "A", HomeGeoNameId = 1, IdStatus = VerificationStatus.Verified };
        Expect(FounderEligibility.Stamp(stamp, AccountStatus.Active, t0) && !FounderEligibility.Stamp(stamp, AccountStatus.Active, t0.AddDays(1)) && stamp.FounderEligibleAt == t0, "eligibility time never moves");
        Guid G(int n) => new($"00000000-0000-0000-0000-{n:000000000000}");
        var queue = new[] { (G(3), t0.AddDays(2), true, false), (G(1), t0, true, false), (G(2), t0.AddDays(1), false, false), (G(4), t0, true, true), (G(5), t0.AddDays(3), true, false) };
        Expect(FounderEligibility.Allocate(queue, 0).SequenceEqual([G(1), G(3), G(5)]), "eligibility order, skipping founders and lapsed members");
        Expect(FounderEligibility.Allocate(queue, 998).SequenceEqual([G(1), G(3)]), "never past 1,000");
        Expect(FounderEligibility.Allocate(queue, 1000).Count == 0 && FounderEligibility.Allocate(queue, 1200).Count == 0, "stops when full");

        // Webhook signatures (D152).
        const string secret = "test-secret-not-real";
        var body = """{"status":"Approved","webhook_type":"status.updated","event_id":"6f1c2c1e-0000-4000-8000-000000000001","session_id":"11111111-2222-4333-8444-555555555555","timestamp":1793000000,"vendor_data":"acc","metadata":{"z":1.0,"a":[3,1.5,"été ☃ \"q\" /\\\\ \\n"],"e":1e-05,"big":1e16},"decision":null}""";
        const string python = "447b2a171b28bb63e5f11eccb211c3b7362544b8e67d887e098baf27801dc09f"; // Didit's Python rules, computed 25 October 2026
        Expect(DiditSignature.SignV2(body, secret) == python, "canonical V2 matches Didit's Python reference");
        var raw = Encoding.UTF8.GetBytes(body);
        var at = DateTimeOffset.FromUnixTimeSeconds(1793000000);
        Expect(DiditSignature.Check(raw, python, null, secret, at.AddSeconds(120), out var ok) == DiditWebhookCheck.Valid && ok is not null, "a valid V2 signature");
        var rawSig = Convert.ToHexStringLower(System.Security.Cryptography.HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), raw));
        Expect(DiditSignature.Check(raw, null, rawSig, secret, at, out _) == DiditWebhookCheck.Valid, "a valid raw-body signature");
        Expect(DiditSignature.Check(raw, python.Replace('4', '5'), null, secret, at, out _) == DiditWebhookCheck.BadSignature, "a forged signature");
        Expect(DiditSignature.Check(raw, null, null, secret, at, out _) == DiditWebhookCheck.BadSignature, "no signature");
        Expect(DiditSignature.Check(raw, python, null, "another-secret", at, out _) == DiditWebhookCheck.BadSignature, "the wrong secret");
        Expect(DiditSignature.Check(raw, python, null, "", at, out _) == DiditWebhookCheck.BadSignature, "no secret configured fails closed");
        Expect(DiditSignature.Check(raw, "not-hex", null, secret, at, out _) == DiditWebhookCheck.BadSignature, "a malformed signature");
        var tampered = Encoding.UTF8.GetBytes(body.Replace("\"acc\"", "\"other\""));
        Expect(DiditSignature.Check(tampered, python, null, secret, at, out _) == DiditWebhookCheck.BadSignature, "a tampered vendor_data");
        Expect(DiditSignature.Check(raw, python, null, secret, at.AddSeconds(301), out _) == DiditWebhookCheck.Stale, "a stale signed timestamp");
        Expect(DiditSignature.Check(raw, python, null, secret, at.AddSeconds(-301), out _) == DiditWebhookCheck.Stale, "a timestamp from the future");
        Expect(DiditSignature.Check(Encoding.UTF8.GetBytes("not json"), python, null, secret, at, out _) == DiditWebhookCheck.BadSignature, "garbage");
        var noStamp = """{"event_id":"x"}""";
        Expect(DiditSignature.Check(Encoding.UTF8.GetBytes(noStamp), DiditSignature.SignV2(noStamp, secret), null, secret, at, out _) == DiditWebhookCheck.Malformed, "signed but no timestamp");
        // Key order and whitespace don't matter to V2; values do.
        var reordered = """{ "timestamp": 1793000000, "event_id": "6f1c2c1e-0000-4000-8000-000000000001", "decision": null, "metadata": {"big": 1e16, "e": 1e-05, "a": [3, 1.5, "été ☃ \"q\" /\\\\ \\n"], "z": 1}, "session_id": "11111111-2222-4333-8444-555555555555", "status": "Approved", "vendor_data": "acc", "webhook_type": "status.updated" }""";
        Expect(DiditSignature.SignV2(reordered, secret) == python, "re-ordered keys and spacing sign the same");

        Console.WriteLine($"PASS: {checks} Verification cases (Didit status mapping forward only, one self-service attempt, age gate and inheritance, adult from a decision without keeping the date, founder eligibility and the 1,000 limit, webhook V2/raw signatures, forgeries, tampering, stale timestamps).");
    }
}
