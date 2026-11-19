namespace UptimeMonitor.Web.Monitoring.Entities;

// * Hasil Register(): memberi tahu pemanggil apa yang harus dilakukan (buka/tutup insiden di database)
public enum IncidentChange
{
    None,
    Opened,
    Resolved
}
