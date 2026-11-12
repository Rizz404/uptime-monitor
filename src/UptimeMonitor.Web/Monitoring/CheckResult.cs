namespace UptimeMonitor.Web.Monitoring;

public record CheckOutcome(bool IsUp, int? StatusCode, int LatencyMs, string? Error);

public class CheckResult
{
    public long Id { get; set; }
    public int MonitorId { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
    public bool IsUp { get; set; }
    public int? StatusCode { get; set; }
    public int LatencyMs { get; set; }
    public string? Error { get; set; }
}

public class Incident
{
    public int Id { get; set; }
    public int MonitorId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public required string Cause { get; set; }
}
