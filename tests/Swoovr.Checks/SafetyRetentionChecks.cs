using Swoovr.Domain;

static class SafetyRetentionChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool fact, string message) { if (!fact) throw new Exception(message); checks++; }
        var closed = DateTimeOffset.Parse("2024-02-29T12:00:00Z");
        var item = new ModerationCase { Status = CaseStatus.Closed, ClosedAt = closed };
        var ordinary = SafetyRetentionPolicy.ForCase(item, serious: false, referredAt: null);
        Expect(ordinary.ContentUntil == closed.AddMonths(12), "Ordinary qualifying review must retain a full calendar year.");
        Expect(ordinary.RecordsUntil == closed.AddMonths(24), "Compliance case records retain two calendar years.");
        Expect(!SafetyRetentionPolicy.MayDispose(ordinary.ContentUntil, ordinary.ContentUntil!.Value.AddTicks(-1), false), "Never dispose just before minimum.");
        Expect(SafetyRetentionPolicy.MayDispose(ordinary.ContentUntil, ordinary.ContentUntil.Value, false), "Dispose at applicable expiry without hold.");
        Expect(!SafetyRetentionPolicy.MayDispose(ordinary.ContentUntil, closed.AddYears(10), true), "A hold overrides an expired schedule.");
        var serious = SafetyRetentionPolicy.ForCase(item, true, null);
        Expect(serious.ContentUntil == closed.AddMonths(24), "Serious incident proposed minimum is24 calendar months.");
        Expect(!SafetyRetentionPolicy.MayDispose(serious.ContentUntil, closed.AddMonths(23), false), "Serious content cannot expire after an ordinary review minimum.");
        var referral = closed.AddMonths(8);
        var later = SafetyRetentionPolicy.ForCase(item, false, referral);
        Expect(later.ContentUntil == referral.AddMonths(12), "Latest referral extends content beyond earlier review completion.");
        Expect(later.RecordsUntil == referral.AddMonths(24), "Latest referral extends compliance records too.");
        Expect(SafetyRetentionPolicy.ForCase(item, false, closed.AddDays(-1)) == ordinary, "Earlier referral cannot shorten closure minimum.");
        foreach (var status in new[] { CaseStatus.Open, CaseStatus.UnderReview, CaseStatus.Reviewed })
        {
            item.Status = status;
            var open = SafetyRetentionPolicy.ForCase(item, false, referral);
            Expect(open.ContentUntil is null && open.RecordsUntil is null, "Unclosed review has no expiry clock.");
            Expect(!SafetyRetentionPolicy.MayDispose(open.ContentUntil, closed.AddYears(20), false), "Unclosed review never expires by age alone.");
        }
        item.Status = CaseStatus.Closed; item.ClosedAt = null;
        Expect(SafetyRetentionPolicy.ForCase(item, false, referral).ContentUntil is null, "Missing closure provenance fails closed.");
        Expect(!SafetyRetentionPolicy.MayDispose(null, closed.AddYears(20), false), "Unknown expiry never permits disposal.");
        var media = Guid.NewGuid();
        var legacy = ReviewMediaReferences.Read("message", "{\"messages\":[{\"photo\":\"" + media + "\"}]}");
        Expect(legacy.Known && legacy.Ids.Contains(media), "Legacy reviewed-photo bytes remain referenced independently.");
        Expect(!ReviewMediaReferences.Read("message", "not-json").Known, "Malformed legacy provenance fails closed.");
        Expect(!ReviewMediaReferences.Read("message", "{\"messages\":[{\"photo\":\"missing-id\"}]}").Known, "Unresolved photo provenance fails closed.");
        Expect(!ReviewMediaReferences.Read("message", "{}").Known, "A missing message collection cannot prove no photos.");
        Expect(ReviewMediaReferences.Read("profile", "{\"about\":\"Fictional review text\"}").Known, "Known legacy text-only snapshot is understood.");
        Expect(MediaPolicy.CaseReviewable(new MediaItem { Status = MediaStatus.Removed, ScreenedAt = closed }), "Retained screened photo stays accessible to authorised review after ordinary deletion.");
        Expect(MediaPolicy.CaseReviewable(new MediaItem { Status = MediaStatus.Rejected, ScreenedAt = closed }), "Refusal does not erase authorised case evidence.");
        Expect(!MediaPolicy.CaseReviewable(new MediaItem { Status = MediaStatus.Removed }), "An unchecked removed upload never becomes viewable.");
        Expect(!MediaPolicy.CaseReviewable(new MediaItem { Status = MediaStatus.Blocked, ScreenedAt = closed }), "Blocked material stays unviewable under every retained-evidence path.");
        Console.WriteLine($"PASS: safety retention calendar/closure/referral/hold policy ({checks} checks; proposed legal-review periods).");
    }
}
