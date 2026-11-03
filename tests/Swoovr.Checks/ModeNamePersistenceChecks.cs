using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Profiles;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

// Q118: real PostgreSQL in the existing owned database, with fictional identities only.
static class ModeNamePersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options, Action<string, bool> check)
    {
        await using var db = new SwoovrDbContext(options);
        var now = DateTimeOffset.UtcNow;
        var owner = new Account { Id = Guid.NewGuid(), Realm = AccountRealm.Members, Issuer = "https://issuer.example.test", Subject = Guid.NewGuid().ToString(), Status = AccountStatus.Active, CreatedAt = now, LastSeenAt = now };
        var viewer = new Account { Id = Guid.NewGuid(), Realm = AccountRealm.Members, Issuer = owner.Issuer, Subject = Guid.NewGuid().ToString(), Status = AccountStatus.Active, CreatedAt = now, LastSeenAt = now };
        db.Accounts.AddRange(owner, viewer);
        db.MemberProfiles.AddRange(new MemberProfile { AccountId = owner.Id, PublicId = Guid.NewGuid(), DisplayName = "Public Main", AgeStatus = AgeStatus.Attested },
            new MemberProfile { AccountId = viewer.Id, PublicId = Guid.NewGuid(), DisplayName = "Public Viewer", AgeStatus = AgeStatus.Attested });
        foreach (var mode in ConnectionModeIds.All) db.MemberModes.Add(new MemberMode { AccountId = owner.Id, ModeId = mode, Enabled = true, Visible = true, DisplayName = "Alias " + mode, UpdatedAt = now });
        var (low, high) = ConnectionRequest.Pair(owner.Id, viewer.Id);
        db.Conversations.Add(new Conversation { Id = Guid.NewGuid(), PairLow = low, PairHigh = high, CreatedAt = now, OriginModeId = "friends" });
        await db.SaveChangesAsync();
        await ProfilePrivacy.CaptureAsync(db, owner.Id, now, default);
        var before = await db.ProfileVersions.Where(v => v.AccountId == owner.Id && v.ModeId == "dating").SingleAsync();
        var original = before.ContentJson; var originalHash = Convert.ToHexString(before.Sha256);
        var dating = await db.MemberModes.SingleAsync(m => m.AccountId == owner.Id && m.ModeId == "dating");
        dating.DisplayName = null; await db.SaveChangesAsync();
        await ProfilePrivacy.CaptureAsync(db, owner.Id, now.AddSeconds(1), default);
        check("Q118 changed name appends snapshot without rewriting report history", await db.ProfileVersions.CountAsync(v => v.AccountId == owner.Id && v.ModeId == "dating") == 2
            && before.ContentJson == original && Convert.ToHexString(before.Sha256) == originalHash && original.Contains("Alias dating"));
        check("Q118 only selected mode changes", await db.MemberModes.CountAsync(m => m.AccountId == owner.Id && m.DisplayName != null) == 3);
        var ownerExport = JsonSerializer.Serialize(await DataExportBuilder.BuildAsync(db, owner, now, default));
        check("Q118 export contains own mode aliases", ConnectionModeIds.All.Where(m => m != "dating").All(m => ownerExport.Contains("Alias " + m)));
        var viewerExport = JsonSerializer.Serialize(await DataExportBuilder.BuildAsync(db, viewer, now, default));
        check("Q118 export uses conversation origin alias without other-mode leakage", viewerExport.Contains("Alias friends") && !viewerExport.Contains("Alias business") && !viewerExport.Contains("Alias activities"));
        db.Accounts.Remove(owner); await db.SaveChangesAsync();
        check("Q118 account cascade removes mode names", !await db.MemberModes.AnyAsync(m => m.AccountId == owner.Id));
        db.Accounts.Remove(viewer); await db.SaveChangesAsync();
    }
}
