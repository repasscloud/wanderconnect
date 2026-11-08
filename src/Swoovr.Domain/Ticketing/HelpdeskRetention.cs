namespace Swoovr.Domain;

/// <summary>D333 retention categories (#207). Each maps to one approved period, counted from the ticket's final closure.
/// D349 adds HomeCityCheck: the scheduled Home City check's items, kept 6 months after closure (subject to the final Privacy Notice).</summary>
public enum HelpdeskRetentionCategory { General, Billing, FinancialRecord, TrustAndSafety, Legal, Privacy, Security, HomeCityCheck }

/// <summary>Where a ticket is in the helpdesk retention ledger (Q136 / D337).</summary>
public enum HelpdeskRetentionState { Open, Retained, Disposing, Disposed, Failed }

/// <summary>
/// One row per Zammad ticket: IDs, classification and dates only, never content (D333). Swoovr's backend decides when a
/// ticket may go; Zammad deletes it through its data privacy task when asked.
/// </summary>
public sealed class HelpdeskTicketRetention
{
    public long ProviderTicketId { get; set; }
    public long ProviderCustomerId { get; set; }
    public long GroupId { get; set; }
    /// <summary>Category from the ticket's group (136.2 2a).</summary>
    public HelpdeskRetentionCategory Category { get; set; }
    /// <summary>Staff override (Admin or Legal, with a reason, audited); wins over the group category.</summary>
    public HelpdeskRetentionCategory? OverrideCategory { get; set; }
    public string? OverrideReason { get; set; }
    public Guid? OverriddenBy { get; set; }
    public DateTimeOffset? OverriddenAt { get; set; }
    /// <summary>Zammad's last_close_at while the ticket is closed or merged; null while it is open again.</summary>
    public DateTimeOffset? ClosedAt { get; set; }
    public DateTimeOffset? RetainUntil { get; set; }
    public HelpdeskRetentionState State { get; set; }
    /// <summary>The Zammad data privacy task deleting this ticket.</summary>
    public long? DeletionTaskId { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? FailureCode { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastReviewedAt { get; set; }
    public DateTimeOffset? DisposedAt { get; set; }
    /// <summary>Set once the ticket's customer has been considered for deletion after its last ticket went.</summary>
    public DateTimeOffset? CustomerReviewedAt { get; set; }
}

public static class HelpdeskRetentionPolicy
{
    /// <summary>136.2 2a: the category a Zammad group implies. Unknown groups fall back to General (24 months).</summary>
    public static HelpdeskRetentionCategory CategoryForGroup(string? group) => group switch
    {
        "Billing & Subscriptions" => HelpdeskRetentionCategory.Billing,
        "Trust & Safety" or "Trust & Safety — Restricted" or "Appeals" => HelpdeskRetentionCategory.TrustAndSafety,
        "Legal & Law Enforcement" => HelpdeskRetentionCategory.Legal,
        "Privacy & Data Protection" => HelpdeskRetentionCategory.Privacy,
        "Security" => HelpdeskRetentionCategory.Security,
        HomeCityCheckPolicy.Group => HelpdeskRetentionCategory.HomeCityCheck,
        _ => HelpdeskRetentionCategory.General,
    };

    /// <summary>D333's approved periods.</summary>
    public static (int Years, int Months) Period(HelpdeskRetentionCategory category) => category switch
    {
        HelpdeskRetentionCategory.General or HelpdeskRetentionCategory.Billing => (0, 24),
        HelpdeskRetentionCategory.FinancialRecord or HelpdeskRetentionCategory.Legal => (7, 0),
        HelpdeskRetentionCategory.TrustAndSafety or HelpdeskRetentionCategory.Security => (5, 0),
        HelpdeskRetentionCategory.Privacy => (3, 0),
        HelpdeskRetentionCategory.HomeCityCheck => (0, 6),
        _ => throw new ArgumentOutOfRangeException(nameof(category)),
    };

    public static HelpdeskRetentionCategory Effective(HelpdeskTicketRetention row) => row.OverrideCategory ?? row.Category;

    /// <summary>Retention ends this long after the final closure; null while the ticket is open (reopening recalculates).</summary>
    public static DateTimeOffset? RetainUntil(DateTimeOffset? closedAt, HelpdeskRetentionCategory category)
    {
        if (closedAt is not { } closed) return null;
        var (years, months) = Period(category);
        return closed.AddYears(years).AddMonths(months);
    }

    /// <summary>Applies a fresh observation of the ticket. A disposed or disposing row never comes back to life here.</summary>
    public static void Observe(HelpdeskTicketRetention row, long customerId, long groupId, string? groupName, bool closed, DateTimeOffset? lastCloseAt, DateTimeOffset now)
    {
        if (row.State is HelpdeskRetentionState.Disposing or HelpdeskRetentionState.Disposed) { row.LastReviewedAt = now; return; }
        (row.ProviderCustomerId, row.GroupId, row.Category) = (customerId, groupId, CategoryForGroup(groupName));
        row.ClosedAt = closed ? lastCloseAt ?? row.ClosedAt ?? now : null;
        row.RetainUntil = RetainUntil(row.ClosedAt, Effective(row));
        row.State = row.ClosedAt is null ? HelpdeskRetentionState.Open : HelpdeskRetentionState.Retained;
        if (row.State == HelpdeskRetentionState.Open) (row.Attempts, row.NextAttemptAt, row.FailureCode) = (0, null, null);
        row.LastReviewedAt = now;
    }

    public static bool Due(HelpdeskTicketRetention row, DateTimeOffset now) =>
        row.State is HelpdeskRetentionState.Retained or HelpdeskRetentionState.Failed && row.RetainUntil is { } until && until <= now
        && (row.NextAttemptAt is null || row.NextAttemptAt <= now);

    /// <summary>Disposed rows (IDs only) outlive the 35-day helpdesk backups, so a restore can be reconciled (D333).</summary>
    public static readonly TimeSpan DisposedLedgerRetention = TimeSpan.FromDays(60);
    /// <summary>D333: system and integration logs, 90 days.</summary>
    public static readonly TimeSpan IntegrationLogRetention = TimeSpan.FromDays(90);
    public const int MaximumAttempts = 10;
    public static TimeSpan Backoff(int attempts) => TimeSpan.FromMinutes(Math.Min(24 * 60, 15 * Math.Pow(2, Math.Max(0, attempts - 1))));

    /// <summary>136.3 3a: an opaque retired login that never contains the member ID.</summary>
    public static string RetiredLogin(Guid random) => "retired-" + random.ToString("N");
    public const string RetiredName = RetiredFirstName + " " + RetiredLastName;
    public const string RetiredFirstName = "Retired", RetiredLastName = "Swoovr Member";
}
