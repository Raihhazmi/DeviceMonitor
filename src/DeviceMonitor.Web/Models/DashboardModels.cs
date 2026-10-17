namespace DeviceMonitor.Web.Models;

// ---------- Building block grafik (dirender oleh wwwroot/js/charts.js) ----------

public class ChartPoint
{
    public string Label { get; set; } = string.Empty;
    public double Value { get; set; }
    public string? Color { get; set; }
    public string? Meta { get; set; }
}

public class ChartSeries
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#4f8cff";
    public bool Fill { get; set; }
    public List<double> Data { get; set; } = new();
}

public class ChartModel
{
    public List<string> Labels { get; set; } = new();
    public List<ChartSeries> Series { get; set; } = new();
    public string? ValueSuffix { get; set; }

    /// <summary>Warna per slice/bar (dipakai chart donut & bar).</summary>
    public List<string> SliceColors { get; set; } = new();

    /// <summary>Teks tambahan per bar (tooltip kecil di bawah nilai).</summary>
    public List<string?> Meta { get; set; } = new();
}

public class KpiCard
{
    public string Title { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Sub { get; set; }
    public string? Icon { get; set; }
    public string Tone { get; set; } = "default";   // default | good | warn | bad | info
    public double? Progress { get; set; }          // 0..100, opsional
}

// ---------- 01 Executive Overview ----------

public class UptimeCategory
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Percent { get; set; }
    public string Tone { get; set; } = "info";
    public string Note { get; set; } = string.Empty;
}

public class DeviceStatusRow
{
    public string Hostname { get; set; } = string.Empty;
    public string DeviceType { get; set; } = "Desktop";
    public string Status { get; set; } = "Offline";
    public string? Department { get; set; }
    public string? AssignedUser { get; set; }
    public string? IpAddress { get; set; }
    public string? OsName { get; set; }
    public double RamTotalGb { get; set; }
    public double RamUsedPercent { get; set; }
    public double CpuUsagePercent { get; set; }
    public double DiskUsedPercent { get; set; }
    public double DiskFreeGb { get; set; }
    public double UptimeSeconds { get; set; }
    public string UptimeText { get; set; } = "-";
    public string UptimeCategory { get; set; } = "-";
    public DateTime? LastSeenUtc { get; set; }
    public string LastSeenText { get; set; } = "-";
    public string AgentVersion { get; set; } = "-";
    public int OfflineQueueCount { get; set; }

    /// <summary>true = belum pernah ada snapshot telemetry untuk perangkat ini.</summary>
    public bool IsFresh { get; set; }
}

public class OverviewViewModel
{
    public List<KpiCard> Kpis { get; set; } = new();
    public int TotalDevices { get; set; }
    public int OnlineCount { get; set; }
    public int OfflineCount { get; set; }
    public int WarningCount { get; set; }
    public double OnlinePercent { get; set; }

    public double FleetStorageTotalTb { get; set; }
    public double FleetStorageUsedTb { get; set; }
    public double FleetStorageFreeTb { get; set; }
    public double FleetStorageUsedPercent { get; set; }

    public List<UptimeCategory> UptimeCategories { get; set; } = new();
    public ChartModel UptimeTrend { get; set; } = new();      // rata-rata uptime per hari
    public ChartModel AvailabilityTrend { get; set; } = new(); // online vs offline per hari
    public ChartModel DepartmentChart { get; set; } = new();
    public ChartModel DeviceTypeChart { get; set; } = new();

    public List<DeviceStatusRow> DeviceStatus { get; set; } = new();
    public List<DeviceStatusRow> NeedsAttention { get; set; } = new();

    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
    public string? DataNotice { get; set; }
}
