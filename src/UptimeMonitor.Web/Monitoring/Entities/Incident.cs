namespace UptimeMonitor.Web.Monitoring.Entities;

// * Satu periode down, dibuka saat gagal berturut-turut mencapai batas dan ditutup saat pulih
public class Incident
{
    public int Id { get; set; }
    public int MonitorId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    // ? ResolvedAt null = insiden masih berlangsung
    public DateTimeOffset? ResolvedAt { get; set; }
    public required string Cause { get; set; }
}
