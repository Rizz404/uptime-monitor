using UptimeMonitor.Web.Monitoring.Entities;

namespace UptimeMonitor.Web.Monitoring.Queries;

// * Record ini adalah bentuk data khusus untuk tampilan: hanya field yang dibutuhkan status page, bukan entity penuh
// ? Uptime null = belum ada data pengecekan pada rentang itu
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
