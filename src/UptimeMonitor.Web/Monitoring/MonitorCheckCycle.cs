using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using UptimeMonitor.Web.Data;

namespace UptimeMonitor.Web.Monitoring;

// * Satu putaran kerja: ambil monitor jatuh tempo, cek semuanya, simpan hasil, buka/tutup insiden. Scheduler yang memanggilnya berulang
// ? TimeProvider (bukan DateTime.Now) supaya test bisa memakai waktu palsu; IOptions membungkus MonitoringOptions
public class MonitorCheckCycle(
    AppDbContext db,
    EndpointChecker checker,
    TimeProvider time,
    IOptions<MonitoringOptions> options)
{
    // ! Harus sama dengan HasMaxLength(500) di AppDbContext, kalau lebih panjang SQL Server menolak simpan
    private const int MaxErrorLength = 500;

    // * Mengembalikan jumlah monitor yang dicek pada putaran ini
    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var active = await db.Monitors.Where(monitor => !monitor.IsPaused).ToListAsync(cancellationToken);
        // ? IsDue disaring di memori (setelah ToList) karena method C# tidak bisa diterjemahkan EF menjadi SQL
        var due = active.Where(monitor => monitor.IsDue(now)).ToList();
        if (due.Count == 0)
        {
            return 0;
        }

        // * Semua cek dijalankan bersamaan (bukan satu-satu); urutan hasil sama dengan urutan "due"
        var outcomes = await Task.WhenAll(due.Select(monitor => checker.CheckAsync(monitor, cancellationToken)));
        var checkedAt = time.GetUtcNow();

        // ! Dicatat satu-satu, bukan paralel: DbContext tidak aman dipakai banyak thread sekaligus
        for (var index = 0; index < due.Count; index++)
        {
            await RecordAsync(due[index], outcomes[index], checkedAt, cancellationToken);
        }

        // * Perubahan pada objek monitor (hasil Register) ikut tersimpan karena EF melacaknya; semua disimpan sekali dalam satu transaksi
        await db.SaveChangesAsync(cancellationToken);
        return due.Count;
    }

    // * Hapus riwayat yang lebih tua dari RetentionDays supaya tabel tidak membengkak
    // ? ExecuteDeleteAsync langsung menjalankan DELETE di SQL tanpa memuat barisnya ke memori
    public Task<int> PruneAsync(CancellationToken cancellationToken)
    {
        var cutoff = time.GetUtcNow().AddDays(-options.Value.RetentionDays);
        return db.CheckResults.Where(result => result.CheckedAt < cutoff).ExecuteDeleteAsync(cancellationToken);
    }

    private async Task RecordAsync(
        EndpointMonitor monitor,
        CheckOutcome outcome,
        DateTimeOffset checkedAt,
        CancellationToken cancellationToken)
    {
        // * Pesan error dipotong agar muat di kolom
        var error = outcome.Error is { Length: > MaxErrorLength } ? outcome.Error[..MaxErrorLength] : outcome.Error;

        // * Riwayat selalu ditambah, apa pun hasilnya (belum tersimpan sampai SaveChangesAsync)
        db.CheckResults.Add(new CheckResult
        {
            MonitorId = monitor.Id,
            CheckedAt = checkedAt,
            IsUp = outcome.IsUp,
            StatusCode = outcome.StatusCode,
            LatencyMs = outcome.LatencyMs,
            Error = error
        });

        // * Aturan bisnis dari Langkah 2 dipakai di sini: hasilnya menentukan perlu buka/tutup insiden atau tidak
        var change = monitor.Register(outcome, checkedAt, options.Value.FailureThreshold);

        if (change == IncidentChange.Opened)
        {
            // * Penyebab insiden = pesan error pengecekan yang memicunya
            db.Incidents.Add(new Incident
            {
                MonitorId = monitor.Id,
                StartedAt = checkedAt,
                Cause = error ?? string.Empty
            });
        }
        else if (change == IncidentChange.Resolved)
        {
            // * Cari insiden yang masih terbuka (ResolvedAt null) milik monitor ini, lalu tutup
            var open = await db.Incidents
                .Where(incident => incident.MonitorId == monitor.Id && incident.ResolvedAt == null)
                .ToListAsync(cancellationToken);

            foreach (var incident in open)
            {
                incident.ResolvedAt = checkedAt;
            }
        }
    }
}
