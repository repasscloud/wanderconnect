using Microsoft.EntityFrameworkCore;
using Npgsql;
using Swoovr.Api.Accounts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

static class RegistrationAvailabilityPersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> sourceOptions,Action<string,bool> check)
    {
        await using var source=new SwoovrDbContext(sourceOptions);
        var original=source.Database.GetDbConnection().ConnectionString;
        await using var admin=new NpgsqlConnection(original);await admin.OpenAsync();
        var name="swoovr_registration_"+Guid.NewGuid().ToString("N");
        await using(var create=new NpgsqlCommand("CREATE DATABASE "+name,admin))await create.ExecuteNonQueryAsync();
        try
        {
            var options=new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(original){Database=name,Pooling=false}.ConnectionString).Options;
            await using var db=new SwoovrDbContext(options);await db.Database.MigrateAsync();
            check("D295 migration starts UAT beta-only",(await RegistrationAuthority.ReadAsync(db,default))?.Policy is{BetaEnabled:true,PublicEnabled:false,Revision:0});
            const string invited="fictional.invited@example.test";
            db.BetaEnrolments.Add(new(){Id=Guid.NewGuid(),Email=invited,CreatedAt=DateTimeOffset.UtcNow});await db.SaveChangesAsync();
            check("provider admission ignores whitespace/case only",await RegistrationAuthority.InvitedAsync(db," Fictional.Invited@EXAMPLE.TEST ",default));
            foreach(var beta in new[]{false,true})foreach(var publicOpen in new[]{false,true})
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE registration_availability SET beta_enabled={beta},public_enabled={publicOpen},revision=revision+1 WHERE id=1");
                async Task<bool> Adoption(string? email)
                {
                    await using var transaction=await db.Database.BeginTransactionAsync();
                    try{await RegistrationAuthority.RequireAdoptionAsync(db,email,default);return true;}
                    catch(IdentifierCheckException error)when(error.Code=="registration-closed"){return false;}
                }
                check("new invited adoption follows four-state policy",await Adoption(invited)==(beta||publicOpen));
                check("new uninvited adoption follows public availability",await Adoption("fictional.other@example.test")==publicOpen);
                check("unverified email cannot consume beta invitation",await Adoption(null)==publicOpen);
            }
            await db.BetaEnrolments.ExecuteUpdateAsync(set=>set.SetProperty(e=>e.RevokedAt,(DateTimeOffset?)DateTimeOffset.UtcNow));
            check("revoked invitation cannot authorize provider creation",!await RegistrationAuthority.InvitedAsync(db,invited,default));
            await db.Database.ExecuteSqlRawAsync("DELETE FROM registration_availability");
            check("missing singleton never recreates an open default",await RegistrationAuthority.ReadAsync(db,default)is null);
            await using var tx=await db.Database.BeginTransactionAsync();
            bool refused=false;try{await RegistrationAuthority.RequireAdoptionAsync(db,invited,default);}catch(IdentifierCheckException){refused=true;}
            check("missing authority denies adoption even for historical invite",refused);
        }
        finally
        {
            await using var drop=new NpgsqlCommand("DROP DATABASE "+name+" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();
        }
    }
}
