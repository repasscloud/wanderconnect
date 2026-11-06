using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Retention;
using Swoovr.Contracts;
using Swoovr.Domain;

namespace Swoovr.Api.Ticketing;

public static class TicketingEndpoints
{
    public static void Map(WebApplication app)
    {
        var member=app.MapGroup("/api/me/tickets").RequireAuthorization();
        var admin=app.MapGroup("/api/admin/tickets").RequireAuthorization();
        member.MapGet("",(HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,false,(r,ct)=>r.ListAsync(Int(c,"page",1),Int(c,"pageSize",25),null,null,null,null,null,"updated_at",true,ct)));
        member.MapGet("/options",(HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,false,(r,ct)=>r.MemberOptionsAsync(ct),true));
        member.MapGet("/{id:long}",(long id,HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,false,(r,ct)=>r.DetailAsync(id,ct)));
        member.MapPost("",(TicketCreate request,HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,false,(r,ct)=>r.CreateAsync(request,Guid.Empty,ct),true));
        member.MapPost("/{id:long}/replies",(long id,TicketReply request,HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,false,(r,ct)=>r.ReplyAsync(id,request,ct)));
        member.MapGet("/{id:long}/articles/{article:long}/attachments/{attachment:long}",(long id,long article,long attachment,HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,false,(r,ct)=>r.DownloadAsync(id,article,attachment,ct)));
        // Issue 220 (D330): the member web app sends files as multipart (no base64 in the browser), up to the owner's limits.
        member.MapPost("/upload",async(HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>
        {
            if(await UploadAsync(c) is not { } form)return Problem(400,"invalid-request");
            if(!Guid.TryParse(form.Field("operationId"),out var operation))return Problem(400,"invalid-request");
            var request=new TicketCreate(operation,form.Field("title")??"",form.Field("body")??"",form.Field("reference"),form.Files);
            return await Run(c,a,f,t,false,(r,ct)=>r.CreateAsync(request,Guid.Empty,ct),true);
        });
        member.MapPost("/{id:long}/replies/upload",async(long id,HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>
        {
            if(await UploadAsync(c) is not { } form)return Problem(400,"invalid-request");
            if(!Guid.TryParse(form.Field("operationId"),out var operation))return Problem(400,"invalid-request");
            var request=new TicketReply(operation,form.Field("body")??"",false,form.Files);
            return await Run(c,a,f,t,false,(r,ct)=>r.ReplyAsync(id,request,ct));
        });
        member.MapPost("/{id:long}/close",(long id,TicketClose request,HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,false,(r,ct)=>r.CloseAsync(id,request,ct)));
        admin.MapGet("",(HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,true,(r,ct)=>r.ListAsync(Int(c,"page",1),Int(c,"pageSize",25),c.Request.Query["query"],Long(c,"group"),Long(c,"state"),Long(c,"priority"),Long(c,"owner"),c.Request.Query["sort"].FirstOrDefault()??"updated_at",!bool.TryParse(c.Request.Query["descending"],out var desc)||desc,ct)));
        admin.MapGet("/overview",(HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,true,(r,ct)=>r.OverviewAsync(ct)));
        admin.MapGet("/capabilities",(HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,true,(r,ct)=>r.CapabilitiesAsync(ct)));
        admin.MapGet("/diagnostics",(HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,true,(r,ct)=>r.DiagnosticsAsync(ct)));
        admin.MapPost("/staff-sync",(HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,true,(r,ct)=>r.SyncStaffAsync(ct)));
        admin.MapGet("/text-modules",(HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,true,(r,ct)=>r.TextModulesAsync(ct)));
        admin.MapGet("/macros",(HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,true,(r,ct)=>r.MacrosAsync(ct)));
        admin.MapPost("/customers",(TicketCustomerLookup request,HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,true,(r,ct)=>r.CustomerAsync(request,ct)));
        admin.MapPost("",(TicketOnBehalf request,HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,true,(r,ct)=>r.CreateAsync(request.Ticket,request.AccountId,ct)));
        admin.MapGet("/{id:long}",(long id,HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,true,(r,ct)=>r.DetailAsync(id,ct)));
        admin.MapPut("/{id:long}",(long id,TicketChange request,HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,true,(r,ct)=>r.ChangeAsync(id,request,ct)));
        admin.MapPost("/{id:long}/replies",(long id,TicketReply request,HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,true,(r,ct)=>r.ReplyAsync(id,request,ct)));
        // Issue 222: staff replies and private notes take the same files as members (multipart from the Admin server).
        admin.MapPost("/{id:long}/replies/upload",async(long id,HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>
        {
            if(await UploadAsync(c) is not { } form)return Problem(400,"invalid-request");
            if(!Guid.TryParse(form.Field("operationId"),out var operation)||form.Field("private") is not("true" or "false"))return Problem(400,"invalid-request");
            var request=new TicketReply(operation,form.Field("body")??"",form.Field("private")=="true",form.Files);
            return await Run(c,a,f,t,true,(r,ct)=>r.ReplyAsync(id,request,ct));
        });
        admin.MapGet("/{id:long}/retention",(long id,HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,true,(r,ct)=>r.RetentionAsync(id,ct)));
        admin.MapPut("/{id:long}/retention",(long id,TicketRetentionChange request,HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,true,(r,ct)=>r.SetRetentionAsync(id,request,ct)));
        admin.MapGet("/{id:long}/links",(long id,HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,true,(r,ct)=>r.LinksAsync(id,ct)));
        admin.MapPost("/{id:long}/links",(long id,TicketLink request,HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,true,(r,ct)=>r.LinkAsync(id,request,ct)));
        admin.MapPost("/{id:long}/macros/{macro:long}",(long id,long macro,TicketMacroApply request,HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,true,(r,ct)=>r.ApplyMacroAsync(id,macro,request,ct)));
        admin.MapGet("/{id:long}/articles/{article:long}/attachments/{attachment:long}",(long id,long article,long attachment,HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,true,(r,ct)=>r.DownloadAsync(id,article,attachment,ct)));
        admin.MapPost("/bulk",(TicketBulk request,HttpContext c,AccountResolver a,TicketingActorFactory f,TimeProvider t)=>Run(c,a,f,t,true,async(r,ct)=>
        {
            if(request.Items.Count is <1 or >20||request.Items.Select(i=>i.Id).Distinct().Count()!=request.Items.Count)throw new ArgumentException("Choose one to twenty distinct tickets.");
            var results=new List<TicketBulkResult>();
            foreach(var item in request.Items)
            {
                try{results.Add(new(item.Id,200,null,await r.ChangeAsync(item.Id,item.Change,ct)));}
                catch(TicketingAccessException e){results.Add(new(item.Id,e.Status,e.Code,null));}
                catch(TicketingProviderException e){results.Add(new(item.Id,e.Uncertain?409:503,e.Uncertain?"ticket-reconciliation-required":"ticket-provider-unavailable",null));}
                catch(ArgumentException){results.Add(new(item.Id,400,"invalid-request",null));}
            }
            return results;
        }));
    }
    /// <summary>The upload routes (issue 220): the only member ticket routes allowed a large body.</summary>
    public static bool IsUpload(PathString path)=>path.Value is { } p&&(System.Text.RegularExpressions.Regex.IsMatch(p,"^/api/me/tickets(/[0-9]{1,18}/replies)?/upload$")
        ||System.Text.RegularExpressions.Regex.IsMatch(p,"^/api/admin/tickets/[0-9]{1,18}/replies/upload$"));

    private sealed record UploadForm(Dictionary<string,string> Fields,List<TicketUpload> Files){public string? Field(string name)=>Fields.GetValueOrDefault(name);}

    /// <summary>
    /// Reads a multipart message: short text fields (operationId, title, body, reference) and up to five files named
    /// "files". Each file is read with the policy's per-type cap and the message's total cap; the API checks content next.
    /// Null for anything malformed or over a limit.
    /// </summary>
    private static async Task<UploadForm?> UploadAsync(HttpContext context)
    {
        if(!Microsoft.Net.Http.Headers.MediaTypeHeaderValue.TryParse(context.Request.ContentType,out var type)||!type.MediaType.Equals("multipart/form-data",StringComparison.OrdinalIgnoreCase)
            ||Microsoft.Net.Http.Headers.HeaderUtilities.RemoveQuotes(type.Boundary).Value is not { Length: >0 and <=200 } boundary)return null;
        var reader=new Microsoft.AspNetCore.WebUtilities.MultipartReader(boundary,context.Request.Body){HeadersLengthLimit=16*1024};
        var form=new UploadForm(new(StringComparer.Ordinal),[]);
        long total=0;
        try
        {
            while(await reader.ReadNextSectionAsync(context.RequestAborted) is { } section)
            {
                if(!Microsoft.Net.Http.Headers.ContentDispositionHeaderValue.TryParse(section.ContentDisposition,out var disposition)||disposition.Name.Value is not { } name)return null;
                name=name.Trim('"');
                if(disposition.FileName.HasValue||disposition.FileNameStar.HasValue)
                {
                    var filename=(disposition.FileNameStar.HasValue?disposition.FileNameStar.Value:disposition.FileName.Value)?.Trim('"')??"";
                    if(name!="files"||form.Files.Count>=SupportAttachmentPolicy.MaxAttachmentsPerMessage||SupportAttachmentPolicy.TypeOf(filename) is not { } kind)return null;
                    var max=Math.Min(SupportAttachmentPolicy.MaxBytes(kind.Kind),SupportAttachmentPolicy.MaxTotalMessageBytes-total);
                    using var memory=new MemoryStream();
                    var buffer=new byte[81920];int read;
                    while((read=await section.Body.ReadAsync(buffer,context.RequestAborted))>0)
                    {
                        if(memory.Length+read>max)return null;
                        memory.Write(buffer,0,read);
                    }
                    total+=memory.Length;
                    form.Files.Add(new(filename,kind.Mime,memory.ToArray()));
                }
                else
                {
                    if(name is not("operationId" or "title" or "body" or "reference" or "private")||form.Fields.ContainsKey(name))return null;
                    using var text=new StreamReader(section.Body,new System.Text.UTF8Encoding(false,true));
                    var chars=new char[16001];var count=await text.ReadBlockAsync(chars,0,chars.Length);
                    if(count>16000)return null;
                    form.Fields[name]=new string(chars,0,count);
                }
            }
        }
        catch(IOException){return null;}
        catch(InvalidDataException){return null;}
        catch(System.Text.DecoderFallbackException){return null;}
        return form;
    }

    private static int Int(HttpContext c,string key,int fallback)=>string.IsNullOrEmpty(c.Request.Query[key])?fallback:int.TryParse(c.Request.Query[key],out var value)?value:throw new ArgumentException("Invalid page.");
    private static long? Long(HttpContext c,string key)=>string.IsNullOrEmpty(c.Request.Query[key])?null:long.TryParse(c.Request.Query[key],out var value)&&value>0?value:throw new ArgumentException("Invalid filter.");
    private static async Task<IResult> Run<T>(HttpContext context,AccountResolver accounts,TicketingActorFactory actors,TimeProvider clock,bool staff,Func<TicketingRuntime,CancellationToken,Task<T>> action,bool create=false)
    {
        context.Response.Headers.CacheControl="no-store";
        context.Response.Headers["X-Content-Type-Options"]="nosniff";
        var ct=context.RequestAborted;
        try
        {
            var current=await accounts.ResolveAsync(context.User,ct);
            if(current is null)return Results.Unauthorized();
            if(accounts.Db is null||current.Account is null)return Problem(503,"persistence-unavailable");
            await using var state=await AccountLifecycleLease.AcquireAsync(accounts.Db,current.Account.Id,ct);
            await using var barrier=await PreservationLease.SharedAsync(accounts.Db,ct);
            if(staff)
            {
                current=await accounts.ResolveAsync(context.User,ct);
                if(current is null)return Results.Unauthorized();
                if(current.Facts.Realm!=AccountRealm.Staff||!new[]{AccountRole.Support,AccountRole.Admin,AccountRole.TrustSafety,AccountRole.Legal}.Any(current.Allows))return Results.Forbid();
            }
            else
            {
                var result=await AccountEndpoints.MemberAsync(context.User,accounts,ct,ageGateExempt:true,barredExempt:true,phoneExempt:true);
                if(result.Denied is not null)return result.Denied;current=result.Current!;
            }
            if(!staff&&!create&&context.Request.Method=="GET"&&context.Request.Path.Value?.TrimEnd('/')=="/api/me/tickets"&&
                !await accounts.Db.Set<TicketingIdentity>().AnyAsync(m=>m.AccountId==current.Account!.Id,ct))return Results.Ok(new TicketPage([],Int(context,"page",1),Int(context,"pageSize",25),false));
            var provider=staff?await actors.StaffAsync(accounts,current,ct):await actors.MemberAsync(accounts,current,create,ct);
            return Results.Ok(await action(new(accounts,current,provider,actors,clock,staff),ct));
        }
        catch(TicketingAccessException e){return Problem(e.Status,e.Code);}
        catch(TicketingProviderException e){return Problem(e.Uncertain?409:e.Code is "provider-forbidden" or "provider-not-found"?404:503,e.Uncertain?"ticket-reconciliation-required":"ticket-provider-unavailable");}
        catch(DbUpdateException e)when(e.InnerException is Npgsql.PostgresException{SqlState:"23505"}){return Problem(409,"ticket-operation-conflict");}
        catch(ArgumentException){return Problem(400,"invalid-request");}
        catch(InvalidOperationException){return Problem(409,"ticket-operation-conflict");}
    }
    private static IResult Problem(int status,string code)=>Results.Problem(statusCode:status,title:"Support request could not be completed.",extensions:new Dictionary<string,object?>{{"code",code}});
}
