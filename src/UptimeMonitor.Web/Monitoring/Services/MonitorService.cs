using Microsoft.EntityFrameworkCore;
using UptimeMonitor.Web.Data;
using UptimeMonitor.Web.Monitoring.Entities;

namespace UptimeMonitor.Web.Monitoring.Services;

// * Semua operasi atas monitor (daftar, tambah, ubah, jeda, hapus, riwayat), dipakai oleh API dan halaman admin
// ? Tiap method membuat DbContext sendiri lewat factory, dibuang di akhir method (alasannya ada di Program.cs)
public class MonitorService(IDbContextFactory<AppDbContext> dbFactory, TimeProvider time)
{
    // ? "= default" membuat cancellationToken opsional; pemanggil boleh tidak mengirimnya
    // * AsNoTracking: data hanya dibaca, jadi EF tidak perlu melacak perubahannya (lebih ringan)
    public async Task<List<EndpointMonitor>> ListAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Monitors.AsNoTracking().OrderBy(monitor => monitor.Name).ToListAsync(cancellationToken);
    }

    // * null berarti tidak ditemukan; pemanggil yang memutuskan responsnya (misalnya 404)
    public async Task<EndpointMonitor?> FindAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Monitors.AsNoTracking().FirstOrDefaultAsync(monitor => monitor.Id == id, cancellationToken);
    }

    public async Task<EndpointMonitor> CreateAsync(MonitorInput input, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        // ? Name dan Url diisi dulu karena "required"; nilai finalnya (sudah di-Trim) ditimpa oleh ApplyTo
        var monitor = new EndpointMonitor { Name = input.Name, Url = input.Url, CreatedAt = time.GetUtcNow() };
        input.ApplyTo(monitor);
        db.Monitors.Add(monitor);
        await db.SaveChangesAsync(cancellationToken);
        return monitor;
    }

    // * Tidak memakai AsNoTracking: objek dimuat dengan pelacakan, jadi perubahan dari ApplyTo otomatis tersimpan oleh SaveChangesAsync
    public async Task<bool> UpdateAsync(int id, MonitorInput input, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var monitor = await db.Monitors.FindAsync([id], cancellationToken);
        if (monitor is null)
        {
            return false;
        }

        input.ApplyTo(monitor);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // * ExecuteUpdateAsync: langsung UPDATE satu kolom di SQL tanpa memuat objek dulu (pasangan ExecuteDeleteAsync)
    // ? Mengembalikan bool: true bila ada baris yang terpengaruh, false bila id tidak ada
    public async Task<bool> SetPausedAsync(int id, bool paused, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var updated = await db.Monitors
            .Where(monitor => monitor.Id == id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(monitor => monitor.IsPaused, paused), cancellationToken);
        return updated > 0;
    }

    // * Riwayat dan insiden milik monitor ikut terhapus oleh cascade yang diatur di AppDbContext
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var deleted = await db.Monitors.Where(monitor => monitor.Id == id).ExecuteDeleteAsync(cancellationToken);
        return deleted > 0;
    }

    // * Pengecekan terbaru dulu (urut turun menurut waktu)
    // ! Math.Clamp membatasi jumlah antara 1 sampai 200, supaya tidak bisa meminta jutaan baris sekaligus
    public async Task<List<CheckResult>> RecentChecksAsync(int id, int take, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.CheckResults.AsNoTracking()
            .Where(result => result.MonitorId == id)
            .OrderByDescending(result => result.CheckedAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);
    }
}
