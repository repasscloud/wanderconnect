namespace Swoovr.Web;

// Bind strings first: Blazor's bool conversion throws before page error handling.
internal static class PrototypeQuery
{
    public static string Value(string[]? values, string fallback) => values?.FirstOrDefault() ?? fallback;
    public static bool Boolean(string[]? values) => Value(values, "false") == "true";
    public static bool ValidBoolean(string[]? values) => values is null or [] or ["true"] or ["false"];
    public static bool ValidOption(string[]? values, params string[] allowed) => values is null or []
        || (values.Length == 1 && allowed.Contains(values[0]));
}
