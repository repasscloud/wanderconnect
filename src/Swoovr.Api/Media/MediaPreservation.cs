using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Media;

namespace Swoovr.Api.Media;

public sealed record MediaRecoveryRun(int Completed, int Waiting, int ReviewRequired);

public sealed partial class MediaPipeline
{
    private sealed class PreservationPause : Exception;
    private sealed class PreservationReview(string code) : InvalidOperationException { public string Code { get; } = code; }

    private void PausePreservation(string stage)
    {
        if (services.GetRequiredService<IHostEnvironment>().IsDevelopment()
            && services.GetRequiredService<IConfiguration>()["Media:Preservation:Fault"] == stage) throw new PreservationPause();
    }

    /// <summary>Recover committed refusal/work without ever asking the screener to inspect blocked/sealed bytes.</summary>
    public async Task RecoverBlockedAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var initial = await db.MediaItems.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (initial is not { Status: MediaStatus.Blocked } || initial.PreservationCompletedAt is not null) return;
        await using var lease = await AccountLifecycleLease.AcquireMediaAsync(db, initial.AccountId, cancellationToken);
        {
            await using var coverage = await Retention.PreservationLease.ExclusiveAsync(db, cancellationToken);
            var item = await db.MediaItems.SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
            if (item is null) return;
            await db.Entry(item).ReloadAsync(cancellationToken);
            if (item.Status != MediaStatus.Blocked || item.PreservationCompletedAt is not null) return;
            item.PreservationLegacy ??= true;
            item.PreservationPendingAt ??= now;
            item.PreservationAttempts = (item.PreservationAttempts ?? 0) + 1;
            item.NextPreservationAt = MediaRecoveryPolicy.NextAttempt(item.PreservationAttempts.Value, now);
            item.PreservationError = null;
            await db.SaveChangesAsync(cancellationToken);
            try
            {
                PausePreservation("blocked-save");
                var @case = item.CaseId is { } caseId ? await db.ModerationCases.FindAsync([caseId], cancellationToken) : null;
                if (@case is null)
                {
                    @case = await db.ModerationCases.SingleOrDefaultAsync(c => c.SubjectAccountId == item.AccountId
                        && (c.Status == CaseStatus.Open || c.Status == CaseStatus.UnderReview), cancellationToken);
                    if (@case is null) db.ModerationCases.Add(@case = new ModerationCase
                    {
                        Id = Guid.NewGuid(), SubjectAccountId = item.AccountId, Status = CaseStatus.Open, CreatedAt = now, SystemFlag = "media-known-content",
                    });
                    (@case.Queue, @case.Priority, @case.UpdatedAt) = (CaseQueue.Admin, ReportPriority.Urgent, now);
                    (@case.EscalatedAt, @case.AcknowledgedAt, @case.AcknowledgedBy) = (null, null, null);
                    item.CaseId = @case.Id;
                    await db.SaveChangesAsync(cancellationToken);
                }
                var captured = false;
                var needsReview = false;
                var step = "hold-unavailable";
                try
                {
                    var evidence = services.GetRequiredService<Evidence.EvidencePreservation>();
                    var hold = item.HoldId is { } holdId ? await db.LegalHolds.FindAsync([holdId], cancellationToken) : null;
                    if (hold is null)
                    {
                        // Exact incident provenance includes Released: never resurrect an original released hold.
                        hold = await db.LegalHolds.SingleOrDefaultAsync(h => h.SourceMediaId == item.Id, cancellationToken);
                        if (hold is null && item.PreservationLegacy == true)
                        {
                            // Old BlockAsync used the identical timestamp for the block and its new hold.
                            // Do not adopt an unrelated earlier hold merely because its case/account matches.
                            var legacy = await db.LegalHolds.Where(h => h.SourceMediaId == null && h.CaseId == @case.Id && h.Source == "media-screening" && (h.PlacedAt == item.ScreenedAt || h.PlacedAt == item.ReviewedAt)
                                && db.LegalHoldScopes.Any(s => s.HoldId == h.Id && s.Kind == HoldScopeKind.Account && s.TargetId == item.AccountId))
                                .Take(2).ToListAsync(cancellationToken);
                            if (legacy.Count > 1) throw new PreservationReview("hold-origin-ambiguous");
                            hold = legacy.SingleOrDefault();
                            if (hold is null && await db.LegalHolds.AnyAsync(h => h.CaseId == @case.Id && h.Source == "media-screening", cancellationToken))
                                throw new PreservationReview("hold-origin-unresolved");
                            if (hold is not null) { hold.SourceMediaId = item.Id; await db.SaveChangesAsync(cancellationToken); }
                        }
                        if (hold is null)
                        {
                            hold = await Evidence.LegalHoldEndpoints.PlaceAsync(db, evidence, AccountRole.Unknown, null,
                                $"Known-content match on an uploaded photo, case {@case.Id}", [item.AccountId], [], null, null, null,
                                "Release only after legal review of the reporting and preservation obligations.", @case.Id, "media-screening", now, cancellationToken, deferCapture: true, sourceMediaId: item.Id);
                            PausePreservation("hold-create");
                        }
                        item.HoldId = hold.Id;
                        await db.SaveChangesAsync(cancellationToken);
                    }
                    if (hold.CaseId != @case.Id || hold.SourceMediaId is { } origin && origin != item.Id
                        || !await db.LegalHoldScopes.AnyAsync(s => s.HoldId == hold.Id && s.Kind == HoldScopeKind.Account && s.TargetId == item.AccountId, cancellationToken))
                        throw new PreservationReview("hold-scope-mismatch");
                    if (hold.Status == LegalHoldStatus.Released)
                    {
                        needsReview = true;
                        item.PreservationError = "hold-released"; // An old recovery must never recreate a legally released hold.
                    }
                    else
                    {
                        await coverage.DisposeAsync(); // Valid hold/case links commit before object and capture work; never upgrade a shared barrier.
                        step = "seal-unavailable";
                        await SealBlockedAsync(item, cancellationToken);
                        await db.SaveChangesAsync(cancellationToken);
                        PausePreservation("object-sealed");
                        step = "capture-unavailable";
                        if (services.GetRequiredService<IHostEnvironment>().IsDevelopment()
                            && services.GetRequiredService<IConfiguration>()["Media:Preservation:Fault"] == "capture-error") throw new InvalidOperationException("development-capture-failure");
                        await evidence.CaptureAsync(hold, null, now, cancellationToken);
                        await db.SaveChangesAsync(cancellationToken);
                        captured = await db.EvidenceItems.AnyAsync(e => e.HoldId == hold.Id && e.Kind == "media-item" && e.RecordId == item.Id.ToString(), cancellationToken);
                        if (!captured) item.PreservationError = "capture-unavailable";
                        PausePreservation("capture-complete");
                    }
                }
                catch (PreservationPause) { throw; }
                catch (PreservationReview review) { item.PreservationError = review.Code; needsReview = true; }
                catch (Exception error) when (error is IOException or HttpRequestException or InvalidOperationException or CryptographicException or FormatException or DbUpdateException)
                {
                    // Only machine stage/type and ids; object bytes/provider bodies never enter logs.
                    item.PreservationError = step;
                    log.LogWarning("Media {Id} preservation remains pending at {Stage} ({Error}).", item.Id, step, error.GetType().Name);
                    // Failed SaveChanges can leave added capture/custody entries; do not commit them through the alert save.
                    foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added && e.Entity is EvidenceItem or CustodyEvent).ToList()) entry.State = EntityState.Detached;
                }
                await coverage.DisposeAsync();
                var alerts = await EscalateBlockedAsync(item, @case, now, cancellationToken);
                if (captured && alerts == "accepted")
                {
                    item.PreservationCompletedAt = now; item.NextPreservationAt = null; item.PreservationError = null;
                    Audit.Record(db, now, null, "system", "media.preservation-completed", "media", item.Id.ToString());
                }
                else if (needsReview || captured && alerts is "unknown" or "rejected")
                {
                    item.NextPreservationAt = null;
                    item.PreservationError ??= "alert-" + alerts;
                }
                else item.PreservationError ??= "alert-unavailable";
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (PreservationPause)
            {
                item.PreservationError = "development-pause"; item.NextPreservationAt = now.AddMinutes(2);
                await db.SaveChangesAsync(cancellationToken);
            }
            await AttachmentChangedAsync(item, now, cancellationToken);
            await Profiles.ProfilePrivacy.CaptureAsync(db, item.AccountId, now, cancellationToken);
        }
    }

    private async Task SealBlockedAsync(MediaItem item, CancellationToken cancellationToken)
    {
        if (Store is null) throw new InvalidOperationException("store-unavailable");
        var keys = services.GetRequiredService<Evidence.EvidenceKeys>();
        if (keys.CurrentVersion is not { } version) throw new InvalidOperationException("key-unavailable");
        var target = MediaKeys.Evidence(item.Id);
        var sealedBytes = await Store.GetAsync(target, cancellationToken);
        if (sealedBytes is null)
        {
            if (item.ObjectKey is null || item.ObjectKey.StartsWith("evidence/", StringComparison.Ordinal)) throw new IOException("evidence-unavailable");
            var data = await Store.GetAsync(item.ObjectKey, cancellationToken) ?? throw new IOException("source-unavailable");
            if (!MediaPolicy.Hash(data).SequenceEqual(item.Sha256)) throw new CryptographicException("source-changed");
            item.PreservationSourceKey ??= item.ObjectKey;
            sealedBytes = EvidenceSeal.Seal(version, keys.Encryption[version], Convert.ToBase64String(data));
            await Store.PutAsync(target, sealedBytes, "application/octet-stream", cancellationToken);
        }
        // Restricted integrity verification only. No view/export/scan of this material is enabled.
        var verified = Convert.FromBase64String(EvidenceSeal.Open(keys.Encryption, sealedBytes));
        if (!MediaPolicy.Hash(verified).SequenceEqual(item.Sha256)) throw new CryptographicException("evidence-changed");
        CryptographicOperations.ZeroMemory(verified);
        if (item.ObjectKey != target) item.PreservationSourceKey ??= item.ObjectKey;
        item.ObjectKey = target;
        await db.SaveChangesAsync(cancellationToken); // Commit canonical evidence location before source deletion.
        await using var disposalBarrier = await Retention.PreservationLease.SharedAsync(db, cancellationToken);
        if (item.PreservationSourceKey is { } source && source != target)
        {
            if (!await Store.DeleteAsync(source, cancellationToken)) throw new IOException("source-delete-failed");
            item.PreservationSourceKey = null;
            await db.SaveChangesAsync(cancellationToken);
        }
        // A process can stop after writing a publication copy but before committing its location.
        // Under the shared lifecycle lease neither deterministic clear location may survive sealing.
        foreach (var clear in new[] { MediaKeys.Quarantine(item.Id), MediaKeys.Photo(item.Id) })
            if (!await Store.DeleteAsync(clear, cancellationToken)) throw new IOException("clear-copy-delete-failed");
    }

    private async Task<string> EscalateBlockedAsync(MediaItem item, ModerationCase @case, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var config = services.GetRequiredService<IConfiguration>();
        var addresses = new[] { (Slot: "primary", Address: config["Moderation:EscalationEmail"]), (Slot: "backup", Address: config["Moderation:EscalationBackupEmail"]) }
            .Where(a => !string.IsNullOrWhiteSpace(a.Address)).Select(a => (a.Slot, Address: a.Address!.Trim()))
            .DistinctBy(a => a.Address, StringComparer.OrdinalIgnoreCase).ToList();
        if (addresses.Count == 0) return "retry";
        var legacyRecorded = item.PreservationLegacy == true && @case.EscalatedAt is not null
            && await db.AuditEntries.AnyAsync(a => a.Action == "case.escalated" && a.TargetId == @case.Id.ToString(), cancellationToken);
        var outcomes = new List<string>();
        foreach (var (slot, address) in addresses)
        {
            var delivery = await db.MediaEscalationDeliveries.FindAsync([item.Id, slot], cancellationToken);
            if (delivery is null)
            {
                db.MediaEscalationDeliveries.Add(delivery = new MediaEscalationDelivery { MediaId = item.Id, Slot = slot, Status = legacyRecorded ? "legacy-recorded" : "pending", UpdatedAt = now });
                await db.SaveChangesAsync(cancellationToken);
            }
            if (delivery.Status == "attempting") { delivery.Status = "unknown"; await db.SaveChangesAsync(cancellationToken); }
            if (delivery.Status is "pending" or "retry")
            {
                delivery.Status = "attempting"; delivery.Attempts++; delivery.UpdatedAt = now;
                await db.SaveChangesAsync(cancellationToken);
                EmailOutcome outcome;
                try
                {
                    outcome = await services.GetRequiredService<IEmailSender>().SendOutcomeAsync(new EmailMessage(address,
                        "Swoovr serious safety incident: acknowledgement needed",
                        $"A Serious Safety Incident needs review. If anyone may be in immediate danger, act before routine work.\n\nCase: {@case.Id}\nCategory: media-known-content\nQueue: Admin\n\nReview preservation status in the Swoovr admin app and acknowledge the case. No image or member details are included."), cancellationToken);
                }
                catch (Exception error) when (error is HttpRequestException or IOException or InvalidOperationException or OperationCanceledException) { outcome = new("unknown"); }
                delivery.Status = outcome.Status is "accepted" or "retry" or "rejected" ? outcome.Status : "unknown";
                using var persist = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await db.SaveChangesAsync(persist.Token);
            }
            outcomes.Add(delivery.Status);
        }
        if (outcomes.Any(s => s is "accepted" or "legacy-recorded") && @case.EscalatedAt is null)
        {
            @case.EscalatedAt = now;
            Audit.Record(db, now, null, "system", "case.escalated", "moderation-case", @case.Id.ToString(), "media-preservation");
            await db.SaveChangesAsync(cancellationToken);
        }
        return outcomes.All(s => s == "accepted") ? "accepted" : outcomes.Any(s => s is "unknown" or "legacy-recorded") ? "unknown" : outcomes.Contains("rejected") ? "rejected" : "retry";
    }

    /// <summary>Trusted operator command only: reconcile an uncertain result from independent provider evidence.</summary>
    public async Task<bool> ResolveMediaAlertAsync(Guid id, string slot, bool accepted, string note, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (slot is not ("primary" or "backup") || note.Trim().Length is < 20 or > 2000) return false;
        var item = await db.MediaItems.AsNoTracking().SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null || item.PreservationCompletedAt is not null) return false;
        var key = BinaryPrimitives.ReadInt32BigEndian(SHA256.HashData(item.AccountId.ToByteArray()));
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({0x4D454449}, {key})", cancellationToken);
        var delivery = await db.MediaEscalationDeliveries.FindAsync([id, slot], cancellationToken);
        if (delivery is null || delivery.Status is not ("unknown" or "rejected" or "legacy-recorded")) return false;
        delivery.Status = accepted ? "accepted" : "retry";
        delivery.ResolvedAt = now; delivery.ResolutionActor = "cli"; delivery.ResolutionNote = note.Trim(); delivery.UpdatedAt = now;
        var tracked = await db.MediaItems.SingleAsync(i => i.Id == id, cancellationToken);
        tracked.NextPreservationAt = now;
        Audit.Record(db, now, null, "cli", "media.alert-reconciled", "media", id.ToString(), slot + ":" + delivery.Status);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<MediaRecoveryRun> RecoverBlockedSweepAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var due = await db.MediaItems.AsNoTracking().Where(i => i.Status == MediaStatus.Blocked && i.PreservationCompletedAt == null && i.ObjectKey != null
            && (i.PreservationPendingAt != null && i.NextPreservationAt <= now || i.PreservationPendingAt == null && (i.CaseId == null || i.HoldId == null
                || !db.ModerationCases.Any(c => c.Id == i.CaseId && c.EscalatedAt != null)
                || !db.EvidenceItems.Any(e => e.HoldId == i.HoldId && e.Kind == "media-item" && e.RecordId == i.Id.ToString()))))
            .OrderBy(i => i.CreatedAt).Select(i => i.Id).Take(100).ToListAsync(cancellationToken);
        foreach (var id in due) { await RecoverBlockedAsync(id, now, cancellationToken); db.ChangeTracker.Clear(); }
        return new(await db.MediaItems.CountAsync(i => i.PreservationCompletedAt != null, cancellationToken),
            await db.MediaItems.CountAsync(i => i.PreservationPendingAt != null && i.PreservationCompletedAt == null && i.NextPreservationAt != null, cancellationToken),
            await db.MediaItems.CountAsync(i => i.PreservationPendingAt != null && i.PreservationCompletedAt == null && i.NextPreservationAt == null, cancellationToken));
    }
}
