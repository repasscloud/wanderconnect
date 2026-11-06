using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Accounts;

public static class RegistrationAuthority
{
    public sealed class Row
    {
        public bool BetaEnabled { get; set; }
        public bool PublicEnabled { get; set; }
        public long Revision { get; set; }
        public RegistrationAvailability Policy=>new(BetaEnabled,PublicEnabled,Revision);
    }
    public static Task<Row?> ReadAsync(SwoovrDbContext db,CancellationToken token,bool lockForAdoption=false)=>
        (lockForAdoption?db.Database.SqlQueryRaw<Row>("SELECT beta_enabled AS \"BetaEnabled\",public_enabled AS \"PublicEnabled\",revision AS \"Revision\" FROM registration_availability WHERE id=1 FOR SHARE"):
        db.Database.SqlQueryRaw<Row>("SELECT beta_enabled AS \"BetaEnabled\",public_enabled AS \"PublicEnabled\",revision AS \"Revision\" FROM registration_availability WHERE id=1")).SingleOrDefaultAsync(token);

    public static async Task<bool> InvitedAsync(SwoovrDbContext db,string? email,CancellationToken token)
    {
        var normal=EmailAddress.Normalise(email);
        return normal is not null&&await db.BetaEnrolments.AnyAsync(e=>e.Email==normal&&e.RevokedAt==null&&e.AccountId==null,token);
    }
    public static async Task RequireAdoptionAsync(SwoovrDbContext db,string? verifiedEmail,CancellationToken token)
    {
        var row=await ReadAsync(db,token,lockForAdoption:true);
        // D335: invite-only wording only while beta registration is the open route; closed or unreadable stays neutral.
        if(!RegistrationAvailability.Allows(row?.Policy,await InvitedAsync(db,verifiedEmail,token)))
            throw new IdentifierCheckException(403,"registration-closed",row is{BetaEnabled:true,PublicEnabled:false}?"Swoovr is currently invite-only":"Registration is closed");
    }
}
