using System.ComponentModel.DataAnnotations;
using UptimeMonitor.Web.Localization;
using UptimeMonitor.Web.Monitoring.Entities;

namespace UptimeMonitor.Web.Monitoring.Services;

// * Bentuk data dari form/API beserta aturan validasinya; sengaja dipisah dari EndpointMonitor agar input pengguna tidak langsung menyentuh entity
public class MonitorInput
{
    // ? ErrorMessageResource... pada atribut = ambil pesan dari ValidationMessages (resx) alih-alih teks tetap
    // * Batas 100 dan 2000 sama dengan HasMaxLength di AppDbContext, jadi input yang lolos pasti muat di kolom; StringLength memakai pesan MaxLength
    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.Validation_Required))]
    [StringLength(100, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.Validation_MaxLength))]
    // ? Diberi "= string.Empty" (bukan required) karena form membuat objek kosong dulu, baru diisi pengguna
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.Validation_Required))]
    [StringLength(2000, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.Validation_MaxLength))]
    [HttpUrl]
    public string Url { get; set; } = string.Empty;

    // * Batas wajar: interval 30 detik sampai 1 hari, timeout 1 sampai 60 detik, status code HTTP 100 sampai 599
    // * Nilai awal sama dengan default di EndpointMonitor
    [Range(30, 86400, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.Validation_Range))]
    public int IntervalSeconds { get; set; } = 60;

    [Range(1, 60, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.Validation_Range))]
    public int TimeoutSeconds { get; set; } = 10;

    [Range(100, 599, ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = nameof(ValidationMessages.Validation_Range))]
    public int ExpectedStatusCode { get; set; } = 200;

    // * Arah entity ke input: mengisi form edit dengan data monitor yang ada
    public static MonitorInput From(EndpointMonitor monitor) => new()
    {
        Name = monitor.Name,
        Url = monitor.Url,
        IntervalSeconds = monitor.IntervalSeconds,
        TimeoutSeconds = monitor.TimeoutSeconds,
        ExpectedStatusCode = monitor.ExpectedStatusCode
    };

    // * Arah input ke entity; hanya field yang boleh diubah pengguna yang disalin (status dan riwayat tidak tersentuh)
    // ! Trim() membuang spasi di awal/akhir, supaya " https://x " tersimpan sebagai "https://x"
    public void ApplyTo(EndpointMonitor monitor)
    {
        monitor.Name = Name.Trim();
        monitor.Url = Url.Trim();
        monitor.IntervalSeconds = IntervalSeconds;
        monitor.TimeoutSeconds = TimeoutSeconds;
        monitor.ExpectedStatusCode = ExpectedStatusCode;
    }
}
