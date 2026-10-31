using Microsoft.EntityFrameworkCore;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Accounts;

/// <summary>
/// Adding, revoking and listing private-beta enrolments (D62), shared by the admin routes and
/// the `beta` operator command. Audit entries name the enrolment id, never the address.
/// </summary>
public static class BetaEnrolments
{
    public enum AddResult { Added, AlreadyEnrolled, InvalidEmail }

    public static async Task<(AddResult Result, BetaEnrolment? Enrolment)> AddAsync(SwoovrDbContext db, string? email, Guid? actor,
        DateTimeOffset now, CancellationToken cancellationToken,bool queueInvitation=false)
    {
        if (EmailAddress.Normalise(email) is not { } normalised) return (AddResult.InvalidEmail, null);
        var live = await db.BetaEnrolments.SingleOrDefaultAsync(e => e.Email == normalised && e.RevokedAt == null, cancellationToken);
        if (live is not null) return (AddResult.AlreadyEnrolled, live);
        var enrolment = new BetaEnrolment { Id = Guid.NewGuid(), Email = normalised, CreatedAt = now, CreatedBy = actor,InvitationState=queueInvitation?"pending":"not-queued",InvitationQueuedAt=queueInvitation?now:null,InvitationNextAttemptAt=queueInvitation?now:null };
        db.BetaEnrolments.Add(enrolment);
        Audit.Record(db, now, actor, actor is null ? "cli" : "account", "beta.added", "beta-enrolment", enrolment.Id.ToString());
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            // Added concurrently; the unique live-address index kept one.
            db.ChangeTracker.Clear();
            return (AddResult.AlreadyEnrolled, await db.BetaEnrolments.SingleAsync(e => e.Email == normalised && e.RevokedAt == null, cancellationToken));
        }
        return (AddResult.Added, enrolment);
    }

    /// <summary>Revokes a live enrolment. Returns false when there is nothing live to revoke.</summary>
    public static async Task<bool> RevokeAsync(SwoovrDbContext db, BetaEnrolment? enrolment, Guid? actor, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (enrolment is not { RevokedAt: null }) return false;
        enrolment.RevokedAt = now;
        Audit.Record(db, now, actor, actor is null ? "cli" : "account", "beta.revoked", "beta-enrolment", enrolment.Id.ToString());
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public enum IosUpdateResult { Updated, Unchanged, Invalid, NotFound, Conflict }
    public static async Task<(IosUpdateResult Result,BetaEnrolment? Row)> RecordIosAsync(SwoovrDbContext db,Guid id,IosDistributionUpdate update,Guid actor,DateTimeOffset now,CancellationToken token)
    {
        if(!update.Confirm||update.ExpectedRevision<0||update.ExpectedRevision==long.MaxValue
            ||update.State is not("not-recorded" or "not-invited" or "invited" or "accepted" or "removed"))return(IosUpdateResult.Invalid,null);
        await using var barrier=await Swoovr.Api.Retention.PreservationLease.SharedAsync(db,token);
        await using var transaction=await db.Database.BeginTransactionAsync(token);
        var current=await db.BetaEnrolments.AsNoTracking().SingleOrDefaultAsync(e=>e.Id==id,token);
        if(current is null)return(IosUpdateResult.NotFound,null);
        if(current.IosDistributionRevision!=update.ExpectedRevision)return(IosUpdateResult.Conflict,null);
        if(current.IosDistributionState==update.State){await transaction.CommitAsync(token);return(IosUpdateResult.Unchanged,current);}
        var changed=await db.BetaEnrolments.Where(e=>e.Id==id&&e.IosDistributionRevision==update.ExpectedRevision).ExecuteUpdateAsync(set=>set
            .SetProperty(e=>e.IosDistributionState,update.State).SetProperty(e=>e.IosDistributionRevision,update.ExpectedRevision+1)
            .SetProperty(e=>e.IosDistributionChangedAt,now).SetProperty(e=>e.IosDistributionChangedBy,actor),token);
        if(changed!=1)return(IosUpdateResult.Conflict,null);
        Audit.Record(db,now,actor,"account","beta.ios-recorded","beta-enrolment",id.ToString(),update.State);
        await db.SaveChangesAsync(token);await transaction.CommitAsync(token);
        current.IosDistributionState=update.State;current.IosDistributionRevision=update.ExpectedRevision+1;
        current.IosDistributionChangedAt=now;current.IosDistributionChangedBy=actor;
        return(IosUpdateResult.Updated,current);
    }

    public static BetaEnrolmentView View(BetaEnrolment e) => new(e.Id, e.Email, e.CreatedAt,
        e.RevokedAt is not null ? "revoked" : e.ActivatedAt is not null ? "activated" : "pending", e.ActivatedAt,e.IosDistributionState,e.IosDistributionRevision,e.IosDistributionChangedAt,e.IosDistributionRevision>0?"staff-recorded-not-provider-verified":"not-recorded",e.InvitationState,e.InvitationAttempts,e.InvitationQueuedAt,e.InvitationCompletedAt);
}
