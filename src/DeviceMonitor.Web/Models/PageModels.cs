namespace DeviceMonitor.Web.Models;

// ---------- 02 Hardware & System Inventory ----------

public class NameCount
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percent { get; set; }
    public string? Sub { get; set; }
    public string Tone { get; set; } = "info";
}

/// <summary>Baris inventaris hardware lengkap untuk tabel spesifikasi.</summary>
public class HardwareRow
{
    public string Hostname { get; set; } = string.Empty;
    public string DeviceType { get; set; } = "Desktop";
    public string? AssetTag { get; set; }
    public string? Manufacturer { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? CpuName { get; set; }
    public int CpuCores { get; set; }
    public double RamTotalGb { get; set; }
    public double RamUsedPercent { get; set; }
    public string? OsName { get; set; }
    public string? OsBuild { get; set; }
    public string? BiosVersion { get; set; }
    public string? Department { get; set; }
    public string? Location { get; set; }
    public string? IpAddress { get; set; }
    public string Status { get; set; } = "Offline";
}

public class RamBucket
{
    public string Bucket { get; set; } = string.Empty;   // 4 GB, 8 GB, 16 GB, 32 GB, 64 GB
    public int Count { get; set; }
    public double Percent { get; set; }
    public double TotalGb { get; set; }
    public string Note { get; set; } = string.Empty;      // mis. "perlu upgrade"
    public string Tone { get; set; } = "info";
}

public class HardwareViewModel
{
    public List<KpiCard> Kpis { get; set; } = new();

    public ChartModel ModelDistribution { get; set; } = new();
    public List<NameCount> Models { get; set; } = new();

    public List<RamBucket> RamBuckets { get; set; } = new();
    public ChartModel RamVarietyChart { get; set; } = new();   // variasi kapasitas RAM (donut)
    public ChartModel RamDistributionChart { get; set; } = new(); // distribusi unit per bucket

    public double AvgRamTotalGb { get; set; }
    public double AvgRamUsedGb { get; set; }
    public double AvgRamUsedPercent { get; set; }
    public double FleetRamTotalGb { get; set; }
    public int LowRamDevices { get; set; }
    public int HighRamPressureDevices { get; set; }
    public ChartModel RamPressureChart { get; set; } = new();  // rata-rata RAM terpakai per departemen

    public ChartModel OsDistribution { get; set; } = new();
    public List<NameCount> OsSummary { get; set; } = new();
    public List<NameCount> WindowsVersions { get; set; } = new();
    public int UnsupportedWindowsCount { get; set; }

    public List<NameCount> BiosVersions { get; set; } = new();
    public ChartModel BiosChart { get; set; } = new();
    public int BiosVariantCount { get; set; }
    public string DominantBios { get; set; } = "-";

    public List<NameCount> CpuSummary { get; set; } = new();
    public List<HardwareRow> Inventory { get; set; } = new();
    public List<string> RamUpgradeCandidates { get; set; } = new();

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public string? DataNotice { get; set; }
}

// ---------- 03 Storage & Disk Health ----------

public class DrivePartitionStat
{
    public string DriveLetter { get; set; } = "C";
    public int DeviceCount { get; set; }
    public double TotalGb { get; set; }
    public double UsedGb { get; set; }
    public double FreeGb { get; set; }
    public double AvgUsedPercent { get; set; }
    public int WarningCount { get; set; }
    public int CriticalCount { get; set; }
    public string Tone { get; set; } = "info";
}

public class DeviceStorageRow
{
    public string Hostname { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? DeviceType { get; set; }
    public int DriveCount { get; set; }
    public double TotalGb { get; set; }
    public double UsedGb { get; set; }
    public double FreeGb { get; set; }
    public double UsedPercent { get; set; }
    public double CSizeGb { get; set; }
    public double CFreeGb { get; set; }
    public double CUsedPercent { get; set; }
    public double DSizeGb { get; set; }
    public double DFreeGb { get; set; }
    public double DUsedPercent { get; set; }
    public string? OtherDrives { get; set; }
    public string HealthStatus { get; set; } = "Healthy";
    public string Status { get; set; } = "Offline";
    public DateTime? CollectedAtUtc { get; set; }
}

public class StorageViewModel
{
    public List<KpiCard> Kpis { get; set; } = new();

    public double TotalTb { get; set; }
    public double UsedTb { get; set; }
    public double FreeTb { get; set; }
    public double UsedPercent { get; set; }
    public ChartModel StorageComposition { get; set; } = new(); // used vs free (donut)
    public ChartModel StorageTrend { get; set; } = new();      // tren kapasitas terpakai fleet

    public List<DeviceStorageRow> DeviceStorage { get; set; } = new();
    public ChartModel DeviceStorageChart { get; set; } = new(); // top N perangkat by used%

    public DrivePartitionStat? PartitionC { get; set; }
    public List<DrivePartitionStat> OtherPartitions { get; set; } = new();
    public ChartModel PartitionCChart { get; set; } = new();   // distribusi free space C
    public ChartModel PartitionUsageChart { get; set; } = new(); // used% per partisi
    public List<DeviceStorageRow> CriticalCDrive { get; set; } = new();
    public List<DeviceStorageRow> CriticalOtherDrives { get; set; } = new();

    public List<DeviceStorageRow> Top10LowestStorage { get; set; } = new();
    public ChartModel Top10Chart { get; set; } = new();

    public int CriticalCount { get; set; }
    public int WarningCount { get; set; }
    public int HealthyCount { get; set; }
    public ChartModel HealthChart { get; set; } = new();
    public ChartModel MediaTypeChart { get; set; } = new();

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public string? DataNotice { get; set; }
}

// ---------- 04 Agent Health & Telemetry ----------

public class AgentLogRow
{
    public long Id { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public string StartedText { get; set; } = string.Empty;
    public double DurationMs { get; set; }
    public string Status { get; set; } = "Success";
    public string AgentVersion { get; set; } = "-";
    public string? CollectorsRun { get; set; }
    public int ItemsSent { get; set; }
    public int OfflineQueueCount { get; set; }
    public double CpuPercent { get; set; }
    public double MemoryMb { get; set; }
    public string? ErrorMessage { get; set; }
    public string? TriggerSource { get; set; }
}

public class AgentHealthViewModel
{
    public List<KpiCard> Kpis { get; set; } = new();

    public double AvgExecutionMs { get; set; }
    public double P95ExecutionMs { get; set; }
    public double MaxExecutionMs { get; set; }
    public ChartModel ExecutionTrend { get; set; } = new();     // avg durasi per hari
    public ChartModel ExecutionHistogram { get; set; } = new(); // sebaran durasi
    public List<NameCount> SlowestDevices { get; set; } = new();

    public ChartModel SyncStatusChart { get; set; } = new();
    public List<NameCount> SyncStatus { get; set; } = new();
    public ChartModel RunStatusChart { get; set; } = new();     // Success/Failed/Partial/Queued
    public double SuccessRate { get; set; }
    public int TotalRuns { get; set; }
    public int FailedRuns { get; set; }

    public int DevicesWithQueue { get; set; }
    public int TotalQueuedItems { get; set; }
    public int MaxQueueCount { get; set; }
    public ChartModel QueueChart { get; set; } = new();         // tren akumulasi queue
    public List<DeviceStatusRow> QueueDevices { get; set; } = new();

    public double AvgAgentCpu { get; set; }
    public double AvgAgentMemoryMb { get; set; }
    public double MaxAgentCpu { get; set; }
    public ChartModel FootprintTrend { get; set; } = new();     // CPU agent per hari
    public ChartModel MemoryTrend { get; set; } = new();        // memory agent per hari
    public List<NameCount> HeaviestAgents { get; set; } = new();

    public List<AgentLogRow> Logs { get; set; } = new();
    public List<AgentLogRow> ErrorLogs { get; set; } = new();
    public int LogTotalCount { get; set; }
    public int LogPage { get; set; } = 1;
    public int LogPageSize { get; set; } = 25;
    public int LogTotalPages => LogPageSize <= 0 ? 1 : (int)Math.Ceiling(LogTotalCount / (double)LogPageSize);
    public string? LogFilter { get; set; }
    public string? StatusFilter { get; set; }

    public ChartModel CollectorMixChart { get; set; } = new();
    public List<NameCount> AgentVersions { get; set; } = new();

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public string? DataNotice { get; set; }
}

// ---------- 05 Software Inventory & Compliance ----------

public class SoftwareGroupRow
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? Publisher { get; set; }
    public int DeviceCount { get; set; }
    public double CoveragePercent { get; set; }
    public double TotalSizeMb { get; set; }
    public string ComplianceStatus { get; set; } = "Unmanaged";
    public int VersionCount { get; set; }
    public DateTime? LastSeenUtc { get; set; }
}

public class SoftwareDetailRow
{
    public string Hostname { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string? Publisher { get; set; }
    public DateTime? InstallDate { get; set; }
    public double SizeMb { get; set; }
    public string? Scope { get; set; }
    public string ComplianceStatus { get; set; } = "Unmanaged";
    public DateTime LastSeenUtc { get; set; }
}

public class ComplianceIssue
{
    public string Hostname { get; set; } = string.Empty;
    public string Software { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public string Tone { get; set; } = "warn";
    public string Recommendation { get; set; } = string.Empty;
}

public class SoftwareViewModel
{
    public List<KpiCard> Kpis { get; set; } = new();

    public List<SoftwareGroupRow> Grouped { get; set; } = new();
    public ChartModel TopSoftwareChart { get; set; } = new();
    public ChartModel VersionFragmentationChart { get; set; } = new();
    public ChartModel PublisherChart { get; set; } = new();
    public List<NameCount> Publishers { get; set; } = new();
    public List<NameCount> MostFragmented { get; set; } = new();

    public List<SoftwareDetailRow> Details { get; set; } = new();
    public int DetailTotalCount { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
    public int TotalPages => PageSize <= 0 ? 1 : (int)Math.Ceiling(DetailTotalCount / (double)PageSize);
    public string? Search { get; set; }
    public string? PublisherFilter { get; set; }
    public string? ComplianceFilter { get; set; }
    public string Sort { get; set; } = "name";

    public ChartModel ComplianceChart { get; set; } = new();
    public List<NameCount> ComplianceSummary { get; set; } = new();
    public List<ComplianceIssue> Issues { get; set; } = new();
    public double CompliancePercent { get; set; }

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public string? DataNotice { get; set; }
}

// ---------- Device detail ----------

public class DeviceDetailViewModel
{
    public DeviceMonitor.Shared.Entities.Device Device { get; set; } = null!;
    public List<DeviceMonitor.Shared.Entities.DiskDrive> Disks { get; set; } = new();
    public List<SoftwareDetailRow> Software { get; set; } = new();
    public List<AgentLogRow> Runs { get; set; } = new();
    public ChartModel CpuRamTrend { get; set; } = new();
    public ChartModel StorageTrend { get; set; } = new();
    public ChartModel UptimeTrend { get; set; } = new();
    public List<KpiCard> Kpis { get; set; } = new();
}
