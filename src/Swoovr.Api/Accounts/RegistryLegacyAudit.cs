using Microsoft.EntityFrameworkCore;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Accounts;

/// <summary>Unversioned historical review markers cannot establish a new blocking restriction's key/case/person.</summary>
public sealed record RegistryLegacyInventory(int Emails,int Mobiles,int Expired,int Malformed)
{
    public bool RequiresReview=>Emails+Mobiles>0;
}
public static class RegistryLegacyAudit
{
    public static async Task<RegistryLegacyInventory> ReadAsync(SwoovrDbContext db,DateTimeOffset now,CancellationToken token)
    {
        await using var authority=await Retention.PreservationLease.ExclusiveAsync(db,token);
        return new(await db.TerminatedEmailHolds.CountAsync(token),await db.TerminatedPhoneHolds.CountAsync(token),
            await db.TerminatedEmailHolds.CountAsync(h=>h.ExpiresAt<=now,token)+await db.TerminatedPhoneHolds.CountAsync(h=>h.ExpiresAt<=now,token),
            await db.TerminatedEmailHolds.CountAsync(h=>h.EmailHash.Length!=32||h.ExpiresAt<=h.CreatedAt,token)
                +await db.TerminatedPhoneHolds.CountAsync(h=>h.PhoneHash.Length!=32||h.ExpiresAt<=h.CreatedAt,token));
    }
}
