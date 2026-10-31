using System.Text.Json;
using Swoovr.Domain;

namespace Swoovr.Api.Accounts;

/// <summary>Exact provider CAS fields sealed with existing restricted-contact keys and bound to immutable intent ids.</summary>
public sealed class EmailRetirementCommands(ContactHistoryKeys keys)
{
    private sealed record Command(Guid Operation,Guid Account,Guid Registry,Guid Case,string Issuer,IdentityEmailBinding Binding);
    public byte[]? Seal(EmailRetirementOperation operation,string issuer,IdentityEmailBinding binding) =>
        keys.CurrentVersion is {} version ? ContactHistoryPolicy.Encrypt(version,keys.Encryption[version],
            JsonSerializer.Serialize(new Command(operation.Id,operation.AccountId,operation.RegistryId,operation.CaseId,issuer,binding))) : null;
    public IdentityEmailBinding Open(EmailRetirementOperation operation,string issuer)
    {
        if(operation.EncryptedCommand.Length is <29 or >8192)throw new InvalidOperationException("Restricted command unavailable.");
        var command=JsonSerializer.Deserialize<Command>(ContactHistoryPolicy.Decrypt(keys.Encryption,operation.EncryptedCommand))
            ?? throw new InvalidOperationException("Restricted command unavailable.");
        if(command.Operation!=operation.Id||command.Account!=operation.AccountId||command.Registry!=operation.RegistryId||command.Case!=operation.CaseId
            ||command.Issuer!=issuer||command.Binding.Subject==Guid.Empty)
            throw new InvalidOperationException("Restricted command provenance mismatch.");
        return command.Binding;
    }
}
