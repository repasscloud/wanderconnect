namespace Swoovr.Client;

/// <summary>
/// Issue 254 (D342 DEFAULT): the onboarding mobile step asks for a country code and the number separately, then sends one
/// international number to the API, which still normalises and checks it (D181). Australia is first and the default.
/// </summary>
public static class PhoneEntry
{
    public sealed record Country(string Name, string Code);

    public static readonly IReadOnlyList<Country> Countries =
    [
        new("Australia", "+61"), new("New Zealand", "+64"), new("United Kingdom", "+44"), new("Ireland", "+353"),
        new("United States", "+1"), new("Canada", "+1"), new("Singapore", "+65"), new("Philippines", "+63"),
        new("India", "+91"), new("Indonesia", "+62"), new("Malaysia", "+60"), new("Thailand", "+66"),
        new("Hong Kong", "+852"), new("Japan", "+81"), new("South Africa", "+27"), new("Germany", "+49"),
        new("France", "+33"), new("Netherlands", "+31"), new("Italy", "+39"), new("Spain", "+34"),
    ];

    /// <summary>
    /// The number to send: a number typed with its own leading + is used as typed; otherwise the country code plus the
    /// digits, without the single leading 0 that local formats add (0412 345 678 with +61 → +61412345678).
    /// </summary>
    public static string Compose(string? countryCode, string? number)
    {
        var typed = (number ?? "").Trim();
        if (typed.StartsWith('+')) return typed;
        var digits = new string(typed.Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith('0')) digits = digits[1..];
        return (string.IsNullOrWhiteSpace(countryCode) ? "+61" : countryCode.Trim()) + digits;
    }
}
