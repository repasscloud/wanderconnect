using Microsoft.EntityFrameworkCore;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Notifications;

public static class PushDeviceMaintenance
{
    /// <summary>Upgrade pre-provider prototype registrations without logging tokens. A transaction protects each batch.</summary>
    public static async Task<int> ProtectLegacyAsync(SwoovrDbContext db, PushKeys keys, CancellationToken cancellationToken)
    {
        if (keys.Protector is not { } protector) return 0;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var rows = await db.PushDevices.FromSqlRaw("SELECT * FROM push_devices WHERE token_cipher IS NULL AND invalidated_at IS NULL FOR UPDATE").ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            var hash = protector.Hash(row.Platform, row.Token);
            if (await db.PushDevices.AnyAsync(d => d.Id != row.Id && d.AccountId == row.AccountId && d.Token == hash, cancellationToken))
            { db.PushDevices.Remove(row); continue; }
            row.TokenCipher = protector.Protect(row.Id, row.AccountId, row.Platform, row.Token);
            row.TokenKeyVersion = protector.CurrentVersion;
            row.Token = hash;
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return rows.Count;
    }
}
