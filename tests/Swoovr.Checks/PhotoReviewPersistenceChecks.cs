using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Swoovr.Infrastructure.Media;
using Swoovr.Api.Accounts;
using Swoovr.Api.Media;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

static class PhotoReviewPersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options, Action<string, bool> check)
    {
        await using var db = new SwoovrDbContext(options); var now = DateTimeOffset.UtcNow;
        Account Account(AccountRealm realm) => new() { Id = Guid.NewGuid(), Realm = realm, Issuer = "https://issuer.example.test", Subject = Guid.NewGuid().ToString(), Status = AccountStatus.Active, CreatedAt = now, LastSeenAt = now };
        var owner = Account(AccountRealm.Members); var first = Account(AccountRealm.Staff); var second = Account(AccountRealm.Staff);
        db.Accounts.AddRange(owner, first, second); await db.SaveChangesAsync();
        db.Add(new MediaSafetyAcknowledgement { StaffAccountId = first.Id, Version = PhotoReviewPolicy.NoticeVersion, AcknowledgedAt = now.AddHours(-4) });
        db.Add(new MediaSafetyAcknowledgement { StaffAccountId = second.Id, Version = "prior-notice", AcknowledgedAt = now }); await db.SaveChangesAsync();
        check("D297 four-hour acknowledgement expires server-side", await PhotoReviewWorkflows.AckUntilAsync(db, first.Id, now, default) is null);
        check("D297 another notice version grants no acknowledgement", await PhotoReviewWorkflows.AckUntilAsync(db, second.Id, now, default) is null);
        var ack = await db.Set<MediaSafetyAcknowledgement>().FindAsync(first.Id, PhotoReviewPolicy.NoticeVersion); ack!.AcknowledgedAt = now; await db.SaveChangesAsync();
        check("D297 current acknowledgement belongs only to its staff account", await PhotoReviewWorkflows.AckUntilAsync(db, first.Id, now, default) == now.AddHours(4)
            && await PhotoReviewWorkflows.AckUntilAsync(db, second.Id, now, default) is null);
        db.Add(new MediaSafetyAcknowledgement { StaffAccountId = second.Id, Version = PhotoReviewPolicy.NoticeVersion, AcknowledgedAt = now }); await db.SaveChangesAsync();
        var media = new MediaItem { Id = Guid.NewGuid(), AccountId = owner.Id, Status = MediaStatus.Rejected, CreatedAt = now, RejectedRetainUntil = now.AddDays(30) }; db.MediaItems.Add(media);
        var decision = PhotoReviewWorkflows.Stage(db, media, first.Id, "reject", "REJECT_OTHER", "Private fictional reviewer note", now); await db.SaveChangesAsync();
        var appeal = new MediaAppeal { Id = Guid.NewGuid(), DecisionId = decision.Id, MediaId = media.Id, AccountId = owner.Id, Text = "Fictional member explanation", SubmittedAt = now }; db.Add(appeal); await db.SaveChangesAsync();
        // Issue 232: the deciding Admin can't see or decide the appeal (D298) but is told it waits for another Admin.
        check("Issue 232 deciding Admin sees the appeal only as waiting for another Admin",
            !await PhotoReviewWorkflows.PendingAppeals(db, first.Id).AnyAsync(a => a.Id == appeal.Id)
            && await PhotoReviewWorkflows.AppealsAwaitingOtherAdmin(db, first.Id).CountAsync(a => a.Id == appeal.Id) == 1);
        check("Issue 232 a different Admin can decide it and is not told it waits for another Admin",
            await PhotoReviewWorkflows.PendingAppeals(db, second.Id).CountAsync(a => a.Id == appeal.Id) == 1
            && !await PhotoReviewWorkflows.AppealsAwaitingOtherAdmin(db, second.Id).AnyAsync(a => a.Id == appeal.Id));
        db.StaffExclusions.Add(new StaffExclusion { StaffAccountId = first.Id, SubjectAccountId = owner.Id, Reason = "Fictional conflict", CreatedBy = second.Id, CreatedAt = now }); await db.SaveChangesAsync();
        check("Issue 232 an excluded Admin is not told about the member's appeal", !await PhotoReviewWorkflows.AppealsAwaitingOtherAdmin(db, first.Id).AnyAsync(a => a.Id == appeal.Id));
        db.StaffExclusions.RemoveRange(db.StaffExclusions.Where(e => e.StaffAccountId == first.Id && e.SubjectAccountId == owner.Id)); await db.SaveChangesAsync();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            db.Add(new MediaAppeal { Id = Guid.NewGuid(), DecisionId = decision.Id, MediaId = media.Id, AccountId = owner.Id, Text = "Duplicate", SubmittedAt = now });
            var refused = false; try { await db.SaveChangesAsync(); } catch (DbUpdateException) { refused = true; }
            await transaction.RollbackAsync(); db.ChangeTracker.Clear(); check("D298 database refuses duplicate appeal per decision", refused);
        }
        db.MemberProfiles.Add(new MemberProfile { AccountId = owner.Id, PublicId = Guid.NewGuid(), DisplayName = "Fictional staff-affiliated member", Employee = true }); await db.SaveChangesAsync();
        check("D134 Support excludes staff-affiliated media while Admin remains eligible", !await PhotoReviewWorkflows.Eligible(db, second.Id, mayReviewStaffAffiliated: false).AnyAsync(i => i.Id == media.Id)
            && await PhotoReviewWorkflows.Eligible(db, second.Id, mayReviewStaffAffiliated: true).AnyAsync(i => i.Id == media.Id));
        var exported = JsonSerializer.Serialize(await DataExportBuilder.BuildAsync(db, owner, now, default));
        check("D298 own export includes appeal text without reviewer notes", exported.Contains("Fictional member explanation") && !exported.Contains("Private fictional reviewer note"));
        check("D298 rejection notice is durable and generic in storage", await db.Notifications.CountAsync(n => n.AccountId == owner.Id && n.Kind == NotificationKind.PhotoDecision && n.SubjectId == decision.Id && n.ActorAccountId == null) == 1);
        var prior = await db.Set<MediaDecisionRecord>().AsNoTracking().SingleAsync(d => d.Id == decision.Id);
        PhotoReviewWorkflows.Stage(db, media, second.Id, "appeal-upheld", "APPEAL_FINAL", "Fictional independent outcome", now.AddSeconds(1)); await db.SaveChangesAsync();
        check("D298 outcome appends prior rejection history", await db.Set<MediaDecisionRecord>().CountAsync(d => d.MediaId == media.Id) == 2
            && (await db.Set<MediaDecisionRecord>().AsNoTracking().SingleAsync(d => d.Id == decision.Id)).Notes == prior.Notes);
        // A real competing connection waits for authority while an independent final appeal is committed.
        // The protected generic pipeline must re-read that appeal before reading/scanning/publishing source bytes.
        var raceMedia = new MediaItem { Id = Guid.NewGuid(), AccountId = owner.Id, Status = MediaStatus.Rejected, CreatedAt = now, ReviewedBy = first.Id, ObjectKey = "quarantine/" + Guid.NewGuid().ToString("N"), RejectedRetainUntil = now.AddDays(30) };
        db.MediaItems.Add(raceMedia); var raceDecision = PhotoReviewWorkflows.Stage(db, raceMedia, first.Id, "reject", "REJECT_PRIVACY", null, now); await db.SaveChangesAsync();
        var folder = Path.Combine(Path.GetTempPath(), "swoovr-photo-finality-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LocalMediaStore(folder); await store.PutAsync(raceMedia.ObjectKey, [1, 2, 3], "image/jpeg", default);
            await using var contender = new SwoovrDbContext(options); await contender.Database.OpenConnectionAsync();
            var pid = ((NpgsqlConnection)contender.Database.GetDbConnection()).ProcessID;
            var clock = new MovingClock(now);
            using var services = new ServiceCollection().AddSingleton(contender).AddSingleton<IMediaStore>(store).AddSingleton<TimeProvider>(clock).BuildServiceProvider();
            var screener = new NeverScreen(); var pipeline = new MediaPipeline(services, new PhotoProcessor(), screener, new MediaSettings(true, null, true), new MediaQueue(), NullLogger<MediaPipeline>.Instance);
            var contenderItem = await contender.MediaItems.SingleAsync(i => i.Id == raceMedia.Id);
            await using var authority = await AccountLifecycleLease.AcquireAsync(db, owner.Id, default);
            var attempt = pipeline.ReconsiderAsync(contenderItem, second.Id, "Fictional delayed reconsideration", now, default);
            var waiting = false;
            try
            {
                for (var n = 0; n < 100 && !waiting; n++)
                {
                    waiting = await db.Database.SqlQuery<bool>($"SELECT EXISTS(SELECT 1 FROM pg_locks WHERE pid={pid} AND locktype='advisory' AND NOT granted) AS \"Value\"").SingleAsync();
                    if (!waiting) await Task.Delay(20);
                }
                check("D298 contender genuinely waits for PostgreSQL account authority", waiting);
                db.Add(new MediaAppeal { Id = Guid.NewGuid(), DecisionId = raceDecision.Id, MediaId = raceMedia.Id, AccountId = owner.Id, Text = "Fictional concurrent appeal", SubmittedAt = now, DecidedAt = now, DecidedBy = second.Id, Outcome = "upheld" });
                await db.SaveChangesAsync(); // Commit before the delayed reconsideration obtains authority.
            }
            finally { await authority.DisposeAsync(); }
            check("D298 committed final appeal prevents delayed re-screen and publication", !await attempt && screener.Calls == 0
                && await db.MediaItems.AsNoTracking().AnyAsync(i => i.Id == raceMedia.Id && i.Status == MediaStatus.Rejected)
                && !await db.Set<MediaDecisionRecord>().AnyAsync(d => d.MediaId == raceMedia.Id && d.Decision == "reconsider-request"));
            async Task ClockWhileWaiting(TimeSpan advance, bool renewAcknowledgement)
            {
                var expiring = new MediaItem { Id = Guid.NewGuid(), AccountId = owner.Id, Status = MediaStatus.Rejected, CreatedAt = now, ReviewedBy = first.Id, ObjectKey = "quarantine/" + Guid.NewGuid().ToString("N"), RejectedRetainUntil = now.AddDays(30) };
                db.MediaItems.Add(expiring); PhotoReviewWorkflows.Stage(db, expiring, first.Id, "reject", "REJECT_PRIVACY", null, now); await db.SaveChangesAsync();
                await store.PutAsync(expiring.ObjectKey, [1, 2, 3], "image/jpeg", default);
                clock.Now = now;
                await db.Set<MediaSafetyAcknowledgement>().Where(a => a.StaffAccountId == second.Id && a.Version == PhotoReviewPolicy.NoticeVersion).ExecuteUpdateAsync(u => u.SetProperty(a => a.AcknowledgedAt, now));
                contender.ChangeTracker.Clear(); var blockedItem = await contender.MediaItems.SingleAsync(i => i.Id == expiring.Id);
                await using var mediaAuthority = await AccountLifecycleLease.AcquireMediaAsync(db, owner.Id, default);
                var delayed = pipeline.ReconsiderAsync(blockedItem, second.Id, "Fictional delayed clock test", now, default);
                try
                {
                    var blocked = false;
                    for (var n = 0; n < 100 && !blocked; n++)
                    {
                        blocked = await db.Database.SqlQuery<bool>($"SELECT EXISTS(SELECT 1 FROM pg_locks WHERE pid={pid} AND locktype='advisory' AND NOT granted) AS \"Value\"").SingleAsync();
                        if (!blocked) await Task.Delay(20);
                    }
                    check("D298 clock probe waits on actual media authority", blocked);
                    clock.Now = now + advance;
                    if (renewAcknowledgement) await db.Set<MediaSafetyAcknowledgement>().Where(a => a.StaffAccountId == second.Id && a.Version == PhotoReviewPolicy.NoticeVersion).ExecuteUpdateAsync(u => u.SetProperty(a => a.AcknowledgedAt, clock.Now));
                }
                finally { await mediaAuthority.DisposeAsync(); }
                check(renewAcknowledgement ? "D298 expired retention is rechecked after media wait despite stale caller clock" : "D297 expired acknowledgement is rechecked after media wait despite stale caller clock", !await delayed && screener.Calls == 0
                    && !await db.Set<MediaDecisionRecord>().AnyAsync(d => d.MediaId == expiring.Id && d.Decision == "reconsider-request"));
                await db.MediaItems.Where(i => i.Id == expiring.Id).ExecuteDeleteAsync();
            }
            await ClockWhileWaiting(TimeSpan.FromHours(4), false);
            await ClockWhileWaiting(TimeSpan.FromDays(31), true);
            // A fresh remote response cannot revive an acknowledgement that expired during that response.
            var remote = new MediaItem { Id = Guid.NewGuid(), AccountId = owner.Id, Status = MediaStatus.Rejected, CreatedAt = now, ReviewedBy = first.Id, ObjectKey = "quarantine/" + Guid.NewGuid().ToString("N"), RejectedRetainUntil = now.AddDays(30) };
            db.MediaItems.Add(remote); PhotoReviewWorkflows.Stage(db, remote, first.Id, "reject", "REJECT_PRIVACY", null, now); await db.SaveChangesAsync();
            await store.PutAsync(remote.ObjectKey, [1, 2, 3], "image/jpeg", default); clock.Now = now;
            await db.Set<MediaSafetyAcknowledgement>().Where(a => a.StaffAccountId == second.Id && a.Version == PhotoReviewPolicy.NoticeVersion).ExecuteUpdateAsync(u => u.SetProperty(a => a.AcknowledgedAt, now));
            contender.ChangeTracker.Clear(); var remoteItem = await contender.MediaItems.SingleAsync(i => i.Id == remote.Id);
            var remoteScreen = new AdvancingScreen(clock); var remotePipeline = new MediaPipeline(services, new PhotoProcessor(), remoteScreen, new MediaSettings(true, null, true), new MediaQueue(), NullLogger<MediaPipeline>.Instance);
            check("D297 acknowledgement is refreshed after remote screening before approval", !await remotePipeline.ReconsiderAsync(remoteItem, second.Id, "Fictional remote clock test", now, default) && remoteScreen.Calls == 1
                && await db.MediaItems.AsNoTracking().AnyAsync(i => i.Id == remote.Id && i.Status == MediaStatus.Rejected));
            await db.MediaItems.Where(i => i.Id == remote.Id).ExecuteDeleteAsync();
            await contender.Database.CloseConnectionAsync();
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        await db.MediaItems.Where(i => i.Id == raceMedia.Id).ExecuteDeleteAsync();
        db.Accounts.RemoveRange(await db.Accounts.Where(a => new[] { owner.Id, first.Id, second.Id }.Contains(a.Id)).ToListAsync()); await db.SaveChangesAsync();
        await db.MediaItems.Where(i => i.Id == media.Id).ExecuteDeleteAsync();
    }
    private sealed class MovingClock(DateTimeOffset now) : TimeProvider
    { public DateTimeOffset Now = now; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class AdvancingScreen(MovingClock clock) : IMediaScreening
    {
        public string Name => "simulated";
        public int Calls;
        public Task<ScreeningResponse> ScreenAsync(byte[] image, CancellationToken cancellationToken)
        { Calls++; clock.Now = clock.Now.AddHours(4); return Task.FromResult(new ScreeningResponse(null, EngineState.Completed, [], EngineState.Completed, false, EngineState.Completed, "no-known-match")); }
    }
    private sealed class NeverScreen : IMediaScreening
    {
        public string Name => "simulated";
        public int Calls;
        public Task<ScreeningResponse> ScreenAsync(byte[] image, CancellationToken cancellationToken)
        { Calls++; throw new InvalidOperationException("A final appeal must refuse before screening."); }
    }
}
