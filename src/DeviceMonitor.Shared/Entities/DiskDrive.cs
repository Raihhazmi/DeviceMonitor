namespace DeviceMonitor.Shared.Entities;

/// <summary>
/// Kondisi partisi/disk terkini per perangkat.
/// Bersifat "latest state" (unique DeviceId+DriveLetter) - bukan penjumlahan riwayat.
/// Riwayat tren disimpan di DeviceSnapshot.
/// </summary>
public class DiskDrive
{
    public long Id { get; set; }

    public int DeviceId { get; set; }
    public Device? Device { get; set; }

    /// <summary>C, D, E, ...</summary>
    public string DriveLetter { get; set; } = "C";

    public string? Label { get; set; }

    /// <summary>Fixed / Removable / Network / Optical.</summary>
    public string DriveType { get; set; } = "Fixed";

    /// <summary>SSD / HDD / NVMe / Unknown.</summary>
    public string MediaType { get; set; } = "Unknown";

    public string? FileSystem { get; set; }

    public double SizeGb { get; set; }
    public double UsedGb { get; set; }
    public double FreeGb { get; set; }
    public double UsedPercent { get; set; }

    /// <summary>Healthy / Warning / Critical / Unknown.</summary>
    public string HealthStatus { get; set; } = "Unknown";

    public DateTime CollectedAtUtc { get; set; } = DateTime.UtcNow;
}
