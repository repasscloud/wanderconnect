using System.Text.Json.Nodes;

namespace Swoovr.Domain;

public enum LeCaseStatus { Open, Closed }

public enum LeUrgency { Critical, Urgent, Standard }

/// <summary>
/// A restricted law-enforcement case (police-contact procedure §2–§14, D195): one per approach by an authority, with its
/// requests, the agency and officer as they gave them, independently verified callback evidence, the legal assessment,
/// the APP 6.5 note, privileged and confidential flags, deadline timers and the closure. `legal` only; implicated staff are
/// excluded. Kept seven years after closure (§13). Closing never releases a hold or destroys evidence.
/// </summary>
public sealed class LeCase
{
    public Guid Id { get; set; }
    /// <summary>A short human reference (LE-YYYY-xxxxxx), never derived from member data.</summary>
    public string Reference { get; set; } = "";
    public LeCaseStatus Status { get; set; }
    /// <summary>email, phone, letter, portal or in-person (§2: the channel it arrived through).</summary>
    public string Channel { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; }
    public LeUrgency Urgency { get; set; }
    public string Agency { get; set; } = "";
    /// <summary>ISO 3166 alpha-2 (foreign authorities need a separate jurisdictional review, §3.5, §11).</summary>
    public string AgencyCountry { get; set; } = "AU";
    public string? AgencyReference { get; set; }
    public string OfficerName { get; set; } = "";
    public string? OfficerRank { get; set; }
    public string? OfficerServiceNumber { get; set; }
    public string? OfficerEmail { get; set; }
    public string? OfficerPhone { get; set; }
    public Guid OwnerId { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>§3.2: how the officer and request were verified, through contact details Swoovr found itself.</summary>
    public string? VerificationSource { get; set; }
    public string? VerificationEvidence { get; set; }
    public Guid? VerifiedBy { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    /// <summary>§3.3–§3.7: mandatory or voluntary, the instrument, scope and proportionality.</summary>
    public string? LegalAssessment { get; set; }
    public string? LegalInstrument { get; set; }
    public bool? Mandatory { get; set; }
    public Guid? AssessedBy { get; set; }
    public DateTimeOffset? AssessedAt { get; set; }
    /// <summary>§3 APP 6 note: the written note APP 6.5 requires when relying on APP 6.2(e).</summary>
    public string? App65Note { get; set; }
    public Guid? App65By { get; set; }
    public DateTimeOffset? App65At { get; set; }
    public bool Privileged { get; set; }
    public bool Confidential { get; set; }
    /// <summary>§10: a non-binding confidentiality request gets its own documented assessment.</summary>
    public string? ConfidentialityAssessment { get; set; }
    public DateTimeOffset? StatutoryDeadline { get; set; }
    public DateTimeOffset? FirstResponseAt { get; set; }
    public DateTimeOffset? LastUpdateAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public Guid? ClosedBy { get; set; }
    public string? ClosureNote { get; set; }
    public DateTimeOffset? RetainUntil { get; set; }
}

/// <summary>One request inside a case (§2, §6): what was asked, for which period, and the decision with its approver.</summary>
public sealed class LeRequest
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    /// <summary>preservation, disclosure, emergency-disclosure, assistance or other.</summary>
    public string Kind { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? Until { get; set; }
    /// <summary>open, then preserved, disclosed, partly-disclosed, refused, withdrawn or redirected.</summary>
    public string Status { get; set; } = "open";
    public string? DecisionReason { get; set; }
    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    /// <summary>§5: an on-call emergency approval without the usual separation, reviewed afterwards.</summary>
    public bool BreakGlass { get; set; }
    public DateTimeOffset? RetrospectiveDue { get; set; }
    public Guid? RetrospectiveBy { get; set; }
    public DateTimeOffset? RetrospectiveAt { get; set; }
    public string? RetrospectiveNote { get; set; }
}

/// <summary>A member the case concerns (subject) or a staff account implicated in it, who is then excluded from it (§5).</summary>
public sealed class LeCasePerson
{
    public Guid CaseId { get; set; }
    public Guid AccountId { get; set; }
    /// <summary>subject or implicated-staff.</summary>
    public string Kind { get; set; } = "";
    public Guid AddedBy { get; set; }
    public DateTimeOffset AddedAt { get; set; }
}

/// <summary>§10: whether an affected member is told, and if not, why and when to reconsider.</summary>
public sealed class LeNotification
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid AccountId { get; set; }
    /// <summary>notified, delay or withhold.</summary>
    public string Decision { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset? ReconsiderAt { get; set; }
    public Guid DecidedBy { get; set; }
    public DateTimeOffset DecidedAt { get; set; }
}

/// <summary>§8.4–§8.5: one evidence package handed to a verified recipient, with its hash and receipt.</summary>
public sealed class LeTransfer
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid ExportId { get; set; }
    public byte[] PackageSha256 { get; set; } = [];
    public string Recipient { get; set; } = "";
    /// <summary>secure-link, encrypted-media, agency-portal or in-person.</summary>
    public string Method { get; set; } = "";
    public Guid SentBy { get; set; }
    public DateTimeOffset SentAt { get; set; }
    public DateTimeOffset? ReceiptAt { get; set; }
    public string? ReceiptReference { get; set; }
}

/// <summary>§6, §14: one redaction on a disclosure copy. The preserved original never changes.</summary>
public sealed class EvidenceRedaction
{
    public Guid Id { get; set; }
    public Guid ExportId { get; set; }
    public Guid ItemId { get; set; }
    /// <summary>Comma-separated JSON paths (dotted) replaced in the copy.</summary>
    public string Fields { get; set; } = "";
    public string Reason { get; set; } = "";
    public Guid RedactedBy { get; set; }
    public DateTimeOffset At { get; set; }
}

public enum LeRefusal
{
    None, NotPermitted, Excluded, Invalid, NotVerified, NotAssessed, App65Required, NotSeparated, Closed, AlreadyDecided, NotSubject, ReviewTooFar,
}

/// <summary>The procedure's rules (D195) as plain functions; facts come from application records only.</summary>
public static class LawEnforcementPolicy
{
    public const int TextMax = 2000;
    public const int ShortMax = 200;
    public const int SubjectMax = 20;
    public const int SuppressBelow = 5;
    public static readonly TimeSpan CriticalResponse = TimeSpan.FromHours(1);
    public static readonly TimeSpan UrgentResponse = TimeSpan.FromHours(4);
    public const int StandardResponseBusinessDays = 2;
    public const int UpdateBusinessDays = 5;
    public const int RetrospectiveBusinessDays = 2;
    /// <summary>§7: a temporary police preservation hold is reviewed within 30 days; review is never release.</summary>
    public static readonly TimeSpan TemporaryHoldMax = TimeSpan.FromDays(30);
    /// <summary>§13: case records are kept seven years from closure.</summary>
    public const int RetentionYears = 7;
    public static readonly TimeSpan ReconsiderMax = TimeSpan.FromDays(180);
    public const string PolicePreservation = "police-preservation";

    public static readonly IReadOnlyList<string> Channels = ["email", "phone", "letter", "portal", "in-person"];
    public static readonly IReadOnlyList<string> RequestKinds = ["preservation", "disclosure", "emergency-disclosure", "assistance", "other"];
    public static readonly IReadOnlyList<string> Outcomes = ["preserved", "disclosed", "partly-disclosed", "refused", "withdrawn", "redirected"];
    /// <summary>§3.2: independent routes only. A government email address alone is never verification.</summary>
    public static readonly IReadOnlyList<string> VerificationSources = ["official-directory", "agency-switchboard", "agency-portal", "in-person"];
    public static readonly IReadOnlyList<string> Instruments = ["warrant", "court-order", "subpoena", "statutory-notice", "app-6.2e", "serious-threat", "consent", "none"];
    public static readonly IReadOnlyList<string> NotificationDecisions = ["notified", "delay", "withhold"];
    public static readonly IReadOnlyList<string> TransferMethods = ["secure-link", "encrypted-media", "agency-portal", "in-person"];

    private static readonly TimeZoneInfo Adelaide = FindZone();

    private static TimeZoneInfo FindZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Australia/Adelaide"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.CreateCustomTimeZone("Adelaide", TimeSpan.FromHours(9.5), "Adelaide", "Adelaide"); }
    }

    public static bool Text(string? value, int max = TextMax) => !string.IsNullOrWhiteSpace(value) && value.Length <= max;

    public static bool Optional(string? value, int max = ShortMax) => value is null || value.Length <= max;

    /// <summary>Adds business days (Monday–Friday in Adelaide; public holidays are not modelled, DEFAULT) to a moment.</summary>
    public static DateTimeOffset AddBusinessDays(DateTimeOffset at, int days)
    {
        var local = TimeZoneInfo.ConvertTime(at, Adelaide);
        var added = 0;
        while (added < days)
        {
            local = local.AddDays(1);
            if (local.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)) added++;
        }
        return local.ToUniversalTime();
    }

    /// <summary>
    /// §2: the initial-response target from receipt — critical 1 hour, urgent 4 hours, standard acknowledgement within two
    /// business days. A statutory or court deadline overrides a later service target.
    /// </summary>
    public static DateTimeOffset ResponseDue(LeUrgency urgency, DateTimeOffset received, DateTimeOffset? statutory)
    {
        var target = urgency switch
        {
            LeUrgency.Critical => received + CriticalResponse,
            LeUrgency.Urgent => received + UrgentResponse,
            _ => AddBusinessDays(received, StandardResponseBusinessDays),
        };
        return statutory is { } s && s < target ? s : target;
    }

    /// <summary>§2: complex cases get an update at least every five business days after the last response.</summary>
    public static DateTimeOffset? UpdateDue(LeCase item) =>
        item.Status == LeCaseStatus.Closed || (item.LastUpdateAt ?? item.FirstResponseAt) is not { } last ? null : AddBusinessDays(last, UpdateBusinessDays);

    /// <summary>The timer the case page shows: the response until there is one, then the next update.</summary>
    public static (DateTimeOffset? Due, string State) Timer(LeCase item, DateTimeOffset now)
    {
        if (item.Status == LeCaseStatus.Closed) return (null, "closed");
        if (item.FirstResponseAt is null)
        {
            var due = ResponseDue(item.Urgency, item.ReceivedAt, item.StatutoryDeadline);
            return (due, due <= now ? "response-overdue" : "response-due");
        }
        var update = UpdateDue(item)!.Value;
        if (item.StatutoryDeadline is { } statutory && statutory < update) update = statutory;
        return (update, update <= now ? "update-overdue" : "update-due");
    }

    public static string Reference(DateTimeOffset now, Guid id) => $"LE-{now.Year}-{id.ToString("N")[..6].ToUpperInvariant()}";

    /// <summary>§5: an implicated staff member is kept out of the case and its evidence.</summary>
    public static bool Excluded(IEnumerable<LeCasePerson> people, Guid staff) => people.Any(p => p.Kind == "implicated-staff" && p.AccountId == staff);

    public static LeRefusal CheckVerify(string? source, string? evidence) =>
        source is null || !VerificationSources.Contains(source) || !Text(evidence) ? LeRefusal.Invalid : LeRefusal.None;

    /// <summary>
    /// §3, §5: deciding a request. Disclosure needs a verified requester and a legal assessment, and an APP 6.5 note when the
    /// case relies on APP 6.2(e). The approver differs from the case owner (the collector), except a break-glass emergency
    /// approval, which is reviewed afterwards by another officer.
    /// </summary>
    public static LeRefusal CheckDecide(LeCase item, LeRequest request, Guid approver, string? outcome, string? reason, bool breakGlass)
    {
        if (item.Status == LeCaseStatus.Closed) return LeRefusal.Closed;
        if (request.Status != "open") return LeRefusal.AlreadyDecided;
        if (outcome is null || !Outcomes.Contains(outcome) || !Text(reason)) return LeRefusal.Invalid;
        var discloses = outcome is "disclosed" or "partly-disclosed";
        if (discloses && item.VerifiedAt is null) return LeRefusal.NotVerified;
        if (discloses && item.AssessedAt is null) return LeRefusal.NotAssessed;
        if (discloses && item.LegalInstrument == "app-6.2e" && string.IsNullOrWhiteSpace(item.App65Note)) return LeRefusal.App65Required;
        if (breakGlass && !(request.Kind == "emergency-disclosure" || item.Urgency == LeUrgency.Critical)) return LeRefusal.Invalid;
        if (!breakGlass && approver == item.OwnerId) return LeRefusal.NotSeparated;
        return LeRefusal.None;
    }

    /// <summary>The retrospective review of a break-glass approval is by someone other than the approver.</summary>
    public static LeRefusal CheckRetrospective(LeRequest request, Guid reviewer, string? note) =>
        !request.BreakGlass || request.RetrospectiveAt is not null ? LeRefusal.Invalid
        : request.DecidedBy == reviewer ? LeRefusal.NotSeparated
        : !Text(note) ? LeRefusal.Invalid : LeRefusal.None;

    /// <summary>§10: delaying or withholding notice needs the precise reason and a reconsideration date.</summary>
    public static LeRefusal CheckNotification(string? decision, string? reason, DateTimeOffset? reconsiderAt, DateTimeOffset now)
    {
        if (decision is null || !NotificationDecisions.Contains(decision) || !Text(reason)) return LeRefusal.Invalid;
        if (decision == "notified") return LeRefusal.None;
        return reconsiderAt is not { } at || at <= now || at - now > ReconsiderMax ? LeRefusal.ReviewTooFar : LeRefusal.None;
    }

    /// <summary>§7: a temporary police preservation hold needs a verified request and is reviewed within 30 days.</summary>
    public static LeRefusal CheckTemporaryHold(LeCase item, DateTimeOffset? reviewDue, DateTimeOffset now) =>
        item.Status == LeCaseStatus.Closed ? LeRefusal.Closed
        : item.VerifiedAt is null ? LeRefusal.NotVerified
        : reviewDue is { } due && (due <= now || due - now > TemporaryHoldMax) ? LeRefusal.ReviewTooFar : LeRefusal.None;

    /// <summary>§7: only a senior approver who neither placed nor confirmed the hold may release it.</summary>
    public static bool IndependentRelease(LegalHold hold, Guid releaser) => hold.PlacedBy != releaser && hold.ConfirmedBy != releaser;

    public sealed record ChecklistItem(string Item, bool Done);

    /// <summary>
    /// §13: a case closes only once every request is decided (and every break-glass approval reviewed), every subject has a
    /// notification decision, every transfer has a receipt, an APP 6.5 note exists where APP 6.2(e) was relied on, and every
    /// live hold from the case has a named owner and a future review (holds outlive closure, never silently released).
    /// </summary>
    public static IReadOnlyList<ChecklistItem> Closure(LeCase item, IReadOnlyCollection<LeRequest> requests, IReadOnlyCollection<LeCasePerson> people,
        IReadOnlyCollection<LeNotification> notifications, IReadOnlyCollection<LeTransfer> transfers, IReadOnlyCollection<LegalHold> holds, DateTimeOffset now)
    {
        var subjects = people.Where(p => p.Kind == "subject").Select(p => p.AccountId).ToList();
        var disclosed = requests.Any(r => r.Status is "disclosed" or "partly-disclosed");
        return
        [
            new("requests-decided", requests.Count > 0 && requests.All(r => r.Status != "open")),
            new("emergency-reviewed", requests.All(r => !r.BreakGlass || r.RetrospectiveAt is not null)),
            new("requester-verified", !disclosed || item.VerifiedAt is not null),
            new("legal-assessment", item.AssessedAt is not null),
            new("app65-note", !disclosed || item.LegalInstrument != "app-6.2e" || !string.IsNullOrWhiteSpace(item.App65Note)),
            new("notification-decisions", subjects.All(s => notifications.Any(n => n.AccountId == s))),
            new("transfer-receipts", transfers.All(t => t.ReceiptAt is not null)),
            new("holds-assigned", holds.Where(LegalHoldPolicy.Live).All(h => h.ConfirmedBy is not null && h.ReviewDue > now)),
        ];
    }

    public static DateTimeOffset RetainUntil(DateTimeOffset closedAt) => closedAt.AddYears(RetentionYears);

    /// <summary>§13 transparency: counts under five are suppressed (null) so no small group can be recognised.</summary>
    public static int? Suppress(int count) => count is > 0 and < SuppressBelow ? null : count;

    /// <summary>"2026-Q4" → [1 Oct 2026, 1 Jan 2027) in UTC.</summary>
    public static (DateTimeOffset From, DateTimeOffset Until)? Quarter(string? text)
    {
        if (text is not { Length: 7 } || text[4..6] != "-Q" || !int.TryParse(text[..4], out var year) || year is < 2020 or > 2100
            || !int.TryParse(text[6..], out var q) || q is < 1 or > 4) return null;
        var from = new DateTimeOffset(year, (q - 1) * 3 + 1, 1, 0, 0, 0, TimeSpan.Zero);
        return (from, from.AddMonths(3));
    }

    /// <summary>
    /// §6, §14: a disclosure copy with the given dotted JSON paths replaced by "[REDACTED]". The original is never changed;
    /// a path that isn't present is reported so the redaction record never claims more than it did.
    /// </summary>
    public static (string Json, IReadOnlyList<string> Missing) Redact(string json, IEnumerable<string> paths)
    {
        var root = JsonNode.Parse(json);
        var missing = new List<string>();
        foreach (var path in paths)
        {
            var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
            JsonNode? node = root;
            for (var i = 0; i < parts.Length - 1 && node is JsonObject o; i++) node = o[parts[i]];
            if (parts.Length > 0 && node is JsonObject parent && parent.ContainsKey(parts[^1])) parent[parts[^1]] = "[REDACTED]";
            else missing.Add(path);
        }
        return (root?.ToJsonString() ?? json, missing);
    }
}
