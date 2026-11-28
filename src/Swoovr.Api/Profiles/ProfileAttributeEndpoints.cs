using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Profiles;

/// <summary>
/// Issue 448 (owner decision 27 November 2026, D457): optional, typed profile details. The catalogue lists only the details
/// members can use today (sensitive categories stay unavailable until reviewed). A member saves a detail once and chooses the
/// modes it's published in (none by default); other members see only what's published in a mode they're shown. Values are
/// never used for discovery, filters, ranking or scores. The audit records the detail's id, never its value.
/// </summary>
public static class ProfileAttributeEndpoints
{
    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapGet("/profile-attributes", () => Results.Ok(Catalogue()));
        api.MapGet("/me/profile-attributes", async (ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            return Results.Ok(await MineAsync(accounts.Db!, current!.Profile!.AccountId, cancellationToken));
        });
        api.MapPut("/me/profile-attributes/{id}", async (string id, ProfileDetailUpdate request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, me, now) = (accounts.Db!, current!.Profile!.AccountId, clock.GetUtcNow());
            var existing = await db.ProfileAttributeValues.SingleOrDefaultAsync(v => v.AccountId == me && v.AttributeId == id, cancellationToken);
            var refusal = ProfileAttributes.Check(id, request.Value, request.Modes, out var canonical, out var modes,
                existing is null ? null : ProfileAttributes.OptionIds(existing.Value).ToHashSet());
            switch (refusal)
            {
                case AttributeRefusal.None: break;
                case AttributeRefusal.Unknown or AttributeRefusal.Unavailable:
                    return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownProfileDetail, "That profile detail isn't available.");
                case AttributeRefusal.InvalidModes:
                    return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidProfileDetail, "Choose where to show it from the modes this detail can appear in.");
                default: return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidProfileDetail, "Choose one of the options for this detail.");
            }
            if (existing is null)
                db.ProfileAttributeValues.Add(existing = new ProfileAttributeValue { AccountId = me, AttributeId = id });
            (existing.Value, existing.Modes, existing.CatalogueVersion, existing.UpdatedAt) = (canonical, modes, ProfileAttributes.Version, now);
            Audit.Record(db, now, me, "account", "profile.detail.saved", "profile-detail", id);
            await db.SaveChangesAsync(cancellationToken);
            await ProfilePrivacy.CaptureAsync(db, me, now, cancellationToken);
            return Results.Ok(await MineAsync(db, me, cancellationToken));
        });
        api.MapDelete("/me/profile-attributes/{id}", async (string id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, me, now) = (accounts.Db!, current!.Profile!.AccountId, clock.GetUtcNow());
            if (await db.ProfileAttributeValues.Where(v => v.AccountId == me && v.AttributeId == id).ExecuteDeleteAsync(cancellationToken) > 0)
            {
                Audit.Record(db, now, me, "account", "profile.detail.removed", "profile-detail", id);
                await db.SaveChangesAsync(cancellationToken);
                await ProfilePrivacy.CaptureAsync(db, me, now, cancellationToken);
            }
            return Results.Ok(await MineAsync(db, me, cancellationToken));
        });
    }

    public static ProfileDetailCatalogue Catalogue() => new(ProfileAttributes.Version,
        ProfileAttributes.Categories.Where(c => ProfileAttributes.All.Any(a => a.Category == c.Id && a.Available)).Select(c => new ProfileDetailCategory(c.Id, c.Label)).ToList(),
        ProfileAttributes.All.Where(a => a.Available).Select(a => new ProfileDetailDefinition(a.Id, a.Category, a.Label, a.Kind.ToString().ToLowerInvariant(),
            a.Options.Where(o => !o.Deprecated).Select(o => new ProfileDetailOption(o.Id, o.Label)).ToList(), a.Modes, a.MaxChoices, a.Min, a.Max, a.Unit)).ToList());

    /// <summary>The member's own saved details (only ones still in the catalogue and available).</summary>
    public static async Task<List<ProfileDetailValue>> MineAsync(SwoovrDbContext db, Guid me, CancellationToken cancellationToken) =>
        (await db.ProfileAttributeValues.AsNoTracking().Where(v => v.AccountId == me).ToListAsync(cancellationToken))
            .Where(v => ProfileAttributes.Find(v.AttributeId)?.Available == true)
            .Select(v => new ProfileDetailValue(v.AttributeId, JsonDocument.Parse(v.Value).RootElement.Clone(), v.Modes)).ToList();

    /// <summary>
    /// What a viewer reads of a member's details in each shown mode: only details published in that mode, grouped by
    /// category in catalogue order. One query for the member, whatever the number of modes.
    /// </summary>
    public static async Task<Dictionary<string, IReadOnlyList<ProfileDetailView>>> PublishedAsync(SwoovrDbContext db, Guid account, IReadOnlyCollection<string> modes,
        CancellationToken cancellationToken)
    {
        var values = await db.ProfileAttributeValues.AsNoTracking().Where(v => v.AccountId == account && v.Modes.Count > 0).ToListAsync(cancellationToken);
        var order = ProfileAttributes.All.Select((a, i) => (a.Id, i)).ToDictionary(x => x.Id, x => x.i);
        return modes.ToDictionary(m => m, m => (IReadOnlyList<ProfileDetailView>)values
            .Select(v => (Value: v, Labels: ProfileAttributes.Shown(v, m))).Where(x => x.Labels is not null)
            .OrderBy(x => order.GetValueOrDefault(x.Value.AttributeId, int.MaxValue))
            .Select(x => { var d = ProfileAttributes.Find(x.Value.AttributeId)!; return new ProfileDetailView(d.Id, d.Category, d.Label, x.Labels!, ProfileAttributes.Categories.First(c => c.Id == d.Category).Label); }).ToList());
    }
}
