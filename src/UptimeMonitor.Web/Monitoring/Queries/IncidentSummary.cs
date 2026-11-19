namespace UptimeMonitor.Web.Monitoring.Queries;

// * MonitorName ikut dibawa supaya daftar insiden bisa menampilkan nama tanpa query tambahan
public record IncidentSummary(
    int Id,
    string MonitorName,
    DateTimeOffset StartedAt,
    DateTimeOffset? ResolvedAt,
    string Cause);
