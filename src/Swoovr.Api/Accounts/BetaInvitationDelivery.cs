using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Accounts;

/// <summary>Durable single-recipient invitation effects. A persisted dispatch claim is never blindly replayed.</summary>
public sealed class BetaInvitationDelivery(SwoovrDbContext db,IEmailSender email,IConfiguration config,IHostEnvironment environment,TimeProvider clock)
{
    public static Uri? PublicOrigin(IConfiguration config,bool development)
    {
        if(!config.GetValue("Beta:Invitations:Enabled",false))return null;
        if(!Uri.TryCreate(config["Beta:Invitations:PublicBaseUrl"],UriKind.Absolute,out var uri)
            ||uri.Scheme!="https"&&!(development&&uri.Scheme=="http"&&uri.IsLoopback)
            ||uri.UserInfo.Length!=0||uri.Query.Length!=0||uri.Fragment.Length!=0||uri.AbsolutePath!="/")
            throw new InvalidOperationException("Enabled beta invitations require a public web origin, HTTPS outside Development.");
        return uri;
    }
    public async Task<int> RunAsync(CancellationToken token)
    {
        var origin=PublicOrigin(config,environment.IsDevelopment());if(origin is null)return 0;
        var now=clock.GetUtcNow();var processed=0;
        await using(var reconciliation=await PreservationLease.SharedAsync(db,token))
        {
        // This also locks against an in-flight bounded send before reconciling an old claim.
        await db.BetaEnrolments.Where(e=>e.InvitationState=="dispatching"&&e.InvitationAttemptedAt<now.AddMinutes(-2))
            .ExecuteUpdateAsync(s=>s.SetProperty(e=>e.InvitationState,"unknown").SetProperty(e=>e.InvitationCompletedAt,now).SetProperty(e=>e.InvitationOutcomeCode,"interrupted-dispatch"),token);
        }
        var due=await db.BetaEnrolments.AsNoTracking().Where(e=>e.InvitationState=="pending"&&e.InvitationNextAttemptAt<=now)
            .OrderBy(e=>e.InvitationNextAttemptAt).Take(100).Select(e=>e.Id).ToListAsync(token);
        foreach(var id in due)
        {
            token.ThrowIfCancellationRequested();now=clock.GetUtcNow();
            await using var barrier=await PreservationLease.SharedAsync(db,token);
            await using(var claim=await db.Database.BeginTransactionAsync(token))
            {
                var row=await db.BetaEnrolments.FromSqlInterpolated($"SELECT * FROM beta_enrolments WHERE id={id} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(token);
                if(row is null||row.InvitationState!="pending"||row.InvitationNextAttemptAt>now)continue;
                if(row.RevokedAt!=null||row.ActivatedAt!=null||row.InvitationQueuedAt<=now.AddHours(-72))
                {
                    var expired=row.RevokedAt==null&&row.ActivatedAt==null;
                    await Finish(id,expired?"expired":"cancelled",expired?"queue-expired":row.RevokedAt!=null?"revoked":"activated",now,token);
                    await claim.CommitAsync(token);processed++;continue;
                }
                await db.BetaEnrolments.Where(e=>e.Id==id).ExecuteUpdateAsync(s=>s.SetProperty(e=>e.InvitationState,"dispatching")
                    .SetProperty(e=>e.InvitationAttemptedAt,now).SetProperty(e=>e.InvitationAttempts,e=>e.InvitationAttempts+1)
                    .SetProperty(e=>e.InvitationNextAttemptAt,(DateTimeOffset?)null),token);
                await claim.CommitAsync(token);
            }
            // Persist claim first. Crash/cancellation after this point is unknown, not safe-to-repeat.
            await using var effect=await db.Database.BeginTransactionAsync(token);
            var current=await db.BetaEnrolments.FromSqlInterpolated($"SELECT * FROM beta_enrolments WHERE id={id} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(token);
            if(current is null||current.InvitationState!="dispatching")continue;
            now=clock.GetUtcNow();
            if(current.RevokedAt!=null||current.ActivatedAt!=null||current.InvitationQueuedAt<=now.AddHours(-72))
                await Finish(id,current.RevokedAt==null&&current.ActivatedAt==null?"expired":"cancelled",current.RevokedAt!=null?"revoked":current.ActivatedAt!=null?"activated":"queue-expired",now,token);
            else
            {
                var message=new EmailMessage(current.Email,"You're invited to the Swoovr private beta",
                    "Staff have added this address to the Swoovr private beta. Register or sign in with this verified email at "+new Uri(origin,"account").AbsoluteUri+". You must meet the account and onboarding requirements before connecting with members. If you did not expect it, you can ignore it or contact support@swoovr.com.");
                var outcome=await email.SendOutcomeAsync(message,token);now=clock.GetUtcNow();
                // Provider codes are not persisted verbatim; only the bounded outcome vocabulary is trusted.
                var state=outcome.Status switch{"accepted"=>"accepted","rejected"=>"rejected","retry" when current.InvitationAttempts<3&&current.InvitationQueuedAt>now.AddHours(-72)=>"pending","retry"=>"rejected",_=>"unknown"};
                var code=state=="pending"?"known-no-effect-retry":outcome.Status=="retry"?"attempts-exhausted":state=="accepted"?"provider-accepted":state=="rejected"?"provider-refused":"unconfirmed";
                await Finish(id,state,code,now,token);
            }
            await effect.CommitAsync(token);processed++;
        }
        return processed;
    }
    private Task<int> Finish(Guid id,string state,string code,DateTimeOffset now,CancellationToken token)=>db.BetaEnrolments.Where(e=>e.Id==id)
        .ExecuteUpdateAsync(s=>s.SetProperty(e=>e.InvitationState,state).SetProperty(e=>e.InvitationOutcomeCode,code)
            .SetProperty(e=>e.InvitationCompletedAt,state=="pending"?(DateTimeOffset?)null:now)
            .SetProperty(e=>e.InvitationNextAttemptAt,state=="pending"?now.AddMinutes(5):(DateTimeOffset?)null),token);
}

public sealed class BetaInvitationWorker(IServiceScopeFactory scopes,ILogger<BetaInvitationWorker> log):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        while(!token.IsCancellationRequested)
        {
            try{await using var scope=scopes.CreateAsyncScope();await scope.ServiceProvider.GetRequiredService<BetaInvitationDelivery>().RunAsync(token);}
            catch(OperationCanceledException)when(token.IsCancellationRequested){break;}
            catch(Exception error){log.LogWarning("Beta invitation processing unavailable: {Error}",error.GetType().Name);}
            await Task.Delay(TimeSpan.FromSeconds(15),token);
        }
    }
}
