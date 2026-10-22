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
            await Extra(options, Check);

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
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await Execute(admin, $"DROP DATABASE IF EXISTS {name} WITH (FORCE)");
        }
        Console.WriteLine($"PASS: {count} persistence cases against PostgreSQL (migrations, keys, enums, cascades).");
    }

    /// <summary>Further persistence cases added by later slices.</summary>
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
