using UptimeMonitor.Web.Monitoring;

namespace UptimeMonitor.Tests;

// * Menguji aturan bisnis Langkah 2 (kapan down, kapan insiden dibuka/ditutup) dan helper Uptime; murni logika, tanpa database atau HTTP
public class EndpointMonitorTests
{
    // ? Data contoh dibuat sekali dan dipakai bersama: waktu tetap (bukan jam sungguhan) membuat hasil test selalu sama
    private const int Threshold = 3;
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly CheckOutcome Success = new(true, 200, 120, null);
    private static readonly CheckOutcome Failure = new(false, 500, 80, "Expected status 200 but received 500");

    private static EndpointMonitor CreateMonitor() => new() { Name = "Example", Url = "https://example.test/" };

    [Fact]
    public void New_monitor_is_pending_and_due()
    {
        var monitor = CreateMonitor();

        Assert.Equal(MonitorStatus.Pending, monitor.Status);
        Assert.True(monitor.IsDue(Start));
    }

    [Fact]
    public void Successful_check_marks_monitor_up_without_incident()
    {
        var monitor = CreateMonitor();

        var change = monitor.Register(Success, Start, Threshold);

        Assert.Equal(IncidentChange.None, change);
        Assert.Equal(MonitorStatus.Up, monitor.Status);
        Assert.Equal(120, monitor.LastLatencyMs);
        Assert.Equal(0, monitor.ConsecutiveFailures);
    }

    [Fact]
    // * Dua gagal (batasnya 3) tetap status Down, tapi insiden belum dibuka
    public void Failures_below_threshold_do_not_open_incident()
    {
        var monitor = CreateMonitor();

        var first = monitor.Register(Failure, Start, Threshold);
        var second = monitor.Register(Failure, Start.AddMinutes(1), Threshold);

        Assert.Equal(IncidentChange.None, first);
        Assert.Equal(IncidentChange.None, second);
        Assert.Equal(MonitorStatus.Down, monitor.Status);
        Assert.False(monitor.HasOpenIncident);
    }

    [Fact]
    public void Incident_opens_once_when_threshold_is_reached()
    {
        var monitor = CreateMonitor();

        // * Lima gagal beruntun: insiden hanya Opened di kegagalan ketiga, sisanya None (tidak dibuka dua kali)
        var changes = Enumerable.Range(0, 5)
            .Select(index => monitor.Register(Failure, Start.AddMinutes(index), Threshold))
            .ToList();

        Assert.Equal(
            [IncidentChange.None, IncidentChange.None, IncidentChange.Opened, IncidentChange.None, IncidentChange.None],
            changes);
        Assert.True(monitor.HasOpenIncident);
        Assert.Equal(5, monitor.ConsecutiveFailures);
    }

    [Fact]
    public void Recovery_resolves_open_incident_and_resets_failures()
    {
        var monitor = CreateMonitor();
        for (var index = 0; index < Threshold; index++)
        {
            monitor.Register(Failure, Start.AddMinutes(index), Threshold);
        }

        var change = monitor.Register(Success, Start.AddMinutes(Threshold), Threshold);

        Assert.Equal(IncidentChange.Resolved, change);
        Assert.False(monitor.HasOpenIncident);
        Assert.Equal(0, monitor.ConsecutiveFailures);
        Assert.Equal(MonitorStatus.Up, monitor.Status);
    }

    [Fact]
    // ! Aturannya "gagal BERTURUT-TURUT": satu sukses di tengah mengembalikan hitungan ke awal
    public void Success_between_failures_restarts_the_failure_count()
    {
        var monitor = CreateMonitor();

        monitor.Register(Failure, Start, Threshold);
        monitor.Register(Failure, Start.AddMinutes(1), Threshold);
        monitor.Register(Success, Start.AddMinutes(2), Threshold);
        var change = monitor.Register(Failure, Start.AddMinutes(3), Threshold);

        Assert.Equal(IncidentChange.None, change);
        Assert.Equal(1, monitor.ConsecutiveFailures);
    }

    [Fact]
    public void Monitor_is_due_only_after_its_interval_has_passed()
    {
        var monitor = CreateMonitor();
        monitor.IntervalSeconds = 60;
        monitor.Register(Success, Start, Threshold);

        // ? Batas tepat diuji: detik ke-59 belum jatuh tempo, detik ke-60 sudah
        Assert.False(monitor.IsDue(Start.AddSeconds(59)));
        Assert.True(monitor.IsDue(Start.AddSeconds(60)));
    }

    [Fact]
    public void Paused_monitor_is_never_due()
    {
        var monitor = CreateMonitor();
        monitor.IsPaused = true;

        Assert.False(monitor.IsDue(Start));
        Assert.Equal(MonitorStatus.Paused, monitor.Status);
    }

    // * [Theory] = satu method dijalankan berulang, sekali per baris [InlineData] (sukses, total, hasil yang diharapkan)
    // ? Baris-barisnya: tanpa data = null, semua sukses = 100, pembulatan 2 desimal, dan 0 sukses = 0 (bukan null)
    [Theory]
    [InlineData(0, 0, null)]
    [InlineData(10, 10, 100.0)]
    [InlineData(1, 3, 33.33)]
    [InlineData(0, 4, 0.0)]
    public void Uptime_percentage_is_rounded_and_empty_without_checks(int successful, int total, double? expected)
    {
        Assert.Equal(expected, Uptime.Percentage(successful, total));
    }
}
