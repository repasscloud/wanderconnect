using System.Buffers.Binary;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Retention;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Safety;

/// <summary>Second step for external read-only resources; never authenticates a Swoovr member.</summary>
public sealed class MeetingSafetyContactVerification(SwoovrDbContext db, MeetingSafetyStore store,
    ContactHistoryKeys keys, IEmailSender email, IVerificationMessageSender sms, TimeProvider clock, IHostEnvironment environment)
{
    private sealed record Scope(MeetingSafetyLink Link, MeetingSafetyRecipient Recipient, TrustedContact Contact, MeetingSafetySession Session);

    private async Task<Scope?> ResolveAsync(string raw, CancellationToken token)
    {
        if (!MeetingSafetyPolicy.TokenShape(raw)) return null;
        var hash = MeetingSafetyPolicy.TokenHash(raw);
        var now = clock.GetUtcNow();
        var row = await (from link in db.MeetingSafetyLinks
            join recipient in db.MeetingSafetyRecipients on link.RecipientId equals recipient.Id
            join contact in db.TrustedContacts on recipient.TrustedContactId equals contact.Id
            join session in db.MeetingSafetySessions on recipient.SessionId equals session.Id
            where link.TokenHash == hash && link.ExpiresAt > now && link.RevokedAt == null && recipient.RevokedAt == null
                && contact.Consent == TrustedContactConsent.Confirmed && contact.ConfirmedAt != null
                && contact.MemberId == session.MemberId && session.Status != MeetingSafetyStatus.Revoked
            select new Scope(link, recipient, contact, session)).AsNoTracking().SingleOrDefaultAsync(token);
        return row != null && await store.LiveAsync(row.Session.MemberId, token) ? row : null;
    }

    public async Task<MeetingSafetyContactAccessScope> ScopeAsync(string link, CancellationToken token)
    {
        var initial = await ResolveAsync(link, token);
        if (initial == null) return new(false);
        await using var owner = await AccountLifecycleLease.AcquireAsync(db, initial.Session.MemberId, token);
        await using var preservation = await PreservationLease.SharedAsync(db, token);
        return new(await ResolveAsync(link, token) != null);
    }

    public async Task<MeetingSafetyContactCodeReceipt> RequestCodeAsync(string link, CancellationToken token)
    {
        var initial = await ResolveAsync(link, token);
        if (initial == null || keys.Lookup is not { Length: 32 } lookup) return new("unavailable");
        await using var owner = await AccountLifecycleLease.AcquireAsync(db, initial.Session.MemberId, token);
        await using var preservation = await PreservationLease.SharedAsync(db, token);
        var scope = await ResolveAsync(link, token);
        if (scope == null || scope.Contact.DestinationLookupHash.Length < 4) return new("unavailable");
        if (scope.Contact.Channel == "sms" && !environment.IsDevelopment() && sms.Kind != "twilio") return new("unavailable");
        var destination = store.Destination(scope.Contact);
        var destinationHash = MeetingSafetyContactAccessPolicy.DestinationHash(lookup, scope.Contact.Channel, destination);
        string code;
        await using (var transaction = await db.Database.BeginTransactionAsync(token))
        {
            // Same destination mutex and lock ordering as contact invitation writers, across all members/replicas.
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({0x4D534346}, {BinaryPrimitives.ReadInt32BigEndian(scope.Contact.DestinationLookupHash)})", token);
            scope = await ResolveAsync(link, token);
            if (scope == null) return new("unavailable");
            var now = clock.GetUtcNow();
            var challenges = db.Set<MeetingSafetyContactChallenge>();
            if (await challenges.AnyAsync(c => c.RecipientId == scope.Recipient.Id && c.CreatedAt > now.AddSeconds(-60), token)
                || await challenges.CountAsync(c => c.DestinationHash == destinationHash && c.CreatedAt > now.AddHours(-1), token) >= 3
                || await challenges.CountAsync(c => c.DestinationHash == destinationHash && c.CreatedAt > now.AddDays(-1), token) >= 10)
                return new("try-later");
            await challenges.Where(c => c.RecipientId == scope.Recipient.Id && c.UsedAt == null && c.SupersededAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(c => c.SupersededAt, now), token);
            code = MeetingSafetyContactAccessPolicy.Code();
            var challenge = new MeetingSafetyContactChallenge { Id = Guid.NewGuid(), RecipientId = scope.Recipient.Id,
                DestinationHash = destinationHash, CreatedAt = now, ExpiresAt = now.AddMinutes(5) };
            challenge.CodeHash = MeetingSafetyContactAccessPolicy.CodeHash(lookup, challenge.RecipientId, challenge.Id, code);
            challenges.Add(challenge);
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        }
        // Persist before external effect; there is deliberately no automatic retry after uncertain acceptance.
        var body = $"Your Swoovr meeting safety access code is {code}. It expires in 5 minutes. Do not share this code. If you did not request it, ignore this message.";
        try
        {
            if (scope.Contact.Channel == "email")
            {
                var result = await email.SendOutcomeAsync(new(destination, "Swoovr meeting safety access code", body), token);
                return new(result.Status == "accepted" ? "requested" : "acceptance-unconfirmed");
            }
            var accepted = await sms.SendNoticeAsync(destination, body, token);
            return new(accepted && sms.Kind != "simulated" ? "requested" : "acceptance-unconfirmed");
        }
        catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException)
        { return new("acceptance-unconfirmed"); }
    }

    public async Task<MeetingSafetyContactGrantReceipt> VerifyAsync(string link, string? code, CancellationToken token)
    {
        var initial = await ResolveAsync(link, token);
        if (initial == null || keys.Lookup is not { Length: 32 } lookup) return new(false);
        await using var owner = await AccountLifecycleLease.AcquireAsync(db, initial.Session.MemberId, token);
        await using var preservation = await PreservationLease.SharedAsync(db, token);
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        var scope = await ResolveAsync(link, token);
        if (scope == null) return new(false);
        var challenge = await db.Set<MeetingSafetyContactChallenge>().Where(c => c.RecipientId == scope.Recipient.Id && c.SupersededAt == null)
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).FirstOrDefaultAsync(token);
        var now = clock.GetUtcNow();
        if (challenge == null) return new(false);
        if (!MeetingSafetyContactAccessPolicy.Check(challenge, lookup, code, now))
        { await db.SaveChangesAsync(token); await transaction.CommitAsync(token); return new(false); }
        var raw = MeetingSafetyPolicy.Token();
        var expiry = now.AddHours(4);
        if (scope.Link.ExpiresAt < expiry) expiry = scope.Link.ExpiresAt;
        db.Set<MeetingSafetyContactReadGrant>().Add(new() { Id = Guid.NewGuid(), RecipientId = scope.Recipient.Id,
            TokenHash = MeetingSafetyPolicy.TokenHash(raw), CreatedAt = now, ExpiresAt = expiry });
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        return new(true, raw, expiry);
    }

    public async Task<(bool Available, SharedMeetingSafetyView? View)> ReadAsync(string link,string? grant,CancellationToken token)
    {
        var initial=await ResolveAsync(link,token);if(initial is null)return(false,null);
        await using var owner=await AccountLifecycleLease.AcquireAsync(db,initial.Session.MemberId,token);
        await using var preservation=await PreservationLease.SharedAsync(db,token);
        if(await ResolveAsync(link,token) is null)return(false,null);
        return(true,await AllowedAsync(link,grant,token)?await store.SharedAsync(link,token):null);
    }

    public async Task<bool> AllowedAsync(string link, string? grant, CancellationToken token)
    {
        if (!MeetingSafetyPolicy.TokenShape(grant)) return false;
        var initial = await ResolveAsync(link, token);
        if (initial == null) return false;
        await using var owner = await AccountLifecycleLease.AcquireAsync(db, initial.Session.MemberId, token);
        await using var preservation = await PreservationLease.SharedAsync(db, token);
        var scope = await ResolveAsync(link, token);
        if (scope == null) return false;
        var hash = MeetingSafetyPolicy.TokenHash(grant!);
        var now = clock.GetUtcNow();
        return await db.Set<MeetingSafetyContactReadGrant>().AnyAsync(g => g.RecipientId == scope.Recipient.Id && g.TokenHash == hash && g.ExpiresAt > now, token);
    }
}
