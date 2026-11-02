using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Accounts;

public static class ContactChangeExport
{
    public static async Task<IReadOnlyList<object>> OwnAsync(SwoovrDbContext db,ContactHistoryKeys keys,Guid account,CancellationToken token)
    {
        var rows=await db.ContactHistories.AsNoTracking().Where(h=>h.AccountId==account).OrderBy(h=>h.ValidUntil).ToListAsync(token);
        return rows.Select(row=>(object)new{row.Id,Type=row.ContactType.ToString(),row.ValidUntil,
            EvidenceAvailable=keys.Encryption.ContainsKey(row.EncryptedValue[0]),
            Evidence=keys.Encryption.ContainsKey(row.EncryptedValue[0])?ContactHistoryPolicy.Evidence(keys.Encryption,row.EncryptedValue):null}).ToList();
    }
}
