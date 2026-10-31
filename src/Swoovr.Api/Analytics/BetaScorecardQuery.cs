using System.Data;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Moderation;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Analytics;

public sealed class BetaScorecardQuery(SwoovrDbContext db,IConfiguration configuration,IHostEnvironment environment,TimeProvider clock)
{
    public async Task<BetaScorecardResult> ReadAsync(int weeksAgo,CancellationToken token)
    {
        if(weeksAgo is <1 or >12)throw new ArgumentException("Choose one of12 completed UTC weeks.");
        var today=DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var until=today.AddDays(-(((int)today.DayOfWeek+6)%7)-(weeksAgo-1)*7);var from=until.AddDays(-7);
        var start=new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc));
        var end=new DateTimeOffset(until.ToDateTime(TimeOnly.MinValue,DateTimeKind.Utc));
        var dataset=ModerationTransparencyRecorder.Dataset(environment,configuration);var enabled=configuration.GetValue("BetaAnalytics:Enabled",false);
        await using var transaction=db.Database.CurrentTransaction is null?await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,token):null;
        var members=from a in db.Accounts.AsNoTracking() join p in db.MemberProfiles.AsNoTracking() on a.Id equals p.AccountId
            where a.Realm==AccountRealm.Members&&!p.Bot select a;
        var cohort=members.Where(a=>a.CreatedAt>=start&&a.CreatedAt<end);
        var cohortIds=cohort.Select(a=>a.Id);var registered=await cohort.LongCountAsync(token);
        var observations=db.Set<BetaJourneyDay>().AsNoTracking().Where(d=>d.Dataset==dataset);
        var period=observations.Where(d=>d.Day>=from&&d.Day<until);
        var metrics=new List<BetaScorecardMetric>();
        void Count(string key,string definition,long count)=>metrics.Add(new(key,definition,"retained-source-count",count,null,null));
        void Ratio(string key,string definition,long n,long d,string status="retained-source-ratio")=>metrics.Add(new(key,definition,status,n,d,BetaScorecardPrivacy.Percentage(n,d)));
        void Unknown(string key,string definition)=>metrics.Add(new(key,definition,"not-recorded",null,null,null));
        Count("registrations","Retained non-bot member-realm local account records first created in this UTC week; includes uninvited local account records, not Keycloak form starts.",registered);
        var ready=await (from a in cohort join p in db.MemberProfiles on a.Id equals p.AccountId
            where p.DateOfBirth!=null&&p.DisplayName!=null&&p.DisplayName!=""&&p.HomeGeoNameId!=null&&db.MemberModes.Any(m=>m.AccountId==a.Id&&m.Enabled) select a.Id).LongCountAsync(token);
        Ratio("profile-readiness-snapshot","Current retained cohort with DOB, name, home city and an enabled mode; snapshot, not verified identity or historical completion.",ready,registered);
        if(enabled)
        {
            var discovery=await period.Where(d=>d.ExploreSucceeded).Select(d=>d.AccountId).Distinct().LongCountAsync(token);
            var eligible=await period.Where(d=>d.ExploreHasPeer).Select(d=>d.AccountId).Distinct().LongCountAsync(token);
            var activated=await period.Where(d=>d.ExploreSucceeded&&cohortIds.Contains(d.AccountId)).Select(d=>d.AccountId).Distinct().LongCountAsync(token);
            Ratio("activation-observed","Retained signup cohort observed completing eligible Explore during signup week (includes server match-readiness gates); observed lower bound, not reading/click confirmation.",activated,registered,"observed-lower-bound");
            Ratio("discovery-with-peer","Observed Explore actors with at least one eligible peer / observed successful Explore actors, once per actor per week; no availability/contact/reply promise.",eligible,discovery,"observed-lower-bound");
            foreach(var days in new[]{7,30})
            {
                var cohortStart=start.AddDays(-days);var cohortEnd=end.AddDays(-days);
                var due=members.Where(a=>a.CreatedAt>=cohortStart&&a.CreatedAt<cohortEnd);
                var denominator=await due.LongCountAsync(token);
                var numerator=await (from a in due join d in period on a.Id equals d.AccountId where d.Day==DateOnly.FromDateTime(a.CreatedAt.UtcDateTime).AddDays(days) select a.Id).Distinct().LongCountAsync(token);
                Ratio("observed-day-"+days,"Retained cohort with authenticated member API activity on exact UTC signup day+"+days+" / retained cohort due that day. Includes background requests; incomplete collection is a lower bound, not measured human retention.",numerator,denominator,"observed-lower-bound");
            }
        }
        else foreach(var key in new[]{"activation-observed","discovery-with-peer","observed-day-7","observed-day-30"})Unknown(key,"Daily observation collection is disabled; unavailable is not zero.");
        var requests=db.ConnectionRequests.AsNoTracking().Where(r=>r.CreatedAt>=start&&r.CreatedAt<end);
        var sent=await requests.LongCountAsync(token);
        var accepted=requests.Where(r=>(r.Status==ConnectionRequestStatus.Accepted||r.Status==ConnectionRequestStatus.Ended||r.Status==ConnectionRequestStatus.Disconnected)&&r.AnsweredAt!=null&&r.AnsweredAt<end);
        var acceptedCount=await accepted.LongCountAsync(token);
        Ratio("request-acceptance","Retained requests sent this week and accepted by week end / retained requests sent this week; future answers excluded.",acceptedCount,sent);
        var reciprocal=await accepted.Where(r=>(r.Status==ConnectionRequestStatus.Accepted||r.EndedAt!=null)&&db.Conversations.Any(c=>c.PairLow==r.PairLow&&c.PairHigh==r.PairHigh&&(r.EndedAt==null||c.CreatedAt<r.EndedAt)
            &&db.Messages.Any(m=>m.ConversationId==c.Id&&m.SenderAccountId==r.FromAccountId&&(m.Kind==MessageKind.Text||m.Kind==MessageKind.Photo)&&m.SentAt>=r.AnsweredAt&&m.SentAt<end&&(r.EndedAt==null||m.SentAt<r.EndedAt))
            &&db.Messages.Any(m=>m.ConversationId==c.Id&&m.SenderAccountId==r.ToAccountId&&(m.Kind==MessageKind.Text||m.Kind==MessageKind.Photo)&&m.SentAt>=r.AnsweredAt&&m.SentAt<end&&(r.EndedAt==null||m.SentAt<r.EndedAt)))).LongCountAsync(token);
        Ratio("reciprocal-message-metadata","Accepted request cohort with retained text/photo send metadata from both sides after acceptance by week end / accepted request cohort. Shared conversations can span modes; unknown ended intervals are excluded. No body read, delivery, satisfaction or safety verdict.",reciprocal,acceptedCount,"observed-lower-bound");
        Count("reports","Retained reports created this week; reports are allegations, not confirmed incidents.",await db.Reports.LongCountAsync(r=>r.CreatedAt>=start&&r.CreatedAt<end,token));
        Count("repeat-reported-cases","Retained reported accounts appearing in at least two distinct report cases during this week; never a finding of repeat offending.",await db.Reports.Where(r=>r.CreatedAt>=start&&r.CreatedAt<end).GroupBy(r=>r.SubjectAccountId).Where(g=>g.Select(r=>r.CaseId).Distinct().Count()>=2).LongCountAsync(token));
        foreach(var status in new[]{AppealStatus.Upheld,AppealStatus.Overturned})Count("appeals-"+status.ToString().ToLowerInvariant(),"Retained appeals decided "+status+" in this UTC week; no appeal text or member identifiers.",await db.Appeals.LongCountAsync(a=>a.Status==status&&a.DecidedAt>=start&&a.DecidedAt<end,token));
        Unknown("time-to-first-action","Not a complete historical source; use the operational case queue and prospective annual action receipts, not a fabricated response SLA.");
        Unknown("real-world-outcomes","No attendance or real-world success is inferred from a safety session, closure or check-in.");
        Unknown("conversation-quality","Reciprocal send metadata does not measure chemistry, safety or satisfaction; private feedback is excluded.");
        Unknown("commercial","Subscriptions, venue outcomes and support cost are not instrumented by this Phase0 slice.");
        if(BetaScorecardPrivacy.Suppress(metrics.Select(m=>(m.Numerator,m.Denominator))))metrics=metrics.Select(m=>m.Status=="not-recorded"?m:m with{Status="suppressed-small-cells",Numerator=null,Denominator=null,Percentage=null}).ToList();
        if(transaction is not null)await transaction.CommitAsync(token);
        return new(from,until,dataset,enabled?"best-effort-daily-observations":"disabled","retained-source-subset-not-population-totals",metrics,
            ["No demographic, mode, intention, location or individual filters; no member IDs, raw rows, content or advertising use.",
             "Only12 fixed completed UTC weeks. Sources can be deleted/corrected; this is a dated internal query, not an immutable statistical publication or complete historical cohort.",
             "All numeric cells are suppressed together if any positive numerator/denominator or complementary ratio cell is below5. Zero denominators never produce a percentage.",
             "Daily activity includes background authenticated API calls. Collection is default-off/best-effort and may have gaps; absent observations do not establish absence or inactivity.",
             "Observations expire after90days unless preservation applies. Deletion removes ordinary observations; encrypted held copies have their independent clock."]);
    }
}
