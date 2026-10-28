using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Swoovr.Contracts;
using Swoovr.Domain;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Accounts;

/// <summary>
/// Keys for contact history (D187): `ContactHistory:Keys:{version}` (base64, 32 bytes) and `ContactHistory:LookupKey`, held
/// outside the database (Azure Key Vault in hosted environments, policy §2.3); locally derived from the founder secret
/// with their own labels. Null when neither exists, and then no history is written (logged).
/// </summary>
public sealed class ContactHistoryKeys(IConfiguration configuration, Founders founders)
{
    public IReadOnlyDictionary<byte, byte[]> Encryption { get; } = Read(configuration, founders);
    public byte? CurrentVersion => Encryption.Count == 0 ? null : Encryption.Keys.Max();
    public byte[]? Lookup => configuration["ContactHistory:LookupKey"] is { Length: > 0 } configured ? Convert.FromBase64String(configured)
        : founders.CurrentVersion is { } v ? HMACSHA256.HashData(founders.Secrets[v], Encoding.UTF8.GetBytes("swoovr:contact-history:lookup:v1")) : null;

    private static Dictionary<byte, byte[]> Read(IConfiguration configuration, Founders founders)
    {
        var keys = new Dictionary<byte, byte[]>();
        foreach (var entry in configuration.GetSection("ContactHistory:Keys").GetChildren())
            if (byte.TryParse(entry.Key, out var version) && entry.Value is { Length: > 0 } value && Convert.FromBase64String(value) is { Length: 32 } key)
                keys[version] = key;
        // #89: the derived key stays version 1 (still readable) after newer versions are configured, until `keys rotate`.
        if (!keys.ContainsKey(1) && founders.CurrentVersion is { } v)
            keys[1] = HMACSHA256.HashData(founders.Secrets[v], Encoding.UTF8.GetBytes("swoovr:contact-history:encryption:v1"));
        return keys;
    }

    /// <summary>A history record of a previous value, or null (and nothing kept) when no keys are configured.</summary>
    public ContactHistory? Record(Guid accountId, ContactType type, string previous, DateTimeOffset? validFrom, DateTimeOffset now, Guid? changedBy, string reason) =>
        CurrentVersion is { } version && Lookup is { } lookup
            ? ContactHistoryPolicy.Record(accountId, type, previous, validFrom, now, changedBy, reason, version, Encryption[version], lookup)
            : null;
}

/// <summary>
/// Admin-only lookup of contact history (D187, policy §4, §8, §11): which accounts used a given number or address and
/// when, so support can spot one number moving across many accounts. It returns account ids and dates, never the
/// stored values; a match never proves two accounts are the same person (policy §8). Every search is audited (type only).
/// </summary>
public static class ContactHistoryEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/admin/contact-history", async (string? type, string? value, ClaimsPrincipal user, AccountResolver accounts, ContactHistoryKeys keys,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            ContactType? kind = type switch { "mobile" => ContactType.Mobile, "email" => ContactType.Email, _ => null };
            var canonical = kind == ContactType.Mobile ? PhoneVerificationPolicy.Normalise(value) : kind == ContactType.Email ? EmailAddress.Normalise(value) : null;
            if (kind is null || canonical is null)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Give type=mobile or type=email and a valid value.");
            if (keys.Lookup is not { } lookup) return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.NotConfigured, "Contact history isn't configured.");
            var hash = ContactHistoryPolicy.Lookup(lookup, kind.Value, canonical);
            var past = await db.ContactHistories.Where(h => h.ContactType == kind && h.LookupHmac == hash).OrderBy(h => h.ValidUntil)
                .Select(h => new ContactHistoryHit(h.AccountId, h.ValidFrom, h.ValidUntil, h.ChangeReason)).ToListAsync(cancellationToken);
            var holder = kind == ContactType.Mobile
                ? await db.MemberPhones.Where(p => p.Number == canonical).Select(p => (Guid?)p.AccountId).SingleOrDefaultAsync(cancellationToken)
                : await db.Accounts.Where(a => a.Email == canonical && a.Realm == AccountRealm.Members).Select(a => (Guid?)a.Id).FirstOrDefaultAsync(cancellationToken);
            Audit.Record(db, clock.GetUtcNow(), current!.Account!.Id, "account", "contact-history.searched", null, null, kind.Value.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new ContactHistoryResult(holder, past));
        });
    }
}
