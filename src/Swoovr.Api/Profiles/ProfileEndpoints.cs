using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Profiles;

/// <summary>
/// Connection modes (D92), the member's own core profile and mode settings, Explore for one mode, and another
/// member's profile. Every rule is checked here (Q12: application services, not the client). Callers must pass the
/// same member gate as /api/me (active, adult-locked accounts refused, beta enrolled). Explore and profiles are local
/// to the viewer's home market (D125); MarketEndpoints holds the market lookups.
/// </summary>
public static class ProfileEndpoints
{
    /// <summary>DEFAULT page size until Explore pages through results.</summary>
    public const int ExploreLimit = 50;

    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapGet("/modes", async (ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            return denied ?? Results.Ok(await CatalogueAsync(accounts.Db!, cancellationToken));
        });
        api.MapGet("/me/profile", async (ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            return denied ?? Results.Ok(await OwnAsync(accounts.Db!, current!.Profile!, cancellationToken));
        });
        // D348: the two independent location privacy settings, both off until the member turns them on.
        api.MapGet("/me/location-settings", async (ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            return denied ?? Results.Ok(new LocationSettingsView(current!.Profile!.AppearInNearby, current.Profile.ShowDistance));
        });
        api.MapPut("/me/location-settings", async (LocationSettingsUpdate update, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (profile, db, now) = (current!.Profile!, accounts.Db!, clock.GetUtcNow());
            if (profile.AppearInNearby != update.AppearInNearby || profile.ShowDistance != update.ShowDistance)
            {
                var hiding = profile.AppearInNearby && !update.AppearInNearby;
                (profile.AppearInNearby, profile.ShowDistance) = (update.AppearInNearby, update.ShowDistance);
                Audit.Record(db, now, profile.AccountId, "account", "location-settings.changed", "account", profile.AccountId.ToString());
                await db.SaveChangesAsync(cancellationToken);
                // D348, #278: turning "Appear" off deletes the stored reading; a reading sent afterwards is stored with it off.
                if (hiding) await Travel.NearMeEndpoints.ForgetAsync(db, profile.AccountId, cancellationToken);
            }
            return Results.Ok(new LocationSettingsView(profile.AppearInNearby, profile.ShowDistance));
        });
        // Issue 499 (D410): opt-in online status. Turning it off forgets the last activity at once.
        api.MapGet("/me/online-status", async (ClaimsPrincipal user, AccountResolver accounts, IConfiguration configuration, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            return denied ?? Results.Ok(new OnlineStatusView(OnlineStatus(configuration), current!.Profile!.ShowOnline));
        });
        api.MapPut("/me/online-status", async (OnlineStatusUpdate update, ClaimsPrincipal user, AccountResolver accounts, IConfiguration configuration, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var available = OnlineStatus(configuration);
            // Turning it off always works, so nobody is left on after the host stops offering it.
            if (!available && update.ShowOnline) return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotAvailable, "Online status isn't available.");
            var (profile, db, now) = (current!.Profile!, accounts.Db!, clock.GetUtcNow());
            if (profile.ShowOnline != update.ShowOnline)
            {
                (profile.ShowOnline, profile.ActiveAt) = (update.ShowOnline, update.ShowOnline ? now : null);
                Audit.Record(db, now, profile.AccountId, "account", "online-status.changed", "account", profile.AccountId.ToString());
                await db.SaveChangesAsync(cancellationToken);
            }
            return Results.Ok(new OnlineStatusView(available, profile.ShowOnline));
        });
        api.MapPut("/me/profile", async (ProfileUpdate update, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (name, about) = (ProfileTextPolicy.Clean(update.DisplayName), ProfileTextPolicy.Clean(update.About));
            if (ProfileTextPolicy.Check(name, about) != ProfileTextError.None)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidProfile, "Check the profile fields.");
            var (profile, db) = (current!.Profile!, accounts.Db!);
            // D110: the home city must be a GeoNames city from Swoovr's own copy.
            var city = update.HomeCityId is { } home ? await db.GeoCities.FindAsync([home], cancellationToken) : null;
            if (update.HomeCityId is not null && city is null)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.UnknownPlace, "Pick your home city from the list.");
            var now = clock.GetUtcNow();
            // D125, D146: the home city decides the home market. Moving into a different market waits 30 days after the
            // last move (Q24 rules 28 and 34); refusals and moves are audited with market ids only, never the city.
            var market = PlaceNames.MarketOf(city, await MarketEndpoints.ZonesAsync(db, cancellationToken))?.Id;
            if (MarketAccess.CanMoveHome(profile.HomeMarketId, market, profile.HomeMarketMovedTo, profile.HomeMarketMovedAt, now) == HomeMove.Locked)
            {
                Audit.Record(db, now, profile.AccountId, "account", "home-market.refused", "account", profile.AccountId.ToString(), $"{profile.HomeMarketId ?? "none"}>{market}");
                await db.SaveChangesAsync(cancellationToken);
                return Problem(StatusCodes.Status403Forbidden, ProblemCodes.HomeMarketLocked, "You can move your home city to another Swoovr market once every 30 days.");
            }
            if (market != profile.HomeMarketId)
                Audit.Record(db, now, profile.AccountId, "account", "home-market.changed", "account", profile.AccountId.ToString(), $"{profile.HomeMarketId ?? "none"}>{market ?? "none"}");
            if (market is not null && market != profile.HomeMarketMovedTo) (profile.HomeMarketMovedTo, profile.HomeMarketMovedAt) = (market, now);
            if(profile.About!=about)profile.TextRevision=accounts.Services.GetRequiredService<Swoovr.Api.Moderation.TextHarmQueue>().Enqueue(profile.AccountId,TextHarmSource.ProfileAbout,profile.AccountId,null,about,now);
            // D349: a Home City change restarts the Home City check period and drops the old city's daily outcomes.
            if (profile.HomeGeoNameId != update.HomeCityId) await Travel.HomeCityDayRecorder.RestartAsync(db, profile, cancellationToken);
            (profile.DisplayName, profile.About, profile.HomeGeoNameId, profile.HomeMarketId) = (name, about, update.HomeCityId, market);
            // Interest in a launch follows the home city; it lapses with no home city or once the home market is open.
            if (await db.MarketInterests.FindAsync([profile.AccountId], cancellationToken) is { } interest)
            {
                if (city is null || market is not null && await db.Markets.AnyAsync(m => m.Id == market
                    && (m.Status == MarketStatus.Beta || m.Status == MarketStatus.Active), cancellationToken))
                    db.MarketInterests.Remove(interest);
                else interest.GeoNameId = city.GeoNameId;
            }
            // The audit records that the profile changed, never what it says.
            Audit.Record(db, now, profile.AccountId, "account", "profile.updated", "account", profile.AccountId.ToString());
            if (FounderEligibility.Stamp(profile, current.Account!.Status, now))
                Audit.Record(db, now, null, "system", "founder.eligible", "account", profile.AccountId.ToString());
            await db.SaveChangesAsync(cancellationToken);
            // #91: what others can see is versioned for moderation.
            await ProfilePrivacy.CaptureAsync(db, profile.AccountId, now, cancellationToken);
            return Results.Ok(await OwnAsync(db, profile, cancellationToken));
        });
        api.MapPut("/me/modes/{modeId}", async (string modeId, ModeUpdate update, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (profile, db) = (current!.Profile!, accounts.Db!);
            var mode = await db.ConnectionModes.SingleOrDefaultAsync(m => m.Id == modeId, cancellationToken);
            var offered = await db.ModeIntentions.Where(i => i.ModeId == modeId).ToListAsync(cancellationToken);
            var stored = await db.MemberModeIntentions.Where(i => i.AccountId == profile.AccountId && i.ModeId == modeId).ToListAsync(cancellationToken);
            var change = ModeSettingsPolicy.Check(mode, update.Intentions ?? [], stored.Select(i => i.IntentionId).ToList(), offered);
            switch (change.Error)
            {
                case ModeChangeError.UnknownMode or ModeChangeError.ModeUnavailable:
                    return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownMode, "That connection mode isn't available.");
                case ModeChangeError.TooManyIntentions:
                    return Problem(StatusCodes.Status400BadRequest, ProblemCodes.TooManyIntentions, $"Pick up to {ModeSettingsPolicy.MaxIntentions} intentions.");
                case not ModeChangeError.None:
                    return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidMode, "Check the intentions for this mode.");
            }
            // Q11 rule 49: an official bot switches on only the modes an admin authorised for it.
            if (update.Enabled && profile.Bot && !DiscoveryEligibility.MaySwitchOn(true, await BotModesAsync(db, profile.AccountId, cancellationToken), modeId))
                return Problem(StatusCodes.Status403Forbidden, ProblemCodes.BotModeNotAuthorised, "This mode isn't authorised for this account.");
            var now = clock.GetUtcNow();
            var setting = await db.MemberModes.SingleOrDefaultAsync(m => m.AccountId == profile.AccountId && m.ModeId == modeId, cancellationToken);
            if (setting is null) db.MemberModes.Add(setting = new MemberMode { AccountId = profile.AccountId, ModeId = modeId });
            // Each mode is independent: only this row and its intentions change (Q12 rule 2).
            // D175: this mode's own about (full replacement; blank shows the core about here).
            var name = update.DisplayName is null ? setting.DisplayName : ProfileTextPolicy.Clean(update.DisplayName);
            if (ProfileTextPolicy.Check(name, null) != ProfileTextError.None)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidProfile, $"Keep this mode’s name to {ProfileTextPolicy.DisplayNameMax} characters of plain text.");
            setting.DisplayName = name;
            var about = ProfileTextPolicy.Clean(update.About);
            if (ProfileTextPolicy.Check(null, about) != ProfileTextError.None)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidProfile, $"Keep this mode's about to {ProfileTextPolicy.AboutMax} characters of plain text.");
            if(setting.About!=about)setting.TextRevision=accounts.Services.GetRequiredService<Swoovr.Api.Moderation.TextHarmQueue>().Enqueue(profile.AccountId,TextHarmSource.ModeAbout,profile.AccountId,modeId,about,now);
            (setting.Enabled, setting.Visible, setting.About, setting.UpdatedAt) = (update.Enabled, update.Visible, about, now);
            // #91: consent for the core about to show in this mode is asked, never assumed; null keeps the answer.
            if (update.ShareCoreAbout is { } share) setting.ShareCoreAbout = share;
            // Only the difference is written, so kept rows (including retired chips, D113) are never touched.
            db.MemberModeIntentions.RemoveRange(stored.Where(i => !change.Stored.Contains(i.IntentionId)));
            db.MemberModeIntentions.AddRange(change.Stored.Where(id => stored.All(i => i.IntentionId != id))
                .Select(id => new MemberModeIntention { AccountId = profile.AccountId, ModeId = modeId, IntentionId = id }));
            Audit.Record(db, now, profile.AccountId, "account", "mode.updated", "connection-mode", modeId,
                $"{(update.Enabled ? "on" : "off")}:{(update.Visible ? "visible" : "hidden")}:intentions:{change.Stored.Count}");
            await db.SaveChangesAsync(cancellationToken);
            await ProfilePrivacy.CaptureAsync(db, profile.AccountId, now, cancellationToken);
            return Results.Ok(await OwnAsync(db, profile, cancellationToken));
        });
        // Issue 389 (D382, Q181.5 A): "What would you like your picture to say?" Moderated like the about (text-harm queue,
        // content removal); contact details are refused here. Blank removes it.
        api.MapPut("/me/picture-text", async (PictureTextUpdate update, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var text = PictureTextPolicy.Clean(update.Text);
            switch (PictureTextPolicy.Check(text))
            {
                case PictureTextError.None: break;
                case PictureTextError.ContactDetails:
                    return Problem(StatusCodes.Status400BadRequest, ProblemCodes.PictureTextContact, "Remove phone numbers, email addresses, links and handles from your picture.");
                default:
                    return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidProfile, $"Keep your picture to {PictureTextPolicy.Max} characters on one line.");
            }
            var (profile, db, now) = (current!.Profile!, accounts.Db!, clock.GetUtcNow());
            if (profile.PictureText != text)
            {
                profile.PictureTextRevision = accounts.Services.GetRequiredService<Swoovr.Api.Moderation.TextHarmQueue>()
                    .Enqueue(profile.AccountId, TextHarmSource.ProfilePicture, profile.AccountId, null, text, now);
                profile.PictureText = text;
                // The audit says the picture changed, never what it says.
                Audit.Record(db, now, profile.AccountId, "account", "picture-text.updated", "account", profile.AccountId.ToString(), text is null ? "removed" : "set");
                await db.SaveChangesAsync(cancellationToken);
                await ProfilePrivacy.CaptureAsync(db, profile.AccountId, now, cancellationToken);
            }
            return Results.Ok(await OwnAsync(db, profile, cancellationToken));
        });
        // Issue 566: closing the Explore notice is for good, on every device. Setting it again keeps the first time.
        api.MapPost("/me/explore-notice/dismiss", async (ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (profile, db) = (current!.Profile!, accounts.Db!);
            if (profile.ExploreNoticeDismissedAt is null)
            {
                profile.ExploreNoticeDismissedAt = clock.GetUtcNow();
                await db.SaveChangesAsync(cancellationToken);
            }
            return Results.NoContent();
        });
        api.MapGet("/explore", async (string? mode, string? scope, ClaimsPrincipal user, AccountResolver accounts, IConfiguration configuration, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var selected = await db.ConnectionModes.SingleOrDefaultAsync(m => m.Id == mode && m.Available, cancellationToken);
            if (selected is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownMode, "That connection mode isn't available.");
            // D125, D352: Explore is local to the active location: the market of the most recently checked-in live trip,
            // otherwise the home market (a home city in an open market that offers this mode). It lists people whose home
            // is in that market (or bots assigned to it) and travellers checked in there who chose to be visible there.
            var now = clock.GetUtcNow();
            if (scope is not (null or "active" or "nearMe")) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Choose Active location or Near me.");
            if (scope == "nearMe") return await NearMeAsync(db, configuration, accounts, current!, selected, now, cancellationToken);
            var (market, refused, activeTrip, activeCity) = await MarketEndpoints.ActiveLocalAsync(db, current!.Profile!, selected.Id, now, cancellationToken);
            if (refused is not null) return refused;
            var (own, mine, notReady) = await ReadyAsync(db, current.Profile!, selected, clock.GetUtcNow(), cancellationToken);
            if (notReady is not null) return notReady;
            // #90: searches are limited per hour so repeated queries can't triangulate anyone.
            if (await Searches.LimitedAsync(db, current.Profile!.AccountId, "search", clock.GetUtcNow(), cancellationToken) is { } limited) return limited;
            var visiting = await CheckedInVisitorsAsync(db, configuration, current.Profile!.AccountId, market!, selected.Id, now, cancellationToken);
            var people=await PeopleAsync(db, configuration, current.Profile!, selected, own!, mine!, market!.Id, now, cancellationToken,
                accounts.Services.GetRequiredService<Media.MediaLinks>(), visiting, viewerAway: activeTrip is not null);
            await accounts.Services.GetRequiredService<Swoovr.Api.Analytics.BetaJourneyRecorder>().RecordAsync(current.Account!.Id,true,people.Count>0,cancellationToken);
            return Results.Ok(new ExploreResult(selected.Id,people,market.Name,activeCity?.Name));
        });
        // D175: ?mode= is the mode the profile was opened from; the view shows that mode and the modes the pair is connected in.
        api.MapGet("/profiles/{id:guid}", async (Guid id, string? mode, ClaimsPrincipal user, AccountResolver accounts, IConfiguration configuration, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var viewer = current!.Profile!.AccountId;
            var person = await Candidates(db, configuration, viewer).SingleOrDefaultAsync(c => c.Profile.PublicId == id, cancellationToken);
            // Shown only in modes both have on and the person made visible; otherwise (including a block either way) it
            // reads as not found.
            var shared = person is null ? [] : await SharedModesAsync(db, viewer, person, cancellationToken);
            if (person is null || shared.Count == 0) return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownMode, "That profile isn't available.");
            var (low, high) = ConnectionRequest.Pair(viewer, person.Profile.AccountId);
            var between = await db.ConnectionRequests.Where(r => r.PairLow == low && r.PairHigh == high
                && (r.Status == ConnectionRequestStatus.Pending || r.Status == ConnectionRequestStatus.Accepted)).ToListAsync(cancellationToken);
            // D175: never list the person's other modes; an unshared requested mode reads as not found.
            var shownIds = ModePrivacy.Shown(mode, shared.Select(m => m.Id).ToList(), between.Select(r => r.ModeId).ToHashSet());
            if (shownIds.Count == 0) return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownMode, "That profile isn't available.");
            // D125: the profile also needs the same market, an existing request or connection, or a visit to the viewer's
            // market; anything else is the same 404, so a profile link from another market reveals nothing.
            var reach = await ReachAsync(db, configuration, current!.Profile!, person, shared, between, clock.GetUtcNow(), cancellationToken);
            // D142: a traveller whose Traveller Search is open for the person's market may look before arrival.
            var travel = await Travel.ArrivalEndpoints.ReachAsync(db, current.Profile!, person, shared, clock.GetUtcNow(), cancellationToken);
            if (!MarketAccess.ProfileOpens(reach.SameMarket, between.Count > 0, reach.VisitingModes.Count > 0, travel.SearchModes.Count > 0))
                return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownMode, "That profile isn't available.");
            var chips = await IntentionLabelsAsync(db, [person.Profile.AccountId], null, cancellationToken);
            var identity = (await IdentitiesAsync(db, [person.Profile.AccountId], cancellationToken)).GetValueOrDefault(person.Profile.AccountId);
            var shownAs = Public(identity,
                person.Profile.DateOfBirth is { } dob ? AgePolicy.YearsOn(dob, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)) : null, await GenderLabelsAsync(db, cancellationToken));
            var orientations = identity is { ShowOrientation: true, OrientationId: not null }
                ? await db.ProfileOptions.Where(o => o.Kind == "orientation").ToDictionaryAsync(o => o.Id, o => o.Label, cancellationToken) : [];
            var abouts = await db.MemberModes.Where(m => m.AccountId == person.Profile.AccountId && shownIds.Contains(m.ModeId))
                .ToDictionaryAsync(m => m.ModeId, m => new { m.About, m.ShareCoreAbout, m.DisplayName }, cancellationToken);
            string? AboutIn(string modeId) => abouts.GetValueOrDefault(modeId) is { } a ? ModePrivacy.About(a.About, person.Profile.About, a.ShareCoreAbout) : null;
            // D203, #91: the main photo, and each shown mode's own photos (never another mode's).
            var photos = await Media.MediaEndpoints.PhotosAsync(db, accounts.Services.GetRequiredService<Media.MediaLinks>(), [person.Profile.AccountId], shownIds,
                clock.GetUtcNow(), cancellationToken);
            // Issue 448 (D457): each shown mode's published details only.
            var details = await ProfileAttributeEndpoints.PublishedAsync(db, person.Profile.AccountId, shownIds, cancellationToken);
            // Issue 571 (D460): their trip to the viewer's own area, by the same rule as the viewer's Who's visiting list, in the
            // first shown mode that has one. Nothing about trips anywhere else.
            ProfileTravelView? trip = null;
            foreach (var modeId in shownIds)
            {
                var (home, _) = await MarketEndpoints.LocalAsync(db, current.Profile!, modeId, cancellationToken);
                if (home is null) continue;
                var visit = (await Travel.TravelEndpoints.VisitsToAsync(db, configuration, viewer, home, await MarketEndpoints.ZonesAsync(db, cancellationToken), modeId,
                    clock.GetUtcNow(), cancellationToken, person.Profile.AccountId)).FirstOrDefault();
                if (visit is null) continue;
                trip = new ProfileTravelView(visit.City.Name, visit.City.CountryName, visit.Trip.Arrival, visit.Trip.Departure,
                    TravelPolicy.LocalToday(visit.City.TimeZone, clock.GetUtcNow()) >= visit.Trip.Arrival);
                break;
            }
            // Issue 452 (D459): interests both chose and both published in that mode; labels only, no score.
            var interests = await db.ProfileAttributeValues.AsNoTracking().Where(v => (v.AccountId == viewer || v.AccountId == person.Profile.AccountId) && v.AttributeId == "interests")
                .ToListAsync(cancellationToken);
            IReadOnlyList<string>? SharedIn(string modeId) => ProfileAttributes.SharedInterests(interests.FirstOrDefault(v => v.AccountId == viewer),
                interests.FirstOrDefault(v => v.AccountId == person.Profile.AccountId), modeId) is { Count: > 0 } both ? both : null;
            return Results.Ok(new PersonProfile(person.Profile.PublicId, ModePrivacy.DisplayName(abouts.GetValueOrDefault(mode ?? shownIds[0])?.DisplayName, person.Profile.DisplayName)!, AboutIn(mode ?? shownIds[0]),
                shared.Where(m => shownIds.Contains(m.Id)).Select(m =>
                {
                    var may = MarketAccess.MayRequest(reach.SameMarket && reach.MarketModes.Contains(m.Id), between.Any(r => r.Status == ConnectionRequestStatus.Accepted),
                        reach.VisitingModes.Contains(m.Id), travel.ArrivedTrips.ContainsKey(m.Id));
                    return ModeView(m, chips.GetValueOrDefault((person.Profile.AccountId, m.Id), []), viewer, between, may) with
                        { ArrivalRequired = !may && travel.SearchModes.Contains(m.Id), About = AboutIn(m.Id), DisplayName = ModePrivacy.DisplayName(abouts.GetValueOrDefault(m.Id)?.DisplayName, person.Profile.DisplayName),
                          ConnectionId = between.FirstOrDefault(r => r.ModeId == m.Id && r.Status == ConnectionRequestStatus.Accepted)?.Id,
                          MainPhoto = photos.Main.GetValueOrDefault((person.Profile.AccountId, m.Id)),
                          Photos = photos.Modes.GetValueOrDefault((person.Profile.AccountId, m.Id)) ?? [],
                          Orientation = OrientationIn(m.Id, identity, orientations), Details = details.GetValueOrDefault(m.Id) is { Count: > 0 } d ? d : null,
                          SharedInterests = viewer == person.Profile.AccountId ? null : SharedIn(m.Id) };
                }).ToList(),
                Badges(person.Profile, await BadgeOrderAsync(db, cancellationToken)), shownAs.Age, shownAs.Gender, shownAs.Pronouns,
                photos.Main.GetValueOrDefault((person.Profile.AccountId, mode ?? shownIds[0])),
                await HomeDistanceAsync(db, current.Profile!, person.Profile, reach.SameMarket && !reach.ByPresence, clock.GetUtcNow(), cancellationToken))
                // Issue 389 (D382): the text picture stands in only while no approved main photo shows.
                {
                    PictureText = photos.Main.GetValueOrDefault((person.Profile.AccountId, mode ?? shownIds[0])) is null ? person.Profile.PictureText : null,
                    // Issue 571 (D460): their trip to the viewer's own area only.
                    Travel = viewer == person.Profile.AccountId ? null : trip,
                    // Issue 572 (D448): only the viewer's own private favourite, never anyone else's.
                    Favourite = await db.MemberFavourites.AnyAsync(f => f.AccountId == viewer && f.TargetAccountId == person.Profile.AccountId, cancellationToken),
                    // Issue 568: the green dot as on Explore cards (D410, D422), and the day what the viewer can see last changed.
                    Online = OnlineStatusPolicy.Shows(OnlineStatus(configuration), current.Profile!, person.Profile, clock.GetUtcNow()),
                    // The text picture shows only while the whole gallery is empty, as PersonPanel renders it (PR 582 review).
                    LastUpdated = await LastUpdatedAsync(db, person.Profile.AccountId, shownIds, mode ?? shownIds[0], identity,
                        photos.Main.GetValueOrDefault((person.Profile.AccountId, mode ?? shownIds[0])) is null
                        && shownIds.All(m => photos.Modes.GetValueOrDefault((person.Profile.AccountId, m)) is not { Count: > 0 }), cancellationToken),
                });
        });
    }

    /// <summary>
    /// Issue 568 (D436): the day what the viewer can see of a profile last changed: the profile versions as shown
    /// (ProfileShownChange), and gender, pronouns and age as shown, and the orientation when Dating is shown, from their own
    /// change times (MemberIdentity), since profile versions never hold them.
    /// </summary>
    private static async Task<DateOnly?> LastUpdatedAsync(SwoovrDbContext db, Guid account, IReadOnlyList<string> shownIds, string lead, MemberIdentity? identity,
        bool pictureTextShown, CancellationToken cancellationToken)
    {
        var versions = await db.ProfileVersions.AsNoTracking()
            .Where(v => v.AccountId == account && (shownIds.Contains(v.ModeId) || pictureTextShown && v.ModeId == ProfileVersions.Core)).ToListAsync(cancellationToken);
        var changed = new[]
        {
            ProfileShownChange.Last(versions, shownIds, lead, pictureTextShown),
            identity?.PresentationChangedAt, shownIds.Contains(ConnectionModeIds.Dating) ? identity?.OrientationChangedAt : null,
        }.Max();
        return changed is { } at ? DateOnly.FromDateTime(at.UtcDateTime) : null;
    }

    /// <summary>
    /// Issue 568 (D436, PR 582 review): a date-of-birth change that changes the age others see moves "Profile updated"; a
    /// hidden age, or one that comes out the same, doesn't. A member without identity settings gets the defaults, which are
    /// what a missing row already means (age shown). Unsaved.
    /// </summary>
    internal static async Task AgeChangedAsync(SwoovrDbContext db, Guid account, DateOnly? before, DateOnly? after, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        int? Age(DateOnly? date) => date is { } d ? AgePolicy.YearsOn(d, today) : null;
        if (Age(before) == Age(after)) return;
        var identity = await db.MemberIdentities.FindAsync([account], cancellationToken);
        if (identity is null) db.MemberIdentities.Add(new MemberIdentity { AccountId = account, UpdatedAt = now, PresentationChangedAt = now });
        else if (identity.ShowAge) identity.PresentationChangedAt = now;
    }

    /// <summary>One property's JSON, or the given empty value when an older snapshot shape has none.</summary>
    private static string Shown(System.Text.Json.JsonElement root, string name, string missing) =>
        root.TryGetProperty(name, out var value) ? value.GetRawText() : missing;

    /// <summary>
    /// What discovery needs from the viewer in one mode, for Explore and Traveller Search alike: the mode on (D92), a date
    /// of birth (D139) and, in Dating, a gender and a "show me" (D138). Returns their mode setting and matching facts, or
    /// the problem that refuses.
    /// </summary>
    public static async Task<(MemberMode? Own, MatchFacts? Facts, IResult? Refused)> ReadyAsync(SwoovrDbContext db, MemberProfile viewer, ConnectionMode selected,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var own = await db.MemberModes.SingleOrDefaultAsync(m => m.AccountId == viewer.AccountId && m.ModeId == selected.Id, cancellationToken);
        if (own is not { Enabled: true })
            return (null, null, Problem(StatusCodes.Status403Forbidden, ProblemCodes.ModeNotEnabled, "Turn this mode on to see who's here."));
        // D139: no date of birth, no discovery (either way). D138: Dating needs a gender and a "show me".
        if (viewer.DateOfBirth is null)
            return (null, null, Problem(StatusCodes.Status403Forbidden, ProblemCodes.DateOfBirthRequired, "Add your date of birth to discover people."));
        var mine = (await FactsAsync(db, [viewer.AccountId], selected.Id, DateOnly.FromDateTime(now.UtcDateTime), cancellationToken))[viewer.AccountId];
        if (selected.Id == ConnectionModeIds.Dating && !MatchPolicy.DatingReady(mine))
            return (null, null, Problem(StatusCodes.Status403Forbidden, ProblemCodes.DatingDetailsRequired, "Add your gender and who you'd like to see to use Dating."));
        return (own, mine, null);
    }

    /// <summary>
    /// The people local to one market whom the viewer may see in one mode: the account, mode, bot, block and sanction
    /// rules (Candidates, DiscoveryEligibility), the reciprocal rules and the viewer's own filters, with Plus filters only
    /// while the entitlement service says Plus (D137–D140, D151). Explore uses the viewer's home market (D125); Traveller
    /// Search the trip's destination market (D142). Cards carry age, gender and pronouns only as each person allows.
    /// </summary>
    /// <summary>
    /// D346, D348, #279: Explore's Near me scope. The viewer needs a fresh device reading (409 location-required) inside an
    /// open market that offers the mode (403 market-not-open); being there by GPS is enough, with no trip and no change to
    /// the home city. It lists that market's people by home city plus members present there by GPS who appear in nearby
    /// searches, under the same account, mode, block, sanction, reciprocity and filter rules as Explore, with GPS bands only
    /// as D348 allows and the nearest visible band first. Counts towards the hourly search limit.
    /// </summary>
    private static async Task<IResult> NearMeAsync(SwoovrDbContext db, IConfiguration configuration, AccountResolver accounts, CurrentAccount current,
        ConnectionMode selected, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var viewer = current.Profile!;
        var keys = accounts.Services.GetRequiredService<Travel.LocationKeys>();
        if ((await Travel.NearMeEndpoints.PresenceAsync(db, keys, [viewer.AccountId], now, cancellationToken)).GetValueOrDefault(viewer.AccountId) is not { } mine)
            return Problem(StatusCodes.Status409Conflict, ProblemCodes.LocationRequired, "Share your location to use Near me.");
        var market = mine.Location.MarketId is { } id ? await db.Markets.FindAsync([id], cancellationToken) : null;
        var modes = market is null ? [] : await db.MarketModes.Where(m => m.MarketId == market.Id).Select(m => m.ModeId).ToListAsync(cancellationToken);
        if (MarketAccess.Local(true, market, modes, selected.Id) != LocalAccess.Open)
            return Problem(StatusCodes.Status403Forbidden, ProblemCodes.MarketNotOpen, "Swoovr isn't available where you are yet. We're expanding to new cities.");
        var (own, facts, notReady) = await ReadyAsync(db, viewer, selected, now, cancellationToken);
        if (notReady is not null) return notReady;
        if (await Searches.LimitedAsync(db, viewer.AccountId, "search", now, cancellationToken) is { } limited) return limited;
        var appearing = await Travel.NearMeEndpoints.AppearingInAsync(db, keys, market!.Id, viewer.AccountId, now, cancellationToken);
        var people = await PeopleAsync(db, configuration, viewer, selected, own!, facts!, market.Id, now, cancellationToken,
            accounts.Services.GetRequiredService<Media.MediaLinks>(), nearMe: new NearMe(mine, appearing));
        await accounts.Services.GetRequiredService<Swoovr.Api.Analytics.BetaJourneyRecorder>().RecordAsync(current.Account!.Id, true, people.Count > 0, cancellationToken);
        return Results.Ok(new ExploreResult(selected.Id, people, market.Name, null, "nearMe"));
    }

    /// <summary>D346, #279: the viewer's usable reading and the members appearing near them, for PeopleAsync.</summary>
    public sealed record NearMe(Travel.NearMeEndpoints.Present Viewer, IReadOnlyDictionary<Guid, Travel.NearMeEndpoints.Present> Appearing);

    /// <summary>D348: the wire form of a band and its basis.</summary>
    public static DistanceView Distance((DistanceBand Band, DistanceBasis? Basis) band) => new(Wire(band.Band), band.Basis is { } basis ? Wire(basis) : null);

    /// <summary>
    /// D348, D352: the band between two members' registered home cities for a profile page, under the same rules as Explore:
    /// only between members of the same home market who are both at home. If either is checked in elsewhere, or the viewer
    /// reached the profile from another market (Travel, Traveller Search), home cities say nothing about where they are, so
    /// the band is unknown (or hidden when either hides it).
    /// </summary>
    private static async Task<DistanceView> HomeDistanceAsync(SwoovrDbContext db, MemberProfile viewer, MemberProfile person, bool sameHomeMarket,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!(viewer.ShowDistance && person.ShowDistance)) return Distance(LocationPrivacy.HomeCityBand(null, person.ShowDistance, viewer.ShowDistance));
        var away = await MarketEndpoints.ActiveTripsAsync(db, [viewer.AccountId, person.AccountId], now, cancellationToken);
        bool Away(MemberProfile member) => away.GetValueOrDefault(member.AccountId) is { Trip: { } trip } && trip.ArrivalMarketId != member.HomeMarketId;
        if (!sameHomeMarket || Away(viewer) || Away(person)) return Distance(LocationPrivacy.HomeCityBand(null, true, true));
        var ids = new[] { viewer.HomeGeoNameId, person.HomeGeoNameId }.OfType<int>().ToList();
        var cities = await db.GeoCities.Where(c => ids.Contains(c.GeoNameId)).ToDictionaryAsync(c => c.GeoNameId, cancellationToken);
        double? km = viewer.HomeGeoNameId is { } a && person.HomeGeoNameId is { } b && cities.TryGetValue(a, out var mine) && cities.TryGetValue(b, out var theirs)
            ? PlaceNames.DistanceKm(mine.Latitude, mine.Longitude, theirs.Latitude, theirs.Longitude) : null;
        return Distance(LocationPrivacy.HomeCityBand(km, true, true));
    }

    /// <summary>
    /// D352: travellers checked in to this market whose active trip is visible there, as the visitors feed shows them (same
    /// account, mode, bot and block rules), keyed by account with the destination city they are visiting.
    /// </summary>
    public static async Task<Dictionary<Guid, string>> CheckedInVisitorsAsync(SwoovrDbContext db, IConfiguration configuration, Guid viewer, Market market,
        string modeId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var visits = await Travel.TravelEndpoints.VisitsToAsync(db, configuration, viewer, market, await MarketEndpoints.ZonesAsync(db, cancellationToken), modeId, now, cancellationToken);
        var active = await MarketEndpoints.ActiveTripsAsync(db, visits.Select(v => v.Person.Profile.AccountId).ToList(), now, cancellationToken);
        return visits.Where(v => active.GetValueOrDefault(v.Person.Profile.AccountId) is { Trip: { } trip } && trip.ArrivalMarketId == market.Id && trip.Visible)
            .ToDictionary(v => v.Person.Profile.AccountId, v => active[v.Person.Profile.AccountId].City.Name);
    }

    /// <param name="visiting">D352: checked-in travellers to include, marked as visiting, keyed by account with their destination city.</param>
    /// <param name="viewerAway">D352: the viewer's location here isn't their home city (a checked-in trip, or Traveller Search), so
    /// home-city distances say nothing: no home-city band (unknown) and no home-city distance filter.</param>
    /// <param name="nearMe">D346, #279: Near me. Members appearing there by GPS join the market's people; home cities give no
    /// distance (the viewer is where their reading says), the distance filter and bands use GPS for those appearing, and the
    /// nearest visible band comes first.</param>
    public static async Task<List<ProfileCard>> PeopleAsync(SwoovrDbContext db, IConfiguration configuration, MemberProfile viewer, ConnectionMode selected,
        MemberMode own, MatchFacts mine, string marketId, DateTimeOffset now, CancellationToken cancellationToken, Media.MediaLinks? links = null,
        IReadOnlyDictionary<Guid, string>? visiting = null, bool viewerAway = false, NearMe? nearMe = null)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var visitorIds = (visiting ?? new Dictionary<Guid, string>()).Keys.Concat(nearMe?.Appearing.Keys ?? []).Distinct().ToList();
        if (nearMe is not null) viewerAway = true;
        var people = await Candidates(db, configuration, viewer.AccountId).InMarketOrVisiting(db, marketId, visitorIds)
            .Where(c => db.MemberModes.Any(m => m.AccountId == c.Profile.AccountId
                && m.ModeId == selected.Id && m.Enabled && m.Visible)
                && (!c.Profile.Bot || db.BotModeAuthorisations.Any(b => b.AccountId == c.Profile.AccountId && b.ModeId == selected.Id)))
            .OrderByDescending(c => db.MemberModes.Where(m => m.AccountId == c.Profile.AccountId && m.ModeId == selected.Id).Select(m => m.UpdatedAt).First())
            .Take(ExploreLimit * 4)
            .ToListAsync(cancellationToken);
        if (nearMe is not null)
        {
            // #279 (Codex review on #292): the recency limit above must not cut out people appearing near the viewer, or
            // nearest-band-first would miss them. The nearest appearing members (by GPS, on the server only) are always
            // candidates, capped like the recency window; filtering, ordering and paging follow as for everyone.
            var listed = people.Select(c => c.Profile.AccountId).ToHashSet();
            var nearest = nearMe.Appearing.Where(a => !listed.Contains(a.Key))
                .OrderBy(a => PlaceNames.DistanceKm(nearMe.Viewer.Cell.Latitude, nearMe.Viewer.Cell.Longitude, a.Value.Cell.Latitude, a.Value.Cell.Longitude))
                .Take(ExploreLimit * 4).Select(a => a.Key).ToList();
            if (nearest.Count > 0)
                people.AddRange(await Candidates(db, configuration, viewer.AccountId)
                    .Where(c => nearest.Contains(c.Profile.AccountId) && db.MemberModes.Any(m => m.AccountId == c.Profile.AccountId
                        && m.ModeId == selected.Id && m.Enabled && m.Visible)
                        && (!c.Profile.Bot || db.BotModeAuthorisations.Any(b => b.AccountId == c.Profile.AccountId && b.ModeId == selected.Id)))
                    .ToListAsync(cancellationToken));
        }
        var bots = await BotModesAsync(db, people.Where(c => c.Profile.Bot).Select(c => c.Profile.AccountId).ToList(), cancellationToken);
        var facts = await FactsAsync(db, people.Select(c => c.Profile.AccountId).ToList(), selected.Id, today, cancellationToken);
        var filter = await db.ModeFilters.FindAsync([viewer.AccountId, selected.Id], cancellationToken);
        var tier = await Moderation.Entitlements.TierAsync(db, viewer, now, cancellationToken);
        // D352: members of this market who are checked in somewhere else are still listed, but their home city isn't where they are.
        var away = (await MarketEndpoints.ActiveTripsAsync(db, people.Where(c => !visitorIds.Contains(c.Profile.AccountId)).Select(c => c.Profile.AccountId).ToList(),
            now, cancellationToken)).Where(a => a.Value.Trip.ArrivalMarketId != people.First(c => c.Profile.AccountId == a.Key).Profile.HomeMarketId).Select(a => a.Key).ToHashSet();
        bool Located(Guid account) => !viewerAway && !visitorIds.Contains(account) && !away.Contains(account);
        // Checked again in code with the same policy the database filter mirrors, so the rule has one source.
        // D347, #279: in Near me the distance filter applies to members appearing by GPS (their reading against the viewer's);
        // the market's people by home city stay, subject to the other filters, so a position can't range-find a home.
        double? GpsKm(Guid account) => nearMe?.Appearing.GetValueOrDefault(account) is { } them
            ? PlaceNames.DistanceKm(nearMe.Viewer.Cell.Latitude, nearMe.Viewer.Cell.Longitude, them.Cell.Latitude, them.Cell.Longitude) : null;
        bool GpsFilterAllows(Guid account) => filter?.MaxDistanceKm is not { } km || GpsKm(account) is not { } d
            || d <= Math.Min(LocationPrivacy.Snap(km)!.Value, LocationPrivacy.MaxFor(tier));
        // D348, #279: a GPS band only for members appearing here who show their distance, while the viewer shows theirs.
        (DistanceBand Band, DistanceBasis? Basis) BandOf(ProfileEndpoints.Row c) =>
            nearMe?.Appearing.GetValueOrDefault(c.Profile.AccountId) is { } them
                ? NearMePolicy.GpsBand(nearMe.Viewer.Location, nearMe.Viewer.Cell, them.Location, them.Cell, c.Profile.AppearInNearby, c.Profile.ShowDistance, viewer.ShowDistance, now)
                : LocationPrivacy.HomeCityBand(Located(c.Profile.AccountId) ? MatchPolicy.DistanceKm(mine, facts[c.Profile.AccountId]) : null, c.Profile.ShowDistance, viewer.ShowDistance);
        var shown = people.Where(c => DiscoveryEligibility.ShownIn(selected.Id, selected, own,
                new MemberMode { ModeId = selected.Id, Enabled = true, Visible = true }, c.Candidate(bots))
            && MatchPolicy.Reciprocal(selected.Id, mine, facts[c.Profile.AccountId])
            && MatchPolicy.FilterAllows(filter, mine, facts[c.Profile.AccountId], tier, Located(c.Profile.AccountId))
            && GpsFilterAllows(c.Profile.AccountId)).ToList();
        if (filter is { Sort: ExploreSort.Compatibility } && tier >= PlanTier.Plus)
            shown = shown.OrderByDescending(c => MatchPolicy.Shared(mine, facts[c.Profile.AccountId])).ToList();
        // Q154: Near me puts the nearest visible band first (stable, so Recent or Compatibility orders within a band).
        if (nearMe is not null) shown = shown.OrderBy(c => NearMePolicy.Rank(BandOf(c).Band)).ToList();
        shown = shown.Take(ExploreLimit).ToList();
        var identities = await IdentitiesAsync(db, shown.Select(c => c.Profile.AccountId).ToList(), cancellationToken);
        var chips = await IntentionLabelsAsync(db, shown.Select(c => c.Profile.AccountId).ToList(), selected.Id, cancellationToken);
        var shownIds = shown.Select(c => c.Profile.AccountId).ToList();
        // D175: the card shows the person's about for this mode.
        var abouts = await db.MemberModes.Where(m => m.ModeId == selected.Id && shownIds.Contains(m.AccountId))
            .ToDictionaryAsync(m => m.AccountId, m => new { m.About, m.ShareCoreAbout, m.DisplayName }, cancellationToken);
        var order = await BadgeOrderAsync(db, cancellationToken);
        var genders = await GenderLabelsAsync(db, cancellationToken);
        var online = OnlineStatus(configuration);
        // Issue 495: the labels once, and only where orientation can show.
        var orientations = selected.Id == ConnectionModeIds.Dating
            ? await db.ProfileOptions.Where(o => o.Kind == "orientation").ToDictionaryAsync(o => o.Id, o => o.Label, cancellationToken) : [];
        // D203: the main photo, else the first photo picked for this mode; approved photos only, as signed links.
        var photos = links is null ? default : await Media.MediaEndpoints.PhotosAsync(db, links, shownIds, [selected.Id], now, cancellationToken);
        // Issue 497: the pending or accepted request with each person in this mode, in one query; never another mode's.
        var me = viewer.AccountId;
        var standing = (await db.ConnectionRequests.AsNoTracking().Where(r => r.ModeId == selected.Id
                && (r.Status == ConnectionRequestStatus.Pending || r.Status == ConnectionRequestStatus.Accepted)
                && (r.FromAccountId == me && shownIds.Contains(r.ToAccountId) || r.ToAccountId == me && shownIds.Contains(r.FromAccountId))).ToListAsync(cancellationToken))
            .GroupBy(r => r.FromAccountId == me ? r.ToAccountId : r.FromAccountId)
            .ToDictionary(g => g.Key, g => ConnectionState(g.OrderByDescending(r => r.Status == ConnectionRequestStatus.Accepted).First(), me));
        // Issue 498: informational only, after the order is settled; never for official bots.
        var newFrom = NewMemberPolicy.From(configuration.GetValue("Explore:NewMemberDays", NewMemberPolicy.DefaultDays), now);
        var recent = newFrom is { } from ? (await db.Accounts.Where(a => shownIds.Contains(a.Id) && a.CreatedAt > from).Select(a => a.Id).ToListAsync(cancellationToken)).ToHashSet() : [];
        // D449 (Q195): the gold star on the card. Only the viewer's own favourites, in one query.
        var favourites = (await db.MemberFavourites.Where(f => f.AccountId == me && shownIds.Contains(f.TargetAccountId)).Select(f => f.TargetAccountId).ToListAsync(cancellationToken)).ToHashSet();
        return shown.Select(c =>
        {
            var identity = identities.GetValueOrDefault(c.Profile.AccountId);
            var (ageShown, genderShown, pronounsShown) = Public(identity, facts[c.Profile.AccountId].Age, genders);
            var shownAs = ModePrivacy.OnCard(selected.Id, ageShown, genderShown, pronounsShown);
            var about = abouts.GetValueOrDefault(c.Profile.AccountId) is { } a ? ModePrivacy.About(a.About, c.Profile.About, a.ShareCoreAbout) : null;
            // D348: a band between home cities only when both show their distance; never a distance.
            return new ProfileCard(c.Profile.PublicId, ModePrivacy.DisplayName(abouts.GetValueOrDefault(c.Profile.AccountId)?.DisplayName, c.Profile.DisplayName)!, about,
                chips.GetValueOrDefault((c.Profile.AccountId, selected.Id), []), Badges(c.Profile, order), shownAs.Age, shownAs.Gender, shownAs.Pronouns,
                Distance(BandOf(c)),
                photos.Main?.GetValueOrDefault((c.Profile.AccountId, selected.Id)), visiting?.GetValueOrDefault(c.Profile.AccountId),
                // Issue 389 (D382, Q181.5 A): the text picture only while the tile has no approved photo.
                photos.Main?.GetValueOrDefault((c.Profile.AccountId, selected.Id)) is null ? c.Profile.PictureText : null,
                // Issue 496: the photos others see in this mode, each once (the main photo included).
                photos.Modes?.GetValueOrDefault((c.Profile.AccountId, selected.Id))?.Select(p => p.Id).Distinct().Count() ?? 0,
                standing.GetValueOrDefault(c.Profile.AccountId), !c.Profile.Bot && recent.Contains(c.Profile.AccountId),
                OrientationIn(selected.Id, identity, orientations),
                // Issue 499 (D410): mutual, worked out after the list and its order are settled.
                OnlineStatusPolicy.Shows(online, viewer, c.Profile, now), favourites.Contains(c.Profile.AccountId));
        }).ToList();
    }

    /// <summary>What a person allows others to see (D138, D139): age unless hidden, gender and pronouns unless hidden.</summary>
    /// <summary>
    /// Issue 447, D175 (CONFIRMED): orientation is a Dating-only detail, shown only when the person switched it on. Every
    /// other mode, a hidden choice or an unknown option gives nothing.
    /// </summary>
    public static string? OrientationIn(string modeId, MemberIdentity? identity, IReadOnlyDictionary<string, string> labels) =>
        modeId == "dating" && identity is { ShowOrientation: true, OrientationId: { } id } ? labels.GetValueOrDefault(id) : null;

    /// <summary>Issue 499 (D410): off until the owner turns it on for a host (`Explore:OnlineStatus`, Q189).</summary>
    internal static bool OnlineStatus(IConfiguration configuration) => configuration.GetValue("Explore:OnlineStatus", false);

    internal static (int? Age, string? Gender, string? Pronouns) Public(MemberIdentity? identity, int? age, IReadOnlyDictionary<string, string> genders)
    {
        if (identity is null) return (age, null, null);
        var gender = identity.ShowGender && identity.GenderId is { } g
            ? g == "another" && identity.GenderDescription is { } d ? d : genders.GetValueOrDefault(g) : null;
        // Issue 389 (D382, Q181.6 A): pronouns have their own switch; "Prefer not to say" shows nothing.
        return (identity.ShowAge ? age : null, gender, identity.ShowPronouns ? PronounText(identity, genders) : null);
    }

    /// <summary>D382: the pronouns as written for others: the member's own words with Self-describe, else the option's label.</summary>
    internal static string? PronounText(MemberIdentity identity, IReadOnlyDictionary<string, string> labels) => identity.PronounId switch
    {
        null or MatchPolicy.PreferNotToSay => null,
        MatchPolicy.SelfDescribe => identity.Pronouns,
        var id => labels.GetValueOrDefault(PronounKey + id),
    };

    /// <summary>Gender labels by id, plus pronoun labels under "pronoun:" + id (D382).</summary>
    internal const string PronounKey = "pronoun:";

    internal static async Task<Dictionary<string, string>> GenderLabelsAsync(SwoovrDbContext db, CancellationToken cancellationToken) =>
        await db.ProfileOptions.Where(o => o.Kind == "gender" || o.Kind == "pronoun")
            .ToDictionaryAsync(o => o.Kind == "pronoun" ? PronounKey + o.Id : o.Id, o => o.Label, cancellationToken);

    internal static async Task<Dictionary<Guid, MemberIdentity>> IdentitiesAsync(SwoovrDbContext db, List<Guid> accounts, CancellationToken cancellationToken) =>
        await db.MemberIdentities.Where(i => accounts.Contains(i.AccountId)).ToDictionaryAsync(i => i.AccountId, cancellationToken);

    /// <summary>
    /// Matching facts (D137–D140) for each account in one mode, from application records: age from the private date of
    /// birth, the mode's chips, the chips they excluded in that mode, ID verification, gender and "show me", and the
    /// home city's coordinates (for the Plus distance filter). Never returned to clients.
    /// </summary>
    public static async Task<Dictionary<Guid, MatchFacts>> FactsAsync(SwoovrDbContext db, List<Guid> accounts, string modeId, DateOnly today, CancellationToken cancellationToken)
    {
        var profiles = await (from p in db.MemberProfiles
                              where accounts.Contains(p.AccountId)
                              join c in db.GeoCities on p.HomeGeoNameId equals (int?)c.GeoNameId into cities
                              from city in cities.DefaultIfEmpty()
                              select new { p.AccountId, p.DateOfBirth, p.IdStatus, p.Bot, Lat = (double?)city.Latitude, Lon = (double?)city.Longitude }).ToListAsync(cancellationToken);
        var chips = (await db.MemberModeIntentions.Where(i => accounts.Contains(i.AccountId) && i.ModeId == modeId).ToListAsync(cancellationToken))
            .GroupBy(i => i.AccountId).ToDictionary(g => g.Key, g => (IReadOnlySet<Guid>)g.Select(i => i.IntentionId).ToHashSet());
        var excluded = (await db.ModeFilters.Where(f => accounts.Contains(f.AccountId) && f.ModeId == modeId).ToListAsync(cancellationToken))
            .ToDictionary(f => f.AccountId, f => (IReadOnlySet<Guid>)f.ExcludedChips.ToHashSet());
        var identities = await IdentitiesAsync(db, accounts, cancellationToken);
        IReadOnlySet<Guid> none = new HashSet<Guid>();
        return profiles.ToDictionary(p => p.AccountId, p =>
        {
            var identity = identities.GetValueOrDefault(p.AccountId);
            return new MatchFacts(p.DateOfBirth is { } dob ? AgePolicy.YearsOn(dob, today) : null, chips.GetValueOrDefault(p.AccountId, none),
                excluded.GetValueOrDefault(p.AccountId, none), p.IdStatus == VerificationStatus.Verified, identity?.GenderId,
                (IReadOnlyCollection<string>?)identity?.DatingShowMe ?? [], p.Lat, p.Lon, p.Bot);
        });
    }

    /// <summary>How the viewer stands to a person under the market rules (D125): same open market, which modes the
    /// viewer's market offers, and in which shared modes the person is visiting the viewer's market.</summary>
    /// <param name="ByPresence">D346: the shared market comes from physical presence, not both homes, so home cities give no distance.</param>
    public sealed record Reach(bool SameMarket, IReadOnlyCollection<string> MarketModes, IReadOnlySet<string> VisitingModes, bool ByPresence = false);

    /// <summary>
    /// Issue 572 (D448): whether this person's profile opens for the viewer at all, by the same rules as GET /api/profiles/{id}
    /// without a mode: a candidate (no block either way, active), a shared visible mode, and the market reach (D125, D142).
    /// ShownMode is the mode that page shows first, so a list can use the same name (the mode's alias) as the linked profile.
    /// </summary>
    /// <param name="Modes">D450: every mode the viewer can open this profile in now (both on and visible), in the shared order.</param>
    public sealed record Opened(Row Person, string ShownMode, IReadOnlyList<string>? Modes = null);

    public static async Task<Opened?> OpensAsync(SwoovrDbContext db, IConfiguration configuration, MemberProfile viewer, Guid publicId, DateTimeOffset now,
        CancellationToken cancellationToken) =>
        (await OpensManyAsync(db, configuration, viewer, [publicId], now, cancellationToken)).GetValueOrDefault(publicId);

    /// <summary>
    /// The same rule for a list (PR 603 review): candidates, shared modes and connection requests load once for everyone,
    /// and the viewer's home market once. Since a profile opens when any reach holds (MarketAccess.ProfileOpens), a request
    /// or connection, or the same home market (not a bot, as ReachAsync decides it), settles it without the per-person
    /// presence, visit and travel lookups; only the rest take them. Keyed by public id; people whose profile doesn't open
    /// are left out.
    /// </summary>
    public static async Task<Dictionary<Guid, Opened>> OpensManyAsync(SwoovrDbContext db, IConfiguration configuration, MemberProfile viewer,
        IReadOnlyCollection<Guid> publicIds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var opened = new Dictionary<Guid, Opened>();
        if (publicIds.Count == 0) return opened;
        var ids = publicIds.Distinct().ToList();
        var people = await Candidates(db, configuration, viewer.AccountId).Where(c => ids.Contains(c.Profile.PublicId)).ToListAsync(cancellationToken);
        if (people.Count == 0) return opened;
        var sharedBy = await SharedModesAsync(db, viewer.AccountId, people, cancellationToken);
        var me = viewer.AccountId;
        var others = people.Select(p => p.Profile.AccountId).ToList();
        var requests = await db.ConnectionRequests.Where(r => (r.Status == ConnectionRequestStatus.Pending || r.Status == ConnectionRequestStatus.Accepted)
            && (r.PairLow == me && others.Contains(r.PairHigh) || r.PairHigh == me && others.Contains(r.PairLow))).ToListAsync(cancellationToken);
        var (home, _) = await MarketEndpoints.HomeAsync(db, viewer, cancellationToken);
        foreach (var person in people)
        {
            var other = person.Profile.AccountId;
            var shared = sharedBy[other];
            if (shared.Count == 0) continue;
            var between = requests.Where(r => r.PairLow == other || r.PairHigh == other).ToList();
            var shownIds = ModePrivacy.Shown(null, shared.Select(m => m.Id).ToList(), between.Select(r => r.ModeId).ToHashSet());
            if (shownIds.Count == 0) continue;
            var opens = between.Count > 0
                || home is { Open: true } && viewer.HomeGeoNameId is not null && !person.Profile.Bot
                   && MarketAccess.SameMarket(home.Id, person.Profile.HomeMarketId, false, null);
            if (!opens)
            {
                var reach = await ReachAsync(db, configuration, viewer, person, shared, between, now, cancellationToken);
                opens = MarketAccess.ProfileOpens(reach.SameMarket, false, reach.VisitingModes.Count > 0)
                    || (await Travel.ArrivalEndpoints.ReachAsync(db, viewer, person, shared, now, cancellationToken)).SearchModes.Count > 0;
            }
            if (opens) opened[person.Profile.PublicId] = new Opened(person, shownIds[0], shared.Select(m => m.Id).ToList());
        }
        return opened;
    }

    public static async Task<Reach> ReachAsync(SwoovrDbContext db, IConfiguration configuration, MemberProfile viewer, Row person,
        IReadOnlyList<ConnectionMode> shared, List<ConnectionRequest> between, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var (market, modes) = await MarketEndpoints.HomeAsync(db, viewer, cancellationToken);
        var bots = person.Profile.Bot ? await MarketEndpoints.BotMarketsAsync(db, person.Profile.AccountId, cancellationToken) : null;
        // D346, #279: physical presence by a fresh reading also opens an open market, with that market's modes; no trip.
        if (market is not { Open: true } || viewer.HomeGeoNameId is null
            || !MarketAccess.SameMarket(market.Id, person.Profile.HomeMarketId, person.Profile.Bot, bots))
        {
            if ((await MarketEndpoints.SharedByPresenceAsync(db, viewer, person.Profile, bots, now, cancellationToken)).Select(m => m.Id).ToList() is { Count: > 0 } present)
                return new(true, await db.MarketModes.Where(m => present.Contains(m.MarketId)).Select(m => m.ModeId).Distinct().ToListAsync(cancellationToken),
                    new HashSet<string>(), ByPresence: true);
            if (market is not { Open: true } || viewer.HomeGeoNameId is null) return new(false, [], new HashSet<string>());
        }
        var same = MarketAccess.SameMarket(market.Id, person.Profile.HomeMarketId, person.Profile.Bot, bots);
        var visiting = new HashSet<string>();
        if (!same)
        {
            var zones = await MarketEndpoints.ZonesAsync(db, cancellationToken);
            foreach (var mode in shared.Where(m => modes.Contains(m.Id)))
                if ((await Travel.TravelEndpoints.VisitsToAsync(db, configuration, viewer.AccountId, market, zones, mode.Id, now, cancellationToken, person.Profile.AccountId)).Count > 0)
                    visiting.Add(mode.Id);
        }
        return new(same, modes, visiting);
    }

    /// <summary>Where the viewer stands in one mode: connected, sent, received, or null for neither.</summary>
    internal static string? ConnectionState(ConnectionRequest? request, Guid viewer) => request switch
    {
        null => null, { Status: ConnectionRequestStatus.Accepted } => "connected", _ when request.FromAccountId == viewer => "sent", _ => "received",
    };

    private static SharedModeView ModeView(ConnectionMode mode, IReadOnlyList<string> chips, Guid viewer, List<ConnectionRequest> between, bool canRequest)
    {
        var request = between.Where(r => r.ModeId == mode.Id).OrderBy(r => r.Status).FirstOrDefault();
        return new(mode.Id, mode.Label, chips, ConnectionState(request, viewer) ?? "none", request is { Status: ConnectionRequestStatus.Pending } ? request.Id : null, canRequest);
    }

    /// <summary>The available modes in which the person is shown to the viewer (DiscoveryEligibility.ShownIn), in order.</summary>
    public static async Task<List<ConnectionMode>> SharedModesAsync(SwoovrDbContext db, Guid viewer, Row person, CancellationToken cancellationToken) =>
        (await SharedModesAsync(db, viewer, [person], cancellationToken))[person.Profile.AccountId];

    /// <summary>
    /// The same rule for several people at once (PR 561 review): the modes and settings load once, and the matching facts
    /// once per mode for everyone eligible in it, instead of once per person and mode. Every person is a key.
    /// </summary>
    public static async Task<Dictionary<Guid, List<ConnectionMode>>> SharedModesAsync(SwoovrDbContext db, Guid viewer, IReadOnlyList<Row> people,
        CancellationToken cancellationToken)
    {
        var result = people.Select(p => p.Profile.AccountId).Distinct().ToDictionary(id => id, _ => new List<ConnectionMode>());
        if (people.Count == 0) return result;
        var ids = result.Keys.ToList();
        var modes = await db.ConnectionModes.Where(m => m.Available).OrderBy(m => m.SortOrder).ToListAsync(cancellationToken);
        var mine = await db.MemberModes.Where(m => m.AccountId == viewer).ToDictionaryAsync(m => m.ModeId, cancellationToken);
        var theirs = (await db.MemberModes.Where(m => ids.Contains(m.AccountId)).ToListAsync(cancellationToken))
            .GroupBy(m => m.AccountId).ToDictionary(g => g.Key, g => g.ToDictionary(m => m.ModeId));
        var bots = await BotModesAsync(db, people.Where(p => p.Profile.Bot).Select(p => p.Profile.AccountId).Distinct().ToList(), cancellationToken);
        // D138, D140: a mode where the reciprocal rules fail (an excluded chip either way, Dating "show me" either way)
        // isn't shared: neither can find nor contact the other there. Date of birth is required by Candidates (D139).
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var mode in modes)
        {
            var eligible = people.Where(p => DiscoveryEligibility.ShownIn(mode.Id, mode, mine.GetValueOrDefault(mode.Id),
                theirs.GetValueOrDefault(p.Profile.AccountId)?.GetValueOrDefault(mode.Id), p.Candidate(bots))).Select(p => p.Profile.AccountId).Distinct().ToList();
            if (eligible.Count == 0) continue;
            var facts = await FactsAsync(db, [viewer, .. eligible], mode.Id, today, cancellationToken);
            if (!facts.TryGetValue(viewer, out var own)) continue;
            foreach (var id in eligible)
                if (id != viewer && facts.TryGetValue(id, out var other) && MatchPolicy.Reciprocal(mode.Id, own, other)) result[id].Add(mode);
        }
        return result;
    }

    // Member-init (not a constructor) so EF can keep composing queries over it.
    public sealed class Row
    {
        public required MemberProfile Profile { get; init; }
        public AccountStatus Status { get; init; }
        public bool BetaActive { get; init; }
        public DiscoveryCandidate Candidate(Dictionary<Guid, HashSet<string>> bots) =>
            new(Status, Profile.AgeStatus, BetaActive, Profile.Bot, Profile.DisplayName, bots.GetValueOrDefault(Profile.AccountId));
    }

    /// <summary>
    /// Members-realm people other than the viewer who pass DiscoveryEligibility.Discoverable, filtered in the database,
    /// leaving out anyone the viewer blocked or who blocked the viewer (D112: a block hides both people everywhere).
    /// </summary>
    public static IQueryable<Row> Candidates(SwoovrDbContext db, IConfiguration configuration, Guid viewer)
    {
        // Current persisted authority is composed into the same server-side candidate query.
        // A missing singleton returns no candidates rather than reopening access from a static flag.
        var authority=db.Database.SqlQueryRaw<Swoovr.Api.Accounts.RegistrationAuthority.Row>("SELECT beta_enabled AS \"BetaEnabled\",public_enabled AS \"PublicEnabled\",revision AS \"Revision\" FROM registration_availability WHERE id=1");
        AgeStatus[] adult = [AgeStatus.Attested, AgeStatus.SelfDeclaredAdult, AgeStatus.Verified, AgeStatus.AdultOverride];
        var now = DateTimeOffset.UtcNow;
        return from profile in db.MemberProfiles
               join account in db.Accounts on profile.AccountId equals account.Id
               where account.Realm == AccountRealm.Members && account.Status == AccountStatus.Active
                     && adult.Contains(profile.AgeStatus) && profile.DisplayName != null && (profile.DateOfBirth != null || profile.Bot) && profile.AccountId != viewer
                     && !db.Blocks.Any(b => b.BlockerAccountId == viewer && b.BlockedAccountId == profile.AccountId
                         || b.BlockerAccountId == profile.AccountId && b.BlockedAccountId == viewer)
                     // D133, D325: suspended and terminated accounts, and restrictions that take the member out of discovery
                     // (or name nothing, which limits everything), are out of discovery while the sanction lasts.
                     && !db.Sanctions.Any(s => s.AccountId == profile.AccountId && s.LiftedAt == null && s.StartsAt <= now && (s.EndsAt == null || s.EndsAt > now)
                         && (s.Kind == SanctionKind.Suspension || s.Kind == SanctionKind.Termination
                             || s.Kind == SanctionKind.Restriction && (s.HideFromDiscovery || !s.NoNewRequests && !s.NoMessages && !s.NoPhotoUploads)))
               let beta = authority.Any(p=>p.PublicEnabled || !p.BetaEnabled) || db.BetaEnrolments.Any(e => e.AccountId == account.Id && e.RevokedAt == null)
               where authority.Any() && beta
               select new Row { Profile = profile, Status = account.Status, BetaActive = beta };
    }

    /// <summary>The configured badge order (Q11 rule 61); falls back to the D74 order when the table is empty.</summary>
    public static async Task<IReadOnlyList<Badge>> BadgeOrderAsync(SwoovrDbContext db, CancellationToken cancellationToken)
    {
        var order = await db.BadgeDefinitions.OrderBy(b => b.SortOrder).Select(b => b.Badge).ToListAsync(cancellationToken);
        return order.Count > 0 ? order : BadgePolicy.DefaultOrder;
    }

    /// <summary>Public badges only: never admin verified, evidence or roles (Q11 rule 64).</summary>
    public static IReadOnlyList<BadgeView> Badges(MemberProfile profile, IReadOnlyList<Badge> order) =>
        BadgePolicy.For(profile, order).Select(b => new BadgeView(Wire(b.Badge), b.CheckedAt)).ToList();

    public static async Task<HashSet<string>> BotModesAsync(SwoovrDbContext db, Guid bot, CancellationToken cancellationToken) =>
        (await db.BotModeAuthorisations.Where(b => b.AccountId == bot).Select(b => b.ModeId).ToListAsync(cancellationToken)).ToHashSet();

    public static async Task<Dictionary<Guid, HashSet<string>>> BotModesAsync(SwoovrDbContext db, List<Guid> bots, CancellationToken cancellationToken) =>
        bots.Count == 0 ? [] : (await db.BotModeAuthorisations.Where(b => bots.Contains(b.AccountId)).ToListAsync(cancellationToken))
            .GroupBy(b => b.AccountId).ToDictionary(g => g.Key, g => g.Select(b => b.ModeId).ToHashSet());

    public static async Task<IReadOnlyList<ConnectionModeView>> CatalogueAsync(SwoovrDbContext db, CancellationToken cancellationToken)
    {
        var modes = await db.ConnectionModes.Where(m => m.Available).OrderBy(m => m.SortOrder).ToListAsync(cancellationToken);
        var intentions = await db.ModeIntentions.Where(i => i.Available).OrderBy(i => i.SortOrder).ToListAsync(cancellationToken);
        return modes.Select(m => new ConnectionModeView(m.Id, m.Label,
            intentions.Where(i => i.ModeId == m.Id).Select(i => new IntentionView(i.Id, i.Label)).ToList(), ModeSettingsPolicy.MaxIntentions)).ToList();
    }

    private static async Task<MyProfileView> OwnAsync(SwoovrDbContext db, MemberProfile profile, CancellationToken cancellationToken)
    {
        var modes = await db.ConnectionModes.Where(m => m.Available).OrderBy(m => m.SortOrder).ToListAsync(cancellationToken);
        var settings = await db.MemberModes.Where(m => m.AccountId == profile.AccountId).ToDictionaryAsync(m => m.ModeId, cancellationToken);
        // Retired chips stay stored but hidden, from the member too (D113).
        var chosen = await (from i in db.MemberModeIntentions
                            join offered in db.ModeIntentions on i.IntentionId equals offered.Id
                            where i.AccountId == profile.AccountId && offered.Available
                            orderby offered.SortOrder
                            select i).ToListAsync(cancellationToken);
        var home = profile.HomeGeoNameId is { } id ? await db.GeoCities.FindAsync([id], cancellationToken) : null;
        return new(profile.PublicId, profile.DisplayName, profile.About, modes.Select(m => settings.GetValueOrDefault(m.Id) is { } s
            ? new MyModeView(m.Id, s.Enabled, s.Visible, chosen.Where(i => i.ModeId == m.Id).Select(i => i.IntentionId).ToList(), s.About, s.ShareCoreAbout == true, s.DisplayName)
            : new MyModeView(m.Id, false, false, [])).ToList(), home is null ? null : Swoovr.Api.Travel.TravelEndpoints.Place(home),
            await db.MarketInterests.AnyAsync(i => i.AccountId == profile.AccountId, cancellationToken), profile.PictureText,
            profile.ExploreNoticeDismissedAt is not null);
    }

    /// <summary>Labels of the available intentions each person chose, by (account, mode).</summary>
    internal static async Task<Dictionary<(Guid, string), IReadOnlyList<string>>> IntentionLabelsAsync(SwoovrDbContext db, List<Guid> people, string? modeId,
        CancellationToken cancellationToken)
    {
        var rows = await (from chosen in db.MemberModeIntentions
                          join intention in db.ModeIntentions on chosen.IntentionId equals intention.Id
                          where people.Contains(chosen.AccountId) && intention.Available && (modeId == null || chosen.ModeId == modeId)
                          orderby intention.SortOrder
                          select new { chosen.AccountId, chosen.ModeId, intention.Label }).ToListAsync(cancellationToken);
        return rows.GroupBy(r => (r.AccountId, r.ModeId)).ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(r => r.Label).ToList());
    }
}
