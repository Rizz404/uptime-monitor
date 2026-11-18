namespace UptimeMonitor.Web.Monitoring;

// * Setting yang bisa diubah tanpa menyentuh kode: nilainya dibaca dari section "Monitoring" di appsettings.json
// ? Nilai di bawah adalah default bila section/key itu tidak ada di appsettings
public class MonitoringOptions
{
    // * Seberapa sering scheduler "bangun" untuk mencari monitor yang jatuh tempo
    public int TickSeconds { get; set; } = 5;
    // * Batas gagal berturut-turut sebelum insiden dibuka (nilai ini yang dikirim ke Register)
    public int FailureThreshold { get; set; } = 3;
    // * Riwayat pengecekan yang lebih tua dari ini dihapus
    public int RetentionDays { get; set; } = 30;
}
