namespace Swoovr.Domain;

/// <summary>
/// Facts read from a token the API has already validated. AuthenticationMethods are
/// RFC 8176 references emitted by the configured Keycloak AMR mapper; IdentityProvider
/// is the broker alias recorded in the Keycloak session. Neither is ever taken from a
/// request body or client-supplied field.
/// </summary>
public sealed record ValidatedSessionFacts(
    AccountRealm Realm,
    string Subject,
    bool EmailVerified,
    IReadOnlyCollection<string> AuthenticationMethods,
    string? IdentityProvider,
    DateTimeOffset AuthenticatedAt,
    DateTimeOffset? AdultAttestedAt);

/// <summary>Configured identity-provider aliases. Unconfigured aliases map to Unknown.</summary>
public sealed record SessionEvidenceOptions(
    string? AppleAlias = null,
    string? GoogleAlias = null,
    string? StaffSsoAlias = null,
    TimeSpan? PrivilegedMaxAge = null)
{
    public TimeSpan MaxAge => PrivilegedMaxAge ?? TimeSpan.FromHours(12);
}

public sealed record SessionEvidence(SignInMethod Method, bool MultiFactor, bool IsFresh);

/// <summary>
/// Turns validated session facts into the method/freshness inputs of
/// <see cref="AccountAccessPolicy"/>. Anything ambiguous maps to Unknown: an empty AMR
/// (for example the session Keycloak issues straight after email-first registration),
/// an unconfigured broker alias, a broker in the wrong realm, or a future auth time.
/// </summary>
public sealed class SessionEvidencePolicy(SessionEvidenceOptions options)
{
    // Reference values configured by scripts/auth/realms.mjs.
    public const string Password = "pwd";
    public const string OneTimePassword = "otp";
    public const string HardwareKey = "hwk";
    public const string EmailLink = "swoovr-email-link";

    public SessionEvidence Evaluate(ValidatedSessionFacts? facts, DateTimeOffset now)
    {
        if (facts is null || string.IsNullOrWhiteSpace(facts.Subject) || facts.Realm == AccountRealm.Unknown)
            return new(SignInMethod.Unknown, false, false);
        var amr = facts.AuthenticationMethods.ToHashSet(StringComparer.Ordinal);
        var age = now - facts.AuthenticatedAt;
        var fresh = age >= TimeSpan.FromSeconds(-5) && age <= options.MaxAge;
        var method = MethodOf(facts.Realm, amr, facts.IdentityProvider);
        var multiFactor = method != SignInMethod.Unknown && amr.Contains(OneTimePassword);
        return new(method, multiFactor, fresh && method != SignInMethod.Unknown);
    }

    private SignInMethod MethodOf(AccountRealm realm, HashSet<string> amr, string? broker)
    {
        if (!string.IsNullOrEmpty(broker))
        {
            // A brokered session never also counts as a local credential.
            if (realm == AccountRealm.Members && Matches(broker, options.AppleAlias)) return SignInMethod.Apple;
            if (realm == AccountRealm.Members && Matches(broker, options.GoogleAlias)) return SignInMethod.Google;
            if (realm == AccountRealm.Staff && Matches(broker, options.StaffSsoAlias)) return SignInMethod.CorporateSso;
            return SignInMethod.Unknown;
        }
        if (amr.Contains(EmailLink) && !amr.Contains(Password)) return SignInMethod.EmailLink;
        if (amr.Contains(Password)) return SignInMethod.EmailPassword;
        if (realm == AccountRealm.Members && amr.Contains(HardwareKey)) return SignInMethod.Passkey;
        return SignInMethod.Unknown;
    }

    private static bool Matches(string broker, string? alias) =>
        !string.IsNullOrEmpty(alias) && string.Equals(broker, alias, StringComparison.Ordinal);
}
