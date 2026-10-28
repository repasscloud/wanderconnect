using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Profiles;

/// <summary>#90: hourly limits on searches and filter changes, so repeated queries can't narrow down where anyone lives.</summary>
public static class Searches
{
    /// <summary>Records one event of the kind ("search" or "filter"), or returns the 429 refusal once the hour's limit is used.</summary>
    public static async Task<IResult?> LimitedAsync(SwoovrDbContext db, Guid account, string kind, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var limit = kind == "filter" ? LocationPrivacy.FilterChangesPerHour : LocationPrivacy.SearchesPerHour;
        var recent = await db.SearchEvents.CountAsync(e => e.AccountId == account && e.Kind == kind && e.At > now - LocationPrivacy.Window, cancellationToken);
        if (!LocationPrivacy.Allowed(recent, limit))
            return Problem(StatusCodes.Status429TooManyRequests, ProblemCodes.SearchLimit,
                kind == "filter" ? "You've changed your filters a lot in the last hour. Try again later." : "You've searched a lot in the last hour. Try again later.");
        db.SearchEvents.Add(new SearchEvent { AccountId = account, Kind = kind, At = now });
        await db.SaveChangesAsync(cancellationToken);
        return null;
    }
}

/// <summary>
/// Profile versions (#91, policy §6): what others can see of a member, per mode, captured whenever it changes, so a
/// moderator sees the version visible when a report was made and a legal hold can preserve them.
/// </summary>
public static class ProfilePrivacy
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Snapshots the core profile and every mode setting whose public presentation changed since its latest version.</summary>
    public static async Task CaptureAsync(SwoovrDbContext db, Guid account, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var profile = await db.MemberProfiles.FindAsync([account], cancellationToken);
        if (profile is null) return;
        var modes = await db.MemberModes.Where(m => m.AccountId == account).ToListAsync(cancellationToken);
        var chips = await (from i in db.MemberModeIntentions join o in db.ModeIntentions on i.IntentionId equals o.Id
                           where i.AccountId == account select new { i.ModeId, o.Label }).ToListAsync(cancellationToken);
        // D203: the photos shown, by id and SHA-256 of the stored image (the main photo in core, each mode's own in the mode).
        var photos = (await (from p in db.MediaPlacements join i in db.MediaItems on p.MediaId equals i.Id
                             where p.AccountId == account && i.Status == MediaStatus.Passed && i.RemovedAt == null
                             orderby p.Position select new { p.Scope, i.Id, i.Sha256 }).ToListAsync(cancellationToken))
            .GroupBy(p => p.Scope).ToDictionary(g => g.Key, g => g.Select(p => new { p.Id, Sha256 = Convert.ToHexStringLower(p.Sha256) }).ToList());
        var snapshots = new List<(string Mode, object Content)>
            { (ProfileVersions.Core, new { profile.DisplayName, profile.Area, profile.About, MainPhoto = photos.GetValueOrDefault(MediaPolicy.Main)?.FirstOrDefault() }) };
        snapshots.AddRange(modes.Select(m => (m.ModeId, (object)new
        {
            m.Enabled, m.Visible, ModeAbout = m.About, ShareCoreAbout = m.ShareCoreAbout == true,
            ShownAbout = ModePrivacy.About(m.About, profile.About, m.ShareCoreAbout),
            Intentions = chips.Where(c => c.ModeId == m.ModeId).Select(c => c.Label).Order().ToList(),
            Photos = photos.GetValueOrDefault(m.ModeId) ?? [],
        })));
        var latest = (await db.ProfileVersions.Where(v => v.AccountId == account).ToListAsync(cancellationToken))
            .GroupBy(v => v.ModeId).ToDictionary(g => g.Key, g => g.OrderByDescending(v => v.CapturedAt).First());
        foreach (var (mode, content) in snapshots)
        {
            var json = JsonSerializer.Serialize(content, Json);
            var hash = ProfileVersions.Hash(json);
            if (ProfileVersions.Changed(latest.GetValueOrDefault(mode), hash))
                db.ProfileVersions.Add(new ProfileVersion { Id = Guid.NewGuid(), AccountId = account, ModeId = mode, CapturedAt = now, ContentJson = json, Sha256 = hash });
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    public static void Map(WebApplication app)
    {
        // #91: the profile as others could see it when the case's reports were made. Same access as the case itself.
        app.MapGet("/api/admin/cases/{id:guid}/profile-at-report", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var item = await db.ModerationCases.FindAsync([id], cancellationToken);
            if (item is null || item.Queue == CaseQueue.Admin && current!.StaffRole != AccountRole.Admin) return Results.NotFound();
            var reportedAt = await db.Reports.Where(r => r.CaseId == id).Select(r => (DateTimeOffset?)r.CreatedAt).MinAsync(cancellationToken) ?? item.CreatedAt;
            var versions = await db.ProfileVersions.Where(v => v.AccountId == item.SubjectAccountId && v.CapturedAt <= reportedAt).ToListAsync(cancellationToken);
            Audit.Record(db, DateTimeOffset.UtcNow, current!.Account!.Id, "account", "case.profile-viewed", "moderation-case", id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new ProfileAtReport(reportedAt, ProfileVersions.VisibleAt(versions, reportedAt)
                .Select(v => new ProfileVersionView(v.ModeId, v.CapturedAt, v.ContentJson, Convert.ToHexStringLower(v.Sha256))).ToList()));
        }).RequireAuthorization();
    }
}
