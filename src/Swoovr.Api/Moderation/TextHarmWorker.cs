using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Evidence;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Moderation;
public sealed class TextHarmWorker(IServiceScopeFactory scopes,TimeProvider clock,ILogger<TextHarmWorker> log):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken token)
    {
        await Task.Delay(TimeSpan.FromSeconds(5),clock,token);
        while(!token.IsCancellationRequested)
        {
            try
            {
                Guid cursor=Guid.Empty;
                while(!token.IsCancellationRequested)
                {
                    List<Guid> ids;
                    await using(var page=scopes.CreateAsyncScope())
                    {
                        var db=page.ServiceProvider.GetRequiredService<SwoovrDbContext>();
                        ids=await db.Database.SqlQuery<Guid>($"SELECT id AS \"Value\" FROM text_harm_intent WHERE processed_at IS NULL AND id>{cursor} ORDER BY id LIMIT 100").ToListAsync(token);
                    }
                    if(ids.Count==0)break;
                    foreach(var id in ids)
                    {
                        try
                        {
                            await using var scope=scopes.CreateAsyncScope();
                            await scope.ServiceProvider.GetRequiredService<TextHarmProcessor>().ProcessAsync(id,token);
                        }
                        catch(Exception error)when(!token.IsCancellationRequested){log.LogError("Text inspection remains pending ({ErrorType}).",error.GetType().Name);}
                    }
                    cursor=ids[^1];
                }
                // Catch committed urgent candidates after a crash before the existing escalation attempt.
                await using var urgentScope=scopes.CreateAsyncScope();
                var urgentDb=urgentScope.ServiceProvider.GetRequiredService<SwoovrDbContext>();
                var urgent=await urgentDb.ModerationCases.Where(c=>c.EscalatedAt==null&&c.Priority==ReportPriority.Urgent&&urgentDb.Set<TextHarmReview>().Any(r=>r.CaseId==c.Id&&r.Categories.Contains("threats"))).OrderBy(c=>c.CreatedAt).Take(100).ToListAsync(token);
                foreach(var item in urgent)
                    await ModerationEndpoints.EscalateAsync(urgentDb,item,"text-threat-candidate",false,urgentScope.ServiceProvider.GetRequiredService<IConfiguration>(),urgentScope.ServiceProvider.GetRequiredService<IEmailSender>(),clock.GetUtcNow(),urgentScope.ServiceProvider.GetRequiredService<ILoggerFactory>(),token);
            }
            catch(Exception error)when(!token.IsCancellationRequested){log.LogError("Text review worker will retry ({ErrorType}).",error.GetType().Name);}
            await Task.Delay(TimeSpan.FromSeconds(10),clock,token);
        }
    }
}
