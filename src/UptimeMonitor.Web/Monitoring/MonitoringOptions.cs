namespace UptimeMonitor.Web.Monitoring;

public class MonitoringOptions
{
    public int TickSeconds { get; set; } = 5;
    public int FailureThreshold { get; set; } = 3;
    public int RetentionDays { get; set; } = 30;
}
