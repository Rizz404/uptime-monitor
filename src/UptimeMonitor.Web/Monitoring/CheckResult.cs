namespace UptimeMonitor.Web.Monitoring;

// * Hasil satu pengecekan, sifatnya sementara (belum disimpan). "record" = data ringkas yang tidak diubah
// ? Dibuat bersamaan dengan EndpointMonitor karena Register(CheckOutcome, ...) langsung membutuhkannya
public record CheckOutcome(bool IsUp, int? StatusCode, int LatencyMs, string? Error);

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
