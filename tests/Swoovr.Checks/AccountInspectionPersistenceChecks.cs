using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Swoovr.Api.Accounts;
using Swoovr.Api.Media;
using Swoovr.Api.Moderation;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;

static class AccountInspectionPersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options,Action<string,bool> check)
    {
        await using var db=new SwoovrDbContext(options);
        using var services=new ServiceCollection().AddSingleton(db).BuildServiceProvider();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Media:LinkKey"]=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))}).Build();
        var links=new MediaLinks(config,new Founders(services,config,new UnconfiguredEmailSender(),NullLogger<Founders>.Instance));
        var now=DateTimeOffset.Parse("2026-11-02T10:00:00Z");var id=Guid.NewGuid();
        var account=new Account{Id=id,Issuer="https://issuer.example.test",Subject=Guid.NewGuid().ToString(),Realm=AccountRealm.Members,Status=AccountStatus.Active,CreatedAt=now,LastSeenAt=now};
        var staff=new Account{Id=Guid.NewGuid(),Issuer="https://staff.example.test",Subject=Guid.NewGuid().ToString(),Realm=AccountRealm.Staff,Status=AccountStatus.Active,CreatedAt=now,LastSeenAt=now};
        var profile=new MemberProfile{AccountId=id,PublicId=Guid.NewGuid(),DisplayName="Fictional public main",About="PRIVATE CORE",DateOfBirth=new DateOnly(1990,1,1),DateOfBirthVerified=true,AgeStatus=AgeStatus.Verified};
        db.Accounts.AddRange(account,staff);db.MemberProfiles.Add(profile);db.MemberIdentities.Add(new MemberIdentity{AccountId=id,ShowAge=false,ShowGender=false,Pronouns="PRIVATE PRONOUNS",UpdatedAt=now});
        db.MemberModes.AddRange(new MemberMode{AccountId=id,ModeId="friends",Enabled=true,Visible=true,DisplayName="Public friends alias",About="Public friends about",UpdatedAt=now},new MemberMode{AccountId=id,ModeId="dating",Enabled=true,Visible=false,About="PRIVATE DATING",UpdatedAt=now});
        MediaItem Photo(MediaStatus status)=>new(){Id=Guid.NewGuid(),AccountId=id,Status=status,ObjectKey="photos/fictional",Sha256=RandomNumberGenerator.GetBytes(32),UploadSha256=RandomNumberGenerator.GetBytes(32),CreatedAt=now};
        var visible=Photo(MediaStatus.Passed);var hidden=Photo(MediaStatus.Passed);var pending=Photo(MediaStatus.Pending);
        db.MediaItems.AddRange(visible,hidden,pending);
        db.MediaPlacements.AddRange(new MediaPlacement{AccountId=id,MediaId=visible.Id,Scope="friends",Position=0,IsMain=true},new MediaPlacement{AccountId=id,MediaId=hidden.Id,Scope="dating",Position=0,IsMain=true},new MediaPlacement{AccountId=id,MediaId=pending.Id,Scope="friends",Position=1});
        // The product permits only one open case per subject; use a historical Admin case for queue-scope coverage.
        db.ModerationCases.AddRange(new ModerationCase{Id=Guid.NewGuid(),SubjectAccountId=id,Queue=CaseQueue.Support,Status=CaseStatus.Open,CreatedAt=now,UpdatedAt=now},new ModerationCase{Id=Guid.NewGuid(),SubjectAccountId=id,Queue=CaseQueue.Admin,Status=CaseStatus.Closed,CreatedAt=now,UpdatedAt=now});
        for(var i=0;i<30;i++)Audit.Record(db,now.AddSeconds(i),null,"system","profile.updated","account",id.ToString(),"PRIVATE AUDIT DETAIL");
        Audit.Record(db,now,null,"system","identity.decrypted","account",id.ToString(),"PRIVATE LEGAL DETAIL");
        await db.SaveChangesAsync();
        var summary=await ModerationEndpoints.SummaryAsync(db,id,now,default);
        check("Q111 backend age is numeric and distinct from assurance",summary.Age==36&&summary.AgeStatus=="verified"&&summary.DateOfBirthVerified);
        var searchBefore=await db.SearchEvents.CountAsync();var chatsBefore=await db.Conversations.CountAsync();
        var preview=await AccountInspectionEndpoints.PreviewAsync(db,links,id,false,now,default)??throw new Exception("Missing fictional preview");
        var json=JsonSerializer.Serialize(preview);
        check("Q111 preview honors mode name/about and age visibility",preview.Modes.Single(m=>m.Id=="friends").PublicProfile is{DisplayName:"Public friends alias",About:"Public friends about",Age:null});
        check("Q111 preview never returns hidden mode or core content",!json.Contains("PRIVATE")&&preview.Modes.Single(m=>m.Id=="dating").PublicProfile is null);
        check("Q111 public preview excludes pending and hidden-mode photos",json.Contains(visible.Id.ToString())&&!json.Contains(hidden.Id.ToString())&&!json.Contains(pending.Id.ToString()));
        check("Q111 Support case links exclude Admin cases",preview.Cases.Count==1&&!preview.CanInspectPrivatePhotos);
        var admin=await AccountInspectionEndpoints.PreviewAsync(db,links,id,true,now,default);
        check("Q111 Admin gets separately authorised inventory capability and case links",admin!.Cases.Count==2&&admin.CanInspectPrivatePhotos);
        check("Q111 preview changes no account, visit/search or conversation state",!db.ChangeTracker.HasChanges()&&await db.SearchEvents.CountAsync()==searchBefore&&await db.Conversations.CountAsync()==chatsBefore);
        var page=await AccountInspectionEndpoints.HistoryAsync(db,id,null,default);var earlier=await AccountInspectionEndpoints.HistoryAsync(db,id,page.NextCursor,default);
        check("Q111 action history is paginated without repeated entries",page.Items.Count==25&&earlier.Items.Count==5&&!page.Items.Select(x=>x.Id).Intersect(earlier.Items.Select(x=>x.Id)).Any());
        check("Q111 history excludes Legal activity and all raw audit detail",!JsonSerializer.Serialize(page).Contains("PRIVATE")&&!page.Items.Any(a=>a.Action=="identity.decrypted"));
        db.Set<MediaSafetyAcknowledgement>().Add(new MediaSafetyAcknowledgement{StaffAccountId=staff.Id,Version=PhotoReviewPolicy.NoticeVersion,AcknowledgedAt=now-PhotoReviewPolicy.AcknowledgementLifetime+TimeSpan.FromSeconds(90)});
        await db.SaveChangesAsync();
        var restrictedLink=await PhotoReviewWorkflows.ReviewLinkAsync(db,links,staff.Id,visible.Id,"inspection",now,default);
        check("Q111 restricted image token expires with acknowledgement, not its longer normal lifetime",restrictedLink is not null&&restrictedLink.Expires==now.AddSeconds(90)
            &&MediaPolicy.ReadLink(links.Key!,restrictedLink.Link.Split('/').Last(),now.AddSeconds(89))is not null
            &&MediaPolicy.ReadLink(links.Key!,restrictedLink.Link.Split('/').Last(),now.AddSeconds(90))is null);
        check("Q111 expired acknowledgement cannot issue another inspection token",await PhotoReviewWorkflows.ReviewLinkAsync(db,links,staff.Id,visible.Id,"inspection",now.AddSeconds(90),default)is null);
        await db.MediaPlacements.Where(p=>p.AccountId==id).ExecuteDeleteAsync();await db.MediaItems.Where(p=>p.AccountId==id).ExecuteDeleteAsync();
        await db.ModerationCases.Where(c=>c.SubjectAccountId==id).ExecuteDeleteAsync();await db.AuditEntries.Where(a=>a.TargetId==id.ToString()).ExecuteDeleteAsync();
        db.ChangeTracker.Clear();
        await db.Accounts.Where(a=>a.Id==id).ExecuteDeleteAsync();
        await db.Accounts.Where(a=>a.Id==staff.Id).ExecuteDeleteAsync();
    }
}
