namespace Swoovr.Contracts;

// Stable lower-case wire values. These DTOs never carry an account ID or entitlement.
public sealed record AppearancePreference(string Scheme, string Mode);
public sealed record AppearanceSnapshot(AppearancePreference Preference, long Revision);
public sealed record AppearanceUpdate(AppearancePreference Preference, long ExpectedRevision, Guid ChangeId);

public sealed record AppearanceConflict(string Code,AppearanceSnapshot? Current);
