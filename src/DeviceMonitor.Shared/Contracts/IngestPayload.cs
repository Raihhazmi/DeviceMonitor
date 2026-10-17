namespace DeviceMonitor.Shared.Contracts;

/// <summary>
/// Payload yang dikirim agent (atau tools lain) ke POST /api/ingest.
/// Semua bagian opsional kecuali Hostname - agent boleh mengirim sebagian data saja.
/// </summary>
public class IngestPayload
{
    public DevicePayload? Device { get; set; }
    public List<DiskPayload>? Disks { get; set; }
    public List<SoftwarePayload>? Software { get; set; }
    public TelemetryPayload? Telemetry { get; set; }
    public AgentRunPayload? AgentRun { get; set; }

    /// <summary>
    /// true = daftar Software adalah inventaris lengkap perangkat, sehingga software yang
    /// tidak ikut terkirim akan ditandai IsInstalled = false (uninstall terdeteksi).
    /// </summary>
    public bool IsFullSoftwareInventory { get; set; } = true;
}

public class DevicePayload
{
    public string Hostname { get; set; } = string.Empty;
    public string? AssetTag { get; set; }
    public string? DeviceType { get; set; }
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

    public double RamTotalGb { get; set; }

    public string? IpAddress { get; set; }
    public string? MacAddress { get; set; }
    public string? Domain { get; set; }

    public string? Department { get; set; }
    public string? Location { get; set; }
    public string? AssignedUser { get; set; }

    public string? AgentVersion { get; set; }

    /// <summary>Uptime perangkat dalam detik.</summary>
    public double? UptimeSeconds { get; set; }

    public DateTime? CollectedAtUtc { get; set; }
}

public class DiskPayload
{
    public string DriveLetter { get; set; } = "C";
    public string? Label { get; set; }
    public string? DriveType { get; set; }
    public string? MediaType { get; set; }
    public string? FileSystem { get; set; }
    public double SizeGb { get; set; }
    public double FreeGb { get; set; }
    public double? UsedGb { get; set; }
    public string? HealthStatus { get; set; }
}

public class SoftwarePayload
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "unknown";
    public string? Publisher { get; set; }
    public DateTime? InstallDate { get; set; }
    public double SizeMb { get; set; }
    public string? Scope { get; set; }
}

public class TelemetryPayload
{
    public DateTime? CollectedAtUtc { get; set; }
    public string? Status { get; set; }
    public double CpuUsagePercent { get; set; }
    public double RamUsedGb { get; set; }
    public double? RamTotalGb { get; set; }
    public double? RamUsedPercent { get; set; }
    public double? DiskTotalGb { get; set; }
    public double? DiskUsedGb { get; set; }
    public double? DiskFreeGb { get; set; }
    public double? DiskUsedPercent { get; set; }
    public double? CFreeGb { get; set; }
    public double? UptimeSeconds { get; set; }
    public long? ProcessCount { get; set; }
    public double? AgentCpuPercent { get; set; }
    public double? AgentMemoryMb { get; set; }
    public int? OfflineQueueCount { get; set; }
    public long? NetworkRxBytes { get; set; }
    public long? NetworkTxBytes { get; set; }
}

public class AgentRunPayload
{
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public double? DurationMs { get; set; }
    public string? Status { get; set; }
    public string? AgentVersion { get; set; }
    public string? CollectorsRun { get; set; }
    public int? ItemsSent { get; set; }
    public int? OfflineQueueCount { get; set; }
    public double? CpuPercent { get; set; }
    public double? MemoryMb { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TriggerSource { get; set; }
}

public class IngestResult
{
    public bool Success { get; set; }
    public int DeviceId { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public string Status { get; set; } = "ok";
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public double ProcessedMs { get; set; }
    public int DisksUpserted { get; set; }
    public int SoftwareUpserted { get; set; }
    public int SoftwareRemoved { get; set; }
    public List<string> Warnings { get; set; } = new();
    public string? Error { get; set; }
}
