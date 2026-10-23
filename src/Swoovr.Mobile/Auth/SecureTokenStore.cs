using Swoovr.Client;

namespace Swoovr.Mobile;

/// <summary>The app's tokens in the Keychain (iOS) or Keystore-backed storage (Android), never in preferences or files.</summary>
public sealed class SecureTokenStore : ITokenStore
{
	private const string Key = "swoovr.tokens";

	public Task<string?> ReadAsync() => SecureStorage.Default.GetAsync(Key);
	public Task WriteAsync(string value) => SecureStorage.Default.SetAsync(Key, value);
	public void Clear() => SecureStorage.Default.Remove(Key);
}
