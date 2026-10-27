namespace Swoovr.Domain;

/// <summary>
/// Where one ID verification attempt stands (D128, D129). Created: the session exists and the member has the link.
/// InProgress and InReview: Didit is working on it. Approved, Declined: a real check finished. Abandoned: the member
/// started and stopped. Expired: the session lapsed; <see cref="IdVerificationAttempt.Started"/> says whether a real
/// attempt was ever made, so an unused session is distinguishable from a failed check (D129).
/// </summary>
public enum IdAttemptState { Created, InProgress, InReview, Approved, Declined, Abandoned, Expired }

/// <summary>
/// One Didit session for one member (D128): only the outcome and operational metadata, never document images,
/// numbers, selfies, names, dates of birth or biometric data. The hosted link is kept only while the session is open so
/// the member can go back to it, and cleared once the attempt settles.
/// </summary>
public sealed class IdVerificationAttempt
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    /// <summary>Didit's session id (a UUID string).</summary>
    public string SessionId { get; set; } = "";
    public string WorkflowId { get; set; } = "";
    public IdAttemptState State { get; set; }
    /// <summary>Didit's last status string as received (for example "In Review"), for admins.</summary>
    public string ProviderStatus { get; set; } = "";
    /// <summary>Whether the member ever got past "Not Started" (D129).</summary>
    public bool Started { get; set; }
    /// <summary>True for the member's own attempt; false for one an admin allowed again (D129).</summary>
    public bool SelfService { get; set; }
    /// <summary>The outcome of the age check inside the document check, when Didit reported one: adult or not. Never the date or age.</summary>
    public bool? Adult { get; set; }
    /// <summary>Didit's hosted link while the session is open; null once settled.</summary>
    public string? Url { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

/// <summary>
/// A Didit webhook delivery already applied (D152): the event id is the primary key, written in the same transaction as
/// the state change, so a replay or a retry changes nothing. Holds ids and the outcome, never the body.
/// </summary>
public sealed class DiditWebhookEvent
{
    public Guid EventId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public string WebhookType { get; set; } = "";
    public string? SessionId { get; set; }
    /// <summary>"applied", "ignored", "unknown-session", "vendor-mismatch" or "workflow-mismatch".</summary>
    public string Outcome { get; set; } = "";
}

public enum IdStartRefusal { None, AlreadyVerified, AttemptUsed, InProgress, NotAMember, Bot }

/// <summary>The member's side of a session, as the provider reports it (no document data reaches this record).</summary>
/// <param name="DateOfBirth">The date of birth on an approved document, only for comparing with the profile (D158); never stored.</param>
public sealed record ProviderSession(string SessionId, string Status, string? VendorData, string? WorkflowId, bool? Adult, string? Url = null, DateOnly? DateOfBirth = null);

/// <summary>
/// The provider boundary for ID verification (D126): Infrastructure implements it for Didit; tests use a fake.
/// Credentials stay inside the implementation.
/// </summary>
public interface IIdVerificationProvider
{
    bool Configured { get; }
    string WorkflowId { get; }
    /// <summary>Creates a hosted session bound to the internal account id (vendor_data). Null when the provider failed.</summary>
    Task<ProviderSession?> CreateSessionAsync(Guid accountId, string callbackUrl, CancellationToken cancellationToken);
    /// <summary>Reads the current decision for one session. Null when the provider failed or doesn't know it.</summary>
    Task<ProviderSession?> GetDecisionAsync(string sessionId, CancellationToken cancellationToken);
    /// <summary>Deletes a session and its data at the provider (account deletion, D173). True once it is gone (or was never known).</summary>
    Task<bool> DeleteSessionAsync(string sessionId, CancellationToken cancellationToken);
}

/// <summary>
/// ID verification rules (D127–D129, D147). Optional and free; one self-service attempt per member; admins may allow
/// another or revoke the result. Didit's statuses map onto attempt states that only move forward, so an old or
/// replayed status (from a webhook or the return re-read) never undoes a settled result.
/// </summary>
public static class IdVerificationPolicy
{
    /// <summary>Didit session statuses (webhooks and decisions), case-insensitive; "Kyc Expired"/"KYC Expired" both appear.</summary>
    public static IdAttemptState? Map(string? status) => status?.Trim().ToLowerInvariant() switch
    {
        "not started" => IdAttemptState.Created,
        "in progress" or "awaiting user" or "resubmitted" => IdAttemptState.InProgress,
        "in review" => IdAttemptState.InReview,
        "approved" => IdAttemptState.Approved,
        "declined" => IdAttemptState.Declined,
        "abandoned" => IdAttemptState.Abandoned,
        "expired" or "kyc expired" => IdAttemptState.Expired,
        _ => null,
    };

    public static bool Settled(IdAttemptState state) => state is IdAttemptState.Approved or IdAttemptState.Declined
        or IdAttemptState.Abandoned or IdAttemptState.Expired;

    private static int Rank(IdAttemptState state) => state switch
    {
        IdAttemptState.Created => 0,
        IdAttemptState.InProgress => 1,
        IdAttemptState.InReview => 2,
        _ => 3,
    };

    /// <summary>
    /// The next state for an attempt given a reported status, or null to ignore it: unknown statuses, anything that
    /// would move backwards, and any change to a settled attempt. Didit's "Resubmitted" reopens only an attempt in review.
    /// </summary>
    public static IdAttemptState? Next(IdAttemptState current, string? status)
    {
        if (Map(status) is not { } next || next == current) return null;
        if (Settled(current)) return null;
        return Rank(next) > Rank(current) || current == IdAttemptState.InReview && next == IdAttemptState.InProgress ? next : null;
    }

    /// <summary>Whether this member may start a self-service check now (D128, D129).</summary>
    public static IdStartRefusal CanStart(MemberProfile profile, IReadOnlyCollection<IdVerificationAttempt> attempts)
    {
        if (profile.Bot) return IdStartRefusal.Bot; // official bots never carry an ID badge (Q11 rule 54)
        if (profile.IdStatus == VerificationStatus.Verified) return IdStartRefusal.AlreadyVerified;
        if (attempts.Any(a => !Settled(a.State))) return IdStartRefusal.InProgress;
        return profile.CanRequestIdVerification ? IdStartRefusal.None : IdStartRefusal.AttemptUsed;
    }

    /// <summary>
    /// Whether settling this attempt gives the member their self-service attempt back (DEFAULT, D155): a session that
    /// expired or was abandoned before the member ever started is not a real attempt (D129).
    /// </summary>
    public static bool ReturnsAttempt(IdVerificationAttempt attempt) =>
        attempt.State is IdAttemptState.Expired or IdAttemptState.Abandoned && !attempt.Started;

    /// <summary>
    /// The member's ID verification status from an attempt's state. Only Approved gives the public badge (D127);
    /// pending states show nothing publicly.
    /// </summary>
    public static VerificationStatus StatusFor(IdAttemptState state) => state switch
    {
        IdAttemptState.Approved => VerificationStatus.Verified,
        IdAttemptState.Declined => VerificationStatus.Rejected,
        IdAttemptState.Expired or IdAttemptState.Abandoned => VerificationStatus.Expired,
        IdAttemptState.InReview or IdAttemptState.InProgress => VerificationStatus.Pending,
        _ => VerificationStatus.NotStarted,
    };

    /// <summary>
    /// The age status after an approved check (D127, D139): an approved adult becomes Verified; a check that shows the
    /// member is under 18 makes them Underage (the account locks). Without an age result the status is unchanged.
    /// </summary>
    /// <summary>D158: the profile's date of birth is verified only when an approved document reports exactly the same date.</summary>
    public static bool DateOfBirthMatches(DateOnly? profile, DateOnly? document) => profile is { } p && document is { } d && p == d;

    public static AgeStatus AgeAfterApproval(AgeStatus current, bool? adult) =>
        adult switch { true => AgeStatus.Verified, false => AgeStatus.Underage, null => current };
}

/// <summary>
/// The age-assurance gate (D127): an account flagged NeedsVerification can use only its own account, session and ID
/// verification until the check passes or an authorised resolution (an adult override) is recorded. The flag follows
/// the verified email to a new account in the same realm so it can't be escaped by registering again.
/// </summary>
public static class AgeAssurance
{
    public static bool Blocks(AgeStatus status) => status == AgeStatus.NeedsVerification;

    /// <summary>A new members-realm profile starts NeedsVerification when another account on the same verified email is flagged or under 18.</summary>
    public static bool Inherits(IEnumerable<AgeStatus> sameEmailStatuses) =>
        sameEmailStatuses.Any(s => s is AgeStatus.NeedsVerification or AgeStatus.Underage);
}

/// <summary>
/// Founding places (D130): exactly 1,000, allocated by a daily job in order of when each member first met every
/// condition (required profile setup plus ID or Photo verification). Profile setup here is a display name and a home city
/// (DEFAULT, D155). The eligibility time, allocation time and entitlement start are kept separately.
/// </summary>
public static class FounderEligibility
{
    public const int Places = 1000;

    public static bool Meets(MemberProfile profile, AccountStatus status) =>
        status == AccountStatus.Active && !profile.Bot
        && !string.IsNullOrWhiteSpace(profile.DisplayName) && profile.HomeGeoNameId is not null
        && (profile.IdStatus == VerificationStatus.Verified || profile.PhotoStatus == VerificationStatus.Verified)
        && profile.AgeStatus is not (AgeStatus.Underage or AgeStatus.NeedsVerification);

    /// <summary>
    /// Records the first time the member met every condition; later changes never move it (losing and regaining
    /// eligibility keeps the original place in the queue). Returns whether it changed.
    /// </summary>
    public static bool Stamp(MemberProfile profile, AccountStatus status, DateTimeOffset now)
    {
        if (profile.FounderEligibleAt is not null || !Meets(profile, status)) return false;
        profile.FounderEligibleAt = now;
        return true;
    }

    /// <summary>
    /// Who gets the remaining places in this run: eligible non-founders by eligibility time (account id breaks ties),
    /// never more than the places left. Members who no longer meet the conditions wait.
    /// </summary>
    public static IReadOnlyList<Guid> Allocate(IEnumerable<(Guid AccountId, DateTimeOffset EligibleAt, bool StillMeets, bool Founder)> queue, int allocated) =>
        queue.Where(q => q.StillMeets && !q.Founder).OrderBy(q => q.EligibleAt).ThenBy(q => q.AccountId)
            .Take(Math.Max(0, Places - allocated)).Select(q => q.AccountId).ToList();
}
