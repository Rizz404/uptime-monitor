using Microsoft.EntityFrameworkCore;
using UptimeMonitor.Web.Monitoring;

namespace UptimeMonitor.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<EndpointMonitor> Monitors => Set<EndpointMonitor>();
    public DbSet<CheckResult> CheckResults => Set<CheckResult>();
    public DbSet<Incident> Incidents => Set<Incident>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EndpointMonitor>(monitor =>
        {
            monitor.ToTable("Monitors");
            monitor.Property(m => m.Name).HasMaxLength(100);
            monitor.Property(m => m.Url).HasMaxLength(2000);
        });

        modelBuilder.Entity<CheckResult>(result =>
        {
            result.Property(r => r.Error).HasMaxLength(500);
            result.HasIndex(r => new { r.MonitorId, r.CheckedAt });
            result.HasOne<EndpointMonitor>().WithMany().HasForeignKey(r => r.MonitorId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Incident>(incident =>
        {
            incident.Property(i => i.Cause).HasMaxLength(500);
            incident.HasIndex(i => new { i.MonitorId, i.ResolvedAt });
            incident.HasOne<EndpointMonitor>().WithMany().HasForeignKey(i => i.MonitorId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
