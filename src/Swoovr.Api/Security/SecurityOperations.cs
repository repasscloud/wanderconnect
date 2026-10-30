using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Security;

/// <summary>
/// Security alerts (internal policy §12; #89): unusual identity openings and bulk exports go to the escalation contacts
/// (D159) with ids only, and open a security incident. A broken hash chain opens a critical one. Alerting never blocks
/// the action that raised it.
/// </summary>
public sealed class SecurityAlerts(IServiceProvider services, IConfiguration configuration, IEmailSender email, ILogger<SecurityAlerts> log)
{
    private SwoovrDbContext db => services.GetRequiredService<SwoovrDbContext>();

    /// <summary>
    /// On each attempt to open an identity record (found or not: probing for records counts too): more than three by one
    /// person within the hour is unusual. Earlier attempts come from the sensitive-access log; this one isn't logged yet.
    /// </summary>
    public async Task IdentityOpenedAsync(Guid staff, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var recent = 1 + await db.SensitiveAccessLog.CountAsync(e => e.ActorAccountId == staff && e.Resource == "identity" && e.At > now.AddHours(-1), cancellationToken);
        if (!SecurityOperationsPolicy.Unusual(recent)) return;
        await RaiseAsync("unusual-identity-access", $"Staff {staff} opened {recent} verified identity records in the last hour.", staff, now, cancellationToken);
    }

    public Task BulkExportAsync(Guid staff, Guid hold, Guid approval, DateTimeOffset now, CancellationToken cancellationToken) =>
        RaiseAsync("bulk-export", $"Staff {staff} asked to export hold {hold} in bulk (approval {approval}).", staff, now, cancellationToken, dedupe: false);

    /// <summary>Once per broken chain while its incident is open (the summary names the chain).</summary>
    public Task ChainBrokenAsync(string kind, string what, DateTimeOffset now, CancellationToken cancellationToken) =>
        RaiseAsync(kind, what, null, now, cancellationToken, dedupe: false, oncePerSummary: true);

    /// <summary>Opens the incident (once an hour per kind and person) and emails the escalation contacts with ids only.</summary>
    private async Task RaiseAsync(string kind, string summary, Guid? staff, DateTimeOffset now, CancellationToken cancellationToken, bool dedupe = true, bool oncePerSummary = false)
    {
        try
        {
            if (dedupe && await db.SecurityIncidents.AnyAsync(i => i.Kind == kind && i.SubjectStaffId == staff && i.Status != IncidentStatus.Closed && i.DetectedAt > now.AddHours(-1), cancellationToken))
                return;
            if (oncePerSummary && await db.SecurityIncidents.AnyAsync(i => i.Kind == kind && i.Summary == summary && i.Status != IncidentStatus.Closed, cancellationToken))
                return;
            var incident = SecurityOperationsPolicy.Open(kind, SecurityOperationsPolicy.SeverityFor(kind), summary, staff, null, now);
            db.SecurityIncidents.Add(incident);
            Audit.Record(db, now, null, "system", "security.alert", "security-incident", incident.Id.ToString(), kind);
            await db.SaveChangesAsync(cancellationToken);
            foreach (var address in new[] { configuration["Moderation:EscalationEmail"], configuration["Moderation:EscalationBackupEmail"] }
                         .Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
                await email.SendAsync(new EmailMessage(address, $"Swoovr security alert ({incident.Severity}): {kind}",
                    $"""
                    A security alert needs review.

                    Incident: {incident.Id}
                    Severity: {incident.Severity}
                    Kind: {kind}
                    {summary}

                    Open the Swoovr admin app (Security incidents). If this may be an eligible data breach, start the assessment now; it must finish within 30 days.
                    """), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            log.LogError(exception, "Security alert {Kind} not raised.", kind);
        }
    }
}

/// <summary>Security incidents (policy §12; procedure §12): the security and legal officer and the root admin.</summary>
public static class SecurityEndpoints
{
    public static void Map(WebApplication app)
    {
        var security = app.MapGroup("/api/admin/security").RequireAuthorization();

        security.MapGet("/incidents", async (string? status, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (_, denied) = await OfficerAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var now = clock.GetUtcNow();
            var query = status == "closed" ? accounts.Db!.SecurityIncidents.Where(i => i.Status == IncidentStatus.Closed) : accounts.Db!.SecurityIncidents.Where(i => i.Status != IncidentStatus.Closed);
            var rows = await query.OrderBy(i => i.Severity).ThenByDescending(i => i.DetectedAt).Take(200).ToListAsync(cancellationToken);
            return Results.Ok(rows.Select(i => View(i, now)).ToList());
        });

        security.MapPost("/incidents", async (NewSecurityIncident request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await OfficerAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            if (!Enum.TryParse<IncidentSeverity>(request.Severity, true, out var severity) || !Enum.IsDefined(severity)
                || string.IsNullOrWhiteSpace(request.Summary) || request.Summary.Length > SecurityOperationsPolicy.SummaryMax)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Choose a severity (sev1–sev4) and describe what happened.");
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            var incident = SecurityOperationsPolicy.Open("manual", severity, request.Summary.Trim(), null, current!.Account!.Id, now);
            db.SecurityIncidents.Add(incident);
            Audit.Record(db, now, current.Account.Id, "account", "security.opened", "security-incident", incident.Id.ToString(), Wire(severity));
            await db.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/admin/security/incidents/{incident.Id}", View(incident, now));
        });

        security.MapPost("/incidents/{id:guid}/assess", async (Guid id, IncidentAssessment request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, incident, denied) = await IncidentAsync(user, accounts, id, cancellationToken);
            if (denied is not null) return denied;
            if (request.Notifiable is null || string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > SecurityOperationsPolicy.SummaryMax)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Record whether it is an eligible data breach, and why.");
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            (incident!.Notifiable, incident.AssessmentReason, incident.AssessedBy, incident.AssessedAt) = (request.Notifiable, request.Reason.Trim(), current!.Account!.Id, now);
            Audit.Record(db, now, current.Account.Id, "account", "security.assessed", "security-incident", id.ToString(), request.Notifiable == true ? "notifiable" : "not-notifiable");
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(View(incident, now));
        });

        security.MapPost("/incidents/{id:guid}/{step:regex(^(contain|close)$)}", async (Guid id, string step, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, incident, denied) = await IncidentAsync(user, accounts, id, cancellationToken);
            if (denied is not null) return denied;
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            if (incident!.Status == IncidentStatus.Closed) return Problem(StatusCodes.Status409Conflict, ProblemCodes.InvalidRequest, "Already closed.");
            if (step == "contain") (incident.Status, incident.ContainedAt) = (IncidentStatus.Contained, now);
            else
            {
                if (!SecurityOperationsPolicy.CanClose(incident)) return Problem(StatusCodes.Status409Conflict, ProblemCodes.InvalidRequest, "Record the data-breach assessment before closing.");
                (incident.Status, incident.ClosedAt, incident.ClosedBy) = (IncidentStatus.Closed, now, current!.Account!.Id);
            }
            Audit.Record(db, now, current!.Account!.Id, "account", "security." + (step == "contain" ? "contained" : "closed"), "security-incident", id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(View(incident, now));
        });
    }

    private static async Task<(CurrentAccount? Current, IResult? Denied)> OfficerAsync(ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken)
    {
        var current = await accounts.ResolveAsync(user, cancellationToken);
        if (current is null) return (null, Results.Unauthorized());
        if (current.Account is null) return (null, Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.PersistenceUnavailable, "Accounts are not available."));
        return current.IsRootAdmin || current.Allows(AccountRole.Legal) ? (current, null) : (null, Results.Forbid());
    }

    private static async Task<(CurrentAccount? Current, SecurityIncident? Incident, IResult? Denied)> IncidentAsync(ClaimsPrincipal user, AccountResolver accounts, Guid id,
        CancellationToken cancellationToken)
    {
        var (current, denied) = await OfficerAsync(user, accounts, cancellationToken);
        if (denied is not null) return (null, null, denied);
        var incident = await accounts.Db!.SecurityIncidents.FindAsync([id], cancellationToken);
        return incident is null ? (null, null, Results.NotFound()) : (current, incident, null);
    }

    private static SecurityIncidentView View(SecurityIncident i, DateTimeOffset now) =>
        new(i.Id, Wire(i.Severity), SecurityOperationsPolicy.Describe(i.Severity), Wire(i.Status), i.Kind, i.Summary, i.SubjectStaffId, i.DetectedAt, i.AssessmentDue,
            SecurityOperationsPolicy.AssessmentOverdue(i, now), i.Notifiable, i.AssessmentReason, i.AssessedAt, i.ContainedAt, i.ClosedAt);
}

/// <summary>
/// `keys rotate` (#89): re-seals every value of a key set under a newer configured version. Older versions stay readable
/// until re-sealed, so the command can run while the hosts serve; it pages through rows and saves as it goes.
/// Sets: contact-history (contact_histories), evidence (evidence_items), police (reports.police_reference, evidence keys),
/// identity (restricted.verified_identities). Nothing is logged but counts.
/// </summary>
public sealed class KeyRotation(IServiceProvider services, ContactHistoryKeys contact, Evidence.EvidenceKeys evidence, Verification.IdentityKeys identity)
{
    // Resolved on use: without a database the host still starts (fail closed elsewhere).
    private SwoovrDbContext db => services.GetRequiredService<SwoovrDbContext>();

    public static readonly IReadOnlyList<string> Sets = ["contact-history", "evidence", "police", "identity"];

    public IReadOnlyDictionary<byte, byte[]> Keys(string set) => set switch
    {
        "contact-history" => contact.Encryption,
        "evidence" or "police" => evidence.Encryption,
        _ => identity.Encryption,
    };

    /// <remarks>Unreadable: values under a key version that isn't configured; left exactly as they were.</remarks>
    public sealed record Run(string Set, byte Target, int Resealed, int Already, int Unreadable);

    public async Task<Run> RotateAsync(string set, byte? to, CancellationToken cancellationToken)
    {
        var keys = Keys(set);
        if (keys.Count == 0) throw new InvalidOperationException($"No {set} keys are configured.");
        var target = to ?? keys.Keys.Max();
        if (!keys.ContainsKey(target)) throw new InvalidOperationException($"Key version {target} of {set} isn't configured.");
        var (resealed, already, unreadable) = (0, 0, 0);
        void Apply(byte[] value, Action<byte[]> set)
        {
            if (value.Length > 0 && !keys.ContainsKey(value[0])) { unreadable++; return; }
            if (SecurityOperationsPolicy.Reseal(value, keys, target) is { } next) { set(next); resealed++; } else already++;
        }
        switch (set)
        {
            case "contact-history":
                foreach (var row in await db.ContactHistories.ToListAsync(cancellationToken)) Apply(row.EncryptedValue, v => row.EncryptedValue = v);
                foreach (var row in await db.EmailRetirementOperations.ToListAsync(cancellationToken)) Apply(row.EncryptedCommand, v => row.EncryptedCommand = v);
                foreach (var row in await db.TrustedContacts.ToListAsync(cancellationToken)) Apply(row.DestinationEncrypted, v => row.DestinationEncrypted = v);
                break;
            case "evidence":
                foreach(var row in await db.Set<TextHarmIntent>().Where(r=>r.SealedText!=null).ToListAsync(cancellationToken))Apply(row.SealedText!,value=>row.SealedText=value);
                foreach(var row in await db.Set<TextHarmReview>().Where(r=>r.SealedEvidence!=null).ToListAsync(cancellationToken))Apply(row.SealedEvidence!,value=>row.SealedEvidence=value);
                foreach(var row in await db.Set<ContentRemoval>().Where(r=>r.SealedSnapshot!=null).ToListAsync(cancellationToken))Apply(row.SealedSnapshot!,value=>row.SealedSnapshot=value);
                // Evidence items are immutable in content: the SHA-256 is of the plain JSON, so re-sealing never changes it.
                var ids = await db.EvidenceItems.Select(i => i.Id).ToListAsync(cancellationToken);
                foreach (var page in ids.Chunk(200))
                {
                    foreach (var item in await db.EvidenceItems.Where(i => page.Contains(i.Id)).ToListAsync(cancellationToken)) Apply(item.Sealed, v => item.Sealed = v);
                    await db.SaveChangesAsync(cancellationToken);
                    db.ChangeTracker.Clear();
                }
                break;
            case "police":
                foreach (var report in await db.Reports.Where(r => r.PoliceReference != null).ToListAsync(cancellationToken)) Apply(report.PoliceReference!, v => report.PoliceReference = v);
                break;
            default:
                foreach (var record in await db.VerifiedIdentities.ToListAsync(cancellationToken)) Apply(record.Sealed, v => record.Sealed = v);
                break;
        }
        Audit.Record(db, DateTimeOffset.UtcNow, null, "system", "keys.rotated", null, null, $"{set}:v{target}:resealed:{resealed}");
        await db.SaveChangesAsync(cancellationToken);
        return new(set, target, resealed, already, unreadable);
    }

    /// <summary>How many values each key version seals in a set.</summary>
    public async Task<Dictionary<byte, int>> StatusAsync(string set, CancellationToken cancellationToken)
    {
        var firsts = set switch
        {
            "contact-history" => await db.ContactHistories.Select(h => h.EncryptedValue).Concat(db.EmailRetirementOperations.Select(o=>o.EncryptedCommand)).Concat(db.TrustedContacts.Select(c=>c.DestinationEncrypted)).ToListAsync(cancellationToken),
            "evidence" => await db.EvidenceItems.Select(i => i.Sealed).Concat(db.Set<TextHarmIntent>().Where(r=>r.SealedText!=null).Select(r=>r.SealedText!)).Concat(db.Set<TextHarmReview>().Where(r=>r.SealedEvidence!=null).Select(r=>r.SealedEvidence!)).Concat(db.Set<ContentRemoval>().Where(r=>r.SealedSnapshot!=null).Select(r=>r.SealedSnapshot!)).ToListAsync(cancellationToken),
            "police" => (await db.Reports.Where(r => r.PoliceReference != null).Select(r => r.PoliceReference).ToListAsync(cancellationToken)).Select(v => v!).ToList(),
            _ => await db.VerifiedIdentities.Select(i => i.Sealed).ToListAsync(cancellationToken),
        };
        return firsts.Where(v => v.Length > 0).GroupBy(v => v[0]).ToDictionary(g => g.Key, g => g.Count());
    }
}
