using System.Security.Cryptography;
using System.Text.Json;

namespace Swoovr.Domain;

/// <summary>Restricted decision metadata only; never use internal notes, reason or case UpdatedAt.</summary>
public sealed record ReportOutcomeDecision(Guid Id, string Type, DateTimeOffset At, DateTimeOffset? EndsAt = null,
    DateTimeOffset? LiftedAt = null, string? State = null, bool HideFromDiscovery = false, bool NoNewRequests = false, bool NoMessages = false);
public sealed record ReportOutcomeObservation(string Fingerprint, string? GeneralOutcome);
public sealed record ReportOutcomeTransition(string Fingerprint, long Revision, bool Notify);

/// <summary>Per-report provenance/notification checkpoint shares source report/case retention.</summary>
public sealed class ReportOutcomeState
{
    public Guid ReportId { get; set; }
    public Guid SourceCaseId { get; set; }
    public Guid SourceAccountId { get; set; } // reporter/recipient, never the reported account
    public string? Fingerprint { get; set; } // null/missing means silent legacy baseline
    public long Revision { get; set; }
}

public static class ReportOutcomePolicy
{
    public static ReportOutcomeObservation Observe(CaseStatus status, DateTimeOffset? closedAt,
        IEnumerable<ReportOutcomeDecision> decisions)
    {
        if (status == CaseStatus.Closed && closedAt is null)
            throw new InvalidOperationException("Closed report outcome lacks closure provenance.");
        var rows = decisions.OrderBy(item => item.Type, StringComparer.Ordinal).ThenBy(item => item.Id)
            .Select(item => item with { At = item.At.ToUniversalTime(), EndsAt = item.EndsAt?.ToUniversalTime(), LiftedAt = item.LiftedAt?.ToUniversalTime() }).ToArray();
        var action = rows.Any(item => item.Type is "sanction" or "removal");
        var outcome = ReportPolicy.Outcome(status, action); // exact existing general-outcome rule
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Schema = 1, Outcome = outcome, ClosedAt = status == CaseStatus.Closed ? closedAt?.ToUniversalTime() : null,
            Decisions = rows
        })));
        return new(fingerprint, outcome);
    }

    public static ReportOutcomeTransition Advance(string? prior, long revision, ReportOutcomeObservation observation)
    {
        if (revision < 0) throw new InvalidOperationException("Invalid report checkpoint revision.");
        if (prior is null) return new(observation.Fingerprint, revision, false);
        if (prior == observation.Fingerprint) return new(prior, revision, false);
        return new(observation.Fingerprint, checked(revision + 1), true);
    }
    public static string Dedupe(Guid reportId, long revision) => $"report-outcome:{reportId:N}:{revision}";
}
