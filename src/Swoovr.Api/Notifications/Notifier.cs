using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Messaging;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Notifications;

/// <summary>
/// Delivers notifications (#49; policy §10; D135). Every call checks, at that moment, that the person who caused it is
/// active, not restricted, suspended or terminated, and not blocked either way; that the recipient may hear about it;
/// and the recipient's switches, muted modes and quiet hours. The centre row holds ids only; deliveries record what each
/// channel did. A failure here never fails the action that caused it.
/// </summary>
public sealed class Notifier(IServiceProvider services, IPushSender push, IEmailSender email, ILogger<Notifier> log)
{
    private PushKeys Keys => services.GetRequiredService<PushKeys>();
    private SwoovrDbContext Db => services.GetRequiredService<SwoovrDbContext>();

    /// <param name="text">A message's text, used only for a lock screen when the recipient turned private previews off; never stored.</param>
    /// <param name="dedupe">A key that makes this notification happen once (reminders).</param>
    public async Task NotifyAsync(Guid recipient, NotificationKind kind, Guid? actor, string? mode, Guid? subject, DateTimeOffset now, CancellationToken cancellationToken,
        string? text = null, string? dedupe = null)
    {
        try { await DeliverAsync(recipient, kind, actor, mode, subject, now, text, dedupe, cancellationToken); }
        catch (Exception error) when (error is DbUpdateException or InvalidOperationException or HttpRequestException or IOException)
        {
            log.LogWarning("Notification {Kind} for {Account} not delivered: {Error}.", kind, recipient, error.GetType().Name);
        }
    }

    /// <summary>D213: add the centre/outbox item to the caller's closure transaction; no external send before commit.</summary>
    public async Task<Guid?> QueueClosureAsync(Guid recipient, Guid actor, Guid conversation, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!await ActiveAsync(Db, recipient, cancellationToken) || await BarredAsync(Db, recipient, now, cancellationToken)
            || !await AllowedAsync(Db, recipient, actor, now, cancellationToken)) return null;
        var notification = new Notification
        {
            Id = Guid.NewGuid(), AccountId = recipient, Kind = NotificationKind.ConversationEnded, ActorAccountId = actor, SubjectId = conversation,
            CreatedAt = now, UpdatedAt = now, DedupeKey = $"conversation-ended:{conversation}",
        };
        Db.Notifications.Add(notification);
        return notification.Id;
    }

    /// <summary>
    /// Session lock permits each device's intent/outcome to commit separately. An uncertain attempted send is never
    /// blindly repeated: it is recorded as unknown, not as delivery. A later device cannot roll back earlier progress.
    /// </summary>
    public async Task DispatchClosureAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken, bool retry = false)
    {
        const int lockNamespace = 0x53574F4F;
        var lockKey = BinaryPrimitives.ReadInt32BigEndian(SHA256.HashData(id.ToByteArray()));
        await Db.Database.OpenConnectionAsync(cancellationToken);
        var locked = false;
        try
        {
            // Also attempt unlock if cancellation races the server's successful lock acquisition.
            locked = true;
            await Db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_lock({lockNamespace}, {lockKey})", cancellationToken);
            var notification = await Db.Notifications.SingleOrDefaultAsync(n => n.Id == id && (retry || n.Kind == NotificationKind.ConversationEnded), cancellationToken);
            if (notification is null || !retry && await Db.NotificationDeliveries.AnyAsync(d => d.NotificationId == id && d.Channel == "dispatch" && d.Status == "complete", cancellationToken)) return;
            await DeliverAsync(notification.AccountId, notification.Kind, notification.ActorAccountId, notification.ModeId, notification.SubjectId, now, null,
                notification.DedupeKey, cancellationToken, notification);
            // A due retry that was not attempted is now suppressed/unavailable, not an endless retry loop.
            if (retry) await Db.NotificationDeliveries.Where(d => d.NotificationId == id && d.Status == "retry" && d.NextAttemptAt <= now)
                .ExecuteUpdateAsync(u => u.SetProperty(d => d.Status, "suppressed").SetProperty(d => d.NextAttemptAt, (DateTimeOffset?)null), cancellationToken);
            if (!await Db.NotificationDeliveries.AnyAsync(d => d.NotificationId == id && d.Channel == "dispatch" && d.Status == "complete", cancellationToken))
                Record(Db, notification, "dispatch", "complete", null, now);
            await Db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception error) when (error is DbUpdateException or InvalidOperationException or HttpRequestException or IOException)
        {
            Db.ChangeTracker.Clear();
            log.LogWarning("Closure notification {Id} remains queued after {Error}.", id, error.GetType().Name);
        }
        finally
        {
            try
            {
                if (locked)
                {
                    using var release = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await Db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock({lockNamespace}, {lockKey})", release.Token);
                }
            }
            finally { await Db.Database.CloseConnectionAsync(); }
        }
    }

    private async Task SendClosureDeviceAsync(Notification notification, PushDevice device, string title, string body, string link,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var prior = await Db.NotificationDeliveries.SingleOrDefaultAsync(d => d.NotificationId == notification.Id && d.Channel == "push" && d.DeviceId == device.Id, cancellationToken);
        if (prior is not null)
        {
            if (prior.Status == "attempting")
            {
                prior.Status = "unknown";
                await Db.SaveChangesAsync(cancellationToken);
            }
            if (prior.Status != "retry" || prior.NextAttemptAt > now || prior.Attempts >= 3) return;
        }
        // Durable intent precedes the external side effect, so a crash after provider acceptance cannot cause blind resend.
        var delivery = prior ?? new NotificationDelivery { NotificationId = notification.Id, Channel = "push", DeviceId = device.Id, At = now };
        if (prior is null) Db.NotificationDeliveries.Add(delivery);
        delivery.Status = "attempting"; delivery.Attempts = (delivery.Attempts ?? 0) + 1; delivery.AttemptId ??= Guid.NewGuid(); delivery.NextAttemptAt = null;
        await Db.SaveChangesAsync(cancellationToken);
        try
        {
            var accepted = await SendProtectedAsync(device, title, body, link, notification.Kind == NotificationKind.Account, delivery.AttemptId.Value, cancellationToken);
            delivery.Status = accepted.Status;
            if (accepted.Status == "retry")
            {
                var delay = accepted.RetryAfter ?? TimeSpan.FromMinutes(1);
                if (delivery.Attempts >= 3) delivery.Status = "failed";
                else if (delay > TimeSpan.FromDays(1)) delivery.Status = "deferred"; // Never retry sooner than a long provider Retry-After.
                else delivery.NextAttemptAt = now + (delay < TimeSpan.FromMinutes(1) ? TimeSpan.FromMinutes(1) : delay);
            }
            if (accepted.InvalidToken)
                await Db.PushDevices.Where(d => d.Id == device.Id && d.Token == device.Token)
                    .ExecuteUpdateAsync(u => u.SetProperty(d => d.InvalidatedAt, now).SetProperty(d => d.TokenCipher, (byte[]?)null).SetProperty(d => d.TokenKeyVersion, (int?)null), cancellationToken);
        }
        catch (Exception error) when (error is HttpRequestException or IOException or InvalidOperationException or OperationCanceledException)
        {
            delivery.Status = "unknown";
            log.LogWarning("Closure push {Id} to device {Device} has an unknown provider outcome ({Error}).", notification.Id, device.Id, error.GetType().Name);
        }
        using var persist = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await Db.SaveChangesAsync(persist.Token);
    }

    private async Task<PushOutcome> SendProtectedAsync(PushDevice device, string title, string body, string link, bool highPriority, Guid attempt, CancellationToken cancellationToken)
    {
        if (device.InvalidatedAt is not null || device.TokenCipher is null || device.TokenKeyVersion is not { } version || Keys.Protector is not { } protector) return new("unavailable");
        string token;
        try { token = protector.Unprotect(device.Id, device.AccountId, device.Platform, version, device.TokenCipher); }
        catch (CryptographicException) { log.LogWarning("Push token for device {Device} could not be opened; no send attempted.", device.Id); return new("key-unavailable"); }
        return await push.SendAsync(new PushMessage(device.Id, device.Platform, token, title, body, link, highPriority) { AttemptId = attempt }, cancellationToken);
    }

    private async Task DeliverAsync(Guid recipient, NotificationKind kind, Guid? actor, string? mode, Guid? subject, DateTimeOffset now, string? text, string? dedupe,
        CancellationToken cancellationToken, Notification? prepared = null)
    {
        var db = Db;
        if(kind==NotificationKind.Message&&!await Swoovr.Api.Connections.BetaMessageAudience.AllowsAsync(services,recipient,cancellationToken))return;
        var settings = await SettingsAsync(db, recipient, cancellationToken);
        var muted = await db.NotificationModeMutes.Where(m => m.AccountId == recipient).Select(m => m.ModeId).ToListAsync(cancellationToken);
        var modeMuted = mode is not null && muted.Contains(mode);
        if (kind is NotificationKind.Message or NotificationKind.ConversationEnded && actor is { } sender && muted.Count > 0)
        {
            // A conversation is shared across modes (D143): muted only when every mode connecting the pair is muted.
            var (low, high) = ConnectionRequest.Pair(recipient, sender);
            var closureAt = prepared?.CreatedAt ?? now;
            var modes = await db.ConnectionRequests.Where(r => r.PairLow == low && r.PairHigh == high && (r.Status == ConnectionRequestStatus.Accepted || kind == NotificationKind.ConversationEnded && r.Status == ConnectionRequestStatus.Disconnected && r.EndedAt == closureAt))
                .Select(r => r.ModeId).Distinct().ToListAsync(cancellationToken);
            modeMuted = modes.Count > 0 && modes.All(muted.Contains);
        }
        var decision = NotificationPolicy.Decide(new NotificationFacts(kind,
            await ActiveAsync(db, recipient, cancellationToken), await BarredAsync(db, recipient, now, cancellationToken),
            actor is not null, actor is not { } a || await AllowedAsync(db, recipient, a, now, cancellationToken),
            NotificationPolicy.KindOn(settings, kind), modeMuted, NotificationPolicy.InQuietHours(settings, now), settings.Push, settings.Email));
        if (!decision.Centre)
        {
            if (prepared is not null)
            {
                if (!await db.NotificationDeliveries.AnyAsync(d => d.NotificationId == prepared.Id && d.Channel == "push" && d.DeviceId == null, cancellationToken))
                    Record(db, prepared, "push", NotificationPolicy.Suppressed, null, now);
                await db.SaveChangesAsync(cancellationToken);
            }
            return;
        }

        // Unread messages from one conversation collapse into one item; only the first is pushed.
        if (prepared is null && kind == NotificationKind.Message && subject is not null
            && await db.Notifications.SingleOrDefaultAsync(n => n.AccountId == recipient && n.Kind == kind && n.SubjectId == subject && n.ReadAt == null, cancellationToken) is { } open)
        {
            (open.Count, open.UpdatedAt) = (open.Count + 1, now);
            await db.SaveChangesAsync(cancellationToken);
            return;
        }
        Notification notification;
        if (prepared is not null) notification = prepared;
        else
        {
            if (dedupe is not null && await db.Notifications.AnyAsync(n => n.AccountId == recipient && n.DedupeKey == dedupe, cancellationToken)) return;
            notification = new Notification
            {
                Id = Guid.NewGuid(), AccountId = recipient, Kind = kind, ActorAccountId = actor, ModeId = mode, SubjectId = subject, CreatedAt = now, UpdatedAt = now,
                DedupeKey = dedupe,
            };
            db.Notifications.Add(notification);
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException) when (dedupe is not null)
            {
                db.ChangeTracker.Clear();
                return;
            }
        }

        var link = await NotificationLinks.ForAsync(db, notification, cancellationToken);
        if (decision.Push == NotificationPolicy.Send)
        {
            var devices = await db.PushDevices.Where(d => d.AccountId == recipient && d.InvalidatedAt == null && d.TokenCipher != null).ToListAsync(cancellationToken);
            if (devices.Count == 0)
            {
                if (prepared is null || !await db.NotificationDeliveries.AnyAsync(d => d.NotificationId == notification.Id && d.Channel == "push" && d.DeviceId == null, cancellationToken))
                    Record(db, notification, "push", "no-device", null, now);
            }
            else
            {
                var name = actor is { } who ? await db.MemberProfiles.Where(p => p.AccountId == who).Select(p => p.DisplayName).SingleOrDefaultAsync(cancellationToken) : null;
                var label = mode is null ? null : await db.ConnectionModes.Where(m => m.Id == mode).Select(m => m.Label).SingleOrDefaultAsync(cancellationToken);
                var place = kind == NotificationKind.Travel && subject is { } trip ? await NotificationLinks.PlaceAsync(db, trip, cancellationToken) : null;
                var (title, body) = NotificationPolicy.Push(kind, settings.PrivatePreviews, name, label, text, place);
                foreach (var device in devices)
                {
                    await SendClosureDeviceAsync(notification, device, title, body, link, now, cancellationToken);
                }
            }
        }
        else if (decision.Push != NotificationPolicy.NotForKind
            && (prepared is null || !await db.NotificationDeliveries.AnyAsync(d => d.NotificationId == notification.Id && d.Channel == "push" && d.DeviceId == null, cancellationToken)))
            Record(db, notification, "push", decision.Push, null, now);

        if (prepared is not null && await db.NotificationDeliveries.AnyAsync(d => d.NotificationId == notification.Id && d.Channel == "email", cancellationToken)) { /* Retry never repeats email. */ }
        else if (decision.Email == NotificationPolicy.Send)
        {
            var address = await db.Accounts.Where(x => x.Id == recipient).Select(x => x.Email).SingleOrDefaultAsync(cancellationToken);
            if (address is null) Record(db, notification, "email", "no-address", null, now);
            else
            {
                var (subjectLine, body) = NotificationPolicy.Email(kind);
                Record(db, notification, "email", await email.SendAsync(new EmailMessage(address, subjectLine, body), cancellationToken) ? "sent" : "failed", null, now);
            }
        }
        else if (decision.Email != NotificationPolicy.NotForKind) Record(db, notification, "email", decision.Email, null, now);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static void Record(SwoovrDbContext db, Notification notification, string channel, string status, Guid? device, DateTimeOffset now) =>
        db.NotificationDeliveries.Add(new NotificationDelivery { NotificationId = notification.Id, Channel = channel, Status = status, DeviceId = device, At = now });

    /// <summary>The member's settings, or the defaults when they never saved any.</summary>
    public static async Task<NotificationSettings> SettingsAsync(SwoovrDbContext db, Guid account, CancellationToken cancellationToken) =>
        await db.NotificationSettings.AsNoTracking().SingleOrDefaultAsync(s => s.AccountId == account, cancellationToken) ?? new NotificationSettings { AccountId = account };

    private static Task<bool> ActiveAsync(SwoovrDbContext db, Guid account, CancellationToken cancellationToken) =>
        db.Accounts.AnyAsync(a => a.Id == account && a.Status == AccountStatus.Active, cancellationToken);

    /// <summary>Restricted, suspended or terminated right now (D160); a warning bars nothing.</summary>
    public static Task<bool> BarredAsync(SwoovrDbContext db, Guid account, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.Sanctions.AnyAsync(s => s.AccountId == account && s.LiftedAt == null && s.StartsAt <= now && (s.EndsAt == null || s.EndsAt > now)
            && s.Kind != SanctionKind.Warning, cancellationToken);

    /// <summary>The person who caused it is active, not barred, has a profile, and neither of them blocked the other.</summary>
    public static async Task<bool> AllowedAsync(SwoovrDbContext db, Guid recipient, Guid actor, DateTimeOffset now, CancellationToken cancellationToken) =>
        await ActiveAsync(db, actor, cancellationToken) && !await BarredAsync(db, actor, now, cancellationToken)
        && await db.MemberProfiles.AnyAsync(p => p.AccountId == actor, cancellationToken)
        && !await db.Blocks.AnyAsync(b => (b.BlockerAccountId == recipient && b.BlockedAccountId == actor) || (b.BlockerAccountId == actor && b.BlockedAccountId == recipient), cancellationToken);

    /// <summary>
    /// Trip-start reminders (#49): once per trip, the day before it starts (or on the day, if it was added late) in the
    /// destination's time zone, to the traveller only. Returns how many were written.
    /// </summary>
    public async Task<int> TravelRemindersAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var db = Db;
        // Provider requests have a bounded HTTP timeout. Stale durable intents are uncertain, never resend candidates.
        var stale = now - TimeSpan.FromMinutes(5);
        await db.NotificationDeliveries.Where(d => d.Channel == "push" && d.Status == "attempting" && d.At < stale)
            .ExecuteUpdateAsync(u => u.SetProperty(d => d.Status, "unknown").SetProperty(d => d.NextAttemptAt, (DateTimeOffset?)null), cancellationToken);
        // Closure outbox items contain ids only. Serialize dispatch per item; never duplicate the centre entry.
        var pendingClosures = await db.Notifications.AsNoTracking().Where(n => n.Kind == NotificationKind.ConversationEnded
                && !db.NotificationDeliveries.Any(d => d.NotificationId == n.Id && d.Channel == "dispatch" && d.Status == "complete"))
            .OrderBy(n => n.CreatedAt).Select(n => n.Id).Take(100).ToListAsync(cancellationToken);
        foreach (var id in pendingClosures) await DispatchClosureAsync(id, now, cancellationToken);
        var retries = await db.NotificationDeliveries.AsNoTracking().Where(d => d.Channel == "push" && d.Status == "retry" && d.Attempts < 3 && d.NextAttemptAt <= now)
            .Select(d => d.NotificationId).Distinct().Take(100).ToListAsync(cancellationToken);
        foreach (var id in retries) await DispatchClosureAsync(id, now, cancellationToken, retry: true);
        var earliest = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-1);
        var latest = earliest.AddDays(3);
        var trips = await (from t in db.Trips join c in db.GeoCities on t.GeoNameId equals c.GeoNameId
                           where t.CancelledAt == null && t.ArrivedAt == null && t.Arrival >= earliest && t.Arrival <= latest
                           select new { t.Id, t.AccountId, t.Arrival, c.TimeZone }).ToListAsync(cancellationToken);
        var sent = 0;
        foreach (var trip in trips)
        {
            var zone = NotificationPolicy.Zone(trip.TimeZone);
            var today = DateOnly.FromDateTime(zone is null ? now.UtcDateTime : TimeZoneInfo.ConvertTime(now, zone).DateTime);
            if (trip.Arrival != today && trip.Arrival != today.AddDays(1)) continue;
            var key = $"trip-start:{trip.Id}";
            if (await db.Notifications.AnyAsync(n => n.AccountId == trip.AccountId && n.DedupeKey == key, cancellationToken)) continue;
            await NotifyAsync(trip.AccountId, NotificationKind.Travel, null, null, trip.Id, now, cancellationToken, dedupe: key);
            if (await db.Notifications.AnyAsync(n => n.AccountId == trip.AccountId && n.DedupeKey == key, cancellationToken)) sent++;
        }
        return sent;
    }
}

/// <summary>Recovers pending closure notifications and writes travel reminders every 15 minutes (the `notifications sweep` command does the same once).</summary>
public sealed class NotificationWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<NotificationWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<Notifier>().TravelRemindersAsync(clock.GetUtcNow(), stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                log.LogError(exception, "Notification sweep failed.");
            }
            await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
        }
    }
}
