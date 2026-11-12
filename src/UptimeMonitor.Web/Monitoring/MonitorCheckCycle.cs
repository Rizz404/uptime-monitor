using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UptimeMonitor.Web.Data;

namespace UptimeMonitor.Web.Monitoring;

public class MonitorCheckCycle(
    AppDbContext db,
    EndpointChecker checker,
    TimeProvider time,
    IOptions<MonitoringOptions> options)
{
    private const int MaxErrorLength = 500;

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var active = await db.Monitors.Where(monitor => !monitor.IsPaused).ToListAsync(cancellationToken);
        var due = active.Where(monitor => monitor.IsDue(now)).ToList();
        if (due.Count == 0)
        {
            return 0;
        }

        var outcomes = await Task.WhenAll(due.Select(monitor => checker.CheckAsync(monitor, cancellationToken)));
        var checkedAt = time.GetUtcNow();

        for (var index = 0; index < due.Count; index++)
        {
            await RecordAsync(due[index], outcomes[index], checkedAt, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return due.Count;
    }

    public Task<int> PruneAsync(CancellationToken cancellationToken)
    {
        var cutoff = time.GetUtcNow().AddDays(-options.Value.RetentionDays);
        return db.CheckResults.Where(result => result.CheckedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
    }

    private async Task RecordAsync(
        EndpointMonitor monitor,
        CheckOutcome outcome,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        var error = outcome.Error is { Length: > MaxErrorLength } ? outcome.Error[..MaxErrorLength] : outcome.Error;

        db.CheckResults.Add(new CheckResult
        {
            MonitorId = monitor.Id,
            CheckedAt = checkedAt,
            IsUp = outcome.IsUp,
            StatusCode = outcome.StatusCode,
            LatencyMs = outcome.LatencyMs,
            Error = error
        });

        var change = monitor.Register(outcome, checkedAt, options.Value.FailureThreshold);

        if (change == IncidentChange.Opened)
        {
            db.Incidents.Add(new Incident
            {
                MonitorId = monitor.Id,
                StartedAt = checkedAt,
                Cause = error ?? string.Empty
            });
        }
        else if (change == IncidentChange.Resolved)
        {
            var open = await db.Incidents
                .Where(incident => incident.MonitorId == monitor.Id && incident.ResolvedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var incident in open)
            {
                incident.ResolvedAt = checkedAt;
            }
        }
    }
}
