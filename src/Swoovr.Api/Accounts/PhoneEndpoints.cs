using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Accounts;

/// <summary>
/// Mobile number verification (Q45, D148, D153, D154; details D181). Swoovr makes, hashes, checks and expires its own
/// codes; the sender only delivers them. `Phone:Required` (default on, D184; local test hosts set it false) is the
/// registration gate: members without a verified number get 403 phone-verification-required everywhere except their
/// account summary, notices, appeals, deletion and these routes. Audit entries carry ids and outcomes, never a number or
/// a code.
/// </summary>
public static class PhoneEndpoints
{
    public static void Map(WebApplication app)
    {
        var me = app.MapGroup("/api/v1/me/phone").RequireAuthorization();

        me.MapGet("", async (ClaimsPrincipal user, AccountResolver accounts, IVerificationMessageSender sender, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken, phoneExempt: true);
            return denied ?? Results.Ok(await ViewAsync(accounts.Db!, current!.Account!.Id, sender, accounts.PhoneRequired, clock.GetUtcNow(), cancellationToken));
        });

        me.MapPost("", async (PhoneStart request, ClaimsPrincipal user, AccountResolver accounts, IVerificationMessageSender sender, PhoneKeys keys,
            HttpContext context, IConfiguration configuration, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken, phoneExempt: true);
            if (denied is not null) return denied;
            var (account, db, now) = (current!.Account!, accounts.Db!, clock.GetUtcNow());
            if (PhoneVerificationPolicy.Normalise(request.Number) is not { } number)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidPhone, "Enter a mobile number, for example 0412 345 678 or +61 412 345 678.");
            if (keys.Key is not { } key) return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.NotConfigured, "Mobile verification isn't available right now.");
            var address = PhoneVerificationPolicy.AddressHash(key, ClientAddress(context, configuration));
            var hour = now.AddHours(-1);
            var facts = new PhoneSendFacts(
                await db.PhoneCodes.Where(c => c.AccountId == account.Id && c.Number == number).MaxAsync(c => (DateTimeOffset?)c.CreatedAt, cancellationToken),
                await db.PhoneCodes.CountAsync(c => c.AccountId == account.Id && c.CreatedAt > hour, cancellationToken),
                await db.PhoneCodes.CountAsync(c => c.Number == number && c.CreatedAt > hour, cancellationToken),
                await db.PhoneCodes.CountAsync(c => c.IpHash == address && c.CreatedAt > hour, cancellationToken),
                await db.MemberPhones.AnyAsync(p => p.AccountId == account.Id && p.Number == number, cancellationToken));
            switch (PhoneVerificationPolicy.CanSend(facts, now))
            {
                case PhoneSendRefusal.AlreadyVerified:
                    return Problem(StatusCodes.Status409Conflict, ProblemCodes.PhoneAlreadyVerified, "That number is already verified.");
                case PhoneSendRefusal.Cooldown:
                    return Problem(StatusCodes.Status429TooManyRequests, ProblemCodes.PhoneCodeCooldown, "Wait a minute before asking for another code.");
                case not PhoneSendRefusal.None:
                    Audit.Record(db, now, account.Id, "account", "phone.code-limited", "account", account.Id.ToString());
                    await db.SaveChangesAsync(cancellationToken);
                    return Problem(StatusCodes.Status429TooManyRequests, ProblemCodes.PhoneCodeLimit, "Too many codes. Try again in an hour.");
            }
            var code = PhoneVerificationPolicy.NewCode();
            var row = new PhoneCode
            {
                Id = Guid.NewGuid(), AccountId = account.Id, Number = number, CodeHash = PhoneVerificationPolicy.Hash(key, account.Id, number, code),
                IpHash = address, CreatedAt = now, ExpiresAt = now + PhoneVerificationPolicy.CodeLifetime,
            };
            db.PhoneCodes.Add(row);
            Audit.Record(db, now, account.Id, "account", "phone.code-sent", "phone-code", row.Id.ToString(), sender.Kind);
            await db.SaveChangesAsync(cancellationToken);
            if (!await sender.SendCodeAsync(number, code, cancellationToken))
                return Problem(StatusCodes.Status502BadGateway, ProblemCodes.PhoneSendFailed, "We couldn't send a code just now. Try again.");
            return Results.Ok(await ViewAsync(db, account.Id, sender, accounts.PhoneRequired, now, cancellationToken));
        });

        me.MapPost("/confirm", async (PhoneConfirm request, ClaimsPrincipal user, AccountResolver accounts, IVerificationMessageSender sender, PhoneKeys keys,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken, phoneExempt: true);
            if (denied is not null) return denied;
            var (account, db, now) = (current!.Account!, accounts.Db!, clock.GetUtcNow());
            if (PhoneVerificationPolicy.Normalise(request.Number) is not { } number)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidPhone, "Enter the number the code was sent to.");
            if (keys.Key is not { } key) return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.NotConfigured, "Mobile verification isn't available right now.");
            var latest = await db.PhoneCodes.Where(c => c.AccountId == account.Id && c.Number == number).OrderByDescending(c => c.CreatedAt).FirstOrDefaultAsync(cancellationToken);
            var result = PhoneVerificationPolicy.Check(latest, key, account.Id, number, request.Code, now);
            if (result != PhoneCheckResult.Verified)
            {
                Audit.Record(db, now, account.Id, "account", "phone.code-refused", "account", account.Id.ToString(), result.ToString());
                await db.SaveChangesAsync(cancellationToken);
                return result switch
                {
                    PhoneCheckResult.Expired or PhoneCheckResult.NoCode => Problem(StatusCodes.Status400BadRequest, ProblemCodes.PhoneCodeExpired, "That code has expired. Ask for a new one."),
                    PhoneCheckResult.TooManyAttempts => Problem(StatusCodes.Status429TooManyRequests, ProblemCodes.TooManyAttempts, "Too many wrong codes. Ask for a new one."),
                    _ => Problem(StatusCodes.Status400BadRequest, ProblemCodes.PhoneCodeWrong, "That code isn't right. Check it and try again."),
                };
            }
            // D181: one member account per verified number; the answer doesn't say whose.
            if (await db.MemberPhones.AnyAsync(p => p.Number == number && p.AccountId != account.Id, cancellationToken))
            {
                Audit.Record(db, now, account.Id, "account", "phone.in-use", "account", account.Id.ToString());
                await db.SaveChangesAsync(cancellationToken);
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.PhoneInUse, "That number can't be used. Try another.");
            }
            var phone = await db.MemberPhones.FindAsync([account.Id], cancellationToken);
            // D184: verifying a new number replaces (and releases) the old one; the log says it changed, never which number.
            var change = phone is null ? "first" : "changed";
            if (phone is null) db.MemberPhones.Add(phone = new MemberPhone { AccountId = account.Id });
            (phone.Number, phone.VerifiedAt) = (number, now);
            Audit.Record(db, now, account.Id, "account", "phone.verified", "account", account.Id.ToString(), $"{change}:{sender.Kind}");
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException) { return Problem(StatusCodes.Status409Conflict, ProblemCodes.PhoneInUse, "That number can't be used. Try another."); }
            return Results.Ok(await ViewAsync(db, account.Id, sender, accounts.PhoneRequired, now, cancellationToken));
        });
    }

    private static async Task<PhoneView> ViewAsync(SwoovrDbContext db, Guid accountId, IVerificationMessageSender sender, bool required, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var phone = await db.MemberPhones.FindAsync([accountId], cancellationToken);
        var open = await db.PhoneCodes.Where(c => c.AccountId == accountId && c.UsedAt == null && c.ExpiresAt > now && c.FailedAttempts < PhoneVerificationPolicy.MaxWrongAttempts)
            .OrderByDescending(c => c.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        if (open is not null && phone?.Number == open.Number) open = null;
        return new(phone is null ? null : PhoneVerificationPolicy.Masked(phone.Number), phone is not null, phone?.VerifiedAt,
            open is null ? null : PhoneVerificationPolicy.Masked(open.Number), open?.ExpiresAt, sender.Kind, required && phone is null);
    }

    /// <summary>
    /// The member's address for the per-address limit. The web host's backend-for-frontend forwards the browser's
    /// address in X-Forwarded-For; it is believed only from a proxy in `Phone:TrustedProxies` (default loopback).
    /// </summary>
    public static string? ClientAddress(HttpContext context, IConfiguration configuration)
    {
        var remote = context.Connection.RemoteIpAddress;
        var trusted = configuration.GetSection("Phone:TrustedProxies").Get<string[]>() ?? ["127.0.0.1", "::1"];
        if (remote is not null && trusted.Any(t => IPAddress.TryParse(t, out var ip) && ip.Equals(remote.IsIPv4MappedToIPv6 ? remote.MapToIPv4() : remote))
            && context.Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim() is { Length: > 0 } forwarded && IPAddress.TryParse(forwarded, out var client))
            return client.ToString();
        return remote?.ToString();
    }
}

/// <summary>
/// The key for code and address hashes: `Phone:CodeKey`, or else derived from the current founder secret with its own
/// label so the uses never share a key. Null when neither exists (verification is then unavailable).
/// </summary>
public sealed class PhoneKeys(IConfiguration configuration, Founders founders)
{
    public byte[]? Key => configuration["Phone:CodeKey"] is { Length: > 0 } configured ? Encoding.UTF8.GetBytes(configured)
        : founders.CurrentVersion is { } version ? HMACSHA256.HashData(founders.Secrets[version], Encoding.UTF8.GetBytes("swoovr:phone-code:v1"))
        : null;
}
