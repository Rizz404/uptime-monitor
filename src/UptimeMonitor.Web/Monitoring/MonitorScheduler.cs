using Microsoft.Extensions.Options;

namespace UptimeMonitor.Web.Monitoring;

public class MonitorScheduler(
    IServiceScopeFactory scopes,
    TimeProvider time,
    IOptions<MonitoringOptions> options,
    ILogger<MonitorScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan PruneInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.TickSeconds));
        var nextPrune = time.GetUtcNow();

        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var cycle = scope.ServiceProvider.GetRequiredService<MonitorCheckCycle>();
                await cycle.RunAsync(stoppingToken);

                if (time.GetUtcNow() >= nextPrune)
                {
                    await cycle.PruneAsync(stoppingToken);
                    nextPrune = time.GetUtcNow().Add(PruneInterval);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Monitor check cycle failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
