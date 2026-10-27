using Swoovr.Domain;

/// <summary>
/// Security operations (internal policy §12; #89): severity levels, the 30-day data-breach assessment, closing only after
/// it, the unusual-access threshold and key rotation that re-seals to a new version while older versions stay readable.
/// </summary>
static class SecurityOperationsChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Security operations check failed: " + what); checks++; }
        var now = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

        // Incidents.
        var incident = SecurityOperationsPolicy.Open("manual", IncidentSeverity.Sev2, "Fictional suspected access", null, Guid.NewGuid(), now);
        Expect(incident.Status == IncidentStatus.Open && incident.AssessmentDue == now.AddDays(30), "assessed within 30 days");
        Expect(!SecurityOperationsPolicy.AssessmentOverdue(incident, now.AddDays(29)) && SecurityOperationsPolicy.AssessmentOverdue(incident, now.AddDays(30)), "overdue at 30 days");
        Expect(!SecurityOperationsPolicy.CanClose(incident), "not closed before the assessment");
        (incident.Notifiable, incident.AssessedAt) = (false, now.AddDays(2));
        Expect(SecurityOperationsPolicy.CanClose(incident) && !SecurityOperationsPolicy.AssessmentOverdue(incident, now.AddDays(40)), "assessed, then closable");
        incident.Status = IncidentStatus.Closed;
        Expect(!SecurityOperationsPolicy.CanClose(incident), "closed once");
        Expect(SecurityOperationsPolicy.SeverityFor("custody-chain-broken") == IncidentSeverity.Sev1 && SecurityOperationsPolicy.SeverityFor("access-log-chain-broken") == IncidentSeverity.Sev1,
            "a broken chain is critical");
        Expect(SecurityOperationsPolicy.SeverityFor("unusual-identity-access") == IncidentSeverity.Sev3 && SecurityOperationsPolicy.SeverityFor("bulk-export") == IncidentSeverity.Sev3, "alerts are medium");
        Expect(Enum.GetValues<IncidentSeverity>().All(s => SecurityOperationsPolicy.Describe(s).Length > 10), "every level is described");
        Expect(!SecurityOperationsPolicy.Unusual(3) && SecurityOperationsPolicy.Unusual(4), "more than three openings an hour is unusual");

        // Key rotation.
        var (k1, k2) = (new byte[32], new byte[32]);
        k1[0] = 1; k2[0] = 2;
        var keys = new Dictionary<byte, byte[]> { [1] = k1, [2] = k2 };
        var old = ContactHistoryPolicy.Encrypt(1, k1, "fictional value");
        var resealed = SecurityOperationsPolicy.Reseal(old, keys, 2)!;
        Expect(resealed[0] == 2 && ContactHistoryPolicy.Decrypt(keys, resealed) == "fictional value", "re-sealed under version 2, same value");
        Expect(ContactHistoryPolicy.Decrypt(keys, old) == "fictional value", "version 1 stays readable until re-sealed");
        Expect(SecurityOperationsPolicy.Reseal(resealed, keys, 2) is null, "already on the target: untouched");
        Expect(SecurityOperationsPolicy.Reseal(resealed, keys, 1)![0] == 1, "and back, for a rollback");
        var missing = false;
        try { SecurityOperationsPolicy.Reseal(old, new Dictionary<byte, byte[]> { [2] = k2 }, 2); } catch (KeyNotFoundException) { missing = true; } catch (InvalidOperationException) { missing = true; }
        Expect(missing, "a value whose key is missing is never dropped");

        Console.WriteLine($"PASS: security operations ({checks}).");
    }
}
