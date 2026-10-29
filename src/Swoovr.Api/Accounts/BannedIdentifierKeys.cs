using System.Collections.ObjectModel;
using Microsoft.Extensions.Hosting;

namespace Swoovr.Api.Accounts;

/// <summary>D217 HMAC keys are server configuration, never database values. Old versions must remain for old fingerprints.</summary>
public sealed class BannedIdentifierKeys
{
    private readonly IReadOnlyDictionary<int, byte[]> keys;
    public IReadOnlyCollection<int> Versions => keys.Keys.ToArray();
    public int? CurrentVersion { get; }
    public bool Configured => CurrentVersion is { } version && keys.ContainsKey(version);

    public BannedIdentifierKeys(IConfiguration configuration, IHostEnvironment environment, Founders founders)
    {
        var parsed = new Dictionary<int, byte[]>();
        foreach (var child in configuration.GetSection("Bans:HmacKeys").GetChildren())
        {
            if (!int.TryParse(child.Key, out var version) || version < 1 || string.IsNullOrWhiteSpace(child.Value))
                throw new InvalidOperationException("Bans:HmacKeys requires positive version numbers and nonempty keys.");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(child.Value); }
            catch (FormatException) { throw new InvalidOperationException("Bans:HmacKeys requires base64 keys."); }
            if (bytes.Length < 32) throw new InvalidOperationException("Bans:HmacKeys requires at least32 bytes per key.");
            parsed.Add(version, bytes);
        }
        // Explicit keys replace the ring entirely. A Development fallback is stable founder1, never the latest coupon key.
        if (parsed.Count == 0 && environment.IsDevelopment() && founders.DeriveLegacyKey("swoovr banned identifier HMAC v1") is { } local)
            parsed.Add(1, local);
        if (configuration["Bans:CurrentKeyVersion"] is { } current)
        {
            if (!int.TryParse(current, out var version) || !parsed.ContainsKey(version))
                throw new InvalidOperationException("Bans:CurrentKeyVersion must name a configured HMAC key.");
            CurrentVersion = version;
        }
        else if (parsed.Count > 0) CurrentVersion = parsed.Keys.Max();
        keys = new ReadOnlyDictionary<int, byte[]>(parsed);
    }

    public bool HasVersion(int version) => keys.ContainsKey(version);
    public byte[] Fingerprint(Swoovr.Domain.BannedIdentifierType type, string input, int version) =>
        keys.TryGetValue(version, out var key) ? Swoovr.Domain.BannedIdentifierPolicy.Fingerprint(type, input, key)
            : throw new InvalidOperationException("Required banned-identifier HMAC key is unavailable.");
}
