using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Swoovr.Infrastructure.Verification;

public enum DiditWebhookCheck { Valid, BadSignature, Malformed, Stale }

/// <summary>
/// Didit webhook authentication (D152, docs.didit.me/integration/webhooks, read 25 October 2026). The signature is
/// checked first, over the raw body: X-Signature-V2 is HMAC-SHA256 (hex) of the canonical JSON (keys sorted
/// recursively, compact separators, Unicode kept unescaped, whole-valued floats written as integers, arrays in order);
/// X-Signature is HMAC-SHA256 of the exact bytes. Only after a valid signature is the body's own `timestamp` (which is
/// signed) checked to be within five minutes; the unsigned X-Timestamp header is never trusted. X-Signature-Simple is
/// refused because it doesn't cover vendor_data or the decision.
/// </summary>
public static class DiditSignature
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);

    public static DiditWebhookCheck Check(byte[] rawBody, string? signatureV2, string? signatureRaw, string secret, DateTimeOffset now, out JsonDocument? body)
    {
        body = null;
        if (string.IsNullOrEmpty(secret) || rawBody.Length == 0) return DiditWebhookCheck.BadSignature;
        JsonDocument document;
        try { document = JsonDocument.Parse(rawBody); }
        catch (JsonException) { return DiditWebhookCheck.BadSignature; } // unsigned garbage reads as a forgery
        var key = Encoding.UTF8.GetBytes(secret);
        var valid = signatureV2 is not null && Matches(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(Canonical(document.RootElement))), signatureV2)
            || signatureRaw is not null && Matches(HMACSHA256.HashData(key, rawBody), signatureRaw);
        if (!valid) { document.Dispose(); return DiditWebhookCheck.BadSignature; }
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("timestamp", out var stamp) || !stamp.TryGetInt64(out var seconds))
        {
            document.Dispose();
            return DiditWebhookCheck.Malformed;
        }
        if ((now - DateTimeOffset.FromUnixTimeSeconds(seconds)).Duration() > MaxAge) { document.Dispose(); return DiditWebhookCheck.Stale; }
        body = document;
        return DiditWebhookCheck.Valid;
    }

    /// <summary>Hex HMAC for a body under the V2 rules; used by tests that sign their own payloads.</summary>
    public static string SignV2(string json, string secret)
    {
        using var document = JsonDocument.Parse(json);
        return Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(Canonical(document.RootElement))));
    }

    private static bool Matches(byte[] expected, string presented)
    {
        byte[] given;
        try { given = Convert.FromHexString(presented.Trim()); }
        catch (FormatException) { return false; }
        return CryptographicOperations.FixedTimeEquals(expected, given);
    }

    /// <summary>Python's json.dumps(sort_keys=True, separators=(",", ":"), ensure_ascii=False) after shortening whole floats.</summary>
    public static string Canonical(JsonElement element)
    {
        var builder = new StringBuilder();
        Write(builder, element);
        return builder.ToString();
    }

    private static void Write(StringBuilder builder, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                var first = true;
                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (!first) builder.Append(',');
                    first = false;
                    WriteString(builder, property.Name);
                    builder.Append(':');
                    Write(builder, property.Value);
                }
                builder.Append('}');
                break;
            case JsonValueKind.Array:
                builder.Append('[');
                var i = 0;
                foreach (var item in element.EnumerateArray())
                {
                    if (i++ > 0) builder.Append(',');
                    Write(builder, item);
                }
                builder.Append(']');
                break;
            case JsonValueKind.String: WriteString(builder, element.GetString()!); break;
            case JsonValueKind.Number: builder.Append(Number(element.GetRawText())); break;
            case JsonValueKind.True: builder.Append("true"); break;
            case JsonValueKind.False: builder.Append("false"); break;
            default: builder.Append("null"); break;
        }
    }

    /// <summary>Integers as written; floats as Python's repr, with whole values as integers (100.0 → 100).</summary>
    private static string Number(string raw)
    {
        if (!raw.Contains('.') && !raw.Contains('e') && !raw.Contains('E')) return raw;
        var value = double.Parse(raw, CultureInfo.InvariantCulture);
        if (Math.Floor(value) == value && !double.IsInfinity(value))
            return new System.Numerics.BigInteger(value).ToString(CultureInfo.InvariantCulture);
        var text = value.ToString("R", CultureInfo.InvariantCulture);
        if (!text.Contains('E')) return text;
        // .NET writes 1E-05; Python writes 1e-05 (an exponent of at least two digits, with a sign).
        var (mantissa, exponent) = (text[..text.IndexOf('E')], int.Parse(text[(text.IndexOf('E') + 1)..], CultureInfo.InvariantCulture));
        return $"{mantissa}e{(exponent < 0 ? '-' : '+')}{Math.Abs(exponent):00}";
    }

    private static void WriteString(StringBuilder builder, string value)
    {
        builder.Append('"');
        foreach (var c in value)
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                default:
                    if (c < 0x20) builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else builder.Append(c);
                    break;
            }
        builder.Append('"');
    }
}
