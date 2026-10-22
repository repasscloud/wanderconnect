namespace Swoovr.Contracts;

// Fictional preview projections only. Never reuse for real member/identity records.
public sealed record PrototypePerson(Guid Id, string Name, int Age, string Introduction,
    string Area, IReadOnlyList<string> Intentions, bool HasDemoImage, bool CanContact, bool IsSimulation);
public sealed record PrototypePeople(bool IsSimulation, string ViewerDescription, IReadOnlyList<PrototypePerson> People);
public sealed record PrototypeContact(bool IsSimulation, bool Allowed, string Message);
