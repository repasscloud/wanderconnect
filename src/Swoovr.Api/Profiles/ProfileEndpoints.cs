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
/// same member gate as /api/v1/me (active, adult-locked accounts refused, beta enrolled).
/// </summary>
public static class ProfileEndpoints
{
    /// <summary>DEFAULT page size until Explore pages through results.</summary>
    public const int ExploreLimit = 50;

    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization();
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
        api.MapPut("/me/profile", async (ProfileUpdate update, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (name, area, about) = (ProfileTextPolicy.Clean(update.DisplayName), ProfileTextPolicy.Clean(update.Area), ProfileTextPolicy.Clean(update.About));
            if (ProfileTextPolicy.Check(name, area, about) != ProfileTextError.None)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidProfile, "Check the profile fields.");
            var (profile, db) = (current!.Profile!, accounts.Db!);
            (profile.DisplayName, profile.Area, profile.About) = (name, area, about);
            // The audit records that the profile changed, never what it says.
            Audit.Record(db, clock.GetUtcNow(), profile.AccountId, "account", "profile.updated", "account", profile.AccountId.ToString());
            await db.SaveChangesAsync(cancellationToken);
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
            var now = clock.GetUtcNow();
            var setting = await db.MemberModes.SingleOrDefaultAsync(m => m.AccountId == profile.AccountId && m.ModeId == modeId, cancellationToken);
            if (setting is null) db.MemberModes.Add(setting = new MemberMode { AccountId = profile.AccountId, ModeId = modeId });
            // Each mode is independent: only this row and its intentions change (Q12 rule 2).
            (setting.Enabled, setting.Visible, setting.UpdatedAt) = (update.Enabled, update.Visible, now);
            // Only the difference is written, so kept rows (including retired chips, D113) are never touched.
            db.MemberModeIntentions.RemoveRange(stored.Where(i => !change.Stored.Contains(i.IntentionId)));
            db.MemberModeIntentions.AddRange(change.Stored.Where(id => stored.All(i => i.IntentionId != id))
                .Select(id => new MemberModeIntention { AccountId = profile.AccountId, ModeId = modeId, IntentionId = id }));
            Audit.Record(db, now, profile.AccountId, "account", "mode.updated", "connection-mode", modeId,
                $"{(update.Enabled ? "on" : "off")}:{(update.Visible ? "visible" : "hidden")}:intentions:{change.Stored.Count}");
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(await OwnAsync(db, profile, cancellationToken));
        });
        api.MapGet("/explore", async (string? mode, ClaimsPrincipal user, AccountResolver accounts, IConfiguration configuration, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var selected = await db.ConnectionModes.SingleOrDefaultAsync(m => m.Id == mode && m.Available, cancellationToken);
            if (selected is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownMode, "That connection mode isn't available.");
            var viewer = current!.Profile!.AccountId;
            var own = await db.MemberModes.SingleOrDefaultAsync(m => m.AccountId == viewer && m.ModeId == selected.Id, cancellationToken);
            if (own is not { Enabled: true })
                return Problem(StatusCodes.Status403Forbidden, ProblemCodes.ModeNotEnabled, "Turn this mode on to see who's here.");
            var people = await Candidates(db, configuration)
                .Where(c => c.Profile.AccountId != viewer && db.MemberModes.Any(m => m.AccountId == c.Profile.AccountId
                    && m.ModeId == selected.Id && m.Enabled && m.Visible))
                .OrderByDescending(c => db.MemberModes.Where(m => m.AccountId == c.Profile.AccountId && m.ModeId == selected.Id).Select(m => m.UpdatedAt).First())
                .Take(ExploreLimit)
                .ToListAsync(cancellationToken);
            // Checked again in code with the same policy the database filter mirrors, so the rule has one source.
            var shown = people.Where(c => DiscoveryEligibility.ShownIn(selected.Id, selected, own,
                new MemberMode { ModeId = selected.Id, Enabled = true, Visible = true }, c.Candidate())).ToList();
            var chips = await IntentionLabelsAsync(db, shown.Select(c => c.Profile.AccountId).ToList(), selected.Id, cancellationToken);
            return Results.Ok(new ExploreResult(selected.Id, shown.Select(c => new ProfileCard(c.Profile.PublicId, c.Profile.DisplayName!,
                c.Profile.Area, c.Profile.About, chips.GetValueOrDefault((c.Profile.AccountId, selected.Id), []))).ToList()));
        });
        api.MapGet("/profiles/{id:guid}", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, IConfiguration configuration, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var viewer = current!.Profile!.AccountId;
            var person = await Candidates(db, configuration).SingleOrDefaultAsync(c => c.Profile.PublicId == id && c.Profile.AccountId != viewer, cancellationToken);
            // Shown only in modes both have on and the person made visible; otherwise it reads as not found.
            var modes = await db.ConnectionModes.Where(m => m.Available).OrderBy(m => m.SortOrder).ToListAsync(cancellationToken);
            var mine = await db.MemberModes.Where(m => m.AccountId == viewer).ToDictionaryAsync(m => m.ModeId, cancellationToken);
            var theirs = person is null ? [] : await db.MemberModes.Where(m => m.AccountId == person.Profile.AccountId).ToDictionaryAsync(m => m.ModeId, cancellationToken);
            var shared = person is null ? [] : modes.Where(m => DiscoveryEligibility.ShownIn(m.Id, m, mine.GetValueOrDefault(m.Id), theirs.GetValueOrDefault(m.Id), person.Candidate())).ToList();
            if (person is null || shared.Count == 0) return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownMode, "That profile isn't available.");
            var chips = await IntentionLabelsAsync(db, [person.Profile.AccountId], null, cancellationToken);
            return Results.Ok(new PersonProfile(person.Profile.PublicId, person.Profile.DisplayName!, person.Profile.Area, person.Profile.About,
                shared.Select(m => new SharedModeView(m.Id, m.Label, chips.GetValueOrDefault((person.Profile.AccountId, m.Id), []))).ToList()));
        });
    }

    // Member-init (not a constructor) so EF can keep composing queries over it.
    private sealed class Row
    {
        public required MemberProfile Profile { get; init; }
        public AccountStatus Status { get; init; }
        public bool BetaActive { get; init; }
        public DiscoveryCandidate Candidate() => new(Status, Profile.AgeStatus, BetaActive, Profile.Bot, Profile.Employee, Profile.DisplayName);
    }

    /// <summary>Members-realm people who pass DiscoveryEligibility.Discoverable, filtered in the database.</summary>
    private static IQueryable<Row> Candidates(SwoovrDbContext db, IConfiguration configuration)
    {
        var betaOn = configuration.GetValue("Beta:Enabled", true);
        AgeStatus[] adult = [AgeStatus.Attested, AgeStatus.SelfDeclaredAdult, AgeStatus.Verified, AgeStatus.AdultOverride];
        return from profile in db.MemberProfiles
               join account in db.Accounts on profile.AccountId equals account.Id
               where account.Realm == AccountRealm.Members && account.Status == AccountStatus.Active
                     && adult.Contains(profile.AgeStatus) && !profile.Bot && !profile.Employee && profile.DisplayName != null
               let beta = !betaOn || db.BetaEnrolments.Any(e => e.AccountId == account.Id && e.RevokedAt == null)
               where beta
               select new Row { Profile = profile, Status = account.Status, BetaActive = beta };
    }

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
        return new(profile.PublicId, profile.DisplayName, profile.Area, profile.About, modes.Select(m => settings.GetValueOrDefault(m.Id) is { } s
            ? new MyModeView(m.Id, s.Enabled, s.Visible, chosen.Where(i => i.ModeId == m.Id).Select(i => i.IntentionId).ToList())
            : new MyModeView(m.Id, false, false, [])).ToList());
    }

    /// <summary>Labels of the available intentions each person chose, by (account, mode).</summary>
    private static async Task<Dictionary<(Guid, string), IReadOnlyList<string>>> IntentionLabelsAsync(SwoovrDbContext db, List<Guid> people, string? modeId,
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
