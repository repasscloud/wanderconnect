using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Ticketing;

/// <summary>Per-request workspace. Every ticket read/write is checked against live provider grants and Swoovr subjects.</summary>
public sealed class TicketingRuntime(AccountResolver accounts,CurrentAccount current,ITicketingWorkspaceProvider provider,
    TicketingActorFactory actors,TimeProvider clock,bool staff)
{
    private SwoovrDbContext Db=>accounts.Db!;
    private readonly HashSet<Guid> freshIntents=[];
    private Task<SupportDirectory>? directoryTask;
    private Task<SupportDirectory> DirectoryAsync(CancellationToken ct)=>directoryTask??=provider.DirectoryAsync(ct);
    private Guid Me=>current.Account!.Id;
    private static TicketingAccessException Refuse(int status=403,string code="ticket-unavailable")=>new(status,code);
    private AccountRole Role(string group)=>new[]{AccountRole.Legal,AccountRole.TrustSafety,AccountRole.Admin,AccountRole.Support}.FirstOrDefault(r=>current.Allows(r)&&TicketingPolicy.CanAccessGroup(r,group));
    /// <summary>
    /// Sensitive queues (owner, 5 November 2026, Q133 option B): the role, the matching elevated capability and a sign-in
    /// within 15 minutes. These queues are never granted to anyone's Zammad web sign-in, so this is the only way in.
    /// </summary>
    private async Task<bool> SensitiveAsync(string group,CancellationToken ct)
    {
        if(!TicketingPolicy.Sensitive.Contains(group))return true;
        var capability=group switch {TicketingPolicy.Legal=>StaffAccessPolicy.TicketLegal,"Privacy & Data Protection"=>StaffAccessPolicy.TicketPrivacy,"Security"=>StaffAccessPolicy.TicketSecurity,_=>StaffAccessPolicy.TicketRestricted};
        return current.RecentlyAuthenticated(clock.GetUtcNow())&&await StaffAccessEndpoints.GrantAsync(Db,Me,capability,clock.GetUtcNow(),ct) is not null;
    }
    private async Task<(SupportActor Actor,SupportDirectory Directory)> FactsAsync(CancellationToken ct)
    {
        var actor=await provider.ActorAsync(ct);
        if(!actor.Active||actor.Roles.Count!=1||actor.Roles[0]!=(staff?"Agent":"Customer")||actor.Login!=(staff?TicketingPolicy.StaffLogin(Me):TicketingPolicy.MemberLogin(Me)))throw Refuse();
        return(actor,await DirectoryAsync(ct));
    }
    private async Task<bool> GroupAsync(long group,string action,CancellationToken ct,bool transfer=false)
    {
        var (actor,directory)=await FactsAsync(ct);
        var name=directory.Groups.SingleOrDefault(g=>g.Id==group)?.Name;
        if(name is null||!TicketingPolicy.Groups.Contains(name))return false;
        var allowed=transfer?new[]{AccountRole.Legal,AccountRole.TrustSafety,AccountRole.Admin,AccountRole.Support}.Any(r=>current.Allows(r)&&TicketingPolicy.CanTransfer(r,name)):Role(name)!=AccountRole.Unknown;
        if(!allowed||!actor.Groups.TryGetValue(group,out var grants)||!grants.Any(g=>g=="full"||g==action))return false;
        return transfer&&action=="create"&&TicketingPolicy.IsHandOffTarget(name)||await SensitiveAsync(name,ct);
    }
    private async Task<Guid?> CustomerAccountAsync(long customer,CancellationToken ct)=>await Db.Set<TicketingIdentity>().Where(m=>m.ProviderUserId==customer&&m.State==TicketingOperationState.Applied&&m.DeactivatedAt==null).Select(m=>(Guid?)m.AccountId).SingleOrDefaultAsync(ct);
    private async Task CheckSubjectsAsync(SupportTicket ticket,CancellationToken ct)
    {
        var member=await CustomerAccountAsync(ticket.CustomerId,ct);
        var subjects=new HashSet<Guid>();
        if(member is null)
        {
            // A customer that ever belonged to a member keeps the member rules (an inactive or deleted member stays refused).
            if(await Db.Set<TicketingIdentity>().AnyAsync(m=>m.ProviderUserId==ticket.CustomerId,ct))throw Refuse(404);
            // A guest must still be active. Anyone else wrote to the support mailbox: an unverified email sender with no Swoovr
            // account, so there are no member conflicts to apply; case links below still apply.
            var guest=await Db.Set<TicketingGuest>().Where(g=>g.ProviderUserId==ticket.CustomerId).Select(g=>new{g.State,g.DeactivatedAt}).SingleOrDefaultAsync(ct);
            if(guest is not null&&(guest.State!=TicketingOperationState.Applied||guest.DeactivatedAt!=null))throw Refuse(404);
        }
        else
        {
            if(!await Db.Accounts.AnyAsync(a=>a.Id==member&&a.Status==AccountStatus.Active,ct))throw Refuse(404);
            subjects.Add(member.Value);
        }
        var links=await Db.Set<TicketingCaseLink>().Where(l=>l.ProviderTicketId==ticket.Id).Select(l=>l.CaseId).Distinct().ToArrayAsync(ct);
        var cases=await Db.ModerationCases.Where(c=>links.Contains(c.Id)).ToArrayAsync(ct);
        if(cases.Length!=links.Length||cases.Any(c=>c.Queue==CaseQueue.Admin&&current.StaffRole!=AccountRole.Admin))throw Refuse(404);
        foreach(var caseId in links)foreach(var subject in await Swoovr.Api.Retention.SafetyRetention.CaseAccountsAsync(Db,caseId,ct))subjects.Add(subject);
        if(await StaffAccessEndpoints.ExcludedAsync(Db,Me,subjects,ct))throw Refuse(403,"conflict-of-interest");
    }
    public async Task<SupportTicket> TicketAsync(long id,bool write,CancellationToken ct)
    {
        var ticket=await provider.GetTicketAsync(id,ct)??throw Refuse(404);
        if(staff)
        {
            if(!await GroupAsync(ticket.GroupId,write?"change":"read",ct))throw Refuse(404);
            await CheckSubjectsAsync(ticket,ct);
        }
        else
        {
            var mapped=await CustomerAccountAsync(ticket.CustomerId,ct);
            // A ticket opened by email (no Swoovr operation) can't be tied to a verified Swoovr identity: staff only.
            if(mapped!=Me||ticket.OperationReference is null)throw Refuse(404);
            var effective=await provider.ActorAsync(ct);TicketingActorFactory.ValidateCustomer(effective,Me);
            if(effective.Id!=ticket.CustomerId)throw Refuse(404);
        }
        return ticket;
    }
    public static TicketSummary Summary(SupportTicket ticket)=>new(ticket.Id,TicketingProjection.Reference(ticket.Id),TicketingProjection.Plain(ticket.Title,"text/plain"),ticket.StateId,ticket.PriorityId,ticket.UpdatedAt);
    public async Task<TicketDetail> DetailAsync(long id,CancellationToken ct)
    {
        var ticket=await TicketAsync(id,false,ct);
        var articles=await provider.GetArticlesAsync(id,ct);
        // Read access again after the provider fetch; a transfer/revocation cannot expose a fetched private projection.
        ticket=await TicketAsync(id,false,ct);
        var views=articles.Where(a=>a.TicketId==id&&(staff||a.CustomerVisible)).Select(a=>new TicketArticle(a.Id,TicketingProjection.Plain(a.Body,a.ContentType),a.Internal,a.Sender,a.CreatedAt,
            a.Attachments.Where(x=>SupportAttachmentPolicy.Allowed(TicketingProjection.Filename(x.Filename),x.Size)).Select(x=>new TicketAttachment(x.Id,TicketingProjection.Filename(x.Filename),SupportAttachmentPolicy.TypeOf(TicketingProjection.Filename(x.Filename))!.Value.Mime,x.Size)).ToArray(),staff?a.Type:null)).ToArray();
        return new(Summary(ticket),views,staff?await StaffFieldsAsync(ticket,ct):null,await CanReplyAsync(ticket,ct));
    }
    private async Task<TicketStaffFields> StaffFieldsAsync(SupportTicket ticket,CancellationToken ct)
    {
        var member=await CustomerAccountAsync(ticket.CustomerId,ct);
        return new(ticket.GroupId,ticket.OwnerId,member,ticket.Classification,member is null&&await Db.Set<TicketingGuest>().AnyAsync(g=>g.ProviderUserId==ticket.CustomerId,ct));
    }
    private async Task<bool> CanReplyAsync(SupportTicket ticket,CancellationToken ct)
    {
        if(staff)return await GroupAsync(ticket.GroupId,"change",ct);
        var directory=await DirectoryAsync(ct);var kind=directory.States.SingleOrDefault(s=>s.Id==ticket.StateId)?.Kind;
        return kind is not null&&kind is not("merged" or "removed")&&(kind!="closed"||directory.Groups.SingleOrDefault(g=>g.Id==ticket.GroupId)?.FollowUpPolicy=="yes");
    }
    public async Task<TicketPage> ListAsync(int page,int size,string? query,long? group,long? state,long? priority,long? owner,string sort,bool descending,CancellationToken ct)
    {
        if(page is <1 or >100||size is <1 or >50||query?.Length>200)throw new ArgumentException("Invalid page or search.");
        if(query is { } reference&&reference.StartsWith("TK-",StringComparison.OrdinalIgnoreCase)&&long.TryParse(reference[3..],out var referenceId)&&referenceId>0)
        {
            var found=await TicketAsync(referenceId,false,ct);
            var matches=(group is null||found.GroupId==group)&&(state is null||found.StateId==state)&&(priority is null||found.PriorityId==priority)&&(owner is null||found.OwnerId==owner);
            return new(page==1&&matches?[Summary(found)]:[],page,size,false);
        }
        // Only literal search terms; never accept arbitrary provider query language capable of widening a customer/group scope.
        var terms=string.IsNullOrWhiteSpace(query)?"":string.Join(" AND ",query.Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(v=>"\""+v.Replace("\\","").Replace("\"","")+"\""));
        var clauses=new List<string>();if(terms.Length>0)clauses.Add("("+terms+")");
        if(staff)
        {
            var facts=await FactsAsync(ct);var allowed=new List<long>();
            foreach(var g in facts.Directory.Groups)if(await GroupAsync(g.Id,"read",ct))allowed.Add(g.Id);
            if(group is long chosen){if(!allowed.Contains(chosen))throw Refuse(404);allowed=[chosen];}
            if(allowed.Count==0)return new([],page,size,false);
            clauses.Add("("+string.Join(" OR ",allowed.Select(g=>"group_id:"+g))+")");
            // Exclusions are applied before pagination by bounded exact customer IDs.
            var excluded=await StaffAccessEndpoints.ExcludedSubjects(Db,Me).ToArrayAsync(ct);
            var customerIds=await Db.Set<TicketingIdentity>().Where(m=>excluded.Contains(m.AccountId)&&m.ProviderUserId!=null).Select(m=>m.ProviderUserId!.Value).ToArrayAsync(ct);
            foreach(var customer in customerIds)clauses.Add("NOT customer_id:"+customer);
        }
        else
        {
            var actor=await provider.ActorAsync(ct);TicketingActorFactory.ValidateCustomer(actor,Me);clauses.Add("customer_id:"+actor.Id);
        }
        foreach(var f in new[]{("state_id",state),("priority_id",priority),("owner_id",owner)})if(f.Item2 is long value){if(value<1)throw new ArgumentException("Invalid filter.");clauses.Add(f.Item1+":"+value);}
        var expression=string.Join(" AND ",clauses);
        if(expression.Length>1000)throw Refuse(503,"ticket-search-scope-too-large");
        // Scan authorised results before application pagination. No totals leak excluded linked-case or unavailable customers.
        var visible=new List<TicketSummary>();var target=page*size+1;var sourcePage=1;var exhausted=false;
        while(visible.Count<target&&sourcePage<=100)
        {
            var rows=await provider.SearchAsync(expression,sourcePage++,100,sort,descending,ct);
            foreach(var row in rows)
            {
                try{var checkedTicket=await TicketAsync(row.Id,false,ct);if((group is null||checkedTicket.GroupId==group)&&(state is null||checkedTicket.StateId==state)&&(priority is null||checkedTicket.PriorityId==priority)&&(owner is null||checkedTicket.OwnerId==owner))visible.Add(Summary(checkedTicket));}catch(TicketingAccessException e)when(e.Status is 403 or 404){}catch(TicketingProviderException e)when(e.Code is "provider-forbidden" or "provider-not-found"){}
                if(visible.Count>=target)break;
            }
            if(rows.Count<100){exhausted=true;break;}
        }
        if(!exhausted&&sourcePage>100&&visible.Count<target)throw Refuse(503,"ticket-search-scope-too-large");
        return new(visible.Skip((page-1)*size).Take(size).ToArray(),page,size,visible.Count>page*size);
    }
    public async Task<TicketMemberOptions> MemberOptionsAsync(CancellationToken ct)
    {
        var directory=await DirectoryAsync(ct);
        return new(directory.States.Select(s=>new TicketOption(s.Id,s.Name)).ToArray(),directory.Priorities.Select(s=>new TicketOption(s.Id,s.Name)).ToArray(),AttachmentPolicyText);
    }
    /// <summary>Issue 220 (D330): the owner's list and limits, in one line for clients.</summary>
    public static string AttachmentPolicyText=>$"{string.Join(" ",SupportAttachmentPolicy.AllowedExtensions)}; up to {SupportAttachmentPolicy.MaxAttachmentsPerMessage} per message, {SupportAttachmentPolicy.MaxTotalMessageBytes/(1024*1024)} MB in all. No identity documents or sensitive evidence.";
    /// <summary>The staff member behind either of their Zammad identities, or null for anything Swoovr doesn't manage.</summary>
    private static (Guid Staff,bool Web)? StaffOf(SupportActor actor)
    {
        if(!actor.Active||actor.Roles.Count!=1||actor.Roles[0]!="Agent")return null;
        foreach(var web in new[]{true,false})
        {
            var prefix=web?"swoovr-staff-web-":"swoovr-staff-";
            if(actor.Login.StartsWith(prefix,StringComparison.Ordinal)&&Guid.TryParseExact(actor.Login[prefix.Length..],"D",out var id)&&actor.Login==(web?TicketingPolicy.StaffWebLogin(id):TicketingPolicy.StaffLogin(id)))return(id,web);
        }
        return null;
    }
    private async Task<long[]> OwnerGroupsAsync(SupportActor actor,CancellationToken ct)
    {
        if(StaffOf(actor) is not var(id,web))return [];
        var account=await Db.Accounts.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==id&&a.Realm==AccountRealm.Staff&&a.Status==AccountStatus.Active,ct);
        if(account is null)return [];
        var roles=TicketingPolicy.EffectiveRoles(await Db.RoleGrants.Where(r=>r.AccountId==id).Select(r=>r.Role).ToListAsync(ct),account.IsRootAdmin);
        var directory=await DirectoryAsync(ct);
        return actor.Groups.Where(g=>g.Value.Contains("full")&&directory.Groups.Any(d=>d.Id==g.Key&&roles.Any(r=>TicketingPolicy.CanAccessGroup(r,d.Name))&&!(web&&TicketingPolicy.Sensitive.Contains(d.Name)))).Select(g=>g.Key).ToArray();
    }
    public async Task<TicketCapabilities> CapabilitiesAsync(CancellationToken ct)
    {
        var facts=await FactsAsync(ct);var read=new List<TicketOption>();var transfers=new List<TicketOption>();
        foreach(var g in facts.Directory.Groups)
        {
            if(await GroupAsync(g.Id,"read",ct))read.Add(new(g.Id,g.Name));
            if(await GroupAsync(g.Id,"create",ct,true))transfers.Add(new(g.Id,g.Name));
        }
        var owners=new List<TicketOwner>();
        var agents=(await provider.AgentsAsync(ct)).Select(a=>(Actor:a,Staff:StaffOf(a))).Where(a=>a.Staff is not null).ToList();
        foreach(var (actor,who) in agents)
        {
            var groups=(await OwnerGroupsAsync(actor,ct)).Where(g=>read.Any(r=>r.Id==g)).ToList();
            // One owner per person per queue: their web sign-in where it works the queue (so it shows in their own Zammad
            // views), otherwise the identity Swoovr Admin acts as.
            if(!who!.Value.Web&&agents.FirstOrDefault(o=>o.Staff is{Web:true} w&&w.Staff==who.Value.Staff).Actor is{} web)groups.RemoveAll((await OwnerGroupsAsync(web,ct)).Contains);
            if(groups.Count==0)continue;
            // Staff choose an owner by their Swoovr staff email, never by a provider login.
            var label=await Db.Accounts.Where(a=>a.Id==who.Value.Staff&&a.Realm==AccountRealm.Staff).Select(a=>a.Email).SingleOrDefaultAsync(ct);
            owners.Add(new(actor.Id,string.IsNullOrWhiteSpace(label)?"Staff member "+actor.Id:label,groups.ToArray()));
        }
        return new(read,transfers,facts.Directory.States.Select(s=>new TicketOption(s.Id,s.Name)).ToArray(),facts.Directory.Priorities.Select(s=>new TicketOption(s.Id,s.Name)).ToArray(),true,AttachmentPolicyText,
            owners,
            Categories, ["ordinary","restricted"],Resolutions);
    }
    public async Task<TicketSummary> CreateAsync(TicketCreate request,Guid customerAccount,CancellationToken ct)
    {
        if(request.OperationId==Guid.Empty)throw new ArgumentException("An operation ID is required.");
        ValidateText(request.Title,200);ValidateText(request.Body,16000);
        if(!staff)customerAccount=Me;
        var account=await Db.Accounts.SingleOrDefaultAsync(a=>a.Id==customerAccount&&a.Realm==AccountRealm.Members&&a.Status==AccountStatus.Active,ct)??throw Refuse(404);
        if(staff&&await StaffAccessEndpoints.ExcludedAsync(Db,Me,[customerAccount],ct))throw Refuse();
        await using var lease=await AccountLifecycleLease.AcquireAsync(Db,customerAccount,ct);
        await Db.Entry(account).ReloadAsync(ct);
        if(account.Status!=AccountStatus.Active)throw Refuse(404);
        var identity=actors.Identity();var operations=new TicketingOperations(Db,identity,provider,clock);
        var customer=await operations.EnsureCustomerAsync(customerAccount,account.Email,ct);
        var directory=await DirectoryAsync(ct);var group=directory.Groups.SingleOrDefault(g=>g.Name=="General Support")??throw Refuse(503);
        if(staff&&!await GroupAsync(group.Id,"create",ct))throw Refuse();
        // Issue 220: files may come with the first message.
        var uploads=request.Attachments is { Count: >0 } files?TicketingProjection.MemberAttachments(files.Select(f=>(f.Filename,f.Data))):null;
        var created=await operations.CreateTicketAsync(customerAccount,request.OperationId,new(request.Title,request.Body,customer,group.Id,request.OperationId.ToString("D"),staff?"staff":"member",uploads),ct);
        // DEFAULT: a member's own report reference links the request to that report's case. Someone else's or an unknown
        // reference is ignored without a word, so references can't be probed. Nothing from the report is sent to the helpdesk.
        if(!staff&&SupportReference.Normalise(request.Reference) is { } reference
            &&await Db.Reports.Where(r=>r.SupportReference==reference&&r.ReporterAccountId==Me).Select(r=>(Guid?)r.CaseId).FirstOrDefaultAsync(ct) is Guid caseId)
            await AddLinkAsync(created.Id,caseId,null,ct);
        return Summary(created);
    }
    private async Task AddLinkAsync(long ticket,Guid caseId,Guid? appeal,CancellationToken ct)
    {
        if(await Db.Set<TicketingCaseLink>().AnyAsync(l=>l.ProviderTicketId==ticket&&l.CaseId==caseId&&l.AppealId==appeal,ct))return;
        Db.Add(new TicketingCaseLink{Id=Guid.NewGuid(),ProviderTicketId=ticket,CaseId=caseId,AppealId=appeal,LinkedBy=Me,LinkedAt=clock.GetUtcNow()});
        try{await Db.SaveChangesAsync(ct);}
        catch(DbUpdateException e)when(e.InnerException is Npgsql.PostgresException{SqlState:"23505"}){Db.ChangeTracker.Clear();}
    }
    public async Task<IReadOnlyList<TicketLinkView>> LinksAsync(long id,CancellationToken ct)
    {
        await TicketAsync(id,false,ct);
        return await (from l in Db.Set<TicketingCaseLink>() join c in Db.ModerationCases on l.CaseId equals c.Id where l.ProviderTicketId==id orderby l.LinkedAt
                      select new TicketLinkView(l.CaseId,l.AppealId,c.Status.ToString(),l.LinkedAt)).ToListAsync(ct);
    }
    /// <summary>Staff link a request to a case or an appeal they may see. A link is a reference only: closing or reopening
    /// the request never changes the case, a sanction or an appeal decision, which stay in Swoovr.</summary>
    public async Task<IReadOnlyList<TicketLinkView>> LinkAsync(long id,TicketLink request,CancellationToken ct)
    {
        await TicketAsync(id,true,ct);
        var found=await Db.ModerationCases.AsNoTracking().SingleOrDefaultAsync(c=>c.Id==request.CaseId,ct)??throw Refuse(404,"ticket-case-unavailable");
        if(found.Queue==CaseQueue.Admin&&!current.Allows(AccountRole.Admin))throw Refuse(404,"ticket-case-unavailable");
        if(await StaffAccessEndpoints.ExcludedAsync(Db,Me,(await Swoovr.Api.Retention.SafetyRetention.CaseAccountsAsync(Db,found.Id,ct)).ToArray(),ct))throw Refuse(403,"conflict-of-interest");
        if(request.AppealId is Guid appeal&&!await (from a in Db.Appeals join s in Db.Sanctions on a.SanctionId equals s.Id where a.Id==appeal&&s.CaseId==found.Id select a.Id).AnyAsync(ct))
            throw Refuse(404,"ticket-case-unavailable");
        await AddLinkAsync(id,found.Id,request.AppealId,ct);
        return await LinksAsync(id,ct);
    }
    // DEFAULT reversible vocabulary; provider owns the selected attributes, not a parallel classification engine.
    public static readonly TicketCategory[] Categories=[new("account",["access","profile","verification"]),new("billing",["subscription","payment","refund"]),new("safety",["report","appeal","other"]),new("technical",["web","mobile","other"]),new("other",["general"])];
    public static readonly string[] Resolutions=["unresolved","answered","resolved","duplicate","referred"];
    public async Task<TicketCustomerView> CustomerAsync(TicketCustomerLookup request,CancellationToken ct)
    {
        var directory=await DirectoryAsync(ct);var general=directory.Groups.Single(g=>g.Name=="General Support");
        if(!await GroupAsync(general.Id,request.Create?"create":"read",ct))throw Refuse();
        if(request.Query is {Length:>0} query)
        {
            if(query.Length>254||request.AccountId!=Guid.Empty)throw new ArgumentException("Choose one lookup key.");
            Guid? match=null;
            if(query.StartsWith("swoovr-",StringComparison.Ordinal)&&Guid.TryParseExact(query[7..],"D",out var canonical)&&TicketingPolicy.MemberLogin(canonical)==query)match=canonical;
            else if(long.TryParse(query,out var numeric)&&numeric>0)match=await CustomerAccountAsync(numeric,ct);
            else if(Guid.TryParse(query,out var guid))match=guid;
            else
            {
                var email=EmailAddress.Normalise(query)??throw new ArgumentException("Use an exact email, GUID or canonical login.");
                var matches=await Db.Accounts.Where(a=>a.Realm==AccountRealm.Members&&a.Status==AccountStatus.Active&&a.Email==email).Select(a=>a.Id).Take(2).ToArrayAsync(ct);
                if(matches.Length>1)throw Refuse(409,"ticket-customer-ambiguous");
                if(matches.Length==1)match=matches[0];
            }
            if(match is null)throw Refuse(404,"ticket-customer-not-found");
            request=request with{AccountId=match.Value};
        }
        if(request.AccountId==Guid.Empty||await StaffAccessEndpoints.ExcludedAsync(Db,Me,[request.AccountId],ct))throw Refuse();
        var account=await Db.Accounts.SingleOrDefaultAsync(a=>a.Id==request.AccountId&&a.Realm==AccountRealm.Members&&a.Status==AccountStatus.Active,ct)??throw Refuse(404);
        var mapping=await Db.Set<TicketingIdentity>().AsNoTracking().SingleOrDefaultAsync(m=>m.AccountId==request.AccountId,ct);
        if(request.Create){var identity=actors.Identity();await new TicketingOperations(Db,identity,provider,clock).EnsureCustomerAsync(account.Id,account.Email,ct);return new(account.Id,"mapped");}
        return new(account.Id,mapping is null?"not-created":mapping.State.ToString().ToLowerInvariant());
    }
    private static void ValidateText(string value,int maximum){if(string.IsNullOrWhiteSpace(value)||value.Length>maximum||value.Contains('\0'))throw new ArgumentException("Invalid support text.");}
    private async Task<TicketingOperation> IntentAsync(Guid operationId,string kind,object request,long ticket,CancellationToken ct)
    {
        if(operationId==Guid.Empty)throw new ArgumentException("An operation ID is required.");
        var digest=Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));
        var operation=await Db.Set<TicketingOperation>().SingleOrDefaultAsync(o=>o.Id==operationId,ct);
        if(operation is not null)
        {
            if(operation.AccountId!=Me||operation.Kind!=kind||operation.RequestDigest!=digest||operation.ProviderTicketId!=ticket)throw Refuse(409,"ticket-operation-conflict");
            if(operation.State!=TicketingOperationState.Applied&&(kind!="reply"||operation.State==TicketingOperationState.Rejected))throw Refuse(409,"ticket-reconciliation-required");
            return operation;
        }
        operation=new(){Id=operationId,AccountId=Me,Kind=kind,RequestDigest=digest,ProviderTicketId=ticket,State=TicketingOperationState.Pending,CreatedAt=clock.GetUtcNow(),UpdatedAt=clock.GetUtcNow()};
        Db.Add(operation);await Db.SaveChangesAsync(ct);freshIntents.Add(operationId);return operation;
    }
    private async Task FinishAsync(TicketingOperation operation,TicketingOperationState state,long? article=null)
    {
        operation.State=state;operation.ProviderArticleId=article;operation.UpdatedAt=clock.GetUtcNow();
        using var recovery=new CancellationTokenSource(TimeSpan.FromSeconds(5));await Db.SaveChangesAsync(recovery.Token);
    }
    public async Task<TicketDetail> ReplyAsync(long id,TicketReply request,CancellationToken ct)
    {
        var ticket=await TicketAsync(id,true,ct);
        if(!await CanReplyAsync(ticket,ct))throw Refuse(409,"ticket-follow-up-unavailable");
        ValidateText(request.Body,16000);
        if(!staff&&request.Private)throw Refuse();
        var uploads=TicketingProjection.MemberAttachments((request.Attachments??[]).Select(a=>(a.Filename,a.Data)));
        // A digest of the files rather than their bytes (large uploads); a reply without files keeps its earlier digest.
        var operation=await IntentAsync(request.OperationId,"reply",uploads.Count==0?request:new{request.OperationId,request.Body,request.Private,Files=Fingerprints(uploads)},id,ct);
        if(operation.State!=TicketingOperationState.Applied)
        {
            if(!freshIntents.Contains(operation.Id))
            {
                var actor=await provider.ActorAsync(ct);
                var matches=(await provider.GetArticlesAsync(id,ct)).Where(a=>a.TicketId==id&&a.OperationReference==operation.Id.ToString("D")&&a.AuthorId==actor.Id&&a.Internal==request.Private).ToArray();
                if(matches.Length!=1)throw Refuse(409,"ticket-reconciliation-required");
                await FinishAsync(operation,TicketingOperationState.Applied,matches[0].Id);
                return await DetailAsync(id,ct);
            }
            try{var article=await provider.ReplyWithOperationAsync(id,request.Body,request.Private,!staff,uploads,operation.Id.ToString("D"),ct);await FinishAsync(operation,TicketingOperationState.Applied,article.Id);}
            catch(TicketingProviderException e){await FinishAsync(operation,e.Uncertain?TicketingOperationState.Uncertain:TicketingOperationState.Rejected);throw;}
        }
        return await DetailAsync(id,ct);
    }
    public async Task<TicketSummary> ChangeAsync(long id,TicketChange request,CancellationToken ct)
    {
        // Cross-staff ticket mutex orders stale-update checking, transfers and bulk/macro operations.
        var ticketKey=new Guid(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("ticket:"+id)).AsSpan(0,16));
        await using var ticketLease=await AccountLifecycleLease.AcquireAsync(Db,ticketKey,ct);
        var ticket=await TicketAsync(id,true,ct);
        if(request.Classification?.Count>0&&(request.GroupId!=null||request.OwnerId!=null||request.StateId!=null||request.PriorityId!=null))throw new ArgumentException("Submit classification and assignment changes separately.");
        if(request.Classification is {Count:>0} classification)
        {
            var merged=ticket.Classification.ToDictionary(k=>k.Key,k=>k.Value);
            foreach(var value in classification)merged[value.Key]=value.Value;
            if(merged.TryGetValue("swoovr_category",out var category)&&!Categories.Any(c=>c.Id==category))throw new ArgumentException("Unknown category.");
            if(merged.TryGetValue("swoovr_subcategory",out var subcategory)&&!Categories.Any(c=>c.Id==merged.GetValueOrDefault("swoovr_category")&&c.Subcategories.Contains(subcategory)))throw new ArgumentException("Choose a subcategory of the selected category.");
            if(merged.TryGetValue("swoovr_resolution",out var resolution)&&!Resolutions.Contains(resolution))throw new ArgumentException("Unknown resolution.");
        }
        if(request.Classification is { } attrs&&attrs.TryGetValue("swoovr_sensitivity",out var sensitivity)&&sensitivity is not("ordinary" or "restricted"))throw new ArgumentException("Unknown sensitivity.");
        if(request.Classification is { } c&&c.TryGetValue("swoovr_sensitivity",out var sensitive)&&sensitive=="restricted")
        {
            var name=(await DirectoryAsync(ct)).Groups.Single(g=>g.Id==ticket.GroupId).Name;
            if(name is not("Trust & Safety — Restricted" or "Legal & Law Enforcement"))throw Refuse(409,"transfer-to-restricted-group-first");
        }
        if(request.GroupId is long target&&!await GroupAsync(target,"create",ct,true))throw Refuse();
        var directory=await DirectoryAsync(ct);
        if(request.StateId is long state&&!directory.States.Any(s=>s.Id==state)||request.PriorityId is long priority&&!directory.Priorities.Any(p=>p.Id==priority))throw new ArgumentException("Unknown status or priority.");
        if(request.OwnerId is long owner)
        {
            var user=await provider.UserAsync(owner,ct);
            if(!(await OwnerGroupsAsync(user,ct)).Contains(request.GroupId??ticket.GroupId))throw Refuse(400,"ticket-owner-unavailable");
        }
        var operation=await IntentAsync(request.OperationId,"change",request,id,ct);
        if(operation.State==TicketingOperationState.Applied)return Summary(ticket);
        if(ticket.UpdatedAt!=request.ExpectedUpdatedAt){await FinishAsync(operation,TicketingOperationState.Rejected);throw Refuse(409,"ticket-changed");}
        try
        {
            var result=request.Classification?.Count>0?await provider.ClassifyAsync(id,request.Classification,ct):await provider.ChangeTicketAsync(id,new(request.GroupId,request.OwnerId,request.StateId,request.PriorityId),ct);
            await FinishAsync(operation,TicketingOperationState.Applied);
            // A transfer receipt contains no conversation; subsequent reads always apply destination access.
            return Summary(result);
        }
        catch(TicketingProviderException e){await FinishAsync(operation,e.Uncertain?TicketingOperationState.Uncertain:TicketingOperationState.Rejected);throw;}
    }
    public async Task<TicketDownload> DownloadAsync(long id,long articleId,long attachmentId,CancellationToken ct)
    {
        var detail=await DetailAsync(id,ct);var article=detail.Articles.SingleOrDefault(a=>a.Id==articleId)??throw Refuse(404);
        var attachment=article.Attachments.SingleOrDefault(a=>a.Id==attachmentId)??throw Refuse(404);
        var data=await provider.DownloadAsync(id,articleId,attachmentId,ct);
        await TicketAsync(id,false,ct);
        var checkedFile=TicketingProjection.MemberAttachment(attachment.Filename,data.Data);
        return new(checkedFile.Filename,checkedFile.ContentType,data.Data);
    }
    /// <summary>Name, size and SHA-256 of each file: what an operation's digest covers.</summary>
    public static IReadOnlyList<string> Fingerprints(IEnumerable<SupportUpload> files)=>files.Select(f=>$"{f.Filename}:{f.Data.Length}:{Convert.ToHexStringLower(SHA256.HashData(f.Data))}").ToArray();
    /// <summary>
    /// Issue 220 (D330): the member closes their own open request, as themselves, so Zammad applies its customer rules.
    /// Closing twice is fine. Whether a closed request takes replies stays the queue's follow-up rule (not reopened).
    /// </summary>
    public async Task<TicketDetail> CloseAsync(long id,TicketClose request,CancellationToken ct)
    {
        if(staff)throw Refuse();
        var ticketKey=new Guid(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("ticket:"+id)).AsSpan(0,16));
        await using var ticketLease=await AccountLifecycleLease.AcquireAsync(Db,ticketKey,ct);
        var ticket=await TicketAsync(id,true,ct);
        var directory=await DirectoryAsync(ct);
        var kind=directory.States.SingleOrDefault(s=>s.Id==ticket.StateId)?.Kind;
        if(kind is "merged" or "removed")throw Refuse(409,"ticket-follow-up-unavailable");
        if(kind=="closed")return await DetailAsync(id,ct);
        var closed=directory.States.FirstOrDefault(s=>s.Kind=="closed"&&s.Name=="closed")??directory.States.FirstOrDefault(s=>s.Kind=="closed")??throw Refuse(503,"ticket-provider-unavailable");
        var operation=await IntentAsync(request.OperationId,"close",request,id,ct);
        if(operation.State!=TicketingOperationState.Applied)
        {
            try{await provider.ChangeTicketAsync(id,new(null,null,closed.Id,null),ct);await FinishAsync(operation,TicketingOperationState.Applied);}
            catch(TicketingProviderException e){await FinishAsync(operation,e.Uncertain?TicketingOperationState.Uncertain:TicketingOperationState.Rejected);throw;}
        }
        return await DetailAsync(id,ct);
    }
    public async Task<IReadOnlyList<TicketTextModule>> TextModulesAsync(CancellationToken ct)=>(await provider.TextModulesAsync(ct)).Select(m=>new TicketTextModule(m.Id,TicketingProjection.Plain(m.Name,"text/plain"),TicketingProjection.Plain(m.Body,"text/html"))).ToArray();
    public async Task<IReadOnlyList<TicketMacro>> MacrosAsync(CancellationToken ct)=>(await provider.MacrosAsync(ct)).Select(m=>new TicketMacro(m.Id,TicketingProjection.Plain(m.Name,"text/plain"),m.Supported)).ToArray();
    public async Task<TicketSummary> ApplyMacroAsync(long id,long macroId,TicketMacroApply request,CancellationToken ct)
    {
        await TicketAsync(id,true,ct);var macro=(await provider.MacrosAsync(ct)).SingleOrDefault(m=>m.Id==macroId&&m.Supported)??throw Refuse(400,"ticket-macro-unsupported");
        return await ChangeAsync(id,new(request.OperationId,request.ExpectedUpdatedAt,macro.Change.GroupId,macro.Change.OwnerId,macro.Change.StateId,macro.Change.PriorityId),ct);
    }
    public async Task<TicketDiagnostics> DiagnosticsAsync(CancellationToken ct)
    {
        if(!current.Allows(AccountRole.Admin))throw Refuse();
        await provider.ActorAsync(ct);
        var rows=Db.Set<TicketingStaff>();
        var configured=!string.IsNullOrWhiteSpace(accounts.Services.GetRequiredService<IConfiguration>()["Ticketing:Zammad:StaffDirectoryToken"]);
        long? unmanaged=null;
        if(configured)
        {
            // Agents that Swoovr doesn't manage are another way into tickets; they are reported, never silently tolerated.
            var managed=await rows.Where(s=>s.ProviderUserId!=null||s.WebProviderUserId!=null).Select(s=>new{s.ProviderUserId,s.WebProviderUserId}).ToListAsync(ct);
            var ids=managed.SelectMany(m=>new[]{m.ProviderUserId,m.WebProviderUserId}).OfType<long>().ToHashSet();
            try{unmanaged=(await actors.StaffDirectory().AgentsAsync(ct)).LongCount(a=>!ids.Contains(a.Id)&&a.Login!="swoovr-provisioner");}
            catch(TicketingProviderException){}
        }
        var staffStatus=new TicketStaffSyncStatus(configured,await rows.LongCountAsync(s=>s.ProviderUserId!=null,ct),await rows.LongCountAsync(s=>s.WebActive,ct),await rows.LongCountAsync(s=>s.FailureCode!=null,ct),
            await rows.MaxAsync(s=>(DateTimeOffset?)s.SyncedAt,ct),await rows.MaxAsync(s=>(DateTimeOffset?)s.VerifiedAt,ct),unmanaged);
        return new("reachable",await Db.Set<TicketingIdentity>().LongCountAsync(ct),await Db.Set<TicketingIdentity>().LongCountAsync(i=>i.State==TicketingOperationState.Uncertain,ct),await Db.Set<TicketingOperation>().LongCountAsync(i=>i.State==TicketingOperationState.Uncertain||i.State==TicketingOperationState.Pending,ct),await Db.Set<TicketingWebhookWork>().LongCountAsync(i=>i.CompletedAt==null,ct),await Db.Set<TicketingWebhookWork>().LongCountAsync(i=>i.CompletedAt==null&&i.FailureCode!=null,ct),staffStatus);
    }
    /// <summary>Admin: apply and verify every staff member's helpdesk permissions now (normally every minute / ten minutes).</summary>
    public async Task<TicketStaffSyncRun> SyncStaffAsync(CancellationToken ct)
    {
        if(!current.Allows(AccountRole.Admin))throw Refuse();
        var result=await accounts.Services.GetRequiredService<TicketingStaffSync>().RunAsync(true,ct);
        return new(result.Checked,result.Changed,result.Failed,result.Drifted);
    }
}
