using Microsoft.Extensions.Options;

namespace UptimeMonitor.Web.Monitoring;

// * BackgroundService = tugas yang otomatis jalan sepanjang aplikasi hidup, tanpa perlu dipanggil dari halaman/API
// ? IServiceScopeFactory dipakai karena scheduler berumur panjang, sedangkan MonitorCheckCycle (dan DbContext-nya) harus dibuat baru tiap putaran
public class MonitorScheduler(
    IServiceScopeFactory scopes,
    TimeProvider time,
    IOptions<MonitoringOptions> options,
    ILogger<MonitorScheduler> logger) : BackgroundService
{
    private static readonly TimeSpan PruneInterval = TimeSpan.FromHours(1);

    // * Dipanggil sekali saat aplikasi start, dan berhenti saat stoppingToken dibatalkan (aplikasi dimatikan)
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // * PeriodicTimer berdetak tiap TickSeconds
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.TickSeconds));
        var nextPrune = time.GetUtcNow();

        // * do...while: putaran pertama langsung jalan, baru kemudian menunggu detak berikutnya
        do
        {
            try
            {
                // * Scope baru tiap putaran = DbContext baru yang bersih
                await using var scope = scopes.CreateAsyncScope();
                var cycle = scope.ServiceProvider.GetRequiredService<MonitorCheckCycle>();
                await cycle.RunAsync(stoppingToken);

                // * Hapus data lama cukup sejam sekali, tidak perlu tiap detak
                if (time.GetUtcNow() >= nextPrune)
                {
                    await cycle.PruneAsync(stoppingToken);
                    nextPrune = time.GetUtcNow().Add(PruneInterval);
                }
            }
            // ! Error satu putaran hanya dicatat ke log, supaya satu kegagalan tidak mematikan scheduler selamanya
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Monitor check cycle failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
