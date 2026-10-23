using Microsoft.EntityFrameworkCore;
using Npgsql;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

/// <summary>
/// Runs against a real PostgreSQL when SWOOVR_TEST_DATABASE holds an admin connection string
/// (scripts/check.sh sets it for the local container). Each run creates and drops its own
/// throwaway database, so it never touches the application's data.
/// </summary>
static class PersistenceChecks
{
    public static async Task Run()
    {
        var admin = Environment.GetEnvironmentVariable("SWOOVR_TEST_DATABASE");
        if (string.IsNullOrWhiteSpace(admin))
        {
            Console.WriteLine("SKIP: persistence checks (SWOOVR_TEST_DATABASE not set; no local PostgreSQL).");
            return;
        }
        var name = "swoovr_check_" + Guid.NewGuid().ToString("N")[..12];
        await Execute(admin, $"CREATE DATABASE {name}");
        var count = 0;
        void Check(string what, bool condition)
        {
            if (!condition) throw new Exception($"Persistence: {what}");
            count++;
        }
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(admin) { Database = name, Pooling = false }.ConnectionString;
            DbContextOptions<SwoovrDbContext> options = new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(connection).Options;
            await using (var db = new SwoovrDbContext(options))
            {
                await db.Database.MigrateAsync();
                Check("no pending migrations", !(await db.Database.GetPendingMigrationsAsync()).Any());
            }

            var now = DateTimeOffset.Parse("2026-10-23T00:00:00Z");
            var id = Guid.NewGuid();
            await using (var db = new SwoovrDbContext(options))
            {
                db.Accounts.Add(new Account { Id = id, Issuer = "https://issuer.example.test/realms/members", Subject = "s-1", Realm = AccountRealm.Members, Status = AccountStatus.Active, CreatedAt = now, LastSeenAt = now });
                db.MemberProfiles.Add(new MemberProfile { AccountId = id, AgeStatus = AgeStatus.Attested, DateOfBirth = new DateOnly(1990, 5, 1) });
                await db.SaveChangesAsync();
            }
            await using (var db = new SwoovrDbContext(options))
            {
                db.Accounts.Add(new Account { Id = Guid.NewGuid(), Issuer = "https://issuer.example.test/realms/members", Subject = "s-1", Realm = AccountRealm.Members, CreatedAt = now, LastSeenAt = now });
                Check("(issuer, subject) is unique", await Throws(db.SaveChangesAsync()));
            }
            await using (var db = new SwoovrDbContext(options))
            {
                db.Accounts.Add(new Account { Id = Guid.NewGuid(), Issuer = "https://issuer.example.test/realms/staff", Subject = "s-1", Realm = AccountRealm.Staff, CreatedAt = now, LastSeenAt = now });
                await db.SaveChangesAsync();
                Check("same subject under another issuer is another account", await db.Accounts.CountAsync() == 2);
                var stored = await db.Database.SqlQueryRaw<string>("SELECT age_status AS \"Value\" FROM member_profiles").SingleAsync();
                Check("enums stored by name", stored == "Attested");
                var profile = await db.MemberProfiles.SingleAsync();
                Check("date of birth round-trips", profile.DateOfBirth == new DateOnly(1990, 5, 1));
                db.RoleGrants.Add(new RoleGrant { Id = Guid.NewGuid(), AccountId = id, Role = AccountRole.Admin, GrantedAt = now });
                await db.SaveChangesAsync();
            }
            await using (var db = new SwoovrDbContext(options))
            {
                db.RoleGrants.Add(new RoleGrant { Id = Guid.NewGuid(), AccountId = id, Role = AccountRole.Admin, GrantedAt = now });
                Check("a role is granted once even without a venue", await Throws(db.SaveChangesAsync()));
            }
            await using (var db = new SwoovrDbContext(options))
            {
                db.RoleGrants.Add(new RoleGrant { Id = Guid.NewGuid(), AccountId = Guid.NewGuid(), Role = AccountRole.Admin, GrantedAt = now });
                Check("a grant needs an account", await Throws(db.SaveChangesAsync()));
            }
            await using (var db = new SwoovrDbContext(options))
            {
                db.Accounts.Remove(await db.Accounts.SingleAsync(a => a.Id == id));
                await db.SaveChangesAsync();
                Check("profile and grants go with the account", !await db.MemberProfiles.AnyAsync() && !await db.RoleGrants.AnyAsync());
            }
            await Extra(options, Check);
            await Modes(options, Check);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await Execute(admin, $"DROP DATABASE IF EXISTS {name} WITH (FORCE)");
        }
        Console.WriteLine($"PASS: {count} persistence cases against PostgreSQL (migrations, keys, enums, cascades).");
    }

    /// <summary>Further persistence cases added by later slices.</summary>
    /// <summary>D92 tables: seeded modes, stable keys, cascades and the public id default.</summary>
    static async Task Modes(DbContextOptions<SwoovrDbContext> options, Action<string, bool> check)
    {
        var now = DateTimeOffset.Parse("2026-10-23T00:00:00Z");
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        await using (var db = new SwoovrDbContext(options))
        {
            var seeded = await db.ConnectionModes.OrderBy(m => m.SortOrder).Select(m => m.Id + ":" + m.Label).ToListAsync();
            check("four modes seeded in order", seeded.SequenceEqual(["dating:Dating", "friends:Friends", "activities:Activities", "business:Business"]));
            var chips = await db.ModeIntentions.OrderBy(i => i.ModeId).ThenBy(i => i.SortOrder).ToListAsync();
            check("forty approved chips seeded with their fixed ids (Q16, D113)", chips.Count == 40
                && chips.Select(i => (i.Id, i.Label)).ToHashSet().SetEquals(IntentionChips.All.Select(i => (i.Id, i.Label))));
            foreach (var id in new[] { a, b })
            {
                db.Accounts.Add(new Account { Id = id, Issuer = "https://issuer.example.test/realms/members", Subject = "modes-" + id, Realm = AccountRealm.Members, CreatedAt = now, LastSeenAt = now });
                db.MemberProfiles.Add(new MemberProfile { AccountId = id, AgeStatus = AgeStatus.Attested });
            }
            await db.SaveChangesAsync();
            var ids = await db.MemberProfiles.Where(p => p.AccountId == a || p.AccountId == b).Select(p => p.PublicId).ToListAsync();
            check("public ids generated and distinct", ids.All(i => i != Guid.Empty) && ids.Distinct().Count() == 2);
        }
        var chip = IntentionChips.Id(1, 1);
        await using (var db = new SwoovrDbContext(options))
        {
            db.MemberModes.Add(new MemberMode { AccountId = a, ModeId = ConnectionModeIds.Dating, Enabled = true, Visible = true, UpdatedAt = now });
            db.MemberModes.Add(new MemberMode { AccountId = a, ModeId = ConnectionModeIds.Friends, Enabled = true, Visible = false, UpdatedAt = now });
            db.MemberModeIntentions.Add(new MemberModeIntention { AccountId = a, ModeId = ConnectionModeIds.Dating, IntentionId = chip });
            await db.SaveChangesAsync();
            check("several modes at once", await db.MemberModes.CountAsync(m => m.AccountId == a) == 2);
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.MemberModes.Add(new MemberMode { AccountId = a, ModeId = ConnectionModeIds.Dating, UpdatedAt = now });
            check("one setting per member and mode", await Throws(db.SaveChangesAsync()));
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.MemberModes.Add(new MemberMode { AccountId = b, ModeId = "travel", UpdatedAt = now });
            check("no setting for a mode that does not exist (travel is not a mode)", await Throws(db.SaveChangesAsync()));
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.MemberModeIntentions.Add(new MemberModeIntention { AccountId = b, ModeId = ConnectionModeIds.Dating, IntentionId = chip });
            check("an intention needs the member's setting for that mode", await Throws(db.SaveChangesAsync()));
        }
        await using (var db = new SwoovrDbContext(options))
        {
            var order = await db.BadgeDefinitions.OrderBy(d => d.SortOrder).Select(d => d.Badge).ToListAsync();
            check("badge order seeded as data (Q11 rule 61)", order.SequenceEqual(BadgePolicy.DefaultOrder));
            db.BotModeAuthorisations.Add(new BotModeAuthorisation { AccountId = a, ModeId = ConnectionModeIds.Dating, AuthorisedAt = now });
            await db.SaveChangesAsync();
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.BotModeAuthorisations.Add(new BotModeAuthorisation { AccountId = a, ModeId = ConnectionModeIds.Dating, AuthorisedAt = now });
            check("one authorisation per bot and mode", await Throws(db.SaveChangesAsync()));
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.BotModeAuthorisations.Add(new BotModeAuthorisation { AccountId = b, ModeId = "travel", AuthorisedAt = now });
            check("a bot can only be authorised for a real mode", await Throws(db.SaveChangesAsync()));
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.Accounts.Remove(await db.Accounts.SingleAsync(x => x.Id == a));
            await db.SaveChangesAsync();
            check("mode settings, intentions and bot authorisations go with the account", !await db.MemberModes.AnyAsync(m => m.AccountId == a)
                && !await db.MemberModeIntentions.AnyAsync() && !await db.BotModeAuthorisations.AnyAsync());
        }
    }

    static async Task Extra(DbContextOptions<SwoovrDbContext> options, Action<string, bool> check)
    {
        var now = DateTimeOffset.Parse("2026-10-23T00:00:00Z");
        await using (var db = new SwoovrDbContext(options))
        {
            db.BetaEnrolments.Add(new BetaEnrolment { Id = Guid.NewGuid(), Email = "beta@example.test", CreatedAt = now, RevokedAt = now });
            db.BetaEnrolments.Add(new BetaEnrolment { Id = Guid.NewGuid(), Email = "beta@example.test", CreatedAt = now });
            await db.SaveChangesAsync();
            check("a revoked enrolment can be added again", await db.BetaEnrolments.CountAsync() == 2);
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.BetaEnrolments.Add(new BetaEnrolment { Id = Guid.NewGuid(), Email = "beta@example.test", CreatedAt = now });
            check("one live enrolment per address", await Throws(db.SaveChangesAsync()));
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.FounderPlaces.Add(new FounderPlace { Place = 0, GrantedAt = now });
            check("founder places start at 1", await Throws(db.SaveChangesAsync()));
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.FounderPlaces.Add(new FounderPlace { Place = 1, GrantedAt = now });
            db.FounderTokens.Add(new FounderToken { Id = Guid.NewGuid(), Place = 1, Code = "FOUNDER-V1-A", SecretVersion = 1, IssuedAt = now });
            await db.SaveChangesAsync();
            db.FounderTokens.Add(new FounderToken { Id = Guid.NewGuid(), Place = 1, Code = "FOUNDER-V1-A", SecretVersion = 1, IssuedAt = now });
            check("a founder code is stored once", await Throws(db.SaveChangesAsync()));
        }
        await using (var db = new SwoovrDbContext(options))
        {
            db.FounderTokens.Add(new FounderToken { Id = Guid.NewGuid(), Place = 99, Code = "FOUNDER-V1-B", SecretVersion = 1, IssuedAt = now });
            check("a token needs its place", await Throws(db.SaveChangesAsync()));
        }
        await using (var db = new SwoovrDbContext(options))
        {
            var first = Guid.NewGuid(); var second = Guid.NewGuid();
            foreach (var id in new[] { first, second })
                db.Accounts.Add(new Account { Id = id, Issuer = "https://issuer.example.test/realms/members", Subject = id.ToString(), Realm = AccountRealm.Members, CreatedAt = now, LastSeenAt = now });
            db.MemberProfiles.Add(new MemberProfile { AccountId = first, FoundingMember = true, FounderPlace = 1 });
            db.MemberProfiles.Add(new MemberProfile { AccountId = second });
            await db.SaveChangesAsync();
            (await db.MemberProfiles.SingleAsync(p => p.AccountId == second)).FounderPlace = 1;
            check("one live account per founding place", await Throws(db.SaveChangesAsync()));
        }
    }

    static async Task<bool> Throws(Task task)
    {
        try { await task; return false; }
        catch (DbUpdateException) { return true; }
    }

    static async Task Execute(string connection, string sql)
    {
        await using var db = new NpgsqlConnection(connection);
        await db.OpenAsync();
        await using var command = new NpgsqlCommand(sql, db);
        await command.ExecuteNonQueryAsync();
    }
}
