using DeviceMonitor.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeviceMonitor.Web.Data;

public class MonitorDbContext : DbContext
{
    public MonitorDbContext(DbContextOptions<MonitorDbContext> options) : base(options) { }

    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DiskDrive> DiskDrives => Set<DiskDrive>();
    public DbSet<DeviceSnapshot> DeviceSnapshots => Set<DeviceSnapshot>();
    public DbSet<SoftwareInstall> SoftwareInstalls => Set<SoftwareInstall>();
    public DbSet<AgentRun> AgentRuns => Set<AgentRun>();
    public DbSet<ComplianceRule> ComplianceRules => Set<ComplianceRule>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Device>(e =>
        {
            e.ToTable("Devices");
            e.HasIndex(x => x.Hostname).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Department);
            e.Property(x => x.Hostname).HasMaxLength(128).IsRequired();
            e.Property(x => x.AssetTag).HasMaxLength(64);
            e.Property(x => x.DeviceType).HasMaxLength(32);
            e.Property(x => x.Manufacturer).HasMaxLength(128);
            e.Property(x => x.Model).HasMaxLength(128);
            e.Property(x => x.SerialNumber).HasMaxLength(128);
            e.Property(x => x.OsName).HasMaxLength(128);
            e.Property(x => x.OsVersion).HasMaxLength(64);
            e.Property(x => x.OsBuild).HasMaxLength(32);
            e.Property(x => x.OsArchitecture).HasMaxLength(16);
            e.Property(x => x.BiosVendor).HasMaxLength(128);
            e.Property(x => x.BiosVersion).HasMaxLength(64);
            e.Property(x => x.CpuName).HasMaxLength(128);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.Property(x => x.MacAddress).HasMaxLength(32);
            e.Property(x => x.Domain).HasMaxLength(128);
            e.Property(x => x.Department).HasMaxLength(64);
            e.Property(x => x.Location).HasMaxLength(64);
            e.Property(x => x.AssignedUser).HasMaxLength(128);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.AgentVersion).HasMaxLength(32);
            e.Property(x => x.RamTotalGb).HasPrecision(10, 2);
            e.Property(x => x.TotalDiskGb).HasPrecision(12, 2);
            e.Property(x => x.UsedDiskGb).HasPrecision(12, 2);
            e.Property(x => x.UptimeSeconds).HasPrecision(18, 2);

            e.HasMany(x => x.Disks).WithOne(d => d.Device!)
                .HasForeignKey(d => d.DeviceId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Software).WithOne(s => s.Device!)
                .HasForeignKey(s => s.DeviceId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DiskDrive>(e =>
        {
            e.ToTable("DiskDrives");
            e.HasIndex(x => new { x.DeviceId, x.DriveLetter }).IsUnique();
            e.HasIndex(x => x.DriveLetter);
            e.Property(x => x.DriveLetter).HasMaxLength(8).IsRequired();
            e.Property(x => x.Label).HasMaxLength(128);
            e.Property(x => x.DriveType).HasMaxLength(32);
            e.Property(x => x.MediaType).HasMaxLength(32);
            e.Property(x => x.FileSystem).HasMaxLength(16);
            e.Property(x => x.HealthStatus).HasMaxLength(16);
            e.Property(x => x.SizeGb).HasPrecision(12, 2);
            e.Property(x => x.UsedGb).HasPrecision(12, 2);
            e.Property(x => x.FreeGb).HasPrecision(12, 2);
            e.Property(x => x.UsedPercent).HasPrecision(6, 2);
        });

        b.Entity<DeviceSnapshot>(e =>
        {
            e.ToTable("DeviceSnapshots");
            e.HasIndex(x => new { x.DeviceId, x.CollectedAtUtc });
            e.HasIndex(x => x.CollectedAtUtc);
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.CpuUsagePercent).HasPrecision(6, 2);
            e.Property(x => x.RamUsedGb).HasPrecision(10, 2);
            e.Property(x => x.RamTotalGb).HasPrecision(10, 2);
            e.Property(x => x.RamUsedPercent).HasPrecision(6, 2);
            e.Property(x => x.DiskTotalGb).HasPrecision(12, 2);
            e.Property(x => x.DiskUsedGb).HasPrecision(12, 2);
            e.Property(x => x.DiskFreeGb).HasPrecision(12, 2);
            e.Property(x => x.DiskUsedPercent).HasPrecision(6, 2);
            e.Property(x => x.CFreeGb).HasPrecision(12, 2);
            e.Property(x => x.UptimeSeconds).HasPrecision(18, 2);
            e.Property(x => x.AgentCpuPercent).HasPrecision(6, 2);
            e.Property(x => x.AgentMemoryMb).HasPrecision(10, 2);
        });

        b.Entity<SoftwareInstall>(e =>
        {
            e.ToTable("SoftwareInstalls");
            e.HasIndex(x => new { x.DeviceId, x.Name, x.Version }).IsUnique();
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.Publisher);
            e.HasIndex(x => x.ComplianceStatus);
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();
            e.Property(x => x.Version).HasMaxLength(64).IsRequired();
            e.Property(x => x.Publisher).HasMaxLength(256);
            e.Property(x => x.Scope).HasMaxLength(32);
            e.Property(x => x.ComplianceStatus).HasMaxLength(32);
            e.Property(x => x.SizeMb).HasPrecision(12, 2);
        });

        b.Entity<AgentRun>(e =>
        {
            e.ToTable("AgentRuns");
            e.HasIndex(x => x.StartedAtUtc);
            e.HasIndex(x => new { x.DeviceId, x.StartedAtUtc });
            e.Property(x => x.Status).HasMaxLength(16);
            e.Property(x => x.AgentVersion).HasMaxLength(32);
            e.Property(x => x.CollectorsRun).HasMaxLength(256);
            e.Property(x => x.ErrorMessage).HasMaxLength(1024);
            e.Property(x => x.TriggerSource).HasMaxLength(32);
            e.Property(x => x.DurationMs).HasPrecision(12, 2);
            e.Property(x => x.CpuPercent).HasPrecision(6, 2);
            e.Property(x => x.MemoryMb).HasPrecision(10, 2);
        });

        b.Entity<ComplianceRule>(e =>
        {
            e.ToTable("ComplianceRules");
            e.HasIndex(x => x.SoftwareName).IsUnique();
            e.Property(x => x.SoftwareName).HasMaxLength(256).IsRequired();
            e.Property(x => x.MinimumVersion).HasMaxLength(64);
            e.Property(x => x.ProhibitedVersions).HasMaxLength(512);
            e.Property(x => x.Notes).HasMaxLength(512);
        });
    }
}
