using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Accounts;

public enum FounderGrantOutcome { Granted, AlreadyFounder, PlacesFull, NoVerifiedEmail, NotConfigured }
public sealed record FounderGrant(FounderGrantOutcome Outcome, int? Place = null, bool EmailSent = false);

/// <summary>
/// Founding membership (D63) and its recovery token (D65). Secrets come from configuration
/// `Founders:Secrets:{version}` as at least 64 hex characters (32 bytes); the highest version
/// issues new tokens and every configured version can still check old ones. Locally they come
/// from .local/docker.env; a managed key store is needed before any hosted environment.
/// </summary>
public sealed class Founders(IServiceProvider services, IConfiguration configuration, IEmailSender email, ILogger<Founders> log)
{
    /// <summary>DEFAULT: failed coupon attempts allowed per account per hour.</summary>
    public const int AttemptsPerHour = 5;
    private SwoovrDbContext Db => services.GetRequiredService<SwoovrDbContext>();
    private int Places => configuration.GetValue("Founders:Places", 1000);

    public IReadOnlyDictionary<int, byte[]> Secrets { get; } = ReadSecrets(configuration);
    public int? CurrentVersion => Secrets.Count == 0 ? null : Secrets.Keys.Max();

    private static Dictionary<int, byte[]> ReadSecrets(IConfiguration configuration)
    {
        var secrets = new Dictionary<int, byte[]>();
        foreach (var child in configuration.GetSection("Founders:Secrets").GetChildren())
        {
            if (!int.TryParse(child.Key, out var version) || version < 1 || string.IsNullOrWhiteSpace(child.Value)) continue;
            try
            {
                var bytes = Convert.FromHexString(child.Value.Trim());
                if (bytes.Length >= 32) secrets[version] = bytes;
            }
            catch (FormatException) { }
        }
        return secrets;
    }

    /// <summary>
    /// Grants a founding place and issues the token. Places are numbered under a table lock so
    /// two grants can never take the same place or exceed the limit.
    /// </summary>
    public async Task<FounderGrant> GrantAsync(Account account, MemberProfile profile, Guid? actor, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (CurrentVersion is not { } version) return new(FounderGrantOutcome.NotConfigured);
        if (profile.FoundingMember) return new(FounderGrantOutcome.AlreadyFounder, profile.FounderPlace);
        if (account.Email is null) return new(FounderGrantOutcome.NoVerifiedEmail);
        var db = Db;
        var strategy = db.Database.CreateExecutionStrategy();
        var place = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Database.ExecuteSqlRawAsync("LOCK TABLE founder_places IN EXCLUSIVE MODE", cancellationToken);
            var next = (await db.FounderPlaces.MaxAsync(p => (int?)p.Place, cancellationToken) ?? 0) + 1;
            if (next > Places) return (int?)null;
            db.FounderPlaces.Add(new FounderPlace { Place = next, GrantedAt = now });
            var token = NewToken(next, account.Email, version, now);
            db.FounderTokens.Add(token);
            (profile.FoundingMember, profile.FoundingSince, profile.FounderPlace) = (true, now, next);
            Audit.Record(db, now, actor, actor is null ? "system" : "account", "founder.granted", "account", account.Id.ToString(), $"place:{next}");
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return next;
        });
        if (place is null) return new(FounderGrantOutcome.PlacesFull);
        var code = (await db.FounderTokens.FirstAsync(t => t.Place == place && t.Status == FounderTokenStatus.Valid, cancellationToken)).Code;
        var sent = await SendAsync(account, code, "granted", now, cancellationToken);
        return new(FounderGrantOutcome.Granted, place, sent);
    }

    /// <summary>Re-sends the account's current valid token (for example at a deletion request).</summary>
    public async Task<bool?> ResendAsync(Account account, MemberProfile profile, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!profile.FoundingMember || profile.FounderPlace is not { } place || account.Email is null) return null;
        var token = await CurrentTokenAsync(place, account.Email, cancellationToken);
        if (token is null) return null;
        return await SendAsync(account, token.Code, "resent", now, cancellationToken);
    }

    /// <summary>
    /// Owner answer (D65): when a founder's verified email changes, issue a token for the new
    /// address and invalidate the old ones for that place. Non-founders get nothing.
    /// </summary>
    public async Task ReissueForEmailChangeAsync(Account account, MemberProfile profile, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (!profile.FoundingMember || profile.FounderPlace is not { } place || account.Email is null || CurrentVersion is not { } version) return;
        if (await CurrentTokenAsync(place, account.Email, cancellationToken) is not null) return;
        var db = Db;
        foreach (var old in await db.FounderTokens.Where(t => t.Place == place && t.Status == FounderTokenStatus.Valid).ToListAsync(cancellationToken))
            (old.Status, old.InvalidatedAt, old.InvalidatedReason) = (FounderTokenStatus.Invalidated, now, "email-changed");
        var token = NewToken(place, account.Email, version, now);
        db.FounderTokens.Add(token);
        Audit.Record(db, now, account.Id, "system", "founder.reissued", "founder-token", token.Id.ToString(), "email-changed");
        await db.SaveChangesAsync(cancellationToken);
        await SendAsync(account, token.Code, "reissued", now, cancellationToken);
    }

    /// <summary>Failed coupon attempts by this account in the last hour (from the audit log).</summary>
    public Task<int> RecentFailuresAsync(Guid accountId, DateTimeOffset now, CancellationToken cancellationToken) =>
        Db.AuditEntries.CountAsync(a => a.ActorAccountId == accountId && a.Action == "coupon.rejected" && a.At > now.AddHours(-1), cancellationToken);

    private async Task<FounderToken?> CurrentTokenAsync(int place, string verifiedEmail, CancellationToken cancellationToken)
    {
        var valid = await Db.FounderTokens.Where(t => t.Place == place && t.Status == FounderTokenStatus.Valid).ToListAsync(cancellationToken);
        return valid.FirstOrDefault(t => Secrets.TryGetValue(t.SecretVersion, out var secret) && FounderTokenCodec.Matches(t.Code, t.SecretVersion, verifiedEmail, secret));
    }

    private FounderToken NewToken(int place, string verifiedEmail, int version, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), Place = place, Code = FounderTokenCodec.Issue(verifiedEmail, version, Secrets[version]),
        SecretVersion = version, Status = FounderTokenStatus.Valid, IssuedAt = now,
    };

    private async Task<bool> SendAsync(Account account, string code, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var sent = await email.SendAsync(new EmailMessage(account.Email!, "Your Swoovr founder code",
            $"""
            Thank you for being one of Swoovr's founding members.

            Your founder code is:

            {code}

            Keep this email. If you ever delete your Swoovr account and come back with the same email address, enter this code on the coupon code screen to restore your founding status.

            The code only works with this email address. If you change your email in Swoovr, we'll send you a new code and this one stops working.

            — Swoovr (RePass Cloud Pty Ltd)
            """), cancellationToken);
        var db = Db;
        Audit.Record(db, now, null, "system", sent ? "founder.email-sent" : "founder.email-failed", "account", account.Id.ToString(), reason);
        await db.SaveChangesAsync(cancellationToken);
        if (!sent) log.LogWarning("Founder code email was not sent for account {Account}.", account.Id);
        return sent;
    }
}
