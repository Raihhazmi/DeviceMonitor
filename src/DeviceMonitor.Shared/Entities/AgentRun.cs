namespace DeviceMonitor.Shared.Entities;

/// <summary>
/// Log setiap eksekusi agent collector (riwayat aktivitas agent).
/// </summary>
public class AgentRun
{
    public long Id { get; set; }

    public int DeviceId { get; set; }
    public Device? Device { get; set; }

    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }

    /// <summary>Durasi eksekusi dalam milidetik.</summary>
    public double DurationMs { get; set; }

    /// <summary>Success / Failed / Partial / Queued.</summary>
    public string Status { get; set; } = "Success";

    public string AgentVersion { get; set; } = "1.0.0";

    /// <summary>Collector yang dijalankan, mis. "hardware,disks,software,telemetry".</summary>
    public string? CollectorsRun { get; set; }

    public int ItemsSent { get; set; }

    /// <summary>Jumlah payload yang tertunda di queue lokal agent saat run ini.</summary>
    public int OfflineQueueCount { get; set; }

    public double CpuPercent { get; set; }
    public double MemoryMb { get; set; }

    public string? ErrorMessage { get; set; }
    public string? TriggerSource { get; set; }
}
