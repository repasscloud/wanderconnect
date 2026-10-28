using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Notifications;

/// <summary>Where a notification opens, and the trip's place for a travel reminder.</summary>
public static class NotificationLinks
{
    public static async Task<string> ForAsync(SwoovrDbContext db, Notification n, CancellationToken cancellationToken) => n.Kind switch
    {
        NotificationKind.Message => $"/chats/{n.SubjectId}",
        NotificationKind.Request => "/connections",
        NotificationKind.Connection when n.ActorAccountId is { } actor
            && await db.MemberProfiles.Where(p => p.AccountId == actor).Select(p => (Guid?)p.PublicId).SingleOrDefaultAsync(cancellationToken) is { } person
            => $"/people/{person}?mode={n.ModeId}",
        NotificationKind.Connection => "/connections",
        NotificationKind.Travel => "/travel",
        _ => "/me/notices",
    };

    public static Task<string?> PlaceAsync(SwoovrDbContext db, Guid trip, CancellationToken cancellationToken) =>
        (from t in db.Trips join c in db.GeoCities on t.GeoNameId equals c.GeoNameId where t.Id == trip select c.Name).SingleOrDefaultAsync(cancellationToken);
}

/// <summary>
/// The member's notification centre and settings (#49; policy §10). The centre is worded when read: an item from someone
/// now blocked (either way), restricted, suspended, terminated or deleted disappears, and a barred member sees account
/// notices only. Settings: kinds, push, email, private previews, quiet hours, muted modes and photos in chat. Push devices
/// are served by the labelled simulated sender until APNs and FCM exist (Q83).
/// </summary>
public static class NotificationEndpoints
{
    public static void Map(WebApplication app)
    {
        var me = app.MapGroup("/api/v1/me").RequireAuthorization();

        me.MapGet("/notifications", async (ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken, ageGateExempt: true, barredExempt: true);
            if (denied is not null) return denied;
            return Results.Ok(await CentreAsync(accounts.Db!, current!.Account!.Id, current.Enforcement.Barred, clock.GetUtcNow(), cancellationToken));
        });
        me.MapPost("/notifications/{id:guid}/read", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken, ageGateExempt: true, barredExempt: true);
            if (denied is not null) return denied;
            var item = await accounts.Db!.Notifications.SingleOrDefaultAsync(n => n.Id == id && n.AccountId == current!.Account!.Id, cancellationToken);
            if (item is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotAvailable, "That notification isn't available.");
            item.ReadAt ??= clock.GetUtcNow();
            await accounts.Db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });
        me.MapPost("/notifications/read", async (ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken, ageGateExempt: true, barredExempt: true);
            if (denied is not null) return denied;
            var now = clock.GetUtcNow();
            await accounts.Db!.Notifications.Where(n => n.AccountId == current!.Account!.Id && n.ReadAt == null)
                .ExecuteUpdateAsync(u => u.SetProperty(n => n.ReadAt, now), cancellationToken);
            return Results.NoContent();
        });

        me.MapGet("/notification-settings", async (ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            return denied ?? Results.Ok(await SettingsViewAsync(accounts.Db!, current!.Account!.Id, cancellationToken));
        });
        me.MapPut("/notification-settings", async (NotificationSettingsUpdate update, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, account, now) = (accounts.Db!, current!.Account!.Id, clock.GetUtcNow());
            var from = Time(update.QuietFrom);
            var until = Time(update.QuietUntil);
            var zone = update.TimeZone ?? NotificationPolicy.DefaultTimeZone;
            var modes = await db.ConnectionModes.Select(m => m.Id).ToListAsync(cancellationToken);
            var muted = update.MutedModes ?? [];
            if (from.Invalid || until.Invalid || from.Value is null != until.Value is null || NotificationPolicy.Zone(zone) is null
                || muted.Distinct().Count() != muted.Count || muted.Any(m => !modes.Contains(m)))
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidSettings, "Check the quiet hours (both times, as HH:mm), the time zone and the modes.");
            var settings = await db.NotificationSettings.SingleOrDefaultAsync(s => s.AccountId == account, cancellationToken);
            if (settings is null) db.NotificationSettings.Add(settings = new NotificationSettings { AccountId = account });
            settings.Messages = update.Messages ?? true;
            settings.Requests = update.Requests ?? true;
            settings.Connections = update.Connections ?? true;
            settings.Travel = update.Travel ?? true;
            settings.Push = update.Push ?? true;
            settings.Email = update.Email ?? true;
            settings.PrivatePreviews = update.PrivatePreviews ?? true;
            settings.ChatPhotos = update.ChatPhotos ?? true;
            (settings.QuietFrom, settings.QuietUntil, settings.TimeZone, settings.UpdatedAt) = (from.Value, until.Value, zone, now);
            await db.NotificationModeMutes.Where(m => m.AccountId == account).ExecuteDeleteAsync(cancellationToken);
            db.NotificationModeMutes.AddRange(muted.Select(m => new NotificationModeMute { AccountId = account, ModeId = m }));
            Audit.Record(db, now, account, "account", "notifications.settings", "account", account.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(await SettingsViewAsync(db, account, cancellationToken));
        });

        me.MapGet("/devices", async (ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            return Results.Ok(await accounts.Db!.PushDevices.Where(d => d.AccountId == current!.Account!.Id).OrderBy(d => d.CreatedAt)
                .Select(d => new PushDeviceView(d.Id, d.Platform, d.CreatedAt, true)).ToListAsync(cancellationToken));
        });
        me.MapPost("/devices", async (PushDeviceRegistration registration, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, account, now) = (accounts.Db!, current!.Account!.Id, clock.GetUtcNow());
            if (!NotificationPolicy.ValidPlatform(registration.Platform) || !NotificationPolicy.ValidToken(registration.Token))
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidDevice, "That device can't be registered.");
            // A device signed in to by someone else stops receiving the previous person's notifications.
            await db.PushDevices.Where(d => d.Token == registration.Token && d.AccountId != account).ExecuteDeleteAsync(cancellationToken);
            var device = await db.PushDevices.SingleOrDefaultAsync(d => d.AccountId == account && d.Token == registration.Token, cancellationToken);
            if (device is not null)
            {
                (device.Platform, device.LastSeenAt) = (registration.Platform!, now);
                await db.SaveChangesAsync(cancellationToken);
                return Results.Ok(new PushDeviceView(device.Id, device.Platform, device.CreatedAt, true));
            }
            if (await db.PushDevices.CountAsync(d => d.AccountId == account, cancellationToken) >= NotificationPolicy.DevicesMax)
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.DeviceLimit, $"You can receive notifications on up to {NotificationPolicy.DevicesMax} devices.");
            db.PushDevices.Add(device = new PushDevice { Id = Guid.NewGuid(), AccountId = account, Platform = registration.Platform!, Token = registration.Token!, CreatedAt = now, LastSeenAt = now });
            Audit.Record(db, now, account, "account", "device.registered", "push-device", device.Id.ToString(), device.Platform);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/v1/me/devices/{device.Id}", new PushDeviceView(device.Id, device.Platform, device.CreatedAt, true));
        });
        me.MapDelete("/devices/{id:guid}", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            // Open even while barred, so a member can always stop notifications reaching a device.
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken, ageGateExempt: true, barredExempt: true);
            if (denied is not null) return denied;
            var account = current!.Account!.Id;
            var deleted = await accounts.Db!.PushDevices.Where(d => d.Id == id && d.AccountId == account).ExecuteDeleteAsync(cancellationToken);
            if (deleted == 0) return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotAvailable, "That device isn't registered.");
            Audit.Record(accounts.Db, clock.GetUtcNow(), account, "account", "device.removed", "push-device", id.ToString());
            await accounts.Db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });
    }

    private static (TimeOnly? Value, bool Invalid) Time(string? text) =>
        string.IsNullOrEmpty(text) ? (null, false)
        : TimeOnly.TryParseExact(text, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? (time, false) : (null, true);

    public static async Task<NotificationSettingsView> SettingsViewAsync(SwoovrDbContext db, Guid account, CancellationToken cancellationToken)
    {
        var s = await Notifier.SettingsAsync(db, account, cancellationToken);
        var muted = await db.NotificationModeMutes.Where(m => m.AccountId == account).OrderBy(m => m.ModeId).Select(m => m.ModeId).ToListAsync(cancellationToken);
        var devices = await db.PushDevices.CountAsync(d => d.AccountId == account, cancellationToken);
        return new(s.Messages, s.Requests, s.Connections, s.Travel, s.Push, s.Email, s.PrivatePreviews, s.QuietFrom?.ToString("HH:mm", CultureInfo.InvariantCulture),
            s.QuietUntil?.ToString("HH:mm", CultureInfo.InvariantCulture), s.TimeZone, muted, s.ChatPhotos, devices);
    }

    /// <summary>The centre as the member may see it now: worded here, from ids, with the rules applied again.</summary>
    public static async Task<NotificationCentre> CentreAsync(SwoovrDbContext db, Guid me, bool barred, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var rows = await db.Notifications.Where(n => n.AccountId == me && (!barred || n.Kind == NotificationKind.Account))
            .OrderByDescending(n => n.UpdatedAt).Take(NotificationPolicy.PageSize * 2).ToListAsync(cancellationToken);
        var actors = rows.Where(n => n.ActorAccountId is not null).Select(n => n.ActorAccountId!.Value).Distinct().ToList();
        var allowed = new Dictionary<Guid, bool>();
        foreach (var actor in actors) allowed[actor] = await Notifier.AllowedAsync(db, me, actor, now, cancellationToken);
        var names = await db.MemberProfiles.Where(p => actors.Contains(p.AccountId)).ToDictionaryAsync(p => p.AccountId, p => p.DisplayName ?? "Someone", cancellationToken);
        var labels = await db.ConnectionModes.ToDictionaryAsync(m => m.Id, m => m.Label, cancellationToken);
        var tripIds = rows.Where(n => n.Kind == NotificationKind.Travel && n.SubjectId is not null).Select(n => n.SubjectId!.Value).ToList();
        var trips = await (from t in db.Trips join c in db.GeoCities on t.GeoNameId equals c.GeoNameId
                           where tripIds.Contains(t.Id) && t.CancelledAt == null select new { t.Id, t.Arrival, c.Name, c.TimeZone }).ToDictionaryAsync(t => t.Id, cancellationToken);
        var items = new List<NotificationView>();
        foreach (var n in rows)
        {
            if (n.ActorAccountId is { } actor && !allowed[actor]) continue;
            var name = n.ActorAccountId is { } who && names.TryGetValue(who, out var known) ? known : "Someone";
            var mode = n.ModeId is { } id && labels.TryGetValue(id, out var label) ? label : null;
            string? title = n.Kind switch
            {
                NotificationKind.Message => n.Count > 1 ? $"{n.Count} new messages from {name}" : $"New message from {name}",
                NotificationKind.Request => mode is null ? $"{name} sent you a request" : $"{name} sent you a {mode} request",
                NotificationKind.Connection => mode is null ? $"{name} accepted your request" : $"{name} accepted your {mode} request",
                NotificationKind.Travel when n.SubjectId is { } trip && trips.TryGetValue(trip, out var t) => $"Your trip to {t.Name} starts {When(t.Arrival, t.TimeZone, now)}",
                NotificationKind.Travel => null,
                _ => "There's a notice about your account",
            };
            if (title is null) continue;
            items.Add(new(n.Id, Wire(n.Kind), title, await NotificationLinks.ForAsync(db, n, cancellationToken), n.Count, n.UpdatedAt, n.ReadAt is not null));
            if (items.Count == NotificationPolicy.PageSize) break;
        }
        return new(items, items.Count(i => !i.Read));
    }

    private static string When(DateOnly arrival, string zone, DateTimeOffset now)
    {
        var local = NotificationPolicy.Zone(zone) is { } tz ? TimeZoneInfo.ConvertTime(now, tz).DateTime : now.UtcDateTime;
        var today = DateOnly.FromDateTime(local);
        return arrival == today ? "today" : arrival == today.AddDays(1) ? "tomorrow" : "on " + arrival.ToString("d MMMM", CultureInfo.InvariantCulture);
    }
}
