using Microsoft.EntityFrameworkCore;
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

    private static async Task<int> AddAll(SwoovrDbContext db, IEnumerable<string> emails, DateTimeOffset now)
    {
        var (added, existing, invalid) = (0, 0, 0);
        foreach (var email in emails)
            switch ((await BetaEnrolments.AddAsync(db, email, null, now, CancellationToken.None)).Result)
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
        // Commands print their own short results; host and SQL logging would only add noise.
        builder.Logging.ClearProviders();
        builder.Services.AddScoped<Founders>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSwoovrProviders(builder.Configuration);
        builder.Services.AddSwoovrEmail(builder.Configuration["Email:Smtp:Host"], builder.Configuration.GetValue("Email:Smtp:Port", 0),
            builder.Configuration["Email:From"] ?? "Swoovr <no-reply@swoovr.local>");
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
        switch (args)
        {
            // D111: GeoNames place data (cities1000, admin1CodesASCII, countryInfo) from a folder; see scripts/geonames.sh.
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
                return await AddAll(db, File.ReadLines(path).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')), now);
            case ["beta", "add", .. var emails] when emails.Length > 0:
                return await AddAll(db, emails, now);
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
                // D189: text a live legal hold covers is kept until the hold is released.
                var (purged, kept) = (0, 0);
                // D195: fail closed when hold status can't be read.
                if (await scope.ServiceProvider.GetRequiredService<Swoovr.Api.Evidence.EvidencePreservation>().TryLiveAsync(CancellationToken.None) is not { } live)
                {
                    Console.Error.WriteLine("Messages: hold status unavailable; nothing purged.");
                    return 1;
                }
                var (holds, scopes) = live;
                var due = await (from m in db.Messages join c in db.Conversations on m.ConversationId equals c.Id
                                 where m.DeletedAt != null && m.DeletedAt < now - MessagingPolicy.EvidenceRetention && m.Body != "" select new { m, c }).ToListAsync();
                foreach (var row in due)
                    if (Swoovr.Api.Evidence.EvidencePreservation.MessageHeld(row.m, row.c, holds, scopes)) kept++;
                    else if (MessagingPolicy.Purge(row.m, now)) { row.m.Body = ""; purged++; }
                // #41: a chat photo deleted for both is kept 30 days like text, then released to `accounts purge`, which
                // deletes its object unless a hold covers it.
                var photos = 0;
                var photoRows = await (from m in db.Messages join c in db.Conversations on m.ConversationId equals c.Id join i in db.MediaItems on m.MediaId equals i.Id
                                       where m.Kind == MessageKind.Photo && m.DeletedAt != null && m.DeletedAt < now - MessagingPolicy.EvidenceRetention && i.RemovedAt == null
                                       select new { m, c, i }).ToListAsync();
                foreach (var row in photoRows)
                    if (Swoovr.Api.Evidence.EvidencePreservation.MessageHeld(row.m, row.c, holds, scopes)) kept++;
                    else
                    {
                        await using var mediaLease = await Media.MediaAccountLease.AcquireAsync(db, row.i.AccountId, CancellationToken.None);
                        await db.Entry(row.i).ReloadAsync();
                        if (db.Entry(row.i).State == EntityState.Detached || row.i.RemovedAt is not null) continue;
                        row.i.RemovedAt = now;
                        if (row.i.Status is MediaStatus.Pending or MediaStatus.Passed or MediaStatus.Uncertain) (row.i.Status, row.i.NextScreenAt) = (MediaStatus.Removed, null);
                        await db.SaveChangesAsync(); // Commit the media transition before releasing its lifecycle lease.
                        photos++;
                    }
                await db.SaveChangesAsync();
                Console.WriteLine($"Messages: {purged} deleted message texts erased, {photos} deleted chat photos released, {kept} kept under a legal hold.");
                return 0;
            }
            // D170: account deletion, hourly from cron (for example `0 * * * *`): final deletion when due (or asked for now),
            // Didit deletions that failed before, and the end of the two-year retention of tombstones and terminated-email holds.
            case ["accounts", "purge", ..]:
            {
                var run = await scope.ServiceProvider.GetRequiredService<AccountDeletion>().PurgeAsync(now, CancellationToken.None);
                if (run.HoldStatusUnavailable)
                {
                    Console.Error.WriteLine($"Accounts: hold status unavailable; nothing purged ({run.Deleted} deleted before the check failed, {run.Waiting} waiting).");
                    return 1;
                }
                // D203: objects of removed, rejected and released-blocked photos go unless a hold needs them; fail closed.
                if (await scope.ServiceProvider.GetRequiredService<Swoovr.Api.Media.MediaPipeline>().SweepAsync(now, CancellationToken.None) is not { } media)
                {
                    Console.Error.WriteLine("Photos: hold status unavailable; no photo objects deleted.");
                    return 1;
                }
                Console.WriteLine($"Photos: {media.Deleted} objects deleted, {media.Kept} kept.");
                if (run.CasesExpired > 0) Console.WriteLine($"Law-enforcement cases: {run.CasesExpired} past their seven-year retention erased.");
                Console.WriteLine($"Accounts: {run.Deleted} deleted, {run.Waiting} waiting for Keycloak, {run.ProviderRetries} Didit retries done, {run.Expired} retention ended, {run.HoldsExpired} email holds expired, {run.Kept} kept under a legal hold.");
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
                Console.Error.WriteLine("Usage: migrate | root-admin grant <staff subject> | legal grant|revoke <staff subject> | identity backfill | keys status | keys rotate contact-history|evidence|police|identity|all [--to n] | beta add <email>... | beta add --file <path> | beta revoke <email> | beta list | founders allocate | messages purge | accounts purge | notifications sweep | push tokens protect | media preserve [media-id] | media alerts resolve <id> primary|backup accepted|retry --note-file <path>");
                return 2;
        }
    }
}
