namespace Swoovr.Contracts;

public sealed record RegistrationAvailabilityView(bool BetaEnabled,bool PublicEnabled,long Revision,string State,bool CanChange);
public sealed record RegistrationAvailabilityUpdate(bool BetaEnabled,bool PublicEnabled,long ExpectedRevision,bool Confirm,string Reason);
public sealed record RegistrationAdmissionRequest(string Email);
