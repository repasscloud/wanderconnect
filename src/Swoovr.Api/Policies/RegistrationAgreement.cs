using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Policies;

/// <summary>
/// Q124 A (owner, 4 November 2026; D316): creating an account agrees to the Terms of Use and acknowledges the Privacy
/// Notice and Community Guidelines (D312 wording, shown on the registration form and on Keycloak's terms step for
/// Apple/Google sign-ups). When the member profile is first created, record acceptance of the approved version in force,
/// timed at the member's Keycloak 18+ attestation. Only attestations on or after Policies:RegistrationAgreementFrom (when
/// that wording went live) count: earlier accounts saw different wording and are never treated as having agreed.
/// </summary>
public static class RegistrationAgreement
{
    public const string AuditDetail = "registration";

    public static bool Applies(DateTimeOffset? attestedAt, DateTimeOffset? from, DateTimeOffset now) =>
        attestedAt is { } at && from is { } start && at >= start && at <= now.AddMinutes(5);

    public static void Record(SwoovrDbContext db, ApprovedPolicyCatalogue catalogue, IConfiguration configuration, Guid accountId,
        DateTimeOffset? attestedAt, DateTimeOffset now)
    {
        var from = DateTimeOffset.TryParse(configuration["Policies:RegistrationAgreementFrom"], out var parsed) ? parsed : (DateTimeOffset?)null;
        if (!Applies(attestedAt, from, now)) return;
        if (catalogue.Find(PublicPolicies.Version, now) is not { } document) return;
        db.Set<PolicyAcceptance>().Add(new PolicyAcceptance
        {
            AccountId = accountId, Version = document.Metadata.Id, ContentSha256 = document.Metadata.ContentSha256, AcceptedAt = attestedAt!.Value,
        });
        Audit.Record(db, now, accountId, "account", "policy.accepted", "policy-version", document.Metadata.Id, AuditDetail);
    }
}
