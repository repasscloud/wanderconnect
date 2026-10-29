using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Retention;
using Swoovr.Contracts;
using Swoovr.Domain;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Accounts;

/// <summary>Separate restricted identifier-release and account-reinstatement operations; no plaintext/hash responses.</summary>
public static class RegistryAdminEndpoints
{
    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api/admin/banned-identifiers").RequireAuthorization();
        api.MapPost("/lookup",async(IdentifierLookup request,ClaimsPrincipal user,AccountResolver accounts,TimeProvider clock,HttpContext http,CancellationToken token)=>
        {
            var (current,denied) = await AuthorizeAsync(user,accounts,StaffAccessPolicy.IdentifierRelease,clock.GetUtcNow(),token);
            if (denied is not null) return denied;
            if (!Reason(request.Purpose) || request.Type is not ("email" or "mobile")) return Problem(400,ProblemCodes.InvalidRequest,"Give a valid identifier type and a recorded purpose.");
            var type = request.Type == "email" ? BannedIdentifierType.Email : BannedIdentifierType.Mobile;
            if (BannedIdentifierPolicy.Normalise(type,request.Value) is null) return Problem(400,ProblemCodes.InvalidRequest,"Give a valid identifier.");
            var db = accounts.Db!;var now = clock.GetUtcNow();
            await using var barrier = await PreservationLease.SharedAsync(db,token);
            var rows = await accounts.Services.GetRequiredService<BannedIdentifierRegistry>().FindAsync(type,request.Value,now,token);
            if (await ExcludedAsync(db,current!.Account!.Id,rows,token)) return StaffAccessEndpoints.Conflict();
            Audit.Record(db,now,current.Account.Id,"account","registry.lookup",detail:type.ToString());await db.SaveChangesAsync(token);
            http.Response.Headers["Cache-Control"] = "no-store";
            return Results.Ok(rows.OrderByDescending(b=>b.BannedAt).Select(View));
        });
        api.MapGet("/{id:guid}",async(Guid id,ClaimsPrincipal user,AccountResolver accounts,TimeProvider clock,HttpContext http,CancellationToken token)=>
        {
            var (current,denied) = await AuthorizeAsync(user,accounts,null,clock.GetUtcNow(),token);if(denied is not null)return denied;
            var db=accounts.Db!;await using var barrier=await PreservationLease.SharedAsync(db,token);
            var row=await db.BannedIdentifiers.AsNoTracking().SingleOrDefaultAsync(b=>b.Id==id,token);if(row is null)return Results.NotFound();
            if(await ExcludedAsync(db,current!.Account!.Id,[row],token))return StaffAccessEndpoints.Conflict();
            var history=await db.BanTransitions.Where(t=>t.BannedIdentifierId==id).OrderBy(t=>t.At).Select(t=>new BanHistoryView(t.Id,t.CaseId,t.ActorAccountId,t.At,t.Action,t.OldStatus==null?null:t.OldStatus.ToString(),t.NewStatus.ToString(),t.Reason)).ToListAsync(token);
            var ids=history.Select(h=>h.Id).ToArray();
            var notes=await db.BanCaseNotes.Where(n=>ids.Contains(n.TransitionId)).OrderBy(n=>n.At).Select(n=>new BanNoteView(n.CaseId,n.TransitionId,n.AuthorAccountId,n.At,n.Text)).ToListAsync(token);
            http.Response.Headers["Cache-Control"]="no-store";
            return Results.Ok(new BannedIdentifierDetail(View(row),history,notes));
        });
        api.MapGet("",async(Guid? caseId,bool? due,Guid? after,ClaimsPrincipal user,AccountResolver accounts,TimeProvider clock,HttpContext http,CancellationToken token)=>
        {
            var(current,denied)=await AuthorizeAsync(user,accounts,null,clock.GetUtcNow(),token);if(denied is not null)return denied;
            var db=accounts.Db!;await using var barrier=await PreservationLease.SharedAsync(db,token);var now=clock.GetUtcNow();
            var query=db.BannedIdentifiers.AsNoTracking().AsQueryable();
            if(caseId is{}review)query=query.Where(b=>b.CaseId==review);
            if(due==true)query=query.Where(b=>b.Status==BanStatus.Active&&(b.BanExpiresAt==null||b.BanExpiresAt>now)&&(b.LastReviewedAt??b.BannedAt).AddMonths(BannedIdentifierPolicy.NecessityReviewMonths)<=now);
            var visible=new List<BannedIdentifierView>();Guid? cursor=after;
            // Page across excluded cases rather than allowing a conflicted prefix to hide later reviews.
            while(visible.Count<200)
            {
                var pageQuery=query;if(cursor is{}position)pageQuery=pageQuery.Where(b=>b.Id.CompareTo(position)>0);
                var page=await pageQuery.OrderBy(b=>b.Id).Take(200).ToListAsync(token);
                if(page.Count==0)break;
                foreach(var row in page)
                {
                    cursor=row.Id;
                    if(!await ExcludedAsync(db,current!.Account!.Id,[row],token))visible.Add(View(row));
                    if(visible.Count==200)break;
                }
                if(visible.Count==200){http.Response.Headers["Swoovr-Next-Cursor"]=cursor.ToString();break;}
                if(page.Count<200)break;
            }
            http.Response.Headers["Cache-Control"]="no-store";return Results.Ok(visible);
        });
        api.MapPost("/{id:guid}/review",async(Guid id,IdentifierReviewRequest request,ClaimsPrincipal user,AccountResolver accounts,TimeProvider clock,CancellationToken token)=>
        {
            var now=clock.GetUtcNow();var(current,denied)=await AuthorizeAsync(user,accounts,StaffAccessPolicy.IdentifierRelease,now,token);if(denied is not null)return denied;
            if(!Reason(request.Reason))return Problem(400,ProblemCodes.InvalidRequest,"Record why retaining this restriction remains necessary.");
            var db=accounts.Db!;await using var barrier=await PreservationLease.ExclusiveAsync(db,token);
            var row=await db.BannedIdentifiers.SingleOrDefaultAsync(b=>b.Id==id,token);if(row is null)return Results.NotFound();
            if(await ExcludedAsync(db,current!.Account!.Id,[row],token))return StaffAccessEndpoints.Conflict();
            if(!BannedIdentifierPolicy.Blocks(row,now))return Problem(409,ProblemCodes.InvalidRequest,"Only an active restriction can be retained through necessity review.");
            var(review,note)=BannedIdentifierPolicy.ReviewNecessity(row,current.Account.Id,request.Reason,now);db.BanTransitions.Add(review);db.BanCaseNotes.Add(note);
            Audit.Record(db,now,current.Account.Id,"account","registry.reviewed","registry-entry",id.ToString());await db.SaveChangesAsync(token);return Results.Ok(View(row));
        });
        api.MapPost("/release",async(IdentifierReleaseRequest request,ClaimsPrincipal user,AccountResolver accounts,TimeProvider clock,CancellationToken token)=>
        {
            var now=clock.GetUtcNow();var(current,denied)=await AuthorizeAsync(user,accounts,StaffAccessPolicy.IdentifierRelease,now,token);if(denied is not null)return denied;
            if(!Reason(request.Reason)||request.IdentifierIds is not {Count:>0 and <=100})return Problem(400,ProblemCodes.InvalidRequest,"Select restrictions and record a bounded explanation.");
            var ids=request.IdentifierIds.Distinct().ToArray();var db=accounts.Db!;
            await using var barrier=await PreservationLease.ExclusiveAsync(db,token);
            var rows=await db.BannedIdentifiers.Where(b=>ids.Contains(b.Id)).ToListAsync(token);if(rows.Count!=ids.Length)return Results.NotFound();
            if(await ExcludedAsync(db,current!.Account!.Id,rows,token))return StaffAccessEndpoints.Conflict();
            await using var transaction=await db.Database.BeginTransactionAsync(token);var changed=0;
            foreach(var row in rows.Where(b=>b.Status==BanStatus.Active)) { StageRelease(db,row,current.Account.Id,request.Reason,now);changed++; }
            Audit.Record(db,now,current.Account.Id,"account","registry.released",detail:$"count:{changed}");
            await db.SaveChangesAsync(token);await transaction.CommitAsync(token);
            return Results.Ok(new IdentifierWorkflowResult(changed));
        });
        app.MapPost("/api/admin/accounts/{id:guid}/reinstate",ReinstateAsync).RequireAuthorization();
    }

    private static async Task<IResult> ReinstateAsync(Guid id,AccountReinstatementRequest request,ClaimsPrincipal user,AccountResolver accounts,
        TimeProvider clock,CancellationToken token)
    {
        var now=clock.GetUtcNow();var(current,denied)=await AuthorizeAsync(user,accounts,StaffAccessPolicy.AccountReinstatement,now,token);if(denied is not null)return denied;
        if(!Reason(request.Reason)||request.RevokeIdentifierIds is null||request.RevokeIdentifierIds.Count>100)return Problem(400,ProblemCodes.InvalidRequest,"Record an explanation and explicitly reconcile identifier restrictions.");
        var db=accounts.Db!;await using var state=await AccountLifecycleLease.AcquireAsync(db,id,token);
        await using var barrier=await PreservationLease.ExclusiveAsync(db,token);
        var account=await db.Accounts.SingleOrDefaultAsync(a=>a.Id==id&&a.Realm==AccountRealm.Members,token);
        if(account is null)return Results.NotFound();await db.Entry(account).ReloadAsync(token);
        if(account.Status!=AccountStatus.Active||!await db.MemberProfiles.AnyAsync(p=>p.AccountId==id,token))return Problem(409,ProblemCodes.InvalidRequest,"Deleted, deleting or locked accounts cannot be reinstated here.");
        var sanctions=await db.Sanctions.Where(s=>s.AccountId==id&&s.Kind==SanctionKind.Termination&&s.LiftedAt==null).ToListAsync(token);
        if(sanctions.Count==0)return Problem(409,ProblemCodes.InvalidRequest,"There is no active account termination to reinstate.");
        var cases=await db.ModerationCases.Where(c=>c.SubjectAccountId==id).Select(c=>c.Id).ToArrayAsync(token);
        var historyRows=await db.BannedIdentifiers.Where(b=>cases.Contains(b.CaseId)).ToListAsync(token);
        if(historyRows.Count==0)return Problem(409,ProblemCodes.InvalidRequest,"Enforcement provenance needs review before account reinstatement.");
        var rows=await db.BannedIdentifiers.Where(b=>cases.Contains(b.CaseId)&&b.Status==BanStatus.Active&&(b.BanExpiresAt==null||b.BanExpiresAt>now)).ToListAsync(token);
        var subjects=new List<Guid>{id};foreach(var caseId in cases)subjects.AddRange(await SafetyRetention.CaseAccountsAsync(db,caseId,token));
        if(await StaffAccessEndpoints.ExcludedAsync(db,current!.Account!.Id,subjects,token))return StaffAccessEndpoints.Conflict();
        var selected=request.RevokeIdentifierIds.Distinct().ToHashSet();
        if(!selected.SetEquals(rows.Select(b=>b.Id)))return Problem(409,ProblemCodes.InvalidRequest,"Explicitly reconcile every applicable active identifier restriction before reinstatement.");
        var registry=accounts.Services.GetRequiredService<BannedIdentifierRegistry>();
        var known=new List<BannedIdentifier>();
        if(account.Email is { } email)known.AddRange(await registry.FindAsync(BannedIdentifierType.Email,email,now,token,true));
        var phone=await db.MemberPhones.Where(p=>p.AccountId==id).Select(p=>p.Number).SingleOrDefaultAsync(token);
        if(phone is not null)known.AddRange(await registry.FindAsync(BannedIdentifierType.Mobile,phone,now,token,true));
        if(known.Any(b=>!selected.Contains(b.Id)))return Problem(409,ProblemCodes.InvalidRequest,"Another active identifier restriction requires separate review before reinstatement.");
        await using var transaction=await db.Database.BeginTransactionAsync(token);
        foreach(var row in rows)StageRelease(db,row,current.Account.Id,request.Reason,now);
        foreach(var row in historyRows.GroupBy(b=>b.CaseId).Select(g=>g.First()))
        {
            var(review,note)=BannedIdentifierPolicy.RecordReinstatement(row,current.Account.Id,request.Reason,now);db.BanTransitions.Add(review);db.BanCaseNotes.Add(note);
        }
        foreach(var sanction in sanctions)
        {
            sanction.LiftedAt=now;sanction.LiftedBy=current.Account.Id;
            db.MemberNotices.Add(new MemberNotice { Id=Guid.NewGuid(),AccountId=id,Kind=NoticeKind.SanctionLifted,SanctionId=sanction.Id,CreatedAt=now });
        }
        Audit.Record(db,now,current.Account.Id,"account","account.reinstated","account",id.ToString(),$"terminations:{sanctions.Count};identifiers:{rows.Count}");
        await db.SaveChangesAsync(token);await transaction.CommitAsync(token);
        return Results.Ok(new IdentifierWorkflowResult(sanctions.Count));
    }

    private static async Task<(CurrentAccount? Current,IResult? Denied)> AuthorizeAsync(ClaimsPrincipal user,AccountResolver accounts,string? capability,DateTimeOffset now,CancellationToken token)
    {
        var(current,denied)=await AdminEndpoints.StaffAsync(user,accounts,AccountRole.Admin,token);if(denied is not null)return(null,denied);
        if(!current!.RecentlyAuthenticated(now))return(null,Problem(403,ProblemCodes.StaleSession,"Sign in again to do this."));
        if(capability is not null&&await StaffAccessEndpoints.GrantAsync(accounts.Db!,current.Account!.Id,capability,now,token)is null)return(null,StaffAccessEndpoints.NeedsGrant(capability));
        return(current,null);
    }
    private static bool Reason(string? text)=>!string.IsNullOrWhiteSpace(text)&&text.Trim().Length<=BannedIdentifierPolicy.ReasonMax;
    private static void StageRelease(Swoovr.Infrastructure.Persistence.SwoovrDbContext db,BannedIdentifier row,Guid actor,string reason,DateTimeOffset now)
    {
        var(transition,note)=BannedIdentifierPolicy.Revoke(row,actor,reason,now);db.BanTransitions.Add(transition);db.BanCaseNotes.Add(note);
    }
    private static async Task<bool> ExcludedAsync(Swoovr.Infrastructure.Persistence.SwoovrDbContext db,Guid actor,IEnumerable<BannedIdentifier> rows,CancellationToken token)
    {
        var subjects=new List<Guid>();foreach(var id in rows.Select(b=>b.CaseId).Distinct())subjects.AddRange(await SafetyRetention.CaseAccountsAsync(db,id,token));
        return await StaffAccessEndpoints.ExcludedAsync(db,actor,subjects,token);
    }
    private static BannedIdentifierView View(BannedIdentifier b)=>new(b.Id,Wire(b.IdentifierType),b.CaseId,b.BanReasonCode,Wire(b.Status),b.BannedAt,b.BanExpiresAt,b.LastReviewedAt,
        (b.LastReviewedAt??b.BannedAt).AddMonths(BannedIdentifierPolicy.NecessityReviewMonths),b.LastMatchedAt,b.MatchCount,b.RevokedAt,b.RevokedBy);
}
