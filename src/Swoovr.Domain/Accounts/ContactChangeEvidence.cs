namespace Swoovr.Domain;

/// <summary>Bounded security receipt, sealed with its contact record. Unknown provider clocks remain null, never inferred.</summary>
public sealed record ContactChangeEvidence(Guid RequesterAccountId,DateTimeOffset? RequestedAt,DateTimeOffset? VerifiedAt,
    DateTimeOffset? ActivatedAt,DateTimeOffset ObservedAt,string Source,string? RequestSessionHmac,string Method,bool MultiFactor,
    bool Fresh,DateTimeOffset AuthenticatedAt,DateTimeOffset? TokenIssuedAt,string? ActivationSessionHmac=null)
{
    public bool Valid=>RequesterAccountId!=Guid.Empty&&(Source is "phone-code" or "validated-email-claim")
        &&Method is{Length:>0 and <=32}&&Method.All(c=>char.IsAsciiLetter(c))
        &&(RequestSessionHmac is null||Hex(RequestSessionHmac))&&(ActivationSessionHmac is null||Hex(ActivationSessionHmac))
        &&(RequestedAt is null||VerifiedAt is null||RequestedAt<=VerifiedAt)
        &&(VerifiedAt is null||ActivatedAt is null||VerifiedAt<=ActivatedAt);
    private static bool Hex(string value)=>value.Length==64&&value.All(char.IsAsciiHexDigit);
}
