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
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (EmailAddress.Normalise(email) is not { } normalised) return (AddResult.InvalidEmail, null);
        var live = await db.BetaEnrolments.SingleOrDefaultAsync(e => e.Email == normalised && e.RevokedAt == null, cancellationToken);
        if (live is not null) return (AddResult.AlreadyEnrolled, live);
        var enrolment = new BetaEnrolment { Id = Guid.NewGuid(), Email = normalised, CreatedAt = now, CreatedBy = actor };
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

    public static BetaEnrolmentView View(BetaEnrolment e) => new(e.Id, e.Email, e.CreatedAt,
        e.RevokedAt is not null ? "revoked" : e.ActivatedAt is not null ? "activated" : "pending", e.ActivatedAt);
}
