namespace UptimeMonitor.Web.Monitoring.Entities;

// * Riwayat satu pengecekan yang disimpan ke tabel CheckResults (jadi class biasa, bukan record)
public class CheckResult
{
    // ? "long" karena baris riwayat tumbuh terus dan jauh lebih banyak dari monitor
    public long Id { get; set; }
    // * MonitorId = penghubung ke EndpointMonitor (foreign key), relasinya didefinisikan nanti di AppDbContext
    public int MonitorId { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
    public bool IsUp { get; set; }
    // ? StatusCode dan Error nullable: gagal koneksi/timeout tidak punya status code, dan sukses tidak punya error
    public int? StatusCode { get; set; }
    public int LatencyMs { get; set; }
    public string? Error { get; set; }
}
