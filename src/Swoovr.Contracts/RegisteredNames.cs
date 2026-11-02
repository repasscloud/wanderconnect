namespace Swoovr.Contracts;

/// <summary>Own or permitted staff view of declarations; never document names.</summary>
public sealed record RegisteredNamesView(string? FirstName,string? LastName,bool Mononym,bool Confirmed,long Revision,bool? MatchesId);
public sealed record RegisteredNamesUpdate(string? FirstName,string? LastName,bool Mononym,long ExpectedRevision,bool Confirm);
