namespace Swoovr.Domain;

/// <summary>Provider observations, never an authentication/ownership assertion or proof of no effect.</summary>
public enum IdentityContactState { Available, Retired, NotRecorded, BindingChanged, Conflict, NotSupported, Unavailable }
public sealed record IdentityEmailBinding(Guid Subject, string? Email, string? Username, bool EmailVerified, bool Enabled);
public sealed record IdentityEmailBindingResult(IdentityContactState State, IdentityEmailBinding? Binding = null);

/// <summary>D249: restricted provider-owned binding policy. Human review and durable application intent remain API responsibilities.</summary>
public interface IContactIdentityAdmin
{
    bool ContactRetirementConfigured { get; }
    string ContactIssuer { get; }
    Task<IdentityEmailBindingResult> ReadEmailBindingAsync(Guid subject, CancellationToken token);
    /// <summary>NotRecorded is not proof of no effect. Retired requires current state matching this opaque operation.</summary>
    Task<IdentityContactState> EmailRetirementStatusAsync(Guid subject, Guid operation, CancellationToken token);
    /// <summary>A successful attempt response alone is not confirmation; separately read current operation status.</summary>
    Task<IdentityContactState> RetireEmailBindingAsync(IdentityEmailBinding expected, Guid operation, CancellationToken token);
}
