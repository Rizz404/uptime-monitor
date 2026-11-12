using Microsoft.EntityFrameworkCore;
using UptimeMonitor.Web.Data;

namespace UptimeMonitor.Web.Monitoring;

public class MonitorService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider time)
{
    public async Task<List<EndpointMonitor>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Monitors.AsNoTracking().OrderBy(monitor => monitor.Name).ToListAsync(cancellationToken);
    }

    public async Task<EndpointMonitor?> FindAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Monitors.AsNoTracking().FirstOrDefaultAsync(monitor => monitor.Id == id, cancellationToken);
    }

    public async Task<EndpointMonitor> CreateAsync(MonitorInput input, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var monitor = new EndpointMonitor { Name = input.Name, Url = input.Url, CreatedAt = time.GetUtcNow() };
        input.ApplyTo(monitor);
        db.Monitors.Add(monitor);
        await db.SaveChangesAsync(cancellationToken);
        return monitor;
    }

    public async Task<bool> UpdateAsync(int id, MonitorInput input, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var monitor = await db.Monitors.FindAsync([id], cancellationToken);
        if (monitor is null)
        {
            return false;
        }

        input.ApplyTo(monitor);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> SetPausedAsync(int id, bool paused, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var updated = await db.Monitors
            .Where(monitor => monitor.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(monitor => monitor.IsPaused, paused), cancellationToken);
        return updated > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var deleted = await db.Monitors.Where(monitor => monitor.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    public async Task<List<CheckResult>> RecentChecksAsync(int id, int take, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.CheckResults.AsNoTracking()
            .Where(result => result.MonitorId == id)
            .OrderByDescending(result => result.CheckedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);
    }
}
