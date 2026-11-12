using Microsoft.EntityFrameworkCore;
using UptimeMonitor.Web.Data;

namespace UptimeMonitor.Web.Monitoring;

public record MonitorSummary(
    int Id,
    string Name,
    string Url,
    MonitorStatus Status,
    DateTimeOffset? LastCheckedAt,
    int? LastLatencyMs,
    double? Uptime24Hours,
    double? Uptime7Days,
    double? Uptime30Days);

public record IncidentSummary(
    int Id,
    string MonitorName,
    DateTimeOffset StartedAt,
    DateTimeOffset? ResolvedAt,
    string Cause);

public record StatusOverview(IReadOnlyList<MonitorSummary> Monitors, IReadOnlyList<IncidentSummary> Incidents)
{
    public int DownCount => Monitors.Count(monitor => monitor.Status == MonitorStatus.Down);
}

public static class Uptime
{
    public static double? Percentage(int successful, int total) =>
        total == 0 ? null : Math.Round(successful * 100.0 / total, 2);
}

public class StatusQueries(IDbContextFactory<AppDbContext> dbFactory, TimeProvider time)
{
    private const int RecentIncidentCount = 10;

    public async Task<StatusOverview> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = time.GetUtcNow();
        var dayAgo = now.AddDays(-1);
        var weekAgo = now.AddDays(-7);
        var monthAgo = now.AddDays(-30);

        var monitors = await db.Monitors.AsNoTracking().OrderBy(monitor => monitor.Name).ToListAsync(cancellationToken);

        var counts = await db.CheckResults
            .Where(result => result.CheckedAt >= monthAgo)
            .GroupBy(result => result.MonitorId)
            .Select(group => new
            {
                MonitorId = group.Key,
                DayTotal = group.Count(result => result.CheckedAt >= dayAgo),
                DayUp = group.Count(result => result.CheckedAt >= dayAgo && result.IsUp),
                WeekTotal = group.Count(result => result.CheckedAt >= weekAgo),
                WeekUp = group.Count(result => result.CheckedAt >= weekAgo && result.IsUp),
                MonthTotal = group.Count(),
                MonthUp = group.Count(result => result.IsUp)
            })
            .ToDictionaryAsync(row => row.MonitorId, cancellationToken);

        var summaries = monitors.Select(monitor =>
        {
            counts.TryGetValue(monitor.Id, out var count);
            return new MonitorSummary(
                monitor.Id,
                monitor.Name,
                monitor.Url,
                monitor.Status,
                monitor.LastCheckedAt,
                monitor.LastLatencyMs,
                Uptime.Percentage(count?.DayUp ?? 0, count?.DayTotal ?? 0),
                Uptime.Percentage(count?.WeekUp ?? 0, count?.WeekTotal ?? 0),
                Uptime.Percentage(count?.MonthUp ?? 0, count?.MonthTotal ?? 0));
        }).ToList();

        var incidents = await db.Incidents.AsNoTracking()
            .OrderByDescending(incident => incident.StartedAt)
            .Take(RecentIncidentCount)
            .Join(
                db.Monitors,
                incident => incident.MonitorId,
                monitor => monitor.Id,
                (incident, monitor) => new IncidentSummary(
                    incident.Id,
                    monitor.Name,
                    incident.StartedAt,
                    incident.ResolvedAt,
                    incident.Cause))
            .ToListAsync(cancellationToken);

        return new StatusOverview(summaries, incidents.OrderByDescending(incident => incident.StartedAt).ToList());
    }
}
