namespace DatingApp.Contracts;
public sealed record PrototypeFilter(string Travel = "local", bool Incognito = false,
    bool HideSparse = false, bool RequireFriendship = false, string Match = "any", bool RequireActivity = false);
