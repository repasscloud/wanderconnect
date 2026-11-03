using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Contracts;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Accounts;

/// <summary>D292: educational-notice dismissal, separate from policy consent. No reading telemetry.</summary>
public static class AdviceAcknowledgements
{
    public sealed class Row
    {
        public string Revision { get; set; }="";
        public DateTimeOffset AcknowledgedAt { get; set; }
    }
    public static async Task<AdviceAcknowledgementView?> ReadAsync(SwoovrDbContext db,Guid account,CancellationToken token)
    {
        var row=await db.Database.SqlQuery<Row>($"SELECT revision AS \"Revision\", acknowledged_at AS \"AcknowledgedAt\" FROM safety_advice_acknowledgements WHERE account_id={account}").SingleOrDefaultAsync(token);
        return row is null?null:new(row.Revision,row.Revision==AdviceAcknowledgementView.CurrentRevision,row.AcknowledgedAt);
    }
    // Caller owns account lifecycle and preservation authority when part of an application mutation.
    public static async Task<bool> SaveAsync(SwoovrDbContext db,Guid account,string revision,bool confirmed,DateTimeOffset now,CancellationToken token)
    {
        if(!confirmed||revision!=AdviceAcknowledgementView.CurrentRevision)return false;
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO safety_advice_acknowledgements(account_id,revision,acknowledged_at) VALUES({account},{revision},{now})
            ON CONFLICT(account_id) DO UPDATE SET revision=EXCLUDED.revision,acknowledged_at=EXCLUDED.acknowledged_at
            WHERE safety_advice_acknowledgements.revision<>EXCLUDED.revision
            """,token);
        return true;
    }
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/me/safety-advice",async(ClaimsPrincipal user,AccountResolver accounts,CancellationToken token)=>
        {
            var(current,denied)=await AccountEndpoints.MemberAsync(user,accounts,token,ageGateExempt:true,barredExempt:true,pendingDeletionExempt:true,phoneExempt:true);
            if(denied is not null)return denied;
            var view=await ReadAsync(accounts.Db!,current!.Account!.Id,token);
            return Results.Ok(view??new AdviceAcknowledgementView(AdviceAcknowledgementView.CurrentRevision,false,null));
        }).RequireAuthorization();
        app.MapPost("/api/me/safety-advice",async(AdviceAcknowledgementRequest request,ClaimsPrincipal user,AccountResolver accounts,TimeProvider clock,Swoovr.Api.Evidence.EvidencePreservation evidence,CancellationToken token)=>
        {
            var(current,denied)=await AccountEndpoints.MemberAsync(user,accounts,token);
            if(denied is not null)return denied;
            var db=accounts.Db!;var id=current!.Account!.Id;
            await using var lifecycle=await AccountLifecycleLease.AcquireAsync(db,id,token);
            await using var preservation=await Swoovr.Api.Retention.PreservationLease.SharedAsync(db,token);
            (current,denied)=await AccountEndpoints.MemberAsync(user,accounts,token);
            if(denied is not null)return denied;
            if(current!.Profile?.DateOfBirth is null)return AccountEndpoints.Problem(409,"setup-incomplete","Finish setting up your account first.");
            if(request.Confirmed && request.Revision==AdviceAcknowledgementView.CurrentRevision && await ReadAsync(db,id,token) is{}previous && previous.Revision!=request.Revision)
                await evidence.PreserveBeforeDeletionAsync(id,clock.GetUtcNow(),token);
            if(!await SaveAsync(db,id,request.Revision,request.Confirmed,clock.GetUtcNow(),token))return AccountEndpoints.Problem(400,"invalid-advice-revision","Read and acknowledge the current advice.");
            return Results.Ok(await ReadAsync(db,id,token));
        }).RequireAuthorization();
    }
}
