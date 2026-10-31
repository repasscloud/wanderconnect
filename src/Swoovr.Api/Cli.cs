using Microsoft.EntityFrameworkCore;
using Npgsql;
using Swoovr.Api.Accounts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api;

/// <summary>
/// Operator commands run as `dotnet Swoovr.Api.dll &lt;command&gt;` with the same configuration as
/// the host. Anyone who can run them already holds the database connection string, so they
/// are the bootstrap path for things no account can do yet. Each change is audited as "cli".
/// </summary>
public static class Cli
{

    private static async Task<int> AddAll(SwoovrDbContext db, IEnumerable<string> emails, DateTimeOffset now,bool queueInvitation)
    {
        var (added, existing, invalid) = (0, 0, 0);
        foreach (var email in emails)
            switch ((await BetaEnrolments.AddAsync(db, email, null, now, CancellationToken.None,queueInvitation)).Result)
            {
                case BetaEnrolments.AddResult.Added: added++; break;
                case BetaEnrolments.AddResult.AlreadyEnrolled: existing++; break;
                default: invalid++; break;
            }
        // Counts only: addresses are not echoed into terminal logs.
        Console.WriteLine($"Beta enrolments: {added} added, {existing} already enrolled, {invalid} invalid.");
        return invalid > 0 ? 1 : 0;
    }

    public static async Task<int> RunAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args[1..]);
        Swoovr.Hosting.HostRuntime.Configure(builder,Swoovr.Hosting.HostKind.Api,serving:false);
        if(args is ["backups","reconcile",var restoreTarget,_,_])
        {
            if(!System.Text.RegularExpressions.Regex.IsMatch(restoreTarget,"^swoovr_restore_[0-9a-f]{32}$"))return 2;
            // Same PG* secret/environment context as the authenticated operator restore; no connection secret in argv.
            var connection=new NpgsqlConnectionStringBuilder(builder.Configuration.GetConnectionString("Swoovr")??"");
            connection.Database=restoreTarget;
            if(Environment.GetEnvironmentVariable("PGHOST") is{}host)connection.Host=host;
            if(Environment.GetEnvironmentVariable("PGPORT") is{}port)connection.Port=int.Parse(port,System.Globalization.CultureInfo.InvariantCulture);
            if(Environment.GetEnvironmentVariable("PGUSER") is{}login)connection.Username=login;
            if(Environment.GetEnvironmentVariable("PGPASSWORD") is{}password)connection.Password=password;
            if(Environment.GetEnvironmentVariable("PGSSLROOTCERT") is{}certificate)connection.RootCertificate=certificate;
            if(Environment.GetEnvironmentVariable("PGSSLMODE") is{}ssl)connection.SslMode=ssl switch
            {"verify-full"=>SslMode.VerifyFull,"verify-ca"=>SslMode.VerifyCA,"require"=>SslMode.Require,"disable"=>SslMode.Disable,"allow"=>SslMode.Allow,"prefer"=>SslMode.Prefer,_=>throw new InvalidOperationException("Unsupported restore TLS mode.")};
            builder.Configuration["ConnectionStrings:Swoovr"]=connection.ConnectionString;
        }
        // Commands print their own short results; host and SQL logging would only add noise.
        builder.Logging.ClearProviders();
        builder.Services.AddScoped<Founders>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSwoovrProviders(builder.Configuration);
        builder.Services.AddSwoovrApplicationEmail(builder.Configuration,builder.Environment.IsDevelopment());
        BetaInvitationDelivery.PublicOrigin(builder.Configuration,builder.Environment.IsDevelopment());
        var persistence = builder.Services.AddSwoovrPersistence(builder.Configuration.GetConnectionString("Swoovr"));
        // D203: `accounts purge` deletes media objects (never the screening worker here).
        builder.Configuration["Media:Screening:Worker"] = "false";
        Swoovr.Api.Media.MediaSetup.AddSwoovrMedia(builder.Services, builder.Configuration, builder.Environment, persistence);
        if (!persistence)
        {
            Console.Error.WriteLine("ConnectionStrings:Swoovr is not configured.");
            return 2;
        }
        await using var app = builder.Build();
        // Fail at startup/command bootstrap if configured push is unsafe or incomplete.
        app.Services.GetRequiredService<Swoovr.Infrastructure.Messaging.IPushSender>();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SwoovrDbContext>();
        var pushKeys = scope.ServiceProvider.GetRequiredService<Swoovr.Api.Notifications.PushKeys>();
        var now = DateTimeOffset.UtcNow;
        var restoreSafe=args is ["migrate",..] or ["backups","bundle"] or ["backups","reconcile",_,_,_]
            or ["keys","status"] or ["bans","legacy-status"];
        if(!restoreSafe&&!await Swoovr.Api.Retention.RestoreReadiness.ReadyAsync(db,CancellationToken.None))
        {
            Console.Error.WriteLine("Restored database remains fenced; only migration, read-only capture/status and explicit reconciliation commands are allowed.");
            return 2;
        }
        switch (args)
        {
            // D111: GeoNames place data (cities1000, admin1CodesASCII, countryInfo) from a folder; see scripts/geonames.sh.
            case ["policies","notices","stage",var policyVersion,var policyTitle,var policyPath,"--operator",var stagingOperator] when Guid.TryParse(stagingOperator,out var stagingActor):
            {
                try
                {
                    var id=await scope.ServiceProvider.GetRequiredService<Swoovr.Api.Notifications.PolicyNoticePublisher>().StageAsync(policyVersion,policyTitle,policyPath,stagingActor,CancellationToken.None);
                    Console.WriteLine($"Staged internal safety notice {id}: {policyVersion} · {policyTitle} · {policyPath}. Nothing delivered; separately confirm the published source.");return 0;
                }
                catch(InvalidOperationException){Console.Error.WriteLine("Policy source metadata, version or current root operator is unavailable.");return 2;}
            }
            case ["policies","notices","publish",var publication,"--operator",var publishingOperator,"--source-published","true"] when Guid.TryParse(publication,out var publicationId)&&Guid.TryParse(publishingOperator,out var publishingActor):
            {
                var accepted=await scope.ServiceProvider.GetRequiredService<Swoovr.Api.Notifications.PolicyNoticePublisher>().PublishAsync(publicationId,publishingActor,true,CancellationToken.None);
                Console.WriteLine(accepted?"Current members snapshotted for centre-only delivery; no external send.":"Publication disabled or unavailable; no publication performed.");return accepted?0:2;
            }
            case ["policies","notices","process",var processingPublication] when Guid.TryParse(processingPublication,out var processingId):
                Console.WriteLine($"Policy notice targets completed: {await scope.ServiceProvider.GetRequiredService<Swoovr.Api.Notifications.PolicyNoticePublisher>().RunBatchAsync(processingId,CancellationToken.None)} (bounded100; no external send).");return 0;
            case ["geonames", "import", var folder, ..]:
                await db.Database.MigrateAsync();
                var (cities, names) = await Swoovr.Infrastructure.Places.GeoNamesImport.ImportAsync(db, folder);
                Audit.Record(db, now, null, "cli", "geonames.imported", null, null, $"cities:{cities}");
                await db.SaveChangesAsync();
                Console.WriteLine($"GeoNames: {cities} cities and {names} search names imported. Place data © GeoNames (CC BY 4.0).");
                return 0;
            case ["push", "tokens", "protect"]:
                if (pushKeys.Protector is null) { Console.Error.WriteLine("Push token protection is not configured."); return 2; }
                Console.WriteLine($"Push registrations protected: {await Swoovr.Api.Notifications.PushDeviceMaintenance.ProtectLegacyAsync(db, pushKeys, CancellationToken.None)}.");
                return 0;
            case ["backups","reconcile",var target,var archive,var authoritySha]:
                await scope.ServiceProvider.GetRequiredService<Swoovr.Api.Recovery.RestoreReconciliation>().RunAsync(target,archive,authoritySha,CancellationToken.None);
                Console.WriteLine("Restore reconciliation completed: current media verified, expiry/holds reconciled, restored API sessions/device addresses revoked. Serving receipt prepared; paired Keycloak and ingress approval remain separate.");
                return 0;
            case ["backups", "bundle"]:
                // Binary stdout only. The operator wrapper must stream it directly into authenticated encryption.
                await scope.ServiceProvider.GetRequiredService<Swoovr.Api.Recovery.BackupBundle>().WriteAsync(Console.OpenStandardOutput(),CancellationToken.None);
                return 0;
            case ["migrate", ..]:
                await db.Database.MigrateAsync();
                Console.WriteLine("Database is up to date.");
                return 0;
            case ["root-admin", "grant", var subject, ..]:
            {
                // The staff-realm subject (Keycloak user id) of the single break-glass account (D36).
                // It must already have signed in once so its account row exists.
                var staff = await db.Accounts.Where(a => a.Realm == AccountRealm.Staff && a.Subject == subject).ToListAsync();
                if (staff.Count != 1) { Console.Error.WriteLine("No single staff-realm account has that subject; sign in once first."); return 1; }
                if (await db.Accounts.AnyAsync(a => a.IsRootAdmin && a.Id != staff[0].Id)) { Console.Error.WriteLine("Another account is already the root admin."); return 1; }
                staff[0].IsRootAdmin = true;
                Audit.Record(db, now, null, "cli", "root-admin.granted", "account", staff[0].Id.ToString());
                await db.SaveChangesAsync();
                Console.WriteLine("Root admin granted.");
                return 0;
            }
            // D189: appoint (or remove) the security / legal officer: a staff-realm account that has signed in once. The root
            // admin may hold it too. Admins can't grant it themselves; only the root admin's invitation or this command.
            case ["legal", "grant" or "revoke", var subject, ..]:
            {
                var staff = await db.Accounts.Where(a => a.Realm == AccountRealm.Staff && a.Subject == subject).ToListAsync();
                if (staff.Count != 1) { Console.Error.WriteLine("No single staff-realm account has that subject; sign in once first."); return 1; }
                var existing = await db.RoleGrants.SingleOrDefaultAsync(g => g.AccountId == staff[0].Id && g.Role == AccountRole.Legal);
                if (args[1] == "grant" && existing is null)
                    db.RoleGrants.Add(new RoleGrant { Id = Guid.NewGuid(), AccountId = staff[0].Id, Role = AccountRole.Legal, GrantedAt = now });
                else if (args[1] == "revoke" && existing is not null)
                    db.RoleGrants.Remove(existing);
                Audit.Record(db, now, null, "cli", "legal." + (args[1] == "grant" ? "granted" : "revoked"), "account", staff[0].Id.ToString());
                await db.SaveChangesAsync();
                Console.WriteLine(args[1] == "grant" ? "Legal role granted." : "Legal role revoked.");
                return 0;
            }
            // D191: one-off backfill of the verified identity store for approved checks Didit still keeps (30 days). Members
            // whose decision has gone have no record until they verify again; nobody is asked to.
            case ["identity", "backfill", ..]:
            {
                var store = scope.ServiceProvider.GetRequiredService<Swoovr.Api.Verification.VerifiedIdentityStore>();
                var provider = scope.ServiceProvider.GetRequiredService<IIdVerificationProvider>();
                if (!store.Configured || !provider.Configured) { Console.Error.WriteLine("Identity store keys or Didit are not configured."); return 2; }
                var (recorded, gone) = (0, 0);
                var due = await db.IdVerificationAttempts.Where(a => a.State == IdAttemptState.Approved && a.CompletedAt > now.AddDays(-30)
                    && !db.VerifiedIdentities.Any(i => i.AccountId == a.AccountId)).ToListAsync();
                foreach (var attempt in due)
                {
                    var decision = await provider.GetDecisionAsync(attempt.SessionId, CancellationToken.None);
                    if (decision is not null && decision.SessionId == attempt.SessionId && decision.VendorData == attempt.AccountId.ToString()
                        && await store.RecordAsync(attempt.AccountId, attempt, decision, now, CancellationToken.None)) recorded++;
                    else gone++;
                }
                await db.SaveChangesAsync();
                Console.WriteLine($"Identity store: {recorded} recorded, {gone} without a readable decision.");
                return 0;
            }
            case ["beta", "add", "--file", var path, ..]:
                // One address per line; blank lines and lines starting with # are ignored.
                return await AddAll(db, File.ReadLines(path).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')), now,builder.Configuration.GetValue("Beta:Invitations:Enabled",false));
            case ["beta", "add", .. var emails] when emails.Length > 0:
                return await AddAll(db, emails, now,builder.Configuration.GetValue("Beta:Invitations:Enabled",false));
            case ["beta", "revoke", var email, ..]:
            {
                var normalised = EmailAddress.Normalise(email);
                var live = await db.BetaEnrolments.SingleOrDefaultAsync(e => e.Email == normalised && e.RevokedAt == null);
                if (!await BetaEnrolments.RevokeAsync(db, live, null, now, CancellationToken.None)) { Console.Error.WriteLine("No live enrolment for that address."); return 1; }
                Console.WriteLine("Revoked.");
                return 0;
            }
            // D130: the daily founder allocation job, meant for cron (for example `0 3 * * *`).
            case ["founders", "allocate", ..]:
            {
                var founders = scope.ServiceProvider.GetRequiredService<Founders>();
                var run = await founders.AllocateAsync(now, CancellationToken.None);
                Console.WriteLine($"Founders: {run.Stamped} newly eligible, {run.Allocated} places allocated, {run.Skipped} skipped, {run.PlacesLeft} left.");
                return 0;
            }
            // #49: travel reminders once (the API's worker does this every 15 minutes), for cron or tests.
            case ["notifications", "sweep", ..]:
            {
                var written = await scope.ServiceProvider.GetRequiredService<Swoovr.Api.Notifications.Notifier>().TravelRemindersAsync(now, CancellationToken.None);
                Console.WriteLine($"Notifications: {written} travel reminders written.");
                return 0;
            }
            // D167: erases the text of messages deleted for both more than 30 days ago (kept for investigations until then), for cron.
            case ["media", "alerts", "resolve", var alertMediaId, var slot, var outcome, "--note-file", var noteFile]
                when Guid.TryParse(alertMediaId, out var parsedAlertMedia) && slot is "primary" or "backup" && outcome is "accepted" or "retry":
            {
                string note;
                try
                {
                    if (new FileInfo(noteFile).Length > 8192) { Console.Error.WriteLine("Resolution note is too large."); return 2; }
                    note = await File.ReadAllTextAsync(noteFile);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException) { Console.Error.WriteLine("Resolution note file could not be read."); return 2; }
                var resolved = await scope.ServiceProvider.GetRequiredService<Swoovr.Api.Media.MediaPipeline>().ResolveMediaAlertAsync(parsedAlertMedia, slot, outcome == "accepted", note, now, CancellationToken.None);
                Console.WriteLine(resolved ? "Media alert outcome reconciled and audited." : "No uncertain alert reconciled; check state and a 20–2000 character explanation.");
                return resolved ? 0 : 2;
            }
            case ["media", "preserve"]:
            {
                var run = await scope.ServiceProvider.GetRequiredService<Swoovr.Api.Media.MediaPipeline>().RecoverBlockedSweepAsync(now, CancellationToken.None);
                Console.WriteLine($"Media preservation: {run.Completed} completed total, {run.Waiting} waiting, {run.ReviewRequired} require operator review.");
                return run.Waiting == 0 && run.ReviewRequired == 0 ? 0 : 1;
            }
            case ["media", "preserve", var mediaId] when Guid.TryParse(mediaId, out var parsedMedia):
                await scope.ServiceProvider.GetRequiredService<Swoovr.Api.Media.MediaPipeline>().RecoverBlockedAsync(parsedMedia, now, CancellationToken.None);
                var mediaItem = await db.MediaItems.AsNoTracking().SingleOrDefaultAsync(i => i.Id == parsedMedia);
                Console.WriteLine(mediaItem?.PreservationCompletedAt is not null ? "Media preservation: completed." : "Media preservation: unfinished; check restricted status.");
                return mediaItem?.PreservationCompletedAt is not null ? 0 : 1;
            case ["messages", "purge", ..]:
            {
                var run = await scope.ServiceProvider.GetRequiredService<Retention.MessageRetention>().PurgeAsync(now, CancellationToken.None);
                if (run.HoldStatusUnavailable) { Console.Error.WriteLine("Messages: hold status unavailable; remaining records left alone."); return 1; }
                Console.WriteLine($"Messages: {run.Texts} deleted message texts erased, {run.Photos} deleted chat photos released, {run.Kept} kept under a legal hold.");
                return 0;
            }
            case ["bans", "legacy-status"]:
            {
                var legacy=await RegistryLegacyAudit.ReadAsync(db,now,CancellationToken.None);
                Console.WriteLine($"Unversioned legacy markers: {legacy.Emails} email, {legacy.Mobiles} mobile; {legacy.Expired} due expiry; {legacy.Malformed} malformed. No values or source identities exported.");
                if(legacy.RequiresReview)Console.Error.WriteLine("Legacy provenance requires restricted review before UAT data import. Never infer a key version/case or rehash a stored hash. Existing review-only behavior is unchanged.");
                return legacy.RequiresReview?1:0;
            }
            case ["bans", "backfill"]:
            {
                var run = await scope.ServiceProvider.GetRequiredService<RegistryPopulation>().BackfillAsync(now,CancellationToken.None);
                Console.WriteLine($"Registry backfill: {run.Sanctions} terminations inspected; {run.Restrictions} restrictions added; {run.Unresolved} lack usable current contact provenance.");
                return run.Unresolved > 0 ? 1 : 0;
            }
            case ["staff", "conflicts", "reconcile"]:
            {
                var history = scope.ServiceProvider.GetRequiredService<StaffConflictHistory>();
                var added = await history.ReconcileAsync(now, CancellationToken.None);
                var expired = await history.PurgeAsync(now, CancellationToken.None);
                Console.WriteLine($"Staff conflicts: {added} historical associations preserved, {expired.Expired} expired.");
                return expired.HoldStatusUnavailable ? 1 : 0;
            }
            case ["retention", "purge", "--case", var retentionCase] when Guid.TryParse(retentionCase, out var parsedCase):
            {
                var run = await scope.ServiceProvider.GetRequiredService<Retention.SafetyRetention>().PurgeAsync(now, CancellationToken.None, parsedCase);
                Console.WriteLine($"Retention: {run.ContentDisposed} case content sets, {run.CasesDisposed} case records, {run.CopiesDisposed} evidence copies disposed; {run.ReviewRequired} require restricted review.");
                return run.HoldStatusUnavailable ? 1 : 0;
            }
            case ["retention", "purge", "--account", var retentionAccount] when Guid.TryParse(retentionAccount, out var parsedRetentionAccount):
            {
                var run = await scope.ServiceProvider.GetRequiredService<Retention.SafetyRetention>().PurgeAsync(now, CancellationToken.None, onlyAccount: parsedRetentionAccount);
                Console.WriteLine($"Retention: {run.ContentDisposed} case content sets, {run.CasesDisposed} case records, {run.CopiesDisposed} evidence copies disposed; {run.ReviewRequired} require restricted review.");
                return run.HoldStatusUnavailable ? 1 : 0;
            }
            case ["retention", "purge"]:
            {
                var run = await scope.ServiceProvider.GetRequiredService<Retention.RetentionJobs>().RunAsync(now, CancellationToken.None);
                Console.WriteLine($"Retention: {run.Safety.ContentDisposed} case content sets, {run.Safety.CasesDisposed} case records, {run.Safety.CopiesDisposed} copies; {run.Safety.ReviewRequired} require restricted review; {run.MediaDeleted} media objects deleted; {run.ConflictRecordsExpired} minimal staff associations expired; {run.IdentifierRestrictionsExpired} scheduled identifier restrictions expired; {run.IdentifierHistoriesDisposed} inactive identifier histories disposed; {run.IdentifierReceiptsExpired} minimal dispositions expired; {run.OperationalHoldsExpired} released operational groups expired.");
                if (run.FailedClosed) Console.Error.WriteLine("Retention stopped: preservation status or membership was unavailable.");
                return run.FailedClosed || run.Accounts?.Waiting > 0 ? 1 : 0;
            }
            // D170: account deletion, hourly from cron (for example `0 * * * *`): final deletion when due (or asked for now),
            // Didit deletions that failed before, and the end of the two-year retention of tombstones and terminated-email holds.
            case ["contacts", "reconcile"]:
            {
                if(!scope.ServiceProvider.GetRequiredService<IContactIdentityAdmin>().ContactRetirementConfigured)
                {Console.Error.WriteLine("Contact retirement provider is not configured; no operation was processed.");return 2;}
                var inspected=await scope.ServiceProvider.GetRequiredService<EmailRetirementProcessor>().RunAsync(CancellationToken.None);
                var states=await db.EmailRetirementOperations.GroupBy(o=>o.State).Select(g=>new{State=g.Key,Count=g.Count()}).ToListAsync();
                Console.WriteLine($"Contact retirement: {inspected} due operations inspected; {states.Where(s=>s.State==EmailRetirementState.Completed).Sum(s=>s.Count)} confirmed, {states.Where(s=>s.State==EmailRetirementState.Pending).Sum(s=>s.Count)} pending, {states.Where(s=>s.State==EmailRetirementState.NeedsReview).Sum(s=>s.Count)} require review.");
                return 0;
            }
            case ["accounts", "purge", .. var accountArgs]:
            {
                Guid? onlyAccount = accountArgs is [var accountId] && Guid.TryParse(accountId, out var parsedAccount) ? parsedAccount : null;
                if (accountArgs.Length > 0 && onlyAccount is null) { Console.Error.WriteLine("Usage: accounts purge [account-id]"); return 2; }
                var run = await scope.ServiceProvider.GetRequiredService<AccountDeletion>().PurgeAsync(now, CancellationToken.None, onlyAccount);
                if (run.HoldStatusUnavailable)
                {
                    Console.Error.WriteLine($"Accounts: hold status unavailable; nothing purged ({run.Deleted} deleted before the check failed, {run.Waiting} waiting).");
                    return 1;
                }
                // D203: objects of removed, rejected and released-blocked photos go unless a hold needs them; fail closed.
                if (await scope.ServiceProvider.GetRequiredService<Swoovr.Api.Media.MediaPipeline>().SweepAsync(now, CancellationToken.None, onlyAccount: onlyAccount) is not { } media)
                {
                    Console.Error.WriteLine("Photos: hold status unavailable; no photo objects deleted.");
                    return 1;
                }
                Console.WriteLine($"Photos: {media.Deleted} objects deleted, {media.Kept} kept.");
                if (run.Minimized > 0) Console.WriteLine($"Accounts: {run.Minimized} expired identity mappings removed; references remain only for the other member's content.");
                if (run.CasesExpired > 0) Console.WriteLine($"Law-enforcement cases: {run.CasesExpired} past their seven-year retention erased.");
                Console.WriteLine($"Accounts: {run.Deleted} deleted, {run.Waiting} waiting for Keycloak, {run.ProviderRetries} Didit retries done, {run.Expired} retention ended, {run.HoldsExpired} email holds expired, {run.Kept} kept for an active hold or required retained records.");
                return run.Waiting > 0 ? 1 : 0;
            }
            // #89: key rotation. Add the new version (for example `Evidence:Keys:2`) to configuration first, then run this;
            // older versions stay readable until every value is re-sealed. `--to n` picks a configured version.
            case ["keys", "rotate", var set, .. var rest] when Swoovr.Api.Security.KeyRotation.Sets.Contains(set) || set == "all":
            {
                byte? to = rest is ["--to", var v, ..] && byte.TryParse(v, out var parsed) ? parsed : null;
                var rotation = scope.ServiceProvider.GetRequiredService<Swoovr.Api.Security.KeyRotation>();
                try
                {
                    foreach (var name in set == "all" ? Swoovr.Api.Security.KeyRotation.Sets : [set])
                    {
                        var run = await rotation.RotateAsync(name, to, CancellationToken.None);
                        Console.WriteLine($"Keys {run.Set}: {run.Resealed} re-sealed to version {run.Target}, {run.Already} already on it, {run.Unreadable} under a key that isn't configured (left as they were).");
                    }
                }
                catch (InvalidOperationException exception) { Console.Error.WriteLine(exception.Message); return 2; }
                return 0;
            }
            case ["keys", "status", ..]:
            {
                var rotation = scope.ServiceProvider.GetRequiredService<Swoovr.Api.Security.KeyRotation>();
                foreach (var name in Swoovr.Api.Security.KeyRotation.Sets)
                {
                    var counts = await rotation.StatusAsync(name, CancellationToken.None);
                    var versions = string.Join(", ", rotation.Keys(name).Keys.Order());
                    Console.WriteLine($"Keys {name}: configured [{versions}]; values {string.Join(", ", counts.OrderBy(c => c.Key).Select(c => $"v{c.Key}={c.Value}"))}");
                }
                return 0;
            }
            case ["beta", "list", ..]:
                foreach (var e in await db.BetaEnrolments.OrderBy(e => e.CreatedAt).ToListAsync())
                    Console.WriteLine($"{e.Email}\t{BetaEnrolments.View(e).State}");
                return 0;
            default:
                Console.Error.WriteLine("Usage: policies notices stage <version> <public-title> /safety|/safety/advice --operator <root-account-id> | policies notices publish <id> --operator <root-account-id> --source-published true | policies notices process <id> | migrate | backups bundle | backups reconcile <owned-target-db> <authenticated-bundle-path> <authority-sha256> | contacts reconcile | root-admin grant <staff subject> | bans backfill | bans legacy-status | staff conflicts reconcile | legal grant|revoke <staff subject> | identity backfill | keys status | keys rotate contact-history|evidence|police|identity|all [--to n] | beta add <email>... | beta add --file <path> | beta revoke <email> | beta list | founders allocate | messages purge | accounts purge | notifications sweep | retention purge [--case <id>|--account <id>] | push tokens protect | media preserve [media-id] | media alerts resolve <id> primary|backup accepted|retry --note-file <path>");
                return 2;
        }
    }
}
