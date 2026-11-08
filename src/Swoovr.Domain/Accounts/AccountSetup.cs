namespace Swoovr.Domain;

/// <summary>
/// Issue 254 (D342 DEFAULT): the one order every client follows to finish setting up a member account, matching the
/// order the API refuses other routes in (AccountEndpoints.MemberAsync): registered names (D300), a verified mobile
/// number (D148), required policies, then the date of birth (D139/D162; saving it is refused until the earlier steps are
/// done). Null means setup is complete.
/// </summary>
public static class AccountSetup
{
    public const string Names = "names", Phone = "phone", Policies = "policies", DateOfBirth = "dateOfBirth";

    public static string? Next(bool registeredNamesRequired, bool phoneRequired, bool policyRequired, DateOnly? dateOfBirth) =>
        registeredNamesRequired ? Names
        : phoneRequired ? Phone
        : policyRequired ? Policies
        : dateOfBirth is null ? DateOfBirth
        : null;
}

/// <summary>
/// Issue 254 (owner wording): how the member's own email address and mobile number show on their account until they
/// choose to reveal them. Email: first and last character of the name and of the domain with exactly three dots between
/// ("alex.original@example.test" → "a...l@e...e.test"), the top-level domain always visible. Mobile numbers use
/// <see cref="PhoneVerificationPolicy.Masked"/>.
/// </summary>
public static class ContactMask
{
    public static string? Email(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return null;
        var at = address.LastIndexOf('@');
        if (at <= 0 || at == address.Length - 1) return Part(address);
        var domain = address[(at + 1)..];
        var dot = domain.LastIndexOf('.');
        var maskedDomain = dot <= 0 ? Part(domain) : Part(domain[..dot]) + domain[dot..];
        return Part(address[..at]) + "@" + maskedDomain;
    }

    private static string Part(string value) => value.Length switch
    {
        0 => "...",
        1 => value + "...",
        _ => value[0] + "..." + value[^1],
    };
}
