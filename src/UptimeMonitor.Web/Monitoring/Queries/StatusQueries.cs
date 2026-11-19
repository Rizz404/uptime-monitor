using Microsoft.EntityFrameworkCore;
using UptimeMonitor.Web.Data;

namespace UptimeMonitor.Web.Monitoring.Queries;

// * Kumpulan query baca untuk status page; tidak mengubah data apa pun
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

        // * Satu query untuk semua monitor sekaligus: kelompokkan riwayat 30 hari per monitor, lalu hitung total dan yang sukses untuk 24 jam/7 hari/30 hari
        // ? Cara ini menghindari satu query per monitor (makin banyak monitor, makin lambat)
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
            // * Dictionary: mencari hitungan milik satu monitor jadi lookup langsung berdasarkan Id
            .ToDictionaryAsync(row => row.MonitorId, cancellationToken);

        var summaries = monitors.Select(monitor =>
        {
            // ? Monitor yang belum punya riwayat tidak ada di dictionary, jadi count null dan angkanya dianggap 0
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

        // * Ambil 10 insiden terbaru, lalu Join ke Monitors untuk mendapat nama (tidak ada navigation property, jadi join ditulis manual)
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

        // ! Diurutkan lagi di memori karena urutan hasil Join tidak dijamin sama dengan urutan sebelum Join
        return new StatusOverview(summaries, incidents.OrderByDescending(incident => incident.StartedAt).ToList());
    }
}
