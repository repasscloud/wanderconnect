namespace Swoovr.Api.Accounts;

/// <summary>Due database intents survive restart; per-source lifecycle/preservation leases coordinate replicas.</summary>
public sealed class EmailRetirementWorker(IServiceScopeFactory scopes,TimeProvider clock,ILogger<EmailRetirementWorker> log):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15),clock,stoppingToken);
        while(!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope=scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<EmailRetirementProcessor>().RunAsync(stoppingToken);
            }
            catch(Exception error)when(!stoppingToken.IsCancellationRequested)
            {log.LogError("Email retirement reconciliation failed ({Kind}); durable work remains pending.",error.GetType().Name);}
            await Task.Delay(TimeSpan.FromMinutes(1),clock,stoppingToken);
        }
    }
}
