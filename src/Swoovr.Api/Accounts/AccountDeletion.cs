using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Accounts;

/// <summary>What one `accounts purge` run did; counts only, never who. Kept counts records a legal hold kept (D189).</summary>
/// <remarks>HoldStatusUnavailable (D195): hold status couldn't be read, so nothing was purged (fail closed).</remarks>
public sealed record PurgeRun(int Deleted, int Waiting, int ProviderRetries, int Expired, int HoldsExpired, int Kept = 0, int CasesExpired = 0,
    bool HoldStatusUnavailable = false, int Minimized = 0);

/// <summary>
/// Account deletion (Q43, D144; D169–D173). <see cref="RequestAsync"/> deactivates at once; <see cref="PurgeAsync"/> is
/// the hourly `accounts purge` job that performs final deletion when due, retries Didit deletions that failed, and ends
/// the two-year retention of tombstones and terminated-email holds. Audit entries carry ids and machine values only.
/// </summary>
public sealed class AccountDeletion(IServiceProvider services, IIdentityAdmin identity, IIdVerificationProvider verification, Founders founders,
    IConfiguration configuration, ILogger<AccountDeletion> log)
{
    // Resolved on use: without a database the API still starts and the deletion routes answer 503 (fail closed).
    private SwoovrDbContext db => services.GetRequiredService<SwoovrDbContext>();

    public sealed record RequestOutcome(DeletionRequestError Error, bool SignedOutEverywhere, bool FounderCodeEmailed);

    public async Task<RequestOutcome> RequestAsync(Account account, MemberProfile? profile, bool fresh, bool immediately, bool deleteFounderToken,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var lifecycle = await AccountLifecycleLease.AcquireAsync(db, account.Id, cancellationToken);
        await db.Entry(account).ReloadAsync(cancellationToken);
        var error = AccountDeletionPolicy.CheckRequest(account, fresh);
        if (error != DeletionRequestError.None) return new(error, false, false);
        await using var preservation=await Swoovr.Api.Retention.PreservationLease.SharedAsync(db,cancellationToken);
        await using var transaction=await db.Database.BeginTransactionAsync(cancellationToken);
        AccountDeletionPolicy.Request(account, now, immediately, deleteFounderToken && profile is { FoundingMember: true });
        // D170: pending requests by or to them are cancelled; accepted connections wait (they return if the member cancels).
        await db.ConnectionRequests.Where(r => (r.FromAccountId == account.Id || r.ToAccountId == account.Id) && r.Status == ConnectionRequestStatus.Pending)
            .ExecuteUpdateAsync(u => u.SetProperty(r => r.Status, ConnectionRequestStatus.Cancelled).SetProperty(r => r.AnsweredAt, now), cancellationToken);
        // Stop future device sends at deactivation; credentials are not tombstone/compliance records.
        await db.PushDevices.Where(d => d.AccountId == account.Id).ExecuteDeleteAsync(cancellationToken);
        if(services.GetService<Swoovr.Api.Safety.MeetingSafetyStore>() is{}meetingSafety)await meetingSafety.RevokeAccountAsync(account.Id,now,cancellationToken);
        Audit.Record(db, now, account.Id, "account", "account.deletion-requested", "account", account.Id.ToString(), immediately ? "now" : "30-days");
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await transaction.DisposeAsync();
        await preservation.DisposeAsync();
        await lifecycle.DisposeAsync();
        await MemberMutationAuthority.ReleaseForEffectsAsync(services); // Deactivation is committed before remote mail/sign-out.
        // D65: founders get their recovery code before anything is deleted (unless they asked for the token to go too).
        var emailed = profile is { FoundingMember: true } && !account.DeleteFounderToken && await founders.ResendAsync(account, profile, now, cancellationToken) == true;
        var signedOut = await identity.SignOutEverywhereAsync(account.Subject, cancellationToken);
        if (!signedOut) log.LogWarning("Deletion request: Keycloak sign-out did not complete for an account; the API refuses its sessions anyway.");
        return new(DeletionRequestError.None, signedOut, emailed);
    }

    public async Task<PurgeRun> PurgeAsync(DateTimeOffset now, CancellationToken cancellationToken, Guid? onlyAccount = null,bool finalize=true)
    {
        var (deleted, waiting) = (0, 0);
        var due = await db.Accounts.Where(a => a.Status == AccountStatus.PendingDeletion && a.DeletionDueAt <= now && (onlyAccount == null || a.Id == onlyAccount)).Select(a => a.Id).ToListAsync(cancellationToken);
        // D195 (procedure §14): fail closed. Without a reliable hold status nothing is deleted or expired this run.
        if (await services.GetRequiredService<Evidence.EvidencePreservation>().TryHeldAccountsAsync(cancellationToken) is null)
        {
            log.LogError("accounts purge: hold status unavailable; nothing purged.");
            return new(0, due.Count, 0, 0, 0, 0, 0, HoldStatusUnavailable: true);
        }
        var retention = services.GetRequiredService<Retention.SafetyRetention>();
        foreach (var id in finalize?due:[])
        {
            if (await FinalDeleteAsync(id, now, cancellationToken)) deleted++; else waiting++;
            db.ChangeTracker.Clear();
        }
        // Didit deletions that failed earlier: the attempt rows (session ids and outcomes only) stay until Didit confirms.
        var retries = 0;
        if(finalize)foreach (var attempt in await (from t in db.IdVerificationAttempts join a in db.Accounts on t.AccountId equals a.Id
                                       where a.Status == AccountStatus.Deleted && (onlyAccount == null || a.Id == onlyAccount) select t).ToListAsync(cancellationToken))
            if (await verification.DeleteSessionAsync(attempt.SessionId, cancellationToken)) { db.IdVerificationAttempts.Remove(attempt); retries++; }
        await db.SaveChangesAsync(cancellationToken);
        if (onlyAccount is not null) return new(deleted, waiting, retries, 0, 0);
        // D170: tombstones, their audit entries and moderation records go after two years (subject to legal review, D144),
        // except while a legal hold names the account (D189: deletion never removes held records).
        var (expired, kept, minimized) = (0, 0, 0);
        var held = await services.GetRequiredService<Evidence.EvidencePreservation>().TryHeldAccountsAsync(cancellationToken);
        if (held is null)
        {
            log.LogError("accounts purge: hold status unavailable after final deletion; retention left alone.");
            return new(deleted, waiting, retries, 0, 0, 0, 0, HoldStatusUnavailable: true);
        }
        foreach (var tombstone in await db.Accounts.Where(a => a.Status == AccountStatus.Deleted).ToListAsync(cancellationToken))
        {
            await using var lifecycle = await AccountLifecycleLease.AcquireAsync(db, tombstone.Id, cancellationToken);
            await using var barrier = await Retention.PreservationLease.SharedAsync(db, cancellationToken);
            await db.Entry(tombstone).ReloadAsync(cancellationToken);
            if (!AccountDeletionPolicy.RetentionOver(tombstone, now)) continue;
            var freshHeld = await services.GetRequiredService<Evidence.EvidencePreservation>().TryHeldAccountsAsync(cancellationToken);
            if (freshHeld is null) return new(deleted, waiting, retries, expired, 0, kept, 0, HoldStatusUnavailable: true);
            if (freshHeld.Contains(tombstone.Id) || await retention.AccountRecordsRequiredAsync(tombstone.Id, cancellationToken)) { kept++; continue; }
            // D170: the other member keeps their own messages. Expire the identity mapping without cascading their content.
            var ownHistoryRemains = await (from m in db.Messages join c in db.Conversations on m.ConversationId equals c.Id
                where (c.PairLow == tombstone.Id || c.PairHigh == tombstone.Id) && m.SenderAccountId != tombstone.Id
                    && m.Kind != MessageKind.System && (m.Body != "" || m.Kind == MessageKind.Photo && m.MediaId != null) select m.Id).AnyAsync(cancellationToken);
            if (ownHistoryRemains)
            {
                if (tombstone.Issuer != "urn:swoovr:retired")
                {
                    tombstone.Issuer = "urn:swoovr:retired"; tombstone.Subject = tombstone.Id.ToString("N");
                    tombstone.DeletionRequestedAt = null; tombstone.DeletionDueAt = null; tombstone.DeletionFinalizingAt = null;
                    tombstone.SessionsRevokedAt = null;
                    Audit.Record(db, now, null, "retention", "account.mapping-expired", "account", tombstone.Id.ToString());
                    await db.SaveChangesAsync(cancellationToken);
                    minimized++;
                }
                kept++;
                continue;
            }
            var key = tombstone.Id.ToString();
            await db.AuditEntries.Where(e => e.ActorAccountId == tombstone.Id || e.TargetId == key).ExecuteDeleteAsync(cancellationToken);
            await db.IdentityAccesses.Where(a => a.AccountId == tombstone.Id).ExecuteDeleteAsync(cancellationToken);
            await db.Accounts.Where(a => a.Id == tombstone.Id).ExecuteDeleteAsync(cancellationToken);
            expired++;
        }
        // Coverage changes are exclusive; expiry keeps shared authority through every source deletion.
        await using var expiryBarrier = await Retention.PreservationLease.SharedAsync(db, cancellationToken);
        held = await services.GetRequiredService<Evidence.EvidencePreservation>().TryHeldAccountsAsync(cancellationToken);
        if (held is null) return new(deleted, waiting, retries, expired, 0, kept, 0, HoldStatusUnavailable: true);
        var holds = await db.TerminatedEmailHolds.Where(h => h.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken)
            + await db.TerminatedPhoneHolds.Where(h => h.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);
        // D148: phone codes (hashes only) are kept a day for the limits, then go.
        await db.PhoneCodes.Where(c => c.CreatedAt < now - PhoneVerificationPolicy.CodeRetention).ExecuteDeleteAsync(cancellationToken);
        // #90: search events are kept a day for the hourly limits.
        await db.SearchEvents.Where(e => e.At < now - LocationPrivacy.EventRetention).ExecuteDeleteAsync(cancellationToken);
        // D191: a terminated member's identity record goes after its two years, unless a legal hold names the account.
        kept += await db.VerifiedIdentities.CountAsync(i => i.RetainUntil <= now && held.Contains(i.AccountId), cancellationToken);
        await db.VerifiedIdentities.Where(i => i.RetainUntil <= now && !held.Contains(i.AccountId)).ExecuteDeleteAsync(cancellationToken);
        kept += await db.ContactHistories.CountAsync(h => h.RetainUntil <= now && held.Contains(h.AccountId), cancellationToken);
        await db.ContactHistories.Where(h => h.RetainUntil <= now && !held.Contains(h.AccountId)).ExecuteDeleteAsync(cancellationToken);
        // D195 (procedure §13): law-enforcement case records go seven years after closure, unless a live hold serves the case.
        var heldCases = await db.LegalHolds.Where(h => h.LeCaseId != null
            && (h.Status != LegalHoldStatus.Released || db.EvidenceItems.Any(e => e.HoldId == h.Id)))
            .Select(h => h.LeCaseId!.Value).ToListAsync(cancellationToken);
        heldCases.AddRange(await (from export in db.EvidenceExports join hold in db.LegalHolds on export.HoldId equals hold.Id
            where export.LeCaseId != null && (hold.Status != LegalHoldStatus.Released || db.EvidenceItems.Any(e => e.HoldId == hold.Id))
            select export.LeCaseId!.Value).ToListAsync(cancellationToken));
        // Keep police provenance until released operational records (including old export links) expire as a group.
        var cases = await db.LeCases.Where(c => c.Status == LeCaseStatus.Closed && c.RetainUntil <= now && !heldCases.Contains(c.Id)
            && !db.LegalHolds.Any(h => h.LeCaseId == c.Id)
            && !db.EvidenceExports.Any(e => e.LeCaseId == c.Id)).ExecuteDeleteAsync(cancellationToken);
        return new(deleted, waiting, retries, expired, holds, kept, cases, Minimized: minimized);
    }

    /// <summary>
    /// Final deletion of one account (Q43 rules 7–10, D170): the Keycloak user first (without it nothing else happens, so
    /// a failure retries next run), then Didit's sessions, then Swoovr's personal data; a tombstone stays.
    /// </summary>
    private async Task<bool> FinalDeleteAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var lifecycle = await AccountLifecycleLease.AcquireAsync(db, id, cancellationToken);
        if (await db.EmailRetirementOperations.AnyAsync(o=>o.AccountId==id&&o.State!=EmailRetirementState.Completed,cancellationToken)) return false;
        await using var mediaLease = await AccountLifecycleLease.AcquireMediaAsync(db, id, cancellationToken);
        // Capture verified contact provenance before any irreversible provider/source removal. Never revive revoked entries.
        await using (var registryAuthority = await Retention.PreservationLease.ExclusiveAsync(db,cancellationToken))
        {
            var candidate = await db.Accounts.SingleAsync(a=>a.Id==id,cancellationToken);await db.Entry(candidate).ReloadAsync(cancellationToken);
            if(!AccountDeletionPolicy.Due(candidate,now))return false;
            var terminations = await db.Sanctions.Where(s=>s.AccountId==id&&s.Kind==SanctionKind.Termination&&s.LiftedAt==null&&s.StartsAt<=now).ToListAsync(cancellationToken);
            if(terminations.Count>0)
            {
                await using var registryTransaction = await db.Database.BeginTransactionAsync(cancellationToken);
                foreach(var termination in terminations)await services.GetRequiredService<RegistryPopulation>().StageAsync(termination,now,cancellationToken);
                await db.SaveChangesAsync(cancellationToken);await registryTransaction.CommitAsync(cancellationToken);
            }
        }
        await using var barrier = await Retention.PreservationLease.SharedAsync(db, cancellationToken);
        var account = await db.Accounts.SingleAsync(a => a.Id == id, cancellationToken);
        await db.Entry(account).ReloadAsync(cancellationToken);
        if (!AccountDeletionPolicy.Due(account, now)) return false;
        // Blocked preservation must establish its account hold before ordinary profile/history can be erased.
        if (await db.MediaItems.AnyAsync(i => i.AccountId == id && i.Status == MediaStatus.Blocked && i.ObjectKey != null
            && i.PreservationCompletedAt == null && i.HoldId == null
            && !db.LegalHoldScopes.Any(s => s.Kind == HoldScopeKind.Account && s.TargetId == id
                && db.LegalHolds.Any(h => h.Id == s.HoldId && h.Status != LegalHoldStatus.Released)), cancellationToken))
        {
            log.LogWarning("Final deletion waits: blocked-media preservation has not established a hold.");
            return false;
        }
        var evidence = services.GetRequiredService<Evidence.EvidencePreservation>();
        async Task<bool> PreserveAvailableAsync()
        {
            if (await evidence.TryHeldAccountsAsync(cancellationToken) is null)
            {
                log.LogWarning("Final deletion waits: hold status unavailable.");
                return false;
            }
            if ((await evidence.DeletionHoldsAsync(id, cancellationToken)).Count == 0) return await evidence.PolicyAcceptanceCopiesReadyAsync(id,cancellationToken);
            if (!evidence.Configured)
            {
                log.LogWarning("Final deletion waits: a preservation obligation has no configured evidence key.");
                return false;
            }
            await evidence.PreserveBeforeDeletionAsync(id, now, cancellationToken);
            if(!await evidence.PolicyAcceptanceCopiesReadyAsync(id,cancellationToken)){log.LogWarning("Final deletion waits: policy acceptance preservation is unavailable.");return false;}
            return true;
        }
        if (!await PreserveAvailableAsync()) return false;
        if (account.DeletionFinalizingAt is null)
        {
            account.DeletionFinalizingAt = now;
            Audit.Record(db, now, null, "system", "account.deletion-finalizing", "account", id.ToString());
            await db.SaveChangesAsync(cancellationToken); // Commit before an irreversible/ambiguous provider outcome.
        }
        await barrier.DisposeAsync(); // Provider calls never block new preservation requests.
        if (!await identity.DeleteUserAsync(account.Subject, cancellationToken))
        {
            log.LogWarning("Final deletion waits: the Keycloak user could not be deleted.");
            return false;
        }
        foreach (var attempt in await db.IdVerificationAttempts.Where(t => t.AccountId == id).ToListAsync(cancellationToken))
            if (await verification.DeleteSessionAsync(attempt.SessionId, cancellationToken)) db.IdVerificationAttempts.Remove(attempt);

        await using var eraseBarrier = await Retention.PreservationLease.SharedAsync(db, cancellationToken);
        if (!await PreserveAvailableAsync()) return false; // Include holds placed while the provider operation was in flight.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var profile = await db.MemberProfiles.FindAsync([id], cancellationToken);
        // D171: a terminated member leaves only a keyed hash of their email, for two years, to flag a return for review.
        var terminated = await db.Sanctions.AnyAsync(s => s.AccountId == id && s.Kind == SanctionKind.Termination && s.LiftedAt == null, cancellationToken);
        var enforcementCases = await db.Sanctions.Where(s=>s.AccountId==id&&s.Kind==SanctionKind.Termination&&s.CaseId!=null).Select(s=>s.CaseId!.Value).ToArrayAsync(cancellationToken);
        var registryRecorded = await db.BannedIdentifiers.AnyAsync(b=>enforcementCases.Contains(b.CaseId),cancellationToken)
            || await db.BanDispositions.AnyAsync(d=>enforcementCases.Contains(d.CaseId),cancellationToken);
        if (terminated && !registryRecorded && account.Email is { } email && HoldKey() is { } holdKey)
        {
            var hash = HoldHash(holdKey, email);
            if (!await db.TerminatedEmailHolds.AnyAsync(h => h.EmailHash == hash, cancellationToken))
                db.TerminatedEmailHolds.Add(new TerminatedEmailHold { EmailHash = hash, CreatedAt = now, ExpiresAt = now + AccountDeletionPolicy.TerminatedHold });
        }
        // #87: and the verified mobile number's keyed hash, so the number verifying another account is flagged (never blocked).
        if (terminated && !registryRecorded && HoldKey() is { } phoneKey && await db.MemberPhones.Where(p => p.AccountId == id).Select(p => p.Number).SingleOrDefaultAsync(cancellationToken) is { } number)
        {
            var hash = PhoneHoldHash(phoneKey, number);
            if (!await db.TerminatedPhoneHolds.AnyAsync(h => h.PhoneHash == hash, cancellationToken))
                db.TerminatedPhoneHolds.Add(new TerminatedPhoneHold { PhoneHash = hash, CreatedAt = now, ExpiresAt = now + AccountDeletionPolicy.TerminatedHold });
        }
        // D173, #22: the founder recovery token stays (so founding status can be restored) unless the member asked otherwise.
        // The founding place itself is never reassigned (Q43 rule 15).
        if (account.DeleteFounderToken && profile?.FounderPlace is { } place)
            await db.FounderTokens.Where(t => t.Place == place).ExecuteDeleteAsync(cancellationToken);

        // Ordinary personal data (Q43 rule 7). Messages they sent go; copies preserved in moderation cases stay.
        var conversations = db.Conversations.Where(c => c.PairLow == id || c.PairHigh == id);
        await conversations.Where(c => c.ClosedAt == null).ExecuteUpdateAsync(u => u.SetProperty(c => c.ClosedAt, now).SetProperty(c => c.ClosedReason, ClosedReasons.Deleted), cancellationToken);
        // Already closed by a disconnect: still readable by the other member, now as a deleted member's conversation.
        await conversations.Where(c => c.ClosedReason == ClosedReasons.Disconnected).ExecuteUpdateAsync(u => u.SetProperty(c => c.ClosedReason, ClosedReasons.Deleted), cancellationToken);
        await db.MessageHides.Where(h => h.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.ConversationHides.Where(h => h.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.Messages.Where(m => m.SenderAccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.ConnectionRequests.Where(r => r.FromAccountId == id || r.ToAccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.Blocks.Where(b => b.BlockerAccountId == id || b.BlockedAccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.MemberModes.Where(m => m.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.ModeFilters.Where(f => f.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.MemberIdentities.Where(i => i.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.BotModeAuthorisations.Where(b => b.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.BotMarkets.Where(b => b.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.MarketInterests.Where(i => i.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.Trips.Where(t => t.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.Itineraries.Where(i => i.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.HomeLocationChecks.Where(c => c.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.DobReviews.Where(r => r.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.Entitlements.Where(e => e.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.MemberNotices.Where(n => n.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.Set<PolicyAcceptance>().Where(p=>p.AccountId==id).ExecuteDeleteAsync(cancellationToken);
        await db.PushDevices.Where(d => d.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.Notifications.Where(n => n.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.NotificationModeMutes.Where(n => n.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.NotificationSettings.Where(n => n.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        var conflicts = services.GetRequiredService<StaffConflictHistory>();
        foreach (var link in await db.StaffMemberLinks.AsNoTracking().Where(l => l.MemberAccountId == id || l.StaffAccountId == id).ToListAsync(cancellationToken))
            await conflicts.PreserveAsync(link, now, cancellationToken);
        await db.StaffMemberLinks.Where(l => l.MemberAccountId == id || l.StaffAccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.BetaEnrolments.Where(e => e.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.RoleGrants.Where(g => g.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.DataExports.Where(e => e.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.PhoneCodes.Where(c => c.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        // #90, #91: search events and profile versions go with the account (held versions were captured above).
        await db.SearchEvents.Where(e => e.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.ProfileVersions.Where(v => v.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        // D203: photos leave every profile; their objects go in the purge sweep unless a hold needs them (blocked ones stay evidence).
        await Media.MediaPipeline.RemoveAllAsync(db, id, now, cancellationToken);
        // D191: the verified identity record goes too, except a terminated member's, kept two years for ban enforcement.
        if (VerifiedIdentityPolicy.RetainAfterDeletion(terminated, now) is { } retain)
            await db.VerifiedIdentities.Where(i => i.AccountId == id).ExecuteUpdateAsync(u => u.SetProperty(i => i.RetainUntil, retain), cancellationToken);
        else await db.VerifiedIdentities.Where(i => i.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        // D187: contact history goes with the account, except a terminated member's (ban enforcement, policy §8, §13).
        if (!terminated) await db.ContactHistories.Where(h => h.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.MemberPhones.Where(p => p.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        await db.MemberProfiles.Where(p => p.AccountId == id).ExecuteDeleteAsync(cancellationToken);
        // Kept for two years (D170): reports, moderation cases and their evidence, sanctions, appeals and audit entries.
        AccountDeletionPolicy.Tombstone(account, now);
        Audit.Record(db, now, null, "system", "account.deleted", "account", id.ToString(), terminated ? "terminated-hold" : null);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// The key for terminated-email holds: `Accounts:HoldKey`, or else derived from founder secret1 with its own
    /// label, so the two uses never share a key. Null when neither exists (no hold is kept and the run says so in logs).
    /// </summary>
    private byte[]? HoldKey()
    {
        if (configuration["Accounts:HoldKey"] is { Length: > 0 } configured) return Encoding.UTF8.GetBytes(configured);
        if (founders.DeriveLegacyKey("swoovr:terminated-email-hold:v1") is { } legacy) return legacy;
        log.LogWarning("No key for terminated-email holds; none kept.");
        return null;
    }

    /// <summary>#87: the phone hold's hash, labelled so it never equals an email hold's.</summary>
    public static byte[] PhoneHoldHash(byte[] key, string number) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes("tel:" + number));

    /// <summary>
    /// #87: a member verifying a number held for a terminated member gets a case in the admin queue (never blocked; a
    /// reassigned number is a likely innocent explanation).
    /// </summary>
    public async Task FlagIfReturningPhoneAsync(Guid accountId, string number, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (HoldKey() is not { } key) return;
        var hash = PhoneHoldHash(key, number);
        if (!await db.TerminatedPhoneHolds.AnyAsync(h => h.PhoneHash == hash && h.ExpiresAt > now, cancellationToken)) return;
        if (await db.ModerationCases.AnyAsync(c => c.SubjectAccountId == accountId && (c.Status == CaseStatus.Open || c.Status == CaseStatus.UnderReview), cancellationToken)) return;
        var item = new ModerationCase
        {
            Id = Guid.NewGuid(), SubjectAccountId = accountId, Status = CaseStatus.Open, Queue = CaseQueue.Admin, Priority = ReportPriority.High,
            CreatedAt = now, UpdatedAt = now, SystemFlag = "returning-terminated-phone",
        };
        db.ModerationCases.Add(item);
        Audit.Record(db, now, null, "system", "account.returning-terminated", "moderation-case", item.Id.ToString(), "phone");
        await db.SaveChangesAsync(cancellationToken);
    }

    public static byte[] HoldHash(byte[] key, string email) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(EmailAddress.Normalise(email) ?? email));

    /// <summary>
    /// D171: a new member whose verified email matches a hold gets a case in the admin queue (never blocked). Called when
    /// a members-realm profile is first created.
    /// </summary>
    public async Task FlagIfReturningAsync(Guid accountId, string? email, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (email is null || HoldKey() is not { } key) return;
        var hash = HoldHash(key, email);
        if (!await db.TerminatedEmailHolds.AnyAsync(h => h.EmailHash == hash && h.ExpiresAt > now, cancellationToken)) return;
        if (await db.ModerationCases.AnyAsync(c => c.SubjectAccountId == accountId && (c.Status == CaseStatus.Open || c.Status == CaseStatus.UnderReview), cancellationToken)) return;
        var item = new ModerationCase
        {
            Id = Guid.NewGuid(), SubjectAccountId = accountId, Status = CaseStatus.Open, Queue = CaseQueue.Admin, Priority = ReportPriority.High,
            CreatedAt = now, UpdatedAt = now, SystemFlag = "returning-terminated",
        };
        db.ModerationCases.Add(item);
        Audit.Record(db, now, null, "system", "account.returning-terminated", "moderation-case", item.Id.ToString());
        await db.SaveChangesAsync(cancellationToken);
    }
}
