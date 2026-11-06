using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Swoovr.Domain;
using Swoovr.Infrastructure.Scanning;

/// <summary>
/// D336 (#221) and D337 (#224): the retention policy's categories, periods and reopen rule; the ClamAV client against a fake
/// clamd on loopback (clean, EICAR, limits, errors, unreachable, timeout); and the release/download rules. No real scanner.
/// </summary>
static class HelpdeskRetentionAndScanningChecks
{
    public static void Run()
    {
        var count = 0;
        void Expect(bool ok, string name) { if (!ok) throw new Exception("Helpdesk retention/scanning check failed: " + name); count++; }
        var t0 = new DateTimeOffset(2026, 11, 6, 0, 0, 0, TimeSpan.Zero);

        // D333 / 136.2 2a: group → category → period.
        Expect(HelpdeskRetentionPolicy.CategoryForGroup("General Support") == HelpdeskRetentionCategory.General, "general support");
        Expect(HelpdeskRetentionPolicy.CategoryForGroup("Billing & Subscriptions") == HelpdeskRetentionCategory.Billing, "billing");
        foreach (var g in new[] { "Trust & Safety", "Trust & Safety — Restricted", "Appeals" }) Expect(HelpdeskRetentionPolicy.CategoryForGroup(g) == HelpdeskRetentionCategory.TrustAndSafety, "T&S: " + g);
        Expect(HelpdeskRetentionPolicy.CategoryForGroup("Legal & Law Enforcement") == HelpdeskRetentionCategory.Legal, "legal");
        Expect(HelpdeskRetentionPolicy.CategoryForGroup("Privacy & Data Protection") == HelpdeskRetentionCategory.Privacy, "privacy");
        Expect(HelpdeskRetentionPolicy.CategoryForGroup("Security") == HelpdeskRetentionCategory.Security, "security");
        Expect(HelpdeskRetentionPolicy.CategoryForGroup("Partnerships & Business Development") == HelpdeskRetentionCategory.General && HelpdeskRetentionPolicy.CategoryForGroup(null) == HelpdeskRetentionCategory.General, "unknown groups are general");
        Expect(HelpdeskRetentionPolicy.RetainUntil(t0, HelpdeskRetentionCategory.General) == t0.AddMonths(24), "24 months general (not 30 days)");
        Expect(HelpdeskRetentionPolicy.RetainUntil(t0, HelpdeskRetentionCategory.Billing) == t0.AddMonths(24), "24 months billing correspondence");
        Expect(HelpdeskRetentionPolicy.RetainUntil(t0, HelpdeskRetentionCategory.FinancialRecord) == t0.AddYears(7), "7 years financial record");
        Expect(HelpdeskRetentionPolicy.RetainUntil(t0, HelpdeskRetentionCategory.TrustAndSafety) == t0.AddYears(5), "5 years T&S");
        Expect(HelpdeskRetentionPolicy.RetainUntil(t0, HelpdeskRetentionCategory.Legal) == t0.AddYears(7), "7 years legal");
        Expect(HelpdeskRetentionPolicy.RetainUntil(t0, HelpdeskRetentionCategory.Privacy) == t0.AddYears(3), "3 years privacy");
        Expect(HelpdeskRetentionPolicy.RetainUntil(t0, HelpdeskRetentionCategory.Security) == t0.AddYears(5), "5 years security");
        Expect(HelpdeskRetentionPolicy.RetainUntil(null, HelpdeskRetentionCategory.General) is null, "open tickets have no end");

        // Observation: close sets the period, reopen clears it, re-close counts from the new last close; override wins.
        var row = new HelpdeskTicketRetention { ProviderTicketId = 7, FirstSeenAt = t0 };
        HelpdeskRetentionPolicy.Observe(row, 3, 2, "General Support", false, null, t0);
        Expect(row.State == HelpdeskRetentionState.Open && row.RetainUntil is null, "open ticket tracked without an end");
        HelpdeskRetentionPolicy.Observe(row, 3, 2, "General Support", true, t0, t0);
        Expect(row.State == HelpdeskRetentionState.Retained && row.RetainUntil == t0.AddMonths(24), "closing starts 24 months");
        HelpdeskRetentionPolicy.Observe(row, 3, 2, "General Support", false, t0, t0.AddDays(10));
        Expect(row.State == HelpdeskRetentionState.Open && row.RetainUntil is null && row.ClosedAt is null, "reopening clears the period");
        HelpdeskRetentionPolicy.Observe(row, 3, 2, "General Support", true, t0.AddDays(20), t0.AddDays(20));
        Expect(row.RetainUntil == t0.AddDays(20).AddMonths(24), "re-closing recalculates from the final closure");
        HelpdeskRetentionPolicy.Observe(row, 3, 9, "Trust & Safety", true, t0.AddDays(20), t0.AddDays(21));
        Expect(row.RetainUntil == t0.AddDays(20).AddYears(5), "moving to another group reclassifies");
        row.OverrideCategory = HelpdeskRetentionCategory.FinancialRecord;
        HelpdeskRetentionPolicy.Observe(row, 3, 2, "Billing & Subscriptions", true, t0.AddDays(20), t0.AddDays(22));
        Expect(HelpdeskRetentionPolicy.Effective(row) == HelpdeskRetentionCategory.FinancialRecord && row.RetainUntil == t0.AddDays(20).AddYears(7), "staff override wins over the group");
        Expect(!HelpdeskRetentionPolicy.Due(row, t0.AddYears(6)) && HelpdeskRetentionPolicy.Due(row, t0.AddYears(8)), "due only after the period");
        row.NextAttemptAt = t0.AddYears(9);
        Expect(!HelpdeskRetentionPolicy.Due(row, t0.AddYears(8)), "a waiting retry is not due");
        row.State = HelpdeskRetentionState.Disposed;
        HelpdeskRetentionPolicy.Observe(row, 3, 2, "General Support", false, null, t0.AddYears(9));
        Expect(row.State == HelpdeskRetentionState.Disposed, "a disposed ticket never comes back to life");
        var retired = HelpdeskRetentionPolicy.RetiredLogin(Guid.NewGuid());
        Expect(retired.StartsWith("retired-") && retired.Length == 40 && retired[8..].All(char.IsAsciiHexDigitLower), "opaque retired login (136.3 3a)");
        Expect(HelpdeskRetentionPolicy.DisposedLedgerRetention > TimeSpan.FromDays(35), "disposed ledger outlives 35-day backups");

        // ClamAV replies.
        Expect(ClamAvScanner.Interpret("stream: OK", "ClamAV 1.5.4", "28172").State == AttachmentScanState.Clean, "OK is clean");
        var eicar = ClamAvScanner.Interpret("stream: Eicar-Test-Signature FOUND", null, null);
        Expect(eicar.State == AttachmentScanState.Infected && eicar.Detection == "Eicar-Test-Signature", "FOUND is infected with its name");
        Expect(ClamAvScanner.Interpret("stream: Heuristics.Limits.Exceeded.MaxFileSize FOUND", null, null) is { State: AttachmentScanState.Unsupported, Failure: "scan-limit" }, "limit alert is never cleared");
        Expect(ClamAvScanner.Interpret("INSTREAM size limit exceeded. ERROR", null, null) is { State: AttachmentScanState.Unsupported, Failure: "scan-limit" }, "stream limit is never cleared");
        Expect(ClamAvScanner.Interpret("stream: Can't allocate memory ERROR", null, null) is { State: AttachmentScanState.Failed, Failure: "scanner-error" }, "scanner error fails, not clean");
        Expect(ClamAvScanner.Interpret("", null, null).State == AttachmentScanState.Failed && ClamAvScanner.Interpret("stream: ", null, null).State == AttachmentScanState.Failed, "empty reply fails");
        Expect(ClamAvScanner.Version("ClamAV 1.5.4/28172/Mon Nov  2 06:24:10 2026") == ("ClamAV 1.5.4", "28172"), "engine and signature versions");

        // Fake clamd on loopback.
        MalwareScanResult Scan(Func<string, byte[], string?> answer, byte[] content, TimeSpan? timeout = null, long max = 160L * 1024 * 1024)
        {
            var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var server = Task.Run(async () =>
            {
                for (var i = 0; i < 2; i++)
                {
                    using var client = await listener.AcceptTcpClientAsync();
                    var stream = client.GetStream();
                    var command = new List<byte>();
                    var one = new byte[1];
                    while (await stream.ReadAsync(one) == 1 && one[0] != 0) command.Add(one[0]);
                    var name = Encoding.ASCII.GetString(command.ToArray());
                    var received = new MemoryStream();
                    if (name == "zINSTREAM")
                    {
                        var length = new byte[4];
                        while (true)
                        {
                            await stream.ReadExactlyAsync(length);
                            var size = BinaryPrimitives.ReadUInt32BigEndian(length);
                            if (size == 0) break;
                            var chunk = new byte[size]; await stream.ReadExactlyAsync(chunk); received.Write(chunk);
                        }
                    }
                    var reply = answer(name, received.ToArray());
                    if (reply is null) { await Task.Delay(3000); return; }
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(reply + "\0"));
                }
            });
            try { return new ClamAvScanner(new("127.0.0.1", port, timeout ?? TimeSpan.FromSeconds(10), max)).ScanAsync(new MemoryStream(content)).GetAwaiter().GetResult(); }
            finally { listener.Stop(); try { server.Wait(5000); } catch (AggregateException) { } }
        }
        var eicarText = "X5O!P%@AP[4\\PZX54(P^)7CC)7}$EICAR-STANDARD-ANTIVIRUS-TEST-FILE!$H+H*";
        string? Clamd(string command, byte[] body) => command == "zVERSION" ? "ClamAV 1.5.4/28172/Mon Nov  2 06:24:10 2026"
            : Encoding.ASCII.GetString(body).Contains("EICAR-STANDARD") ? "stream: Eicar-Test-Signature FOUND" : "stream: OK";
        var clean = Scan(Clamd, Encoding.UTF8.GetBytes("Fictional support text."));
        Expect(clean.State == AttachmentScanState.Clean && clean.EngineVersion == "ClamAV 1.5.4" && clean.SignatureVersion == "28172", "fake clamd: clean with versions");
        var big = new byte[200 * 1024]; Random.Shared.NextBytes(big);
        Expect(Scan(Clamd, big).State == AttachmentScanState.Clean, "fake clamd: multi-chunk stream reassembled");
        Expect(Scan(Clamd, Encoding.ASCII.GetBytes(eicarText)) is { State: AttachmentScanState.Infected, Detection: "Eicar-Test-Signature" }, "fake clamd: EICAR infected");
        Expect(Scan(Clamd, big, max: 100 * 1024) is { State: AttachmentScanState.Unsupported, Failure: "scan-limit" }, "over the client limit: unsupported, not sent");
        Expect(Scan((c, _) => c == "zVERSION" ? "ClamAV 1.5.4/28172/x" : null, Encoding.UTF8.GetBytes("x"), TimeSpan.FromSeconds(1)) is { State: AttachmentScanState.Failed, Failure: "scan-timeout" }, "slow scanner: timeout fails");
        var closed = new TcpListener(IPAddress.Loopback, 0); closed.Start(); var unused = ((IPEndPoint)closed.LocalEndpoint).Port; closed.Stop();
        var down = new ClamAvScanner(new("127.0.0.1", unused)).ScanAsync(new MemoryStream([1, 2, 3])).GetAwaiter().GetResult();
        Expect(down is { State: AttachmentScanState.Failed, Failure: "scanner-unavailable" }, "unreachable scanner fails closed");
        Expect(!new ClamAvScanner(new("127.0.0.1", unused)).HealthyAsync().GetAwaiter().GetResult(), "unreachable scanner unhealthy");
        Expect(new UnconfiguredAttachmentScanner().ScanAsync(Stream.Null).GetAwaiter().GetResult() is { State: AttachmentScanState.Failed, Failure: "scanner-unconfigured" }, "unconfigured scanner never clears");

        // Release and download rules.
        Expect(!AttachmentScanPolicy.Downloadable(null, t0), "no verdict: not downloadable");
        Expect(AttachmentScanPolicy.Downloadable(new() { State = AttachmentScanState.Clean, ScannedAt = t0 }, t0.AddHours(1)), "fresh clean verdict downloadable");
        Expect(!AttachmentScanPolicy.Downloadable(new() { State = AttachmentScanState.Clean, ScannedAt = t0 }, t0.AddDays(2)), "stale clean verdict rescanned");
        foreach (var state in new[] { AttachmentScanState.Infected, AttachmentScanState.Unsupported, AttachmentScanState.Failed, AttachmentScanState.Pending, AttachmentScanState.Scanning })
            Expect(!AttachmentScanPolicy.Downloadable(new() { State = state, ScannedAt = t0 }, t0), "only clean is downloadable: " + state);
        SupportAttachmentHold H(AttachmentScanState s, int attempts = 0) => new() { State = s, Attempts = attempts };
        Expect(AttachmentScanPolicy.GroupSettled([H(AttachmentScanState.Clean), H(AttachmentScanState.Infected)]), "clean + infected settles (clean files go, infected stay)");
        Expect(!AttachmentScanPolicy.GroupSettled([H(AttachmentScanState.Clean), H(AttachmentScanState.Pending)]), "a pending file holds the message");
        Expect(!AttachmentScanPolicy.GroupSettled([H(AttachmentScanState.Clean), H(AttachmentScanState.Failed, 3)]), "a retrying failure holds the message");
        Expect(AttachmentScanPolicy.GroupSettled([H(AttachmentScanState.Clean), H(AttachmentScanState.Failed, AttachmentScanPolicy.MaximumAttempts)]), "a permanently failed file stops holding the rest");
        Expect(new MalwareScanResult(AttachmentScanState.Failed, "clamav", null, null).Final == false, "failure is never final");
        Console.WriteLine($"PASS helpdesk retention and attachment scanning ({count} checks; fake clamd on loopback, no real scanner or helpdesk).");
    }
}
