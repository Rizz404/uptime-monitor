namespace UptimeMonitor.Web.Monitoring.Entities;

// ? Pending = belum pernah dicek, Paused = dijeda pengguna (keduanya bukan Up/Down)
public enum MonitorStatus
{
    Pending,
    Up,
    Down,
    Paused
}
