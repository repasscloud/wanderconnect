using DatingApp.Contracts;

namespace DatingApp.Client;

public static partial class AppearancePreferences
{
    public static AppearancePreference Default { get; } = new("electric", "system");
    public static bool IsValid(AppearancePreference? preference) => preference is not null
        && IsSupportedScheme(preference.Scheme) && preference.Mode is "system" or "light" or "dark";
}

/// <summary>Immutable retry identity; bind any future transport to this activation's authentication.</summary>
public sealed record AppearanceSaveAttempt(Guid Activation, AppearanceUpdate Update);

/// <summary>
/// Pure state for one client UI thread. No HTTP, storage or authentication implementation.
/// BeginAccount on every sign-in/account switch, then correlate every response with its ticket.
/// A future adapter must bind its requests to the corresponding authenticated account and cancel
/// outgoing work on sign-out. This generation check protects local state, not server authorization.
/// </summary>
public sealed class AppearanceSyncState
{
    private AppearancePreference guest;
    private AppearanceSnapshot? server;
    private AppearanceSaveAttempt? inFlight;
    private Guid activation;
    private bool normalizedConflict;
    private bool explicitSaveRequired;
    public AppearancePreference Current { get; private set; }
    public bool IsLoading { get; private set; }
    public bool IsRevisionExhausted => server?.Revision == long.MaxValue;
    public AppearanceSnapshot? Conflict { get; private set; }
    public bool HasPendingSave => activation != Guid.Empty && !IsLoading
        && (Conflict is not null || inFlight is not null || explicitSaveRequired || server is null || Current != server.Preference);

    public AppearanceSyncState(AppearancePreference guestPreference)
    {
        RequirePreference(guestPreference);
        guest = guestPreference;
        Current = guest;
    }

    public Guid BeginAccount()
    {
        activation = Guid.NewGuid();
        server = null; inFlight = null; Conflict = null;
        normalizedConflict = false; explicitSaveRequired = false;
        IsLoading = true;
        Current = AppearancePreferences.Default;
        return activation;
    }

    public bool CompleteLoad(Guid ticket, AppearanceSnapshot? snapshot)
    {
        if (ticket == Guid.Empty || ticket != activation || !IsLoading
            || (snapshot is not null && !ValidRevision(snapshot))) return false;
        server = snapshot is null ? null : Normalize(snapshot);
        Current = server?.Preference ?? guest;
        IsLoading = false;
        return true;
    }

    public void Select(AppearancePreference preference)
    {
        RequirePreference(preference);
        if (IsLoading) throw new InvalidOperationException("Wait for the account preference before editing.");
        if (IsRevisionExhausted) throw new InvalidOperationException("Account appearance revision is exhausted; further saves are unavailable.");
        Current = preference;
        if (activation == Guid.Empty) guest = preference;
    }

    public AppearanceSaveAttempt? BeginSave()
    {
        if (Conflict is not null || !HasPendingSave || IsRevisionExhausted) return null;
        // A timeout/offline failure is retried verbatim, even after a newer local edit.
        return inFlight ??= new(activation, new(Current, server?.Revision ?? 0, Guid.NewGuid()));
    }

    public bool AcceptSaved(AppearanceSaveAttempt attempt, AppearanceSnapshot snapshot)
    {
        if (!IsCurrent(attempt) || !ValidSnapshot(snapshot)
            || snapshot.Revision != attempt.Update.ExpectedRevision + 1
            || snapshot.Preference != attempt.Update.Preference) return false;
        server = snapshot;
        inFlight = null;
        explicitSaveRequired = false;
        // Never assign Current here: an explicit newer selection must survive the response.
        return true;
    }

    public bool AcceptConflict(AppearanceSaveAttempt attempt, AppearanceSnapshot snapshot)
    {
        if (!IsCurrent(attempt) || !ValidRevision(snapshot)
            || snapshot.Revision <= attempt.Update.ExpectedRevision) return false;
        normalizedConflict = !AppearancePreferences.IsValid(snapshot.Preference);
        Conflict = Normalize(snapshot);
        inFlight = null;
        return true;
    }

    public void ResolveConflict(bool keepLocal)
    {
        if (Conflict is null) throw new InvalidOperationException("No account preference conflict to resolve.");
        server = Conflict;
        // Keeping local is an explicit write even when normalization made the remote
        // display pair equal to Current. Loading alone still never repairs storage.
        explicitSaveRequired = keepLocal && normalizedConflict;
        normalizedConflict = false;
        if (!keepLocal) Current = server.Preference;
        Conflict = null;
    }

    public void SignOut()
    {
        activation = Guid.Empty;
        server = null; inFlight = null; Conflict = null;
        normalizedConflict = false; explicitSaveRequired = false;
        IsLoading = false;
        Current = guest;
    }

    private bool IsCurrent(AppearanceSaveAttempt? attempt) => activation != Guid.Empty
        && attempt is not null && attempt.Activation == activation && attempt == inFlight;
    private static bool ValidRevision(AppearanceSnapshot? value) => value is { Revision: > 0 };
    private static bool ValidSnapshot(AppearanceSnapshot? value) => ValidRevision(value)
        && AppearancePreferences.IsValid(value!.Preference);
    // Unknown stored values must not wedge loading or erase the server revision. This
    // local display fallback does not queue an automatic overwrite of the stored choice.
    private static AppearanceSnapshot Normalize(AppearanceSnapshot value) =>
        AppearancePreferences.IsValid(value.Preference) ? value : value with { Preference = AppearancePreferences.Default };
    private static void RequirePreference(AppearancePreference? value)
    {
        if (!AppearancePreferences.IsValid(value)) throw new ArgumentException("Unsupported appearance preference.");
    }
}
