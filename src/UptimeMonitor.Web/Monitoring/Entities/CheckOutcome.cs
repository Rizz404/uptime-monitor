namespace UptimeMonitor.Web.Monitoring.Entities;

// * Hasil satu pengecekan, sifatnya sementara (belum disimpan). "record" = data ringkas yang tidak diubah
// ? Dibuat bersamaan dengan EndpointMonitor karena Register(CheckOutcome, ...) langsung membutuhkannya
public record CheckOutcome(bool IsUp, int? StatusCode, int LatencyMs, string? Error);
