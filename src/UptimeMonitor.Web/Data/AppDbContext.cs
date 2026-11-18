using Microsoft.EntityFrameworkCore;
using UptimeMonitor.Web.Monitoring;

namespace UptimeMonitor.Web.Data;

// * DbContext = pintu masuk EF Core ke database. Options (provider, connection string) disuntik dari Program.cs
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // * Satu DbSet = satu tabel, dan tiga class dari Langkah 2 menjadi tiga tabel
    // ? Memakai "=> Set<T>()" (bukan { get; set; }) supaya properti tidak pernah null dan compiler tidak memberi warning
    public DbSet<EndpointMonitor> Monitors => Set<EndpointMonitor>();
    public DbSet<CheckResult> CheckResults => Set<CheckResult>();
    public DbSet<Incident> Incidents => Set<Incident>();

    // * Tempat mengatur detail tabel yang tidak bisa ditebak EF dari bentuk class-nya saja
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EndpointMonitor>(monitor =>
        {
            monitor.ToTable("Monitors");
            // ! Tanpa HasMaxLength, string menjadi nvarchar(max) di SQL Server (boros dan tidak bisa diindeks dengan baik)
            monitor.Property(m => m.Name).HasMaxLength(100);
            monitor.Property(m => m.Url).HasMaxLength(2000);
        });

        modelBuilder.Entity<CheckResult>(result =>
        {
            result.Property(r => r.Error).HasMaxLength(500);
            // * Index gabungan: mempercepat query "riwayat satu monitor, urut berdasarkan waktu"
            result.HasIndex(r => new { r.MonitorId, r.CheckedAt });
            // ? HasOne/WithMany tanpa parameter: relasinya cukup lewat MonitorId, tanpa navigation property di class
            // ! Cascade: menghapus monitor otomatis menghapus semua riwayatnya
            result.HasOne<EndpointMonitor>().WithMany().HasForeignKey(r => r.MonitorId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Incident>(incident =>
        {
            incident.Property(i => i.Cause).HasMaxLength(500);
            // * Index ini untuk mencari insiden yang masih terbuka (ResolvedAt null) per monitor
            incident.HasIndex(i => new { i.MonitorId, i.ResolvedAt });
            incident.HasOne<EndpointMonitor>().WithMany().HasForeignKey(i => i.MonitorId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
