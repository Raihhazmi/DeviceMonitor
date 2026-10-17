namespace DeviceMonitor.Shared.Entities;

/// <summary>
/// Master data perangkat (PC / Laptop) kantor.
/// Satu baris = satu perangkat, di-update (upsert) setiap kali agent melapor.
/// </summary>
public class Device
{
    public int Id { get; set; }

    /// <summary>Nama komputer (hostname) - kunci unik untuk upsert.</summary>
    public string Hostname { get; set; } = string.Empty;

    public string? AssetTag { get; set; }

    /// <summary>Desktop / Laptop / Workstation / Server.</summary>
    public string DeviceType { get; set; } = "Desktop";

    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }

    public string? OsName { get; set; }
    public string? OsVersion { get; set; }
    public string? OsBuild { get; set; }
    public string? OsArchitecture { get; set; }

    public string? BiosVendor { get; set; }
    public string? BiosVersion { get; set; }
    public DateTime? BiosReleaseDate { get; set; }

    public string? CpuName { get; set; }
    public int CpuCores { get; set; }
    public int CpuLogicalProcessors { get; set; }

    /// <summary>Total RAM terpasang dalam GB.</summary>
    public double RamTotalGb { get; set; }

    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public string? Domain { get; set; }

    public string? Department { get; set; }
    public string? Location { get; set; }
    public string? AssignedUser { get; set; }

    /// <summary>Online / Offline / Warning / Error.</summary>
    public string Status { get; set; } = "Offline";

    public string AgentVersion { get; set; } = "1.0.0";

    /// <summary>Laporan agent terakhir yang berhasil diterima server.</summary>
    public DateTime? LastSeenUtc { get; set; }

    public DateTime FirstSeenUtc { get; set; }

    /// <summary>Total kapasitas disk (semua partisi) dalam GB.</summary>
    public double TotalDiskGb { get; set; }

    /// <summary>Total ruang terpakai (semua partisi) dalam GB.</summary>
    public double UsedDiskGb { get; set; }

    /// <summary>Uptime perangkat saat snapshot terakhir, dalam detik.</summary>
    public double UptimeSeconds { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public List<DiskDrive> Disks { get; set; } = new();
    public List<SoftwareInstall> Software { get; set; } = new();
}
