
namespace UptimeMonitor.Web.Monitoring.Entities;

// * Inti aplikasi: satu URL yang dipantau, beserta aturan kapan ia dianggap down.
public class EndpointMonitor
{
    public int Id { get; set; }
    // ! "required": compiler memaksa Name dan Url diisi saat membuat objek, jadi tidak ada monitor tanpa nama/URL
    public required string Name { get; set; }
    public required string Url { get; set; }
    // * Nilai awal (default) bila pengguna tidak mengisi: cek tiap 60 detik, batas tunggu 10 detik, harapkan HTTP 200
    public int IntervalSeconds { get; set; } = 60;
    public int TimeoutSeconds { get; set; } = 10;
    public int ExpectedStatusCode { get; set; } = 200;
    public bool IsPaused { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    // ? "private set": nilai di bawah ini hanya boleh berubah lewat Register(), supaya tidak bisa diubah sembarangan dari luar
    // ? Tipe "?" (nullable) = null artinya belum pernah dicek
    public DateTimeOffset? LastCheckedAt { get; private set; }
    public bool? LastCheckSucceeded { get; private set; }
    public int? LastLatencyMs { get; private set; }
    public int ConsecutiveFailures { get; private set; }
    public bool HasOpenIncident { get; private set; }

    // * Status tidak disimpan, tapi dihitung dari field lain tiap kali dibaca (pola switch, dicek dari atas ke bawah)
    public MonitorStatus Status => this switch
    {
        { IsPaused: true } => MonitorStatus.Paused,
        { LastCheckSucceeded: null } => MonitorStatus.Pending,
        { LastCheckSucceeded: true } => MonitorStatus.Up,
        _ => MonitorStatus.Down
    };

    // * Jatuh tempo = tidak dijeda, dan belum pernah dicek atau interval sejak cek terakhir sudah lewat
    // ? "now" dikirim dari luar (bukan DateTimeOffset.Now) supaya mudah diuji dengan waktu palsu
    public bool IsDue(DateTimeOffset now) =>
        !IsPaused && (LastCheckedAt is null || LastCheckedAt.Value.AddSeconds(IntervalSeconds) <= now);

    // * Aturan bisnis utama: catat hasil satu pengecekan, lalu kembalikan apakah insiden dibuka/ditutup/tidak berubah
    public IncidentChange Register(CheckOutcome outcome, DateTimeOffset checkedAt, int failureThreshold)
    {
        LastCheckedAt = checkedAt;
        LastCheckSucceeded = outcome.IsUp;
        LastLatencyMs = outcome.LatencyMs;

        if (outcome.IsUp)
        {
            // * Satu kali sukses langsung mereset hitungan gagal berturut-turut
            ConsecutiveFailures = 0;
            if (!HasOpenIncident)
            {
                return IncidentChange.None;
            }

            // * Sembuh: ada insiden terbuka dan pengecekan sukses, maka insiden ditutup
            HasOpenIncident = false;
            return IncidentChange.Resolved;
        }

        ConsecutiveFailures++;
        // ! Insiden dibuka SEKALI saja: bila sudah terbuka, atau gagalnya belum mencapai batas, tidak ada perubahan
        if (HasOpenIncident || ConsecutiveFailures < failureThreshold)
        {
            return IncidentChange.None;
        }

        HasOpenIncident = true;
        return IncidentChange.Opened;
    }
}
