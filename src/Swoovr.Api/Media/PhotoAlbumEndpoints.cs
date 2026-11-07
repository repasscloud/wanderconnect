using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Media;

/// <summary>
/// #231 (owner, 6–7 November 2026): private albums and "Receive explicit media".
/// Four albums for every plan, ten photos each. Album photos are library photos; an adult photo approved for albums only
/// lives in the album it was uploaded into and can only be deleted. Sharing an album in chat is a later slice: this only
/// stores which modes an album may be shared in.
/// </summary>
public static class PhotoAlbumEndpoints
{
    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        api.MapPost("/me/photos/albums", async (PhotoAlbumCreate request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken token) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, token);
            if (denied is not null) return denied;
            var (db, me, now) = (accounts.Db!, current!.Profile!.AccountId, clock.GetUtcNow());
            if (PhotoAlbumPolicy.Name(request.Name) is not { } name) return InvalidName();
            var modes = await ModesAsync(db, request.Modes, token);
            if (modes is null) return InvalidModes();
            await using var lease = await AccountLifecycleLease.AcquireMediaAsync(db, me, token);
            if (await db.PhotoAlbums.CountAsync(a => a.AccountId == me, token) >= PhotoAlbumPolicy.MaxAlbums)
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.AlbumLimit, $"You can have up to {PhotoAlbumPolicy.MaxAlbums} albums.");
            var album = new PhotoAlbum { Id = Guid.NewGuid(), AccountId = me, Name = name, CreatedAt = now, UpdatedAt = now };
            db.PhotoAlbums.Add(album);
            db.PhotoAlbumModes.AddRange(modes.Select(m => new PhotoAlbumMode { AlbumId = album.Id, ModeId = m }));
            Audit.Record(db, now, me, "account", "album.created", "album", album.Id.ToString(), $"modes:{modes.Count}");
            await db.SaveChangesAsync(token);
            return Results.Ok(await MediaEndpoints.OwnAsync(db, accounts.Services.GetRequiredService<MediaLinks>(), me, now, token));
        });

        api.MapPut("/me/photos/albums/{id:guid}", async (Guid id, PhotoAlbumUpdate request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken token) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, token);
            if (denied is not null) return denied;
            var (db, me, now) = (accounts.Db!, current!.Profile!.AccountId, clock.GetUtcNow());
            if (PhotoAlbumPolicy.Name(request.Name) is not { } name) return InvalidName();
            var modes = await ModesAsync(db, request.Modes, token);
            if (modes is null) return InvalidModes();
            var photos = request.Photos ?? [];
            if (photos.Count > PhotoAlbumPolicy.MaxPhotos || photos.Distinct().Count() != photos.Count)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.AlbumFull, $"Choose up to {PhotoAlbumPolicy.MaxPhotos} different photos.");
            // Same order as uploads (lifecycle, then media): an "albums only" approval holds the lifecycle lease while it
            // checks the photo is still in its album, so it can't interleave with dropping that photo here.
            await using var lifecycle = await AccountLifecycleLease.AcquireAsync(db, me, token);
            await using var lease = await AccountLifecycleLease.AcquireMediaAsync(db, me, token);
            var album = await db.PhotoAlbums.SingleOrDefaultAsync(a => a.Id == id && a.AccountId == me, token);
            if (album is null) return UnknownAlbum();
            // Not tracked: the entries are deleted with ExecuteDelete and re-added with the same keys below.
            var stored = await db.PhotoAlbumItems.AsNoTracking().Where(e => e.AlbumId == id).ToListAsync(token);
            var storedIds = stored.Select(e => e.MediaId).ToHashSet();
            var items = await db.MediaItems.Where(i => photos.Contains(i.Id) || storedIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, token);
            // Album-only photos live only here: they can't be dropped by an update, only deleted.
            if (stored.Any(e => items.TryGetValue(e.MediaId, out var i) && i.AlbumOnly && i.RemovedAt is null && !photos.Contains(e.MediaId)))
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.AlbumOnlyPhoto, "Adult photos in an album can only be deleted.");
            // Photos already here keep their place (including ones still being checked); new ones must be approved library photos.
            foreach (var photo in photos)
            {
                if (storedIds.Contains(photo)) continue;
                if (!items.TryGetValue(photo, out var item) || item.AccountId != me || !PhotoAlbumPolicy.Addable(item))
                    return Problem(StatusCodes.Status409Conflict, ProblemCodes.PhotoNotApproved, "Only your approved photos can be added.");
            }
            await using var transaction = await db.Database.BeginTransactionAsync(token);
            await db.PhotoAlbumItems.Where(e => e.AlbumId == id).ExecuteDeleteAsync(token);
            await db.PhotoAlbumModes.Where(m => m.AlbumId == id).ExecuteDeleteAsync(token);
            db.PhotoAlbumItems.AddRange(photos.Select((photo, i) => new PhotoAlbumItem { AlbumId = id, MediaId = photo, Position = i }));
            db.PhotoAlbumModes.AddRange(modes.Select(m => new PhotoAlbumMode { AlbumId = id, ModeId = m }));
            (album.Name, album.UpdatedAt) = (name, now);
            Audit.Record(db, now, me, "account", "album.updated", "album", id.ToString(), $"photos:{photos.Count};modes:{modes.Count}");
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            return Results.Ok(await MediaEndpoints.OwnAsync(db, accounts.Services.GetRequiredService<MediaLinks>(), me, now, token));
        });

        // Deleting an album keeps its library photos; its album-only photos have nowhere else to live, so they are deleted
        // too (DEFAULT, owner review of the #231 mockup, 7 November 2026).
        api.MapDelete("/me/photos/albums/{id:guid}", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken token) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, token);
            if (denied is not null) return denied;
            var (db, me, now) = (accounts.Db!, current!.Profile!.AccountId, clock.GetUtcNow());
            // Hold the account's lifecycle and media authority (upload order) across the snapshot and the delete: an
            // "albums only" approval or an album upload can't land in between and leave an orphaned adult photo. A later
            // approval of a still-pending photo from this album is refused, because the photo is no longer in it.
            // Both leases are re-entrant, so RemoveAsync below can take the media lease again.
            await using var lifecycle = await AccountLifecycleLease.AcquireAsync(db, me, token);
            await using var lease = await AccountLifecycleLease.AcquireMediaAsync(db, me, token);
            var album = await db.PhotoAlbums.SingleOrDefaultAsync(a => a.Id == id && a.AccountId == me, token);
            if (album is null) return UnknownAlbum();
            var pipeline = accounts.Services.GetRequiredService<MediaPipeline>();
            var adult = await db.MediaItems.Where(i => i.AccountId == me && i.AlbumId == id && i.AlbumOnly && i.RemovedAt == null).ToListAsync(token);
            if (adult.Count > 0 && !pipeline.Available)
                return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.MediaUnavailable, "Photos aren't available right now.");
            foreach (var item in adult) await pipeline.RemoveAsync(item, me, now, token);
            db.PhotoAlbums.Remove(album);
            Audit.Record(db, now, me, "account", "album.deleted", "album", id.ToString(), $"adult-photos:{adult.Count}");
            await db.SaveChangesAsync(token);
            return Results.Ok(await MediaEndpoints.OwnAsync(db, accounts.Services.GetRequiredService<MediaLinks>(), me, now, token));
        });

        api.MapGet("/me/explicit-media", async (ClaimsPrincipal user, AccountResolver accounts, CancellationToken token) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, token);
            return denied ?? Results.Ok(await SettingsAsync(accounts.Db!, current!.Profile!, token));
        });

        api.MapPut("/me/explicit-media", async (ExplicitMediaUpdate request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken token) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, token);
            if (denied is not null) return denied;
            var (db, profile, now) = (accounts.Db!, current!.Profile!, clock.GetUtcNow());
            var modes = await db.ConnectionModes.Where(m => m.Available).Select(m => m.Id).ToListAsync(token);
            if (request.Mode is not { } mode || !modes.Contains(mode)) return InvalidModes();
            // Turning it on needs ID Verified; turning it off never does.
            if (request.Receive && !ExplicitMediaPolicy.Allowed(profile))
                return Problem(StatusCodes.Status403Forbidden, ProblemCodes.IdVerificationRequired, "Only ID Verified members can receive explicit media.");
            var row = await db.ExplicitMediaPreferences.SingleOrDefaultAsync(p => p.AccountId == profile.AccountId && p.ModeId == mode, token);
            if (row is null) db.ExplicitMediaPreferences.Add(row = new ExplicitMediaPreference { AccountId = profile.AccountId, ModeId = mode });
            (row.Receive, row.UpdatedAt) = (request.Receive, now);
            Audit.Record(db, now, profile.AccountId, "account", "explicit-media.set", "connection-mode", mode, request.Receive ? "on" : "off");
            await db.SaveChangesAsync(token);
            return Results.Ok(await SettingsAsync(db, profile, token));
        });
    }

    /// <summary>Every available mode, on only where stored on and the member is ID Verified now.</summary>
    public static async Task<ExplicitMediaSettings> SettingsAsync(SwoovrDbContext db, MemberProfile profile, CancellationToken token)
    {
        var modes = await db.ConnectionModes.Where(m => m.Available).OrderBy(m => m.Id).Select(m => m.Id).ToListAsync(token);
        var stored = await db.ExplicitMediaPreferences.Where(p => p.AccountId == profile.AccountId).ToDictionaryAsync(p => p.ModeId, p => p.Receive, token);
        return new(ExplicitMediaPolicy.Allowed(profile), modes.ToDictionary(m => m, m => ExplicitMediaPolicy.Effective(profile, stored.GetValueOrDefault(m))));
    }

    /// <summary>Distinct available mode ids, or null when any is unknown.</summary>
    private static async Task<List<string>?> ModesAsync(SwoovrDbContext db, IReadOnlyList<string>? requested, CancellationToken token)
    {
        var wanted = (requested ?? []).Distinct().ToList();
        if (wanted.Count == 0) return wanted;
        var available = await db.ConnectionModes.Where(m => m.Available).Select(m => m.Id).ToListAsync(token);
        return wanted.All(available.Contains) ? wanted : null;
    }

    private static IResult InvalidName() => Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidAlbum, $"Give the album a name of up to {PhotoAlbumPolicy.NameMax} characters.");
    private static IResult InvalidModes() => Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidAlbum, "Choose modes from the list.");
    private static IResult UnknownAlbum() => Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownAlbum, "That album isn't available.");
}
