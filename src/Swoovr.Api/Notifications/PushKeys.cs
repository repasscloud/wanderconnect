using System.Security.Cryptography;
using System.Text;
using Swoovr.Api.Accounts;
using Swoovr.Infrastructure.Messaging;

namespace Swoovr.Api.Notifications;

public sealed class PushKeys
{
    public PushTokenProtector? Protector { get; }
    public PushKeys(IConfiguration config, IHostEnvironment environment, Founders founders)
    {
        var keys = new Dictionary<int, byte[]>();
        foreach (var entry in config.GetSection("Push:TokenKeys").GetChildren())
        {
            if (!int.TryParse(entry.Key, out var version) || version < 1 || entry.Value is null) throw new InvalidOperationException("Invalid push token key version.");
            try { var key = Convert.FromBase64String(entry.Value); if (key.Length != 32) throw new FormatException(); keys[version] = key; }
            catch (FormatException) { throw new InvalidOperationException("Push token keys must be base64-encoded 32-byte keys."); }
        }
        byte[]? lookup = null;
        if (config["Push:TokenLookupKey"] is { } value)
        {
            try { lookup = Convert.FromBase64String(value); if (lookup.Length != 32) throw new FormatException(); }
            catch (FormatException) { throw new InvalidOperationException("Push token lookup key must be base64-encoded 32 bytes."); }
        }
        if (environment.IsDevelopment())
        {
            if (!keys.ContainsKey(1) && founders.DeriveLegacyKey("swoovr:push-token:encryption") is { } legacy) keys[1] = legacy;
            lookup ??= founders.DeriveLegacyKey("swoovr:push-token:lookup");
        }
        if (keys.Count > 0 && lookup is not null) Protector = new(keys, lookup);
        else if (config["Push:Provider"] == "providers") throw new InvalidOperationException("Configured push providers require Push:TokenKeys and Push:TokenLookupKey.");
    }
}
