using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api;

/// <summary>
/// Operator commands run as `dotnet Swoovr.Api.dll &lt;command&gt;` with the same configuration as
/// the host. Anyone who can run them already holds the database connection string, so they
/// are the bootstrap path for things no account can do yet. Each change is audited as "cli".
/// </summary>
public static class Cli
{

    private static async Task<int> AddAll(SwoovrDbContext db, IEnumerable<string> emails, DateTimeOffset now)
    {
        var (added, existing, invalid) = (0, 0, 0);
        foreach (var email in emails)
            switch ((await BetaEnrolments.AddAsync(db, email, null, now, CancellationToken.None)).Result)
            {
                case BetaEnrolments.AddResult.Added: added++; break;
                case BetaEnrolments.AddResult.AlreadyEnrolled: existing++; break;
                default: invalid++; break;
            }
        // Counts only: addresses are not echoed into terminal logs.
        Console.WriteLine($"Beta enrolments: {added} added, {existing} already enrolled, {invalid} invalid.");
        return invalid > 0 ? 1 : 0;
    }

    public static async Task<int> RunAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args[1..]);
        // Commands print their own short results; host and SQL logging would only add noise.
        builder.Logging.ClearProviders();
        if (!builder.Services.AddSwoovrPersistence(builder.Configuration.GetConnectionString("Swoovr")))
        {
            Console.Error.WriteLine("ConnectionStrings:Swoovr is not configured.");
            return 2;
        }
        await using var app = builder.Build();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SwoovrDbContext>();
        var now = DateTimeOffset.UtcNow;
        switch (args)
        {
            case ["migrate", ..]:
                await db.Database.MigrateAsync();
                Console.WriteLine("Database is up to date.");
                return 0;
            case ["root-admin", "grant", var subject, ..]:
            {
                // The staff-realm subject (Keycloak user id) of the single break-glass account (D36).
                // It must already have signed in once so its account row exists.
                var staff = await db.Accounts.Where(a => a.Realm == AccountRealm.Staff && a.Subject == subject).ToListAsync();
                if (staff.Count != 1) { Console.Error.WriteLine("No single staff-realm account has that subject; sign in once first."); return 1; }
                if (await db.Accounts.AnyAsync(a => a.IsRootAdmin && a.Id != staff[0].Id)) { Console.Error.WriteLine("Another account is already the root admin."); return 1; }
                staff[0].IsRootAdmin = true;
                Audit.Record(db, now, null, "cli", "root-admin.granted", "account", staff[0].Id.ToString());
                await db.SaveChangesAsync();
                Console.WriteLine("Root admin granted.");
                return 0;
            }
            case ["beta", "add", "--file", var path, ..]:
                // One address per line; blank lines and lines starting with # are ignored.
                return await AddAll(db, File.ReadLines(path).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#')), now);
            case ["beta", "add", .. var emails] when emails.Length > 0:
                return await AddAll(db, emails, now);
            case ["beta", "revoke", var email, ..]:
            {
                var normalised = EmailAddress.Normalise(email);
                var live = await db.BetaEnrolments.SingleOrDefaultAsync(e => e.Email == normalised && e.RevokedAt == null);
                if (!await BetaEnrolments.RevokeAsync(db, live, null, now, CancellationToken.None)) { Console.Error.WriteLine("No live enrolment for that address."); return 1; }
                Console.WriteLine("Revoked.");
                return 0;
            }
            case ["beta", "list", ..]:
                foreach (var e in await db.BetaEnrolments.OrderBy(e => e.CreatedAt).ToListAsync())
                    Console.WriteLine($"{e.Email}\t{BetaEnrolments.View(e).State}");
                return 0;
            default:
                Console.Error.WriteLine("Usage: migrate | root-admin grant <staff subject> | beta add <email>... | beta add --file <path> | beta revoke <email> | beta list");
                return 2;
        }
    }
}
