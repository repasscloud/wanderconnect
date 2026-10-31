using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Swoovr.Api.Accounts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;

static class BetaInvitationChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> sourceOptions,Action<string,bool> check)
    {
        await using var source=new SwoovrDbContext(sourceOptions);
        var original=source.Database.GetDbConnection().ConnectionString;
        await using var admin=new NpgsqlConnection(original);await admin.OpenAsync();
        var name="swoovr_beta_inv_"+Guid.NewGuid().ToString("N");
        await using(var create=new NpgsqlCommand("CREATE DATABASE "+name,admin))await create.ExecuteNonQueryAsync();
        try
        {
            var connection=new NpgsqlConnectionStringBuilder(original){Database=name,Pooling=false}.ConnectionString;
            var options=new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(connection).Options;
            await using var db=new SwoovrDbContext(options);await db.Database.MigrateAsync();
            var now=DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());var clock=new Clock(now);
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Beta:Invitations:Enabled","true"},{"Beta:Invitations:PublicBaseUrl","https://beta.example.test/"}}).Build();
            check("disabled invitations require no public origin",BetaInvitationDelivery.PublicOrigin(new ConfigurationBuilder().Build(),false) is null);
            foreach(var invalid in new[]{"http://beta.example.test/","https://beta.example.test/account","https://beta.example.test/?private=value","https://user:secret@beta.example.test/"})
            {
                var bad=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Beta:Invitations:Enabled","true"},{"Beta:Invitations:PublicBaseUrl",invalid}}).Build();
                try{BetaInvitationDelivery.PublicOrigin(bad,false);check("enabled beta origin rejects unsafe URL",false);}catch(InvalidOperationException error){check("invalid beta origin refusal does not echo value",!error.Message.Contains("secret",StringComparison.Ordinal));}
            }
            var sink=new Sink();var delivery=new BetaInvitationDelivery(db,sink,config,new Host(),clock);
            var disabled=(await BetaEnrolments.AddAsync(db,"fictional.off@example.test",null,now,default)).Enrolment!;
            check("beta invitation defaults off without inventing history",disabled.InvitationState=="not-queued"&&disabled.InvitationQueuedAt==null);
            var pending=(await BetaEnrolments.AddAsync(db,"fictional.queued@example.test",null,now,default,queueInvitation:true)).Enrolment!;
            var duplicate=await BetaEnrolments.AddAsync(db,"FICTIONAL.QUEUED@example.test",null,now,default,queueInvitation:true);
            check("beta invitation queue is atomic and duplicate add never duplicates",duplicate.Result==BetaEnrolments.AddResult.AlreadyEnrolled&&duplicate.Enrolment!.Id==pending.Id&&pending.InvitationState=="pending");
            await delivery.RunAsync(default);
            var saved=await db.BetaEnrolments.AsNoTracking().SingleAsync(e=>e.Id==pending.Id);
            check("beta invitation acknowledges provider acceptance only",sink.Calls==1&&saved.InvitationState=="accepted"&&saved.InvitationAttempts==1&&saved.InvitationCompletedAt==now);
            check("beta invite text has fixed public account link and no Apple claim",sink.Last!.Text.Contains("https://beta.example.test/account",StringComparison.Ordinal)&&!sink.Last.Text.Contains("TestFlight",StringComparison.Ordinal));
            await delivery.RunAsync(default);check("accepted beta invitation never repeats",sink.Calls==1);
            foreach(var reason in new[]{"revoked","activated"})
            {
                var row=(await BetaEnrolments.AddAsync(db,$"fictional.{reason}@example.test",null,now,default,queueInvitation:true)).Enrolment!;
                if(reason=="revoked")await db.BetaEnrolments.Where(e=>e.Id==row.Id).ExecuteUpdateAsync(s=>s.SetProperty(e=>e.RevokedAt,now));
                else await db.BetaEnrolments.Where(e=>e.Id==row.Id).ExecuteUpdateAsync(s=>s.SetProperty(e=>e.ActivatedAt,now));
                await delivery.RunAsync(default);var cancelled=await db.BetaEnrolments.AsNoTracking().SingleAsync(e=>e.Id==row.Id);
                check("beta invitation cancelled before dispatch for "+reason,cancelled.InvitationState=="cancelled"&&sink.Calls==1);
            }
            var concurrent=(await BetaEnrolments.AddAsync(db,"fictional.concurrent@example.test",null,now,default,queueInvitation:true)).Enrolment!;
            await using(var other=new SwoovrDbContext(options))
                await Task.WhenAll(delivery.RunAsync(default),new BetaInvitationDelivery(other,sink,config,new Host(),clock).RunAsync(default));
            check("concurrent beta workers send one accepted invitation",sink.Calls==2&&(await db.BetaEnrolments.AsNoTracking().SingleAsync(e=>e.Id==concurrent.Id)).InvitationAttempts==1);
            sink.Outcome=new("unknown","unconfirmed");
            var unknown=(await BetaEnrolments.AddAsync(db,"fictional.unknown@example.test",null,now,default,queueInvitation:true)).Enrolment!;
            await delivery.RunAsync(default);await delivery.RunAsync(default);
            check("uncertain beta send never blindly retries",sink.Calls==3&&(await db.BetaEnrolments.AsNoTracking().SingleAsync(e=>e.Id==unknown.Id)).InvitationState=="unknown");
            sink.Outcome=new("retry","unconfigured");
            var retry=(await BetaEnrolments.AddAsync(db,"fictional.retry@example.test",null,now,default,queueInvitation:true)).Enrolment!;
            for(var n=0;n<5;n++){await delivery.RunAsync(default);clock.Now=clock.Now.AddHours(1);}
            var exhausted=await db.BetaEnrolments.AsNoTracking().SingleAsync(e=>e.Id==retry.Id);
            check("known-no-effect beta retries are finite",exhausted.InvitationAttempts==3&&exhausted.InvitationState=="rejected"&&sink.Calls==6);
            var stale=(await BetaEnrolments.AddAsync(db,"fictional.interrupted@example.test",null,now,default,queueInvitation:true)).Enrolment!;
            await db.BetaEnrolments.Where(e=>e.Id==stale.Id).ExecuteUpdateAsync(s=>s.SetProperty(e=>e.InvitationState,"dispatching").SetProperty(e=>e.InvitationAttempts,1).SetProperty(e=>e.InvitationAttemptedAt,now).SetProperty(e=>e.InvitationNextAttemptAt,(DateTimeOffset?)null));
            await delivery.RunAsync(default);check("interrupted beta dispatch becomes unknown without resend",sink.Calls==6&&(await db.BetaEnrolments.AsNoTracking().SingleAsync(e=>e.Id==stale.Id)).InvitationState=="unknown");
            var racing=(await BetaEnrolments.AddAsync(db,"fictional.revoke-race@example.test",null,now,default,queueInvitation:true)).Enrolment!;
            var pause=new PausingSink();var sendTask=new BetaInvitationDelivery(db,pause,config,new Host(),clock).RunAsync(default);
            var application="beta_revoke_"+Guid.NewGuid().ToString("N");
            await using(var other=new SwoovrDbContext(new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(connection){ApplicationName=application}.ConnectionString).Options))
            {
                Task<bool>? revoke=null;
                try
                {
                    await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
                    var row=await other.BetaEnrolments.SingleAsync(e=>e.Id==racing.Id);
                    revoke=BetaEnrolments.RevokeAsync(other,row,null,clock.Now,default);
                    var waiting=false;
                    for(var n=0;n<100&&!waiting;n++)
                    {
                        await using var probe=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE application_name=@name AND wait_event_type='Lock')",admin);probe.Parameters.AddWithValue("name",application);
                        waiting=(bool)(await probe.ExecuteScalarAsync())!;if(!waiting)await Task.Delay(20);
                    }
                    check("beta revocation positively waits behind an already-started send",waiting);
                }
                finally{pause.Release.TrySetResult();await sendTask;if(revoke is not null)await revoke;}
            }
            var raced=await db.BetaEnrolments.AsNoTracking().SingleAsync(e=>e.Id==racing.Id);
            check("concurrent revoke cannot interleave a second send",pause.Calls==1&&raced.InvitationState=="accepted"&&raced.RevokedAt!=null);
            var expired=(await BetaEnrolments.AddAsync(db,"fictional.expired@example.test",null,now.AddDays(-4),default,queueInvitation:true)).Enrolment!;
            await delivery.RunAsync(default);check("beta mail expires independently from enrolment access",sink.Calls==6&&(await db.BetaEnrolments.AsNoTracking().SingleAsync(e=>e.Id==expired.Id)).InvitationState=="expired"&&expired.RevokedAt==null);
        }
        finally{await using var drop=new NpgsqlCommand("DROP DATABASE "+name+" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();}
    }
    private sealed class Clock(DateTimeOffset now):TimeProvider{public DateTimeOffset Now=now;public override DateTimeOffset GetUtcNow()=>Now;}
    private sealed class Host:IHostEnvironment{public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="Swoovr.Checks";public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
    private sealed class PausingSink:IEmailSender
    {
        public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);public int Calls;
        public async Task<bool> SendAsync(EmailMessage message,CancellationToken token)=>(await SendOutcomeAsync(message,token)).Status=="accepted";
        public async Task<EmailOutcome> SendOutcomeAsync(EmailMessage message,CancellationToken token){Interlocked.Increment(ref Calls);Entered.TrySetResult();await Release.Task.WaitAsync(token);return new("accepted","local-sink");}
    }
    private sealed class Sink:IEmailSender
    {
        public int Calls;public EmailMessage? Last;public EmailOutcome Outcome=new("accepted","local-sink");
        public async Task<bool> SendAsync(EmailMessage message,CancellationToken token)=>(await SendOutcomeAsync(message,token)).Status=="accepted";
        public Task<EmailOutcome> SendOutcomeAsync(EmailMessage message,CancellationToken token){Interlocked.Increment(ref Calls);Last=message;return Task.FromResult(Outcome);}
    }
}
