using UptimeMonitor.Web.Monitoring.Entities;

namespace UptimeMonitor.Web.Monitoring.Queries;

// * Seluruh isi status page; DownCount dihitung saat dibaca, untuk judul seperti "2 dari 5 layanan down"
public record StatusOverview(IReadOnlyList<MonitorSummary> Monitors, IReadOnlyList<IncidentSummary> Incidents)
{
    public int DownCount => Monitors.Count(monitor => monitor.Status == MonitorStatus.Down);
}


