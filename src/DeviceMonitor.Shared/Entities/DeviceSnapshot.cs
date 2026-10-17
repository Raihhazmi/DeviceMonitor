namespace DeviceMonitor.Shared.Entities;

/// <summary>
/// Telemetry berkala per perangkat (time-series).
/// Dipakai untuk tren uptime, tren storage, rata-rata CPU/RAM, dsb.
/// </summary>
public class DeviceSnapshot
{
    public long Id { get; set; }

    public int DeviceId { get; set; }
    public Device? Device { get; set; }

    public DateTime CollectedAtUtc { get; set; }

    /// <summary>Online / Offline / Warning / Error pada saat snapshot diambil.</summary>
    public string Status { get; set; } = "Online";

    public double CpuUsagePercent { get; set; }
    public double RamUsedGb { get; set; }
    public double RamTotalGb { get; set; }
    public double RamUsedPercent { get; set; }

    public double DiskTotalGb { get; set; }
    public double DiskUsedGb { get; set; }
    public double DiskFreeGb { get; set; }
    public double DiskUsedPercent { get; set; }

    /// <summary>Sisa ruang partisi C dalam GB.</summary>
    public double CFreeGb { get; set; }

    public double UptimeSeconds { get; set; }

    public long ProcessCount { get; set; }

    /// <summary>Pemakaian CPU oleh proses agent (%).</summary>
    public double AgentCpuPercent { get; set; }

    /// <summary>Memori proses agent dalam MB.</summary>
    public double AgentMemoryMb { get; set; }

    /// <summary>Jumlah data yang tertunda dikirim (agent offline queue).</summary>
    public int OfflineQueueCount { get; set; }

    public long NetworkRxBytes { get; set; }
    public long NetworkTxBytes { get; set; }
}
