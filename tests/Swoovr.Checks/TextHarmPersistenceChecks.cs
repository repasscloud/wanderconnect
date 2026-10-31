using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Swoovr.Api.Accounts;
using Swoovr.Api.Evidence;
using Swoovr.Api.Moderation;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;

static class TextHarmPersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options,Action<string,bool> check)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Evidence:Keys:1"]=Convert.ToBase64String(Enumerable.Repeat((byte)51,32).ToArray())}).Build();
        var services=new ServiceCollection();services.AddLogging();services.AddSingleton<IConfiguration>(config);services.AddSingleton<IEmailSender>(new Sink());services.AddScoped<Founders>();services.AddScoped<EvidenceKeys>();
        await using var provider=services.BuildServiceProvider();await using var scope=provider.CreateAsyncScope();
        var keys=scope.ServiceProvider.GetRequiredService<EvidenceKeys>();
        var id=Guid.NewGuid();var now=DateTimeOffset.UtcNow;
        await using(var db=new SwoovrDbContext(options))
        {
            db.Accounts.Add(new(){Id=id,Issuer="https://issuer.example.test",Subject="fictional-text-"+id,Realm=AccountRealm.Members,Status=AccountStatus.Active,CreatedAt=now,LastSeenAt=now});
            db.MemberProfiles.Add(new(){AccountId=id,PublicId=Guid.NewGuid(),AgeStatus=AgeStatus.Attested,DisplayName="Fictional text review"});await db.SaveChangesAsync();
            var off=new TextHarmQueue(db,keys,new());off.Enqueue(id,TextHarmSource.ProfileAbout,id,null,"I will kill you",now);
            check("disabled detector queues no private content",!db.ChangeTracker.Entries<TextHarmIntent>().Any());
            var queue=new TextHarmQueue(db,keys,new(true));
            await using(var transaction=await db.Database.BeginTransactionAsync())
            {
                var profile=await db.MemberProfiles.SingleAsync(p=>p.AccountId==id);
                profile.About="Fictional atomic payment text";profile.TextRevision=queue.Enqueue(id,TextHarmSource.ProfileAbout,id,null,profile.About,now);
                await db.SaveChangesAsync();await transaction.RollbackAsync();
            }
        }
        await using(var db=new SwoovrDbContext(options))
        {
            check("rolled-back source has no inspection intent",!await db.Set<TextHarmIntent>().AnyAsync(i=>i.AccountId==id)&&await db.MemberProfiles.Where(p=>p.AccountId==id).Select(p=>p.About).SingleAsync()==null);
            var queue=new TextHarmQueue(db,keys,new(true));var profile=await db.MemberProfiles.SingleAsync(p=>p.AccountId==id);
            profile.About="Send me gift cards for dinner";profile.TextRevision=queue.Enqueue(id,TextHarmSource.ProfileAbout,id,null,profile.About,now);await db.SaveChangesAsync();
            var intent=await db.Set<TextHarmIntent>().SingleAsync(i=>i.AccountId==id);
            check("committed source and exact revision share durable intent",intent.Revision==profile.TextRevision&&intent.ProcessedAt==null);
            check("inspection text is encrypted and exact",intent.SealedText!=null&&JsonSerializer.Deserialize<string>(EvidenceSeal.Open(keys.Encryption,intent.SealedText))==profile.About);
            check("enqueue alone creates no automatic sanction or notice",!await db.Sanctions.AnyAsync(s=>s.AccountId==id)&&!await db.MemberNotices.AnyAsync(n=>n.AccountId==id));
        }
    }
    private sealed class Sink:IEmailSender{public Task<bool> SendAsync(EmailMessage message,CancellationToken cancellationToken=default)=>Task.FromResult(true);}
}
