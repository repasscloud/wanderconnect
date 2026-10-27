namespace Swoovr.Contracts;

/// <summary>One external contact (#103). Category: emergency, police-non-emergency, crisis, sexual-violence, domestic-violence, online-safety, child-exploitation, other.</summary>
public sealed record SafetyResourceView(Guid Id, string Category, string Name, string Description, string? Phone, string? PhoneDisplay, string? WebsiteUrl,
    string? Region, string Language, int Order, DateOnly? LastVerifiedOn);

/// <summary>A country the selector offers; Readiness: ready, not-ready or demo.</summary>
public sealed record SafetyCountryOption(string Code, string Name, string Readiness);

/// <summary>
/// The Safety &amp; Support pack for one country (#103). Source: manual, cloudflare, fallback or unknown. Readiness:
/// ready, not-ready, demo, unsupported (a country Swoovr has no pack for) or unknown (no country). Stale means the page
/// must say the contacts may not be verified or current. Resources is empty unless Swoovr has a pack for the country:
/// another country's numbers are never shown instead.
/// </summary>
public sealed record SafetyPack(string? Country, string? CountryName, string Source, string Readiness, bool Stale, int Version, DateOnly? ReviewedOn,
    DateOnly? ReviewDueOn, DateTimeOffset GeneratedAt, IReadOnlyList<SafetyResourceView> Resources, IReadOnlyList<SafetyCountryOption> Countries);

/// <summary>A country as admins see it, with what stops it being launch-ready.</summary>
public sealed record SafetyCountryAdminView(string Code, string Name, bool Enabled, string Readiness, DateOnly? LastReviewedOn, DateOnly? ReviewDueOn,
    string? Owner, string? Notes, int Version, DateTimeOffset UpdatedAt, IReadOnlyList<string> LaunchProblems, int Resources);

public sealed record SafetyCountryUpdate(string? Name, bool Enabled, string? Readiness, DateOnly? LastReviewedOn, DateOnly? ReviewDueOn, string? Owner, string? Notes);

public sealed record SafetyResourceAdminView(Guid Id, string Country, string? Region, string Language, string Category, string Name, string Description,
    string? Phone, string? PhoneDisplay, string? WebsiteUrl, string SourceUrl, int Order, bool Enabled, DateOnly? LastVerifiedOn, DateOnly? ReviewDueOn,
    DateTimeOffset UpdatedAt);

/// <summary>A full replacement of a resource (create or edit).</summary>
public sealed record SafetyResourceEdit(string? Country, string? Region, string? Language, string? Category, string? Name, string? Description,
    string? Phone, string? PhoneDisplay, string? WebsiteUrl, string? SourceUrl, int Order, bool Enabled, DateOnly? LastVerifiedOn, DateOnly? ReviewDueOn);

public sealed record SafetyRevisionView(long Id, string Country, Guid? ResourceId, string Action, Guid? Actor, DateTimeOffset At, string Snapshot);
