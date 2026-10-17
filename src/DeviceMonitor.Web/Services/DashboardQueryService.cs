using System.Globalization;
using DeviceMonitor.Shared.Entities;
using DeviceMonitor.Web.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DeviceMonitor.Web.Services;

public class MonitorOptions
{
    public const string SectionName = "Monitor";

    /// <summary>Perangkat dianggap offline jika tidak melapor lebih dari N menit.</summary>
    public int OnlineThresholdMinutes { get; set; } = 30;

    /// <summary>Batas "warning" pemakaian disk (%).</summary>
    public double DiskWarningPercent { get; set; } = 80;

    /// <summary>Batas "critical" pemakaian disk (%).</summary>
    public double DiskCriticalPercent { get; set; } = 90;

    /// <summary>Batas RAM terpakai yang dianggap berat (%).</summary>
    public double RamPressurePercent { get; set; } = 85;

    /// <summary>RAM minimum yang direkomendasikan (GB).</summary>
    public double MinRecommendedRamGb { get; set; } = 8;

    /// <summary>Rentang hari untuk grafik tren.</summary>
    public int TrendDays { get; set; } = 14;
}

/// <summary>
/// Semua query untuk panel dashboard. Dibuat per-request (scoped).
/// </summary>
public class DashboardQueryService
{
    private readonly MonitorDbContext _db;
    private readonly MonitorOptions _opt;

    public DashboardQueryService(MonitorDbContext db, IOptions<MonitorOptions> options)
    {
        _db = db;
        _opt = options.Value;
    }

    public MonitorOptions Options => _opt;

    // ------------------------------------------------------------------
    // Building blocks
    // ------------------------------------------------------------------

    private async Task<List<Device>> DevicesAsync(bool activeOnly = true)
    {
        var q = _db.Devices.AsNoTracking();
        if (activeOnly) q = q.Where(d => d.IsActive);
        return await q.OrderBy(d => d.Hostname).ToListAsync();
    }

    /// <summary>Snapshot terbaru per perangkat (bukan penjumlahan seluruh riwayat).</summary>
    private async Task<Dictionary<int, DeviceSnapshot>> LatestSnapshotsAsync()
    {
        // Snapshot dengan timestamp maksimum per device (aman untuk SQL Server & SQLite).
        var latest = await _db.DeviceSnapshots.AsNoTracking()
            .GroupBy(s => s.DeviceId)
            .Select(g => g.OrderByDescending(x => x.CollectedAtUtc).ThenByDescending(x => x.Id).First())
            .ToListAsync();

        return latest.ToDictionary(s => s.DeviceId, s => s);
    }

    /// <summary>Snapshot terbaru per perangkat untuk rentang hari tertentu (untuk tren harian).</summary>
    private async Task<List<DeviceSnapshot>> DailySnapshotsAsync(DateTime fromUtc)
        => await _db.DeviceSnapshots.AsNoTracking()
            .Where(s => s.CollectedAtUtc >= fromUtc)
            .ToListAsync();

    private async Task<List<DiskDrive>> DisksAsync()
        => await _db.DiskDrives.AsNoTracking().Include(d => d.Device).ToListAsync();

    public async Task<string?> EmptyNoticeAsync()
    {
        var any = await _db.Devices.AnyAsync();
        return any ? null :
            "Database masih kosong. Buka menu Data & Ingest untuk meng-import CSV, mengirim data via API agent, atau men-generate data demo.";
    }

    /// <summary>Status efektif perangkat: Offline bila melewati ambang batas last-seen.</summary>
    public string EffectiveStatus(Device d, DeviceSnapshot? s, DateTime nowUtc)
    {
        var last = d.LastSeenUtc ?? s?.CollectedAtUtc;
        if (last is null || last == default) return "Offline";
        if ((nowUtc - last.Value).TotalMinutes > _opt.OnlineThresholdMinutes) return "Offline";
        return string.IsNullOrWhiteSpace(d.Status) ? "Online" : d.Status;
    }

    // ------------------------------------------------------------------
    // 01 - Executive Overview
    // ------------------------------------------------------------------

    public async Task<Models.OverviewViewModel> GetOverviewAsync()
    {
        var vm = new Models.OverviewViewModel();
        var now = DateTime.UtcNow;
        var devices = await DevicesAsync();
        var snaps = await LatestSnapshotsAsync();
        vm.TotalDevices = devices.Count;

        if (devices.Count == 0)
        {
            vm.DataNotice = await EmptyNoticeAsync();
            return vm;
        }

        var rows = devices.Select(d =>
        {
            snaps.TryGetValue(d.Id, out var s);
            return ToRow(d, s, now);
        }).OrderBy(r => r.Hostname).ToList();

        vm.DeviceStatus = rows;
        vm.OnlineCount = rows.Count(r => r.Status == "Online");
        vm.WarningCount = rows.Count(r => r.Status == "Warning" || r.Status == "Error");
        vm.OfflineCount = rows.Count(r => r.Status == "Offline");
        vm.OnlinePercent = rows.Count == 0 ? 0 : 100.0 * vm.OnlineCount / rows.Count;

        var totalGb = devices.Sum(d => d.TotalDiskGb);
        var usedGb = devices.Sum(d => d.UsedDiskGb);
        vm.FleetStorageTotalTb = totalGb / 1024.0;
        vm.FleetStorageUsedTb = usedGb / 1024.0;
        vm.FleetStorageFreeTb = (totalGb - usedGb) / 1024.0;
        vm.FleetStorageUsedPercent = totalGb <= 0 ? 0 : 100.0 * usedGb / totalGb;

        vm.Kpis = new List<Models.KpiCard>
        {
            new() { Title = "Total PCs", Value = devices.Count.ToString(), Sub = $"{devices.Count(d => d.DeviceType == "Laptop")} laptop · {devices.Count(d => d.DeviceType != "Laptop")} desktop/workstation", Icon = "device", Tone = "info" },
            new() { Title = "Online", Value = vm.OnlineCount.ToString(), Sub = $"{Fmt.Percent(vm.OnlinePercent)} dari fleet aktif", Icon = "online", Tone = "good", Progress = vm.OnlinePercent },
            new() { Title = "Offline", Value = vm.OfflineCount.ToString(), Sub = vm.WarningCount > 0 ? $"{vm.WarningCount} perangkat warning/error" : "Tidak ada perangkat warning", Icon = "offline", Tone = vm.OfflineCount > 0 ? "warn" : "default", Progress = rows.Count == 0 ? 0 : 100.0 * vm.OfflineCount / rows.Count },
            new() { Title = "Total Fleet Storage", Value = Fmt.Tb(vm.FleetStorageTotalTb), Sub = $"Terpakai {Fmt.Tb(vm.FleetStorageUsedTb)} ({Fmt.Percent(vm.FleetStorageUsedPercent)})", Icon = "storage", Tone = vm.FleetStorageUsedPercent >= _opt.DiskCriticalPercent ? "bad" : vm.FleetStorageUsedPercent >= _opt.DiskWarningPercent ? "warn" : "default", Progress = vm.FleetStorageUsedPercent },
        };

        // ---- Fleet Uptime Categories ----
        var catOrder = new[] { "< 24 jam", "1 - 7 hari", "7 - 30 hari", "> 30 hari", "Tanpa Data" };
        var catNote = new Dictionary<string, (string note, string tone)>
        {
            ["< 24 jam"] = ("Baru restart / sering mati", "info"),
            ["1 - 7 hari"] = ("Pola normal", "good"),
            ["7 - 30 hari"] = ("Perlu dipantau", "warn"),
            ["> 30 hari"] = ("Perlu dijadwalkan restart", "bad"),
            ["Tanpa Data"] = ("Belum ada snapshot", "default"),
        };
        var grouped = rows.GroupBy(r => r.UptimeCategory).ToDictionary(g => g.Key, g => g.Count());
        vm.UptimeCategories = catOrder
            .Where(c => grouped.ContainsKey(c))
            .Select(c => new Models.UptimeCategory
            {
                Category = c,
                Count = grouped[c],
                Percent = 100.0 * grouped[c] / rows.Count,
                Note = catNote[c].note,
                Tone = catNote[c].tone
            }).ToList();

        // ---- Tren uptime & ketersediaan harian ----
        var days = Fmt.LastDays(_opt.TrendDays, now);
        var from = days[0];
        var hist = await DailySnapshotsAsync(from);

        var uptimeByDay = hist.GroupBy(s => s.CollectedAtUtc.Date)
            .ToDictionary(g => g.Key, g => g.Average(x => x.UptimeSeconds) / 86400.0);

        // Ketersediaan dihitung sebagai rata-rata perangkat online per hari
        // (rasio, bukan jumlah snapshot) agar hari terakhir yang belum penuh tetap mulus.
        var onlineRatioByDay = hist.GroupBy(s => s.CollectedAtUtc.Date)
            .ToDictionary(g => g.Key, g => g.Average(x => x.Status == "Online" ? 1.0 : 0.0) * devices.Count);
        var offlineByDay = onlineRatioByDay.ToDictionary(kv => kv.Key, kv => (double)devices.Count - kv.Value);

        vm.UptimeTrend = new Models.ChartModel
        {
            Labels = days.Select(Fmt.Day).ToList(),
            ValueSuffix = " hari",
            Series = new List<Models.ChartSeries>
            {
                new() { Name = "Rata-rata uptime", Color = Fmt.Palette[0], Data = Fmt.FillSeries(days, uptimeByDay, 2), Fill = true }
            }
        };

        vm.AvailabilityTrend = new Models.ChartModel
        {
            Labels = days.Select(Fmt.Day).ToList(),
            ValueSuffix = " unit",
            Series = new List<Models.ChartSeries>
            {
                new() { Name = "Online", Color = "#22c9a5", Data = Fmt.FillSeries(days, onlineRatioByDay) },
                new() { Name = "Offline", Color = "#9aa5b1", Data = Fmt.FillSeries(days, offlineByDay) }
            }
        };

        vm.DepartmentChart = Donut(rows.GroupBy(r => string.IsNullOrWhiteSpace(r.Department) ? "Lainnya" : r.Department!)
            .OrderByDescending(g => g.Count())
            .Select((g, i) => new Models.ChartPoint { Label = g.Key, Value = g.Count(), Color = Fmt.Color(i) })
            .ToList(), " unit");

        vm.DeviceTypeChart = Donut(rows.GroupBy(r => r.DeviceType)
            .OrderByDescending(g => g.Count())
            .Select((g, i) => new Models.ChartPoint { Label = g.Key, Value = g.Count(), Color = Fmt.Color(i + 3) })
            .ToList(), " unit");

        // ---- Perangkat yang butuh perhatian ----
        vm.NeedsAttention = rows
            .Where(r => r.Status == "Offline" || r.DiskUsedPercent >= _opt.DiskCriticalPercent
                        || r.RamUsedPercent >= _opt.RamPressurePercent || r.CpuUsagePercent >= 90
                        || r.OfflineQueueCount > 0)
            .OrderByDescending(r => r.Status == "Offline" ? 3 : 0)
            .ThenByDescending(r => r.DiskUsedPercent)
            .Take(12)
            .ToList();

        return vm;
    }

    private Models.DeviceStatusRow ToRow(Device d, DeviceSnapshot? s, DateTime now) => new()
    {
        Hostname = d.Hostname,
        DeviceType = d.DeviceType,
        Status = EffectiveStatus(d, s, now),
        Department = d.Department,
        AssignedUser = d.AssignedUser,
        IpAddress = d.IpAddress,
        OsName = d.OsName,
        RamTotalGb = d.RamTotalGb,
        RamUsedPercent = s?.RamUsedPercent ?? 0,
        CpuUsagePercent = s?.CpuUsagePercent ?? 0,
        DiskUsedPercent = d.TotalDiskGb > 0 ? 100.0 * d.UsedDiskGb / d.TotalDiskGb : s?.DiskUsedPercent ?? 0,
        DiskFreeGb = s?.DiskFreeGb ?? Math.Max(0, d.TotalDiskGb - d.UsedDiskGb),
        UptimeSeconds = s?.UptimeSeconds ?? d.UptimeSeconds,
        UptimeText = Fmt.Duration(s?.UptimeSeconds ?? d.UptimeSeconds),
        UptimeCategory = Fmt.UptimeCategoryOf(s?.UptimeSeconds ?? d.UptimeSeconds),
        LastSeenUtc = d.LastSeenUtc ?? s?.CollectedAtUtc,
        LastSeenText = Fmt.Relative(d.LastSeenUtc ?? s?.CollectedAtUtc, now),
        AgentVersion = string.IsNullOrWhiteSpace(d.AgentVersion) ? "-" : d.AgentVersion,
        OfflineQueueCount = s?.OfflineQueueCount ?? 0,
        IsFresh = s is null || s.CollectedAtUtc == default
    };

    private static Models.ChartModel Donut(List<Models.ChartPoint> points, string suffix) => new()
    {
        Labels = points.Select(p => p.Label).ToList(),
        ValueSuffix = suffix,
        Series = new List<Models.ChartSeries>
        {
            new() { Name = "Jumlah", Data = points.Select(p => p.Value).ToList() }
        },
        SliceColors = points.Select(p => p.Color ?? Fmt.Palette[0]).ToList()
    };

    private static Models.ChartModel Bar(List<Models.ChartPoint> points, string suffix) => new()
    {
        Labels = points.Select(p => p.Label).ToList(),
        ValueSuffix = suffix,
        Series = new List<Models.ChartSeries>
        {
            new() { Name = "Nilai", Data = points.Select(p => p.Value).ToList(), Color = points.FirstOrDefault()?.Color ?? Fmt.Palette[0] }
        },
        SliceColors = points.Select(p => p.Color ?? Fmt.Palette[0]).ToList(),
        Meta = points.Select(p => p.Meta).ToList()
    };

    // ------------------------------------------------------------------
    // 02 - Hardware & System Inventory
    // ------------------------------------------------------------------

    public async Task<Models.HardwareViewModel> GetHardwareAsync()
    {
        var vm = new Models.HardwareViewModel();
        var now = DateTime.UtcNow;
        var devices = await DevicesAsync();
        var snaps = await LatestSnapshotsAsync();
        vm.DataNotice = await EmptyNoticeAsync();
        if (devices.Count == 0) return vm;

        var rows = devices.Select(d => { snaps.TryGetValue(d.Id, out var s); return ToRow(d, s, now); }).ToList();

        // ---- Distribusi model perangkat ----
        var models = devices
            .GroupBy(d => string.IsNullOrWhiteSpace(d.Model) ? "Unknown" : d.Model!)
            .Select(g => new Models.NameCount
            {
                Name = g.Key,
                Count = g.Count(),
                Percent = 100.0 * g.Count() / devices.Count,
                Sub = g.Select(x => x.Manufacturer).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().FirstOrDefault()
            })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name).ToList();
        vm.Models = models;
        vm.ModelDistribution = Bar(models.Select((m, i) => new Models.ChartPoint { Label = m.Name, Value = m.Count, Color = Fmt.Color(i) }).ToList(), " unit");

        // ---- RAM ----
        var buckets = new (double gb, string label, string note, string tone)[]
        {
            (4, "4 GB", "Di bawah standar, wajib upgrade", "bad"),
            (8, "8 GB", "Standar minimum office", "warn"),
            (16, "16 GB", "Ideal untuk multitasking", "good"),
            (32, "32 GB", "Power user / engineering", "info"),
            (64, "64 GB+", "Workstation berat", "info"),
        };

        // Bucketing memakai Norm() agar perangkat 6 GB masuk 8 GB, 12 GB masuk 16 GB, dst.
        var ramBuckets = new List<Models.RamBucket>();
        foreach (var b in buckets)
        {
            var list = devices.Where(d => Norm(d.RamTotalGb) == b.gb).ToList();
            if (list.Count == 0) continue;
            ramBuckets.Add(new Models.RamBucket
            {
                Bucket = b.label,
                Count = list.Count,
                Percent = 100.0 * list.Count / devices.Count,
                TotalGb = list.Sum(x => x.RamTotalGb),
                Note = b.note,
                Tone = b.tone
            });
        }
        vm.RamBuckets = ramBuckets;

        var ramVariety = devices.GroupBy(d => Norm(d.RamTotalGb))
            .OrderBy(g => g.Key)
            .Select((g, i) => new Models.ChartPoint { Label = g.Key.ToString("0.#") + " GB", Value = g.Count(), Color = Fmt.Color(i) })
            .ToList();
        vm.RamVarietyChart = Donut(ramVariety, " unit");

        vm.RamDistributionChart = Bar(ramBuckets.Select((r, i) => new Models.ChartPoint { Label = r.Bucket, Value = r.Count, Color = Fmt.Color(i), Meta = r.Note }).ToList(), " unit");

        vm.FleetRamTotalGb = devices.Sum(d => d.RamTotalGb);
        vm.AvgRamTotalGb = devices.Average(d => d.RamTotalGb);
        var withSnap = rows.Where(r => r.LastSeenUtc is not null).ToList();
        vm.AvgRamUsedPercent = withSnap.Count == 0 ? 0 : withSnap.Average(r => r.RamUsedPercent);
        vm.AvgRamUsedGb = vm.AvgRamTotalGb * vm.AvgRamUsedPercent / 100.0;
        vm.LowRamDevices = devices.Count(d => d.RamTotalGb < _opt.MinRecommendedRamGb);
        vm.HighRamPressureDevices = rows.Count(r => r.RamUsedPercent >= _opt.RamPressurePercent);

        vm.RamPressureChart = Bar(devices
            .GroupBy(d => string.IsNullOrWhiteSpace(d.Department) ? "Lainnya" : d.Department!)
            .Select(g => new { Dept = g.Key, Avg = g.Average(x => snaps.TryGetValue(x.Id, out var s) && s != null ? s.RamUsedPercent : 0) })
            .OrderByDescending(x => x.Avg)
            .Select((x, i) => new Models.ChartPoint { Label = x.Dept, Value = Fmt.N(x.Avg), Color = x.Avg >= _opt.RamPressurePercent ? "#ef4d6f" : x.Avg >= 70 ? "#f5a524" : Fmt.Color(i), Meta = "% terpakai" })
            .ToList(), "%");

        vm.RamUpgradeCandidates = devices
            .Where(d => d.RamTotalGb < _opt.MinRecommendedRamGb
                        || (snaps.TryGetValue(d.Id, out var s) && s != null && s.RamUsedPercent >= 90))
            .OrderBy(d => d.RamTotalGb)
            .Select(d => $"{d.Hostname} · {d.RamTotalGb:0} GB" +
                         (snaps.TryGetValue(d.Id, out var s2) && s2 != null ? $" (terpakai {s2.RamUsedPercent:0}%)" : ""))
            .Take(15).ToList();

        // ---- OS ----
        var osGroups = devices.GroupBy(d => string.IsNullOrWhiteSpace(d.OsName) ? "Unknown" : d.OsName!)
            .Select(g => new Models.NameCount
            {
                Name = g.Key,
                Count = g.Count(),
                Percent = 100.0 * g.Count() / devices.Count,
                Sub = g.Select(x => x.OsVersion).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x).FirstOrDefault()
            })
            .OrderByDescending(x => x.Count).ToList();
        vm.OsSummary = osGroups;
        vm.OsDistribution = Donut(osGroups.Select((o, i) => new Models.ChartPoint { Label = o.Name, Value = o.Count, Color = Fmt.Color(i) }).ToList(), " unit");

        var win = devices
            .Where(d => (d.OsName ?? "").Contains("Windows", StringComparison.OrdinalIgnoreCase))
            .GroupBy(d => $"{ShortWindows(d.OsName)} {d.OsBuild}".Trim())
            .Select(g => new Models.NameCount
            {
                Name = g.Key,
                Count = g.Count(),
                Percent = 100.0 * g.Count() / devices.Count,
                Sub = string.Join(", ", g.Select(x => x.OsVersion).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x).Take(2)),
                Tone = IsWindowsSupported(g.Key) ? "good" : "bad"
            })
            .OrderByDescending(x => x.Count).ToList();
        vm.WindowsVersions = win;
        vm.UnsupportedWindowsCount = win.Where(w => w.Tone == "bad").Sum(w => w.Count);

        // ---- BIOS ----
        var bios = devices.GroupBy(d => string.IsNullOrWhiteSpace(d.BiosVersion) ? "Unknown" : d.BiosVersion!)
            .Select(g => new Models.NameCount
            {
                Name = g.Key,
                Count = g.Count(),
                Percent = 100.0 * g.Count() / devices.Count,
                Sub = g.Select(x => x.BiosVendor).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().FirstOrDefault()
            })
            .OrderByDescending(x => x.Count).ThenBy(x => x.Name).ToList();
        vm.BiosVersions = bios;
        vm.BiosChart = Bar(bios.Select((b, i) => new Models.ChartPoint { Label = b.Name, Value = b.Count, Color = Fmt.Color(i + 2) }).ToList(), " unit");
        vm.BiosVariantCount = bios.Count;
        vm.DominantBios = bios.Count == 0 ? "-" : $"{bios[0].Name} ({bios[0].Count} unit)";

        // ---- CPU ----
        vm.CpuSummary = devices.GroupBy(d => string.IsNullOrWhiteSpace(d.CpuName) ? "Unknown" : ShortCpu(d.CpuName!))
            .Select(g => new Models.NameCount { Name = g.Key, Count = g.Count(), Percent = 100.0 * g.Count() / devices.Count })
            .OrderByDescending(x => x.Count).Take(8).ToList();

        vm.Inventory = devices.Select(d =>
        {
            snaps.TryGetValue(d.Id, out var sn);
            return new Models.HardwareRow
            {
                Hostname = d.Hostname,
                DeviceType = d.DeviceType,
                AssetTag = d.AssetTag,
                Manufacturer = d.Manufacturer,
                Model = d.Model,
                SerialNumber = d.SerialNumber,
                CpuName = d.CpuName,
                CpuCores = d.CpuCores,
                RamTotalGb = d.RamTotalGb,
                RamUsedPercent = sn?.RamUsedPercent ?? 0,
                OsName = d.OsName,
                OsBuild = d.OsBuild,
                BiosVersion = d.BiosVersion,
                Department = d.Department,
                Location = d.Location,
                IpAddress = d.IpAddress,
                Status = EffectiveStatus(d, sn, now)
            };
        }).OrderBy(x => x.Hostname).ToList();

        vm.Kpis = new List<Models.KpiCard>
        {
            new() { Title = "Total Perangkat", Value = devices.Count.ToString(), Sub = $"{models.Count} model berbeda", Icon = "device", Tone = "info" },
            new() { Title = "Average Fleet RAM", Value = Fmt.N(vm.AvgRamTotalGb) + " GB", Sub = $"Total terpasang {Fmt.Gb(vm.FleetRamTotalGb)}", Icon = "ram", Tone = "default" },
            new() { Title = "Rata-rata RAM Terpakai", Value = Fmt.Percent(vm.AvgRamUsedPercent), Sub = $"≈ {Fmt.N(vm.AvgRamUsedGb)} GB per perangkat · {vm.HighRamPressureDevices} unit > {_opt.RamPressurePercent:0}%", Icon = "gauge", Tone = vm.AvgRamUsedPercent >= _opt.RamPressurePercent ? "bad" : vm.AvgRamUsedPercent >= 70 ? "warn" : "good", Progress = vm.AvgRamUsedPercent },
            new() { Title = "Windows Tidak Didukung", Value = vm.UnsupportedWindowsCount.ToString(), Sub = $"{osGroups.Count} varian OS · {vm.BiosVariantCount} varian BIOS", Icon = "shield", Tone = vm.UnsupportedWindowsCount > 0 ? "bad" : "good" },
        };

        return vm;
    }

    private static double Norm(double gb)
    {
        if (gb <= 4) return 4;
        if (gb <= 8) return 8;
        if (gb <= 16) return 16;
        if (gb <= 32) return 32;
        return 64;
    }

    private static string ShortWindows(string? os)
    {
        if (string.IsNullOrWhiteSpace(os)) return "Windows";
        if (os.Contains("11")) return "Windows 11";
        if (os.Contains("10")) return "Windows 10";
        if (os.Contains("8.1") || os.Contains("8")) return "Windows 8.1";
        if (os.Contains("7")) return "Windows 7";
        return "Windows";
    }

    /// <summary>Windows 10 consumer mencapai end of support 14 Oktober 2025.</summary>
    private static bool IsWindowsSupported(string label)
    {
        if (label.Contains("Windows 11")) return true;
        if (label.Contains("Windows 10")) return false;
        if (label.Contains("Windows 8") || label.Contains("Windows 7")) return false;
        return true;
    }

    private static string ShortCpu(string cpu)
    {
        var c = cpu.Replace("(R)", "").Replace("(TM)", "").Replace("CPU", "").Trim();
        c = string.Join(' ', c.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(6));
        return c.Trim();
    }

    // ------------------------------------------------------------------
    // 03 - Storage & Disk Health
    // ------------------------------------------------------------------

    public async Task<Models.StorageViewModel> GetStorageAsync()
    {
        var vm = new Models.StorageViewModel();
        var now = DateTime.UtcNow;
        var devices = await DevicesAsync();
        var disks = await DisksAsync();
        var snaps = await LatestSnapshotsAsync();
        vm.DataNotice = await EmptyNoticeAsync();
        if (devices.Count == 0) return vm;

        var disksByDevice = disks.GroupBy(d => d.DeviceId).ToDictionary(g => g.Key, g => g.ToList());
        var deviceById = devices.ToDictionary(d => d.Id);

        var deviceRows = devices.Select(d =>
        {
            disksByDevice.TryGetValue(d.Id, out var list);
            list ??= new List<DiskDrive>();
            var total = list.Count > 0 ? list.Sum(x => x.SizeGb) : d.TotalDiskGb;
            var used = list.Count > 0 ? list.Sum(x => x.UsedGb) : d.UsedDiskGb;
            var c = list.FirstOrDefault(x => x.DriveLetter.Equals("C", StringComparison.OrdinalIgnoreCase));
            var dd = list.FirstOrDefault(x => x.DriveLetter.Equals("D", StringComparison.OrdinalIgnoreCase));
            var others = list.Where(x => !x.DriveLetter.Equals("C", StringComparison.OrdinalIgnoreCase)
                                      && !x.DriveLetter.Equals("D", StringComparison.OrdinalIgnoreCase)).ToList();

            var usedPct = total > 0 ? 100.0 * used / total : 0;
            var worst = list.Count > 0 ? list.Max(x => x.UsedPercent) : usedPct;
            var health = worst >= _opt.DiskCriticalPercent || (c != null && c.FreeGb <= 10) ? "Critical"
                       : worst >= _opt.DiskWarningPercent ? "Warning" : "Healthy";

            snaps.TryGetValue(d.Id, out var s);

            return new Models.DeviceStorageRow
            {
                Hostname = d.Hostname,
                Department = d.Department,
                DeviceType = d.DeviceType,
                DriveCount = list.Count,
                TotalGb = total,
                UsedGb = used,
                FreeGb = total - used,
                UsedPercent = usedPct,
                CSizeGb = c?.SizeGb ?? 0,
                CFreeGb = c?.FreeGb ?? s?.CFreeGb ?? 0,
                CUsedPercent = c?.UsedPercent ?? 0,
                DSizeGb = dd?.SizeGb ?? 0,
                DFreeGb = dd?.FreeGb ?? 0,
                DUsedPercent = dd?.UsedPercent ?? 0,
                OtherDrives = others.Count == 0 ? null : string.Join(", ", others.Select(o => $"{o.DriveLetter}: {Fmt.Gb(o.FreeGb)} free")),
                HealthStatus = health,
                Status = EffectiveStatus(d, s, now),
                CollectedAtUtc = list.Count > 0 ? list.Max(x => x.CollectedAtUtc) : d.LastSeenUtc
            };
        }).ToList();

        vm.DeviceStorage = deviceRows.OrderByDescending(r => r.UsedPercent).ToList();

        var totalGb = deviceRows.Sum(r => r.TotalGb);
        var usedGb = deviceRows.Sum(r => r.UsedGb);
        vm.TotalTb = totalGb / 1024.0;
        vm.UsedTb = usedGb / 1024.0;
        vm.FreeTb = (totalGb - usedGb) / 1024.0;
        vm.UsedPercent = totalGb > 0 ? 100.0 * usedGb / totalGb : 0;

        vm.CriticalCount = deviceRows.Count(r => r.HealthStatus == "Critical");
        vm.WarningCount = deviceRows.Count(r => r.HealthStatus == "Warning");
        vm.HealthyCount = deviceRows.Count(r => r.HealthStatus == "Healthy");

        vm.Kpis = new List<Models.KpiCard>
        {
            new() { Title = "Total Fleet Storage", Value = Fmt.Tb(vm.TotalTb), Sub = $"{deviceRows.Sum(r => r.DriveCount)} partisi di {devices.Count} perangkat", Icon = "storage", Tone = "info" },
            new() { Title = "Terpakai", Value = Fmt.Tb(vm.UsedTb), Sub = Fmt.Percent(vm.UsedPercent) + " dari total kapasitas", Icon = "used", Tone = vm.UsedPercent >= _opt.DiskCriticalPercent ? "bad" : vm.UsedPercent >= _opt.DiskWarningPercent ? "warn" : "good", Progress = vm.UsedPercent },
            new() { Title = "Sisa Ruang", Value = Fmt.Tb(vm.FreeTb), Sub = $"Rata-rata {Fmt.Gb(deviceRows.Count == 0 ? 0 : deviceRows.Average(r => r.FreeGb))} free per perangkat", Icon = "free", Tone = "default" },
            new() { Title = "Drive Kritis", Value = vm.CriticalCount.ToString(), Sub = $"{vm.WarningCount} warning · {vm.HealthyCount} sehat", Icon = "alert", Tone = vm.CriticalCount > 0 ? "bad" : "good" },
        };

        vm.StorageComposition = Donut(new List<Models.ChartPoint>
        {
            new() { Label = "Terpakai", Value = Fmt.N(usedGb), Color = "#4f8cff" },
            new() { Label = "Kosong", Value = Fmt.N(Math.Max(0, totalGb - usedGb)), Color = "#22c9a5" }
        }, " GB");

        vm.HealthChart = Donut(new List<Models.ChartPoint>
        {
            new() { Label = "Healthy", Value = vm.HealthyCount, Color = "#22c9a5" },
            new() { Label = "Warning", Value = vm.WarningCount, Color = "#f5a524" },
            new() { Label = "Critical", Value = vm.CriticalCount, Color = "#ef4d6f" }
        }, " perangkat");

        vm.MediaTypeChart = Donut(disks.GroupBy(d => string.IsNullOrWhiteSpace(d.MediaType) ? "Unknown" : d.MediaType!)
            .Select((g, i) => new Models.ChartPoint { Label = g.Key, Value = g.Count(), Color = Fmt.Color(i + 4) }).ToList(), " partisi");

        // ---- Monitoring storage perangkat (top 15 by used%) ----
        var top = deviceRows.OrderByDescending(r => r.UsedPercent).Take(15).ToList();
        vm.DeviceStorageChart = Bar(top.Select(r => new Models.ChartPoint
        {
            Label = r.Hostname,
            Value = Fmt.N(r.UsedPercent),
            Color = r.UsedPercent >= _opt.DiskCriticalPercent ? "#ef4d6f" : r.UsedPercent >= _opt.DiskWarningPercent ? "#f5a524" : "#22c9a5",
            Meta = $"free {Fmt.Gb(r.FreeGb)}"
        }).ToList(), "%");

        // ---- Partisi C ----
        var cList = disks.Where(d => d.DriveLetter.Equals("C", StringComparison.OrdinalIgnoreCase)).ToList();
        vm.PartitionC = PartitionStat("C", cList);
        vm.PartitionCChart = Donut(new (string label, double val, string color)[]
            {
                ("Terpakai", cList.Sum(x => x.UsedGb), "#4f8cff"),
                ("Free", cList.Sum(x => x.FreeGb), "#22c9a5")
            }.Select(x => new Models.ChartPoint { Label = x.label, Value = Fmt.N(x.val), Color = x.color }).ToList(), " GB");

        vm.CriticalCDrive = deviceRows
            .Where(r => r.CUsedPercent >= _opt.DiskCriticalPercent || (r.CSizeGb > 0 && r.CFreeGb <= 15))
            .OrderByDescending(r => r.CUsedPercent).Take(12).ToList();

        // ---- Partisi D & lainnya ----
        var others = disks.Where(d => !d.DriveLetter.Equals("C", StringComparison.OrdinalIgnoreCase)).ToList();
        vm.OtherPartitions = others.GroupBy(d => d.DriveLetter.ToUpperInvariant())
            .OrderBy(g => g.Key)
            .Select(g => PartitionStat(g.Key, g.ToList()))
            .ToList();

        vm.PartitionUsageChart = Bar(
            new[] { vm.PartitionC }.Concat(vm.OtherPartitions).Where(p => p != null && p.DeviceCount > 0)
                .Select(p => new Models.ChartPoint
                {
                    Label = p!.DriveLetter + ":",
                    Value = Fmt.N(p.AvgUsedPercent),
                    Color = p.AvgUsedPercent >= _opt.DiskCriticalPercent ? "#ef4d6f" : p.AvgUsedPercent >= _opt.DiskWarningPercent ? "#f5a524" : Fmt.Color(0),
                    Meta = $"{p.DeviceCount} perangkat · {Fmt.Gb(p.FreeGb)} free"
                }).ToList(), "%");

        vm.CriticalOtherDrives = others
            .Where(d => d.UsedPercent >= _opt.DiskCriticalPercent)
            .OrderByDescending(d => d.UsedPercent)
            .Take(12)
            .Select(d => new Models.DeviceStorageRow
            {
                Hostname = d.Device?.Hostname ?? deviceById.GetValueOrDefault(d.DeviceId)?.Hostname ?? $"#{d.DeviceId}",
                Department = d.Device?.Department,
                DeviceType = d.Device?.DeviceType,
                TotalGb = d.SizeGb,
                UsedGb = d.UsedGb,
                FreeGb = d.FreeGb,
                UsedPercent = d.UsedPercent,
                CSizeGb = d.SizeGb,
                CFreeGb = d.FreeGb,
                CUsedPercent = d.UsedPercent,
                OtherDrives = $"{d.DriveLetter}: {d.Label}",
                HealthStatus = d.HealthStatus,
                CollectedAtUtc = d.CollectedAtUtc
            }).ToList();

        // ---- Top 10 PCs with Lowest Storage (sisa ruang paling sedikit) ----
        vm.Top10LowestStorage = deviceRows
            .Where(r => r.TotalGb > 0)
            .OrderBy(r => r.FreeGb)
            .ThenByDescending(r => r.UsedPercent)
            .Take(10).ToList();

        vm.Top10Chart = Bar(vm.Top10LowestStorage.Select(r => new Models.ChartPoint
        {
            Label = r.Hostname,
            Value = Fmt.N(r.FreeGb),
            Color = r.FreeGb <= 20 ? "#ef4d6f" : r.FreeGb <= 50 ? "#f5a524" : "#22c9a5",
            Meta = $"{Fmt.Percent(r.UsedPercent)} terpakai dari {Fmt.Gb(r.TotalGb)}"
        }).ToList(), " GB");

        // ---- Tren storage fleet 14 hari ----
        var days = Fmt.LastDays(_opt.TrendDays, now);
        var hist = await DailySnapshotsAsync(days[0]);
        // Tren storage fleet: rata-rata per perangkat dikali jumlah perangkat
        // supaya hari terakhir (data belum penuh) tidak terlihat anjlok.
        var usedPerDeviceByDay = hist.GroupBy(s => s.CollectedAtUtc.Date)
            .ToDictionary(g => g.Key, g => g.Average(x => x.DiskUsedGb) / 1024.0 * devices.Count);
        var totalPerDeviceByDay = hist.GroupBy(s => s.CollectedAtUtc.Date)
            .ToDictionary(g => g.Key, g => g.Average(x => x.DiskTotalGb) / 1024.0 * devices.Count);

        vm.StorageTrend = new Models.ChartModel
        {
            Labels = days.Select(Fmt.Day).ToList(),
            ValueSuffix = " TB",
            Series = new List<Models.ChartSeries>
            {
                new() { Name = "Kapasitas", Color = "#9aa5b1", Data = Fmt.FillSeries(days, totalPerDeviceByDay, 2) },
                new() { Name = "Terpakai", Color = "#4f8cff", Data = Fmt.FillSeries(days, usedPerDeviceByDay, 2), Fill = true }
            }
        };

        return vm;
    }

    private Models.DrivePartitionStat PartitionStat(string letter, List<DiskDrive> list)
    {
        var total = list.Sum(x => x.SizeGb);
        var used = list.Sum(x => x.UsedGb);
        return new Models.DrivePartitionStat
        {
            DriveLetter = letter,
            DeviceCount = list.Count,
            TotalGb = total,
            UsedGb = used,
            FreeGb = total - used,
            AvgUsedPercent = list.Count == 0 ? 0 : list.Average(x => x.UsedPercent),
            WarningCount = list.Count(x => x.UsedPercent >= _opt.DiskWarningPercent && x.UsedPercent < _opt.DiskCriticalPercent),
            CriticalCount = list.Count(x => x.UsedPercent >= _opt.DiskCriticalPercent),
            Tone = list.Count > 0 && list.Max(x => x.UsedPercent) >= _opt.DiskCriticalPercent ? "bad"
                 : list.Count > 0 && list.Max(x => x.UsedPercent) >= _opt.DiskWarningPercent ? "warn" : "good"
        };
    }

    // ------------------------------------------------------------------
    // 04 - Agent Health & Telemetry
    // ------------------------------------------------------------------

    public async Task<Models.AgentHealthViewModel> GetAgentHealthAsync(int logPage = 1, int logPageSize = 25, string? search = null, string? status = null)
    {
        var vm = new Models.AgentHealthViewModel { LogPage = Math.Max(1, logPage), LogPageSize = logPageSize, LogFilter = search, StatusFilter = status };
        var now = DateTime.UtcNow;
        var devices = await DevicesAsync();
        var snaps = await LatestSnapshotsAsync();
        vm.DataNotice = await EmptyNoticeAsync();
        if (devices.Count == 0) return vm;

        var since = now.AddDays(-30);
        var runs = await _db.AgentRuns.AsNoTracking()
            .Where(r => r.StartedAtUtc >= since)
            .OrderByDescending(r => r.StartedAtUtc)
            .ToListAsync();

        var deviceName = devices.ToDictionary(d => d.Id, d => d.Hostname);

        // ---- Execution duration ----
        vm.TotalRuns = runs.Count;
        vm.FailedRuns = runs.Count(r => r.Status == "Failed");
        vm.AvgExecutionMs = runs.Count == 0 ? 0 : runs.Average(r => r.DurationMs);
        vm.MaxExecutionMs = runs.Count == 0 ? 0 : runs.Max(r => r.DurationMs);
        vm.P95ExecutionMs = Percentile(runs.Select(r => r.DurationMs).OrderBy(x => x).ToList(), 0.95);
        vm.SuccessRate = runs.Count == 0 ? 0 : 100.0 * runs.Count(r => r.Status == "Success") / runs.Count;

        var days = Fmt.LastDays(_opt.TrendDays, now);
        var avgByDay = runs.GroupBy(r => r.StartedAtUtc.Date)
            .ToDictionary(g => g.Key, g => g.Average(x => x.DurationMs));
        vm.ExecutionTrend = new Models.ChartModel
        {
            Labels = days.Select(Fmt.Day).ToList(),
            ValueSuffix = " ms",
            Series = new List<Models.ChartSeries>
            {
                new() { Name = "Avg durasi", Color = Fmt.Palette[0], Data = Fmt.FillSeries(days, avgByDay, 0), Fill = true }
            }
        };

        var histBuckets = new (string label, double min, double max)[]
        {
            ("< 1s", 0, 1000), ("1 - 3s", 1000, 3000), ("3 - 5s", 3000, 5000),
            ("5 - 10s", 5000, 10000), ("10 - 30s", 10000, 30000), ("> 30s", 30000, double.MaxValue)
        };
        vm.ExecutionHistogram = Bar(histBuckets.Select((b, i) => new Models.ChartPoint
        {
            Label = b.label,
            Value = runs.Count(r => r.DurationMs >= b.min && r.DurationMs < b.max),
            Color = Fmt.Color(i)
        }).ToList(), " run");

        vm.SlowestDevices = runs.GroupBy(r => r.DeviceId)
            .Select(g => new { Name = deviceName.GetValueOrDefault(g.Key, $"#{g.Key}"), Avg = g.Average(x => x.DurationMs), Count = g.Count() })
            .OrderByDescending(x => x.Avg).Take(10)
            .Select(x => new Models.NameCount { Name = x.Name, Count = x.Count, Sub = "avg " + Fmt.Ms(x.Avg) })
            .ToList();

        // ---- Sync status distribution (level perangkat) ----
        var rows = devices.Select(d => { snaps.TryGetValue(d.Id, out var s); return ToRow(d, s, now); }).ToList();
        var sync = new List<Models.NameCount>
        {
            new() { Name = "Online", Count = rows.Count(r => r.Status == "Online"), Tone = "good" },
            new() { Name = "Offline", Count = rows.Count(r => r.Status == "Offline"), Tone = "default" },
            new() { Name = "Warning", Count = rows.Count(r => r.Status == "Warning"), Tone = "warn" },
            new() { Name = "Error", Count = rows.Count(r => r.Status == "Error"), Tone = "bad" },
            new() { Name = "Antrian Tertunda", Count = rows.Count(r => r.OfflineQueueCount > 0), Tone = "warn" },
        };
        foreach (var s in sync) s.Percent = rows.Count == 0 ? 0 : 100.0 * s.Count / rows.Count;
        vm.SyncStatus = sync.Where(s => s.Count > 0).ToList();
        vm.SyncStatusChart = Donut(sync.Where(s => s.Name != "Antrian Tertunda")
            .Select(s => new Models.ChartPoint { Label = s.Name, Value = s.Count, Color = Fmt.StatusColor(s.Name) }).ToList(), " perangkat");

        var runStatus = new[] { "Success", "Partial", "Queued", "Failed" }
            .Select(st => new Models.NameCount { Name = st, Count = runs.Count(r => r.Status == st), Percent = runs.Count == 0 ? 0 : 100.0 * runs.Count(r => r.Status == st) / runs.Count })
            .Where(x => x.Count > 0).ToList();
        vm.RunStatusChart = Donut(runStatus.Select(s => new Models.ChartPoint
        {
            Label = s.Name,
            Value = s.Count,
            Color = s.Name switch { "Success" => "#22c9a5", "Partial" => "#f5a524", "Queued" => "#37c6f4", _ => "#ef4d6f" }
        }).ToList(), " run");

        // ---- Offline queue accumulation ----
        vm.DevicesWithQueue = rows.Count(r => r.OfflineQueueCount > 0);
        vm.TotalQueuedItems = rows.Sum(r => r.OfflineQueueCount);
        vm.MaxQueueCount = rows.Count == 0 ? 0 : rows.Max(r => r.OfflineQueueCount);

        var queueByDay = runs.GroupBy(r => r.StartedAtUtc.Date).ToDictionary(g => g.Key, g => (double)g.Sum(x => x.OfflineQueueCount));
        vm.QueueChart = new Models.ChartModel
        {
            Labels = days.Select(Fmt.Day).ToList(),
            ValueSuffix = " item",
            Series = new List<Models.ChartSeries>
            {
                new() { Name = "Item tertunda", Color = "#f5a524", Data = Fmt.FillSeries(days, queueByDay, 0), Fill = true }
            }
        };

        vm.QueueDevices = rows.Where(r => r.OfflineQueueCount > 0).OrderByDescending(r => r.OfflineQueueCount).Take(10).ToList();

        // ---- Resource footprint agent ----
        vm.AvgAgentCpu = runs.Count == 0 ? 0 : runs.Average(r => r.CpuPercent);
        vm.AvgAgentMemoryMb = runs.Count == 0 ? 0 : runs.Average(r => r.MemoryMb);
        vm.MaxAgentCpu = runs.Count == 0 ? 0 : runs.Max(r => r.CpuPercent);

        var cpuByDay = runs.GroupBy(r => r.StartedAtUtc.Date).ToDictionary(g => g.Key, g => g.Average(x => x.CpuPercent));
        var memByDay = runs.GroupBy(r => r.StartedAtUtc.Date).ToDictionary(g => g.Key, g => g.Average(x => x.MemoryMb));
        vm.FootprintTrend = new Models.ChartModel
        {
            Labels = days.Select(Fmt.Day).ToList(),
            ValueSuffix = "%",
            Series = new List<Models.ChartSeries>
            {
                new() { Name = "CPU agent (%)", Color = "#ef4d6f", Data = Fmt.FillSeries(days, cpuByDay, 2), Fill = true }
            }
        };
        vm.MemoryTrend = new Models.ChartModel
        {
            Labels = days.Select(Fmt.Day).ToList(),
            ValueSuffix = " MB",
            Series = new List<Models.ChartSeries>
            {
                new() { Name = "Memory agent (MB)", Color = "#37c6f4", Data = Fmt.FillSeries(days, memByDay, 1), Fill = true }
            }
        };

        vm.HeaviestAgents = runs.GroupBy(r => r.DeviceId)
            .Select(g => new { Name = deviceName.GetValueOrDefault(g.Key, $"#{g.Key}"), Cpu = g.Average(x => x.CpuPercent), Mem = g.Average(x => x.MemoryMb) })
            .OrderByDescending(x => x.Cpu).Take(10)
            .Select(x => new Models.NameCount { Name = x.Name, Percent = Fmt.N(x.Cpu, 2), Sub = $"{x.Cpu:0.00}% CPU · {x.Mem:0.0} MB" })
            .ToList();

        // ---- Collector mix ----
        var collectorCount = new Dictionary<string, int>();
        foreach (var r in runs)
        {
            if (string.IsNullOrWhiteSpace(r.CollectorsRun)) continue;
            foreach (var c in r.CollectorsRun.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                collectorCount[c] = collectorCount.GetValueOrDefault(c) + 1;
        }
        vm.CollectorMixChart = Bar(collectorCount.OrderByDescending(k => k.Value)
            .Select((k, i) => new Models.ChartPoint { Label = k.Key, Value = k.Value, Color = Fmt.Color(i + 1) }).ToList(), " run");

        vm.AgentVersions = devices.GroupBy(d => string.IsNullOrWhiteSpace(d.AgentVersion) ? "unknown" : d.AgentVersion)
            .Select(g => new Models.NameCount { Name = "v" + g.Key.TrimStart('v'), Count = g.Count(), Percent = 100.0 * g.Count() / devices.Count })
            .OrderByDescending(x => x.Name).ToList();

        // ---- Log riwayat aktivitas agent (paged) ----
        var logQuery = _db.AgentRuns.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            logQuery = logQuery.Where(r => r.Device!.Hostname.Contains(search)
                                        || (r.ErrorMessage != null && r.ErrorMessage.Contains(search))
                                        || (r.CollectorsRun != null && r.CollectorsRun.Contains(search)));
        if (!string.IsNullOrWhiteSpace(status))
            logQuery = status == "Error"
                ? logQuery.Where(r => r.Status == "Failed" || (r.ErrorMessage != null && r.ErrorMessage != ""))
                : logQuery.Where(r => r.Status == status);

        vm.LogTotalCount = await logQuery.CountAsync();
        var logs = await logQuery.Include(r => r.Device)
            .OrderByDescending(r => r.StartedAtUtc)
            .Skip((vm.LogPage - 1) * vm.LogPageSize)
            .Take(vm.LogPageSize)
            .ToListAsync();

        vm.Logs = logs.Select(ToLogRow).ToList();
        vm.ErrorLogs = (await _db.AgentRuns.AsNoTracking().Include(r => r.Device)
                .Where(r => r.Status == "Failed" || (r.ErrorMessage != null && r.ErrorMessage != ""))
                .OrderByDescending(r => r.StartedAtUtc).Take(8).ToListAsync())
            .Select(ToLogRow).ToList();

        vm.Kpis = new List<Models.KpiCard>
        {
            new() { Title = "Avg Execution Duration", Value = Fmt.Ms(vm.AvgExecutionMs), Sub = $"P95 {Fmt.Ms(vm.P95ExecutionMs)} · max {Fmt.Ms(vm.MaxExecutionMs)}", Icon = "clock", Tone = vm.AvgExecutionMs > 10000 ? "warn" : "good" },
            new() { Title = "Total Run (30 hari)", Value = vm.TotalRuns.ToString("#,0", CultureInfo.InvariantCulture), Sub = $"Success rate {Fmt.Percent(vm.SuccessRate)} · {vm.FailedRuns} gagal", Icon = "sync", Tone = vm.SuccessRate >= 95 ? "good" : vm.SuccessRate >= 80 ? "warn" : "bad", Progress = vm.SuccessRate },
            new() { Title = "Offline Queue", Value = vm.TotalQueuedItems.ToString(), Sub = $"{vm.DevicesWithQueue} perangkat punya antrian tertunda", Icon = "queue", Tone = vm.TotalQueuedItems > 0 ? "warn" : "good" },
            new() { Title = "Agent Footprint", Value = Fmt.N(vm.AvgAgentCpu, 2) + "% CPU", Sub = $"Rata-rata memori {Fmt.N(vm.AvgAgentMemoryMb)} MB per run", Icon = "cpu", Tone = vm.AvgAgentCpu > 5 ? "warn" : "good" },
        };

        return vm;

        Models.AgentLogRow ToLogRow(AgentRun r) => new()
        {
            Id = r.Id,
            Hostname = r.Device?.Hostname ?? deviceName.GetValueOrDefault(r.DeviceId, $"#{r.DeviceId}"),
            StartedAtUtc = r.StartedAtUtc,
            StartedText = Fmt.Relative(r.StartedAtUtc, now) + " · " + r.StartedAtUtc.ToString("dd MMM HH:mm:ss", CultureInfo.InvariantCulture),
            DurationMs = r.DurationMs,
            Status = r.Status,
            AgentVersion = r.AgentVersion,
            CollectorsRun = r.CollectorsRun,
            ItemsSent = r.ItemsSent,
            OfflineQueueCount = r.OfflineQueueCount,
            CpuPercent = r.CpuPercent,
            MemoryMb = r.MemoryMb,
            ErrorMessage = r.ErrorMessage,
            TriggerSource = r.TriggerSource
        };
    }

    private static double Percentile(List<double> sorted, double p)
    {
        if (sorted.Count == 0) return 0;
        var idx = (int)Math.Ceiling(p * sorted.Count) - 1;
        return sorted[Math.Clamp(idx, 0, sorted.Count - 1)];
    }

    // ------------------------------------------------------------------
    // 05 - Software Inventory & Compliance
    // ------------------------------------------------------------------

    public async Task<Models.SoftwareViewModel> GetSoftwareAsync(int page = 1, int pageSize = 25, string? search = null,
        string? publisher = null, string? compliance = null, string sort = "name")
    {
        var vm = new Models.SoftwareViewModel
        {
            Page = Math.Max(1, page),
            PageSize = pageSize,
            Search = search,
            PublisherFilter = publisher,
            ComplianceFilter = compliance,
            Sort = sort
        };
        var devices = await DevicesAsync();
        vm.DataNotice = await EmptyNoticeAsync();
        if (devices.Count == 0) return vm;

        var deviceCount = devices.Count;
        var sw = await _db.SoftwareInstalls.AsNoTracking().Include(s => s.Device).Where(s => s.IsInstalled).ToListAsync();
        var rules = await _db.ComplianceRules.AsNoTracking().Where(r => r.IsEnabled).ToListAsync();

        // ---- KPI ----
        var managed = sw.Count(s => s.ComplianceStatus != "Unmanaged");
        var compliant = sw.Count(s => s.ComplianceStatus == "Compliant");
        var outdated = sw.Count(s => s.ComplianceStatus == "Outdated");
        var prohibited = sw.Count(s => s.ComplianceStatus == "Prohibited");
        vm.CompliancePercent = managed == 0 ? 0 : 100.0 * compliant / managed;

        vm.Kpis = new List<Models.KpiCard>
        {
            new() { Title = "Total Instalasi", Value = sw.Count.ToString("#,0", CultureInfo.InvariantCulture), Sub = $"{sw.Select(s => s.Name).Distinct().Count()} aplikasi unik di {deviceCount} perangkat", Icon = "apps", Tone = "info" },
            new() { Title = "Versi Terdeteksi", Value = sw.Select(s => s.Name + "|" + s.Version).Distinct().Count().ToString(), Sub = $"{Fmt.N(sw.GroupBy(s => s.Name).Average(g => g.Select(x => x.Version).Distinct().Count()), 1)} versi rata-rata per aplikasi", Icon = "version", Tone = "default" },
            new() { Title = "Compliance", Value = Fmt.Percent(vm.CompliancePercent), Sub = $"{compliant} patuh · {outdated} outdated · {prohibited} dilarang (dari {managed} terkelola)", Icon = "shield", Tone = prohibited > 0 ? "bad" : outdated > 0 ? "warn" : "good", Progress = vm.CompliancePercent },
            new() { Title = "Publisher", Value = sw.Select(s => s.Publisher ?? "Unknown").Distinct().Count().ToString(), Sub = $"Aturan compliance aktif: {rules.Count} · {sw.Count - managed} instalasi belum terkelola aturan", Icon = "publisher", Tone = "default" },
        };

        // ---- Grouped by version ----
        vm.Grouped = sw.GroupBy(s => new { s.Name, s.Version })
            .Select(g => new Models.SoftwareGroupRow
            {
                Name = g.Key.Name,
                Version = g.Key.Version,
                Publisher = g.Select(x => x.Publisher).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                DeviceCount = g.Select(x => x.DeviceId).Distinct().Count(),
                CoveragePercent = 100.0 * g.Select(x => x.DeviceId).Distinct().Count() / deviceCount,
                TotalSizeMb = g.Sum(x => x.SizeMb),
                ComplianceStatus = g.Select(x => x.ComplianceStatus).OrderBy(ComplianceRank).First(),
                VersionCount = sw.Count(x => x.Name == g.Key.Name),
                LastSeenUtc = g.Max(x => x.LastSeenUtc)
            })
            .OrderByDescending(r => r.DeviceCount).ThenBy(r => r.Name).ThenByDescending(r => r.Version, StringComparer.OrdinalIgnoreCase)
            .ToList();

        vm.TopSoftwareChart = Bar(sw.GroupBy(s => s.Name)
            .Select(g => new { g.Key, C = g.Select(x => x.DeviceId).Distinct().Count() })
            .OrderByDescending(x => x.C).Take(15)
            .Select((x, i) => new Models.ChartPoint { Label = TruncName(x.Key), Value = x.C, Color = Fmt.Color(i) })
            .ToList(), " perangkat");

        vm.VersionFragmentationChart = Bar(sw.GroupBy(s => s.Name)
            .Select(g => new { Name = g.Key, Versions = g.Select(x => x.Version).Distinct().Count() })
            .Where(x => x.Versions > 1)
            .OrderByDescending(x => x.Versions).Take(12)
            .Select((x, i) => new Models.ChartPoint { Label = TruncName(x.Name), Value = x.Versions, Color = x.Versions >= 4 ? "#ef4d6f" : x.Versions >= 3 ? "#f5a524" : Fmt.Color(i) })
            .ToList(), " versi");

        vm.MostFragmented = sw.GroupBy(s => s.Name)
            .Select(g => new Models.NameCount
            {
                Name = g.Key,
                Count = g.Select(x => x.Version).Distinct().Count(),
                Sub = string.Join(", ", g.Select(x => x.Version).Distinct().OrderByDescending(v => v, VersionComparer.Instance).Take(4)),
                Percent = 100.0 * g.Select(x => x.DeviceId).Distinct().Count() / deviceCount
            })
            .Where(x => x.Count > 1)
            .OrderByDescending(x => x.Count).ThenByDescending(x => x.Percent)
            .Take(10).ToList();

        vm.Publishers = sw.GroupBy(s => string.IsNullOrWhiteSpace(s.Publisher) ? "Unknown" : s.Publisher!)
            .Select(g => new Models.NameCount { Name = g.Key, Count = g.Count(), Percent = 100.0 * g.Count() / sw.Count })
            .OrderByDescending(x => x.Count).Take(12).ToList();
        vm.PublisherChart = Donut(vm.Publishers.Select((p, i) => new Models.ChartPoint { Label = TruncName(p.Name, 26), Value = p.Count, Color = Fmt.Color(i) }).ToList(), " instalasi");

        // ---- Compliance summary & issues ----
        var compOrder = new[] { "Compliant", "Outdated", "Prohibited", "Unmanaged" };
        vm.ComplianceSummary = compOrder
            .Select(st => new Models.NameCount
            {
                Name = st,
                Count = sw.Count(s => s.ComplianceStatus == st),
                Percent = sw.Count == 0 ? 0 : 100.0 * sw.Count(s => s.ComplianceStatus == st) / sw.Count,
                Tone = st == "Compliant" ? "good" : st == "Outdated" ? "warn" : st == "Prohibited" ? "bad" : "default"
            })
            .Where(x => x.Count > 0).ToList();
        vm.ComplianceChart = Donut(vm.ComplianceSummary
            .Select(c => new Models.ChartPoint { Label = c.Name, Value = c.Count, Color = Fmt.ComplianceColor(c.Name) }).ToList(), " instalasi");

        var issues = new List<Models.ComplianceIssue>();
        foreach (var s in sw.Where(s => s.ComplianceStatus == "Prohibited"))
            issues.Add(new Models.ComplianceIssue
            {
                Hostname = s.Device?.Hostname ?? "-",
                Software = s.Name,
                Version = s.Version,
                Issue = "Versi dilarang oleh kebijakan IT",
                Tone = "bad",
                Recommendation = "Uninstall / ganti ke versi yang diizinkan"
            });
        foreach (var s in sw.Where(s => s.ComplianceStatus == "Outdated").OrderBy(s => s.Name))
        {
            var rule = rules.FirstOrDefault(r => r.SoftwareName.Equals(s.Name, StringComparison.OrdinalIgnoreCase));
            issues.Add(new Models.ComplianceIssue
            {
                Hostname = s.Device?.Hostname ?? "-",
                Software = s.Name,
                Version = s.Version,
                Issue = rule?.MinimumVersion is null ? "Versi di bawah standar" : $"Versi minimum {rule.MinimumVersion}",
                Tone = "warn",
                Recommendation = "Jadwalkan update"
            });
        }
        foreach (var rule in rules.Where(r => r.IsMandatory))
        {
            var missing = devices.Where(d => !sw.Any(s => s.DeviceId == d.Id && s.Name.Equals(rule.SoftwareName, StringComparison.OrdinalIgnoreCase))).ToList();
            foreach (var m in missing.Take(20))
                issues.Add(new Models.ComplianceIssue
                {
                    Hostname = m.Hostname,
                    Software = rule.SoftwareName,
                    Version = "-",
                    Issue = "Software wajib tidak terpasang",
                    Tone = "bad",
                    Recommendation = "Deploy melalui SCCM/Intune atau instalasi manual"
                });
        }
        vm.Issues = issues.OrderBy(i => i.Tone == "bad" ? 0 : 1).ThenBy(i => i.Hostname).Take(60).ToList();

        // ---- Detail (paged + filter) ----
        var detailQuery = _db.SoftwareInstalls.AsNoTracking().Include(s => s.Device).Where(s => s.IsInstalled);
        if (!string.IsNullOrWhiteSpace(search))
            detailQuery = detailQuery.Where(s => s.Name.Contains(search) || s.Version.Contains(search)
                                              || (s.Publisher != null && s.Publisher.Contains(search))
                                              || s.Device!.Hostname.Contains(search));
        if (!string.IsNullOrWhiteSpace(publisher))
            detailQuery = detailQuery.Where(s => s.Publisher == publisher);
        if (!string.IsNullOrWhiteSpace(compliance))
            detailQuery = detailQuery.Where(s => s.ComplianceStatus == compliance);

        detailQuery = sort switch
        {
            "host" => detailQuery.OrderBy(s => s.Device!.Hostname).ThenBy(s => s.Name),
            "version" => detailQuery.OrderBy(s => s.Name).ThenByDescending(s => s.Version),
            "publisher" => detailQuery.OrderBy(s => s.Publisher).ThenBy(s => s.Name),
            "size" => detailQuery.OrderByDescending(s => s.SizeMb),
            "compliance" => detailQuery.OrderBy(s => s.ComplianceStatus).ThenBy(s => s.Name),
            _ => detailQuery.OrderBy(s => s.Name).ThenBy(s => s.Version).ThenBy(s => s.Device!.Hostname)
        };

        vm.DetailTotalCount = await detailQuery.CountAsync();
        var details = await detailQuery.Skip((vm.Page - 1) * vm.PageSize).Take(vm.PageSize).ToListAsync();
        vm.Details = details.Select(s => new Models.SoftwareDetailRow
        {
            Hostname = s.Device?.Hostname ?? "-",
            Department = s.Device?.Department,
            Name = s.Name,
            Version = s.Version,
            Publisher = s.Publisher,
            InstallDate = s.InstallDate,
            SizeMb = s.SizeMb,
            Scope = s.Scope,
            ComplianceStatus = s.ComplianceStatus,
            LastSeenUtc = s.LastSeenUtc
        }).ToList();

        return vm;
    }

    private static int ComplianceRank(string s) => s switch { "Prohibited" => 0, "Outdated" => 1, "Unmanaged" => 2, _ => 3 };
    private static string TruncName(string n, int max = 22) => n.Length <= max ? n : n[..(max - 1)] + "…";

    private sealed class VersionComparer : IComparer<string>
    {
        public static readonly VersionComparer Instance = new();
        public int Compare(string? x, string? y) => Fmt.CompareVersions(x, y);
    }

    // ------------------------------------------------------------------
    // Device detail
    // ------------------------------------------------------------------

    public async Task<Models.DeviceDetailViewModel?> GetDeviceAsync(string hostname)
    {
        var now = DateTime.UtcNow;
        var d = await _db.Devices.AsNoTracking().FirstOrDefaultAsync(x => x.Hostname == hostname);
        if (d is null) return null;

        var snaps = await LatestSnapshotsAsync();
        snaps.TryGetValue(d.Id, out var s);
        var disks = await _db.DiskDrives.AsNoTracking().Where(x => x.DeviceId == d.Id).OrderBy(x => x.DriveLetter).ToListAsync();
        var sw = await _db.SoftwareInstalls.AsNoTracking().Where(x => x.DeviceId == d.Id && x.IsInstalled).OrderBy(x => x.Name).ToListAsync();
        var runs = await _db.AgentRuns.AsNoTracking().Where(x => x.DeviceId == d.Id)
            .OrderByDescending(x => x.StartedAtUtc).Take(25).ToListAsync();

        var row = ToRow(d, s, now);
        var hist = await _db.DeviceSnapshots.AsNoTracking()
            .Where(x => x.DeviceId == d.Id && x.CollectedAtUtc >= now.AddDays(-_opt.TrendDays))
            .OrderBy(x => x.CollectedAtUtc).ToListAsync();

        return new Models.DeviceDetailViewModel
        {
            Device = d,
            Disks = disks,
            Software = sw.Select(x => new Models.SoftwareDetailRow
            {
                Hostname = d.Hostname, Name = x.Name, Version = x.Version, Publisher = x.Publisher,
                InstallDate = x.InstallDate, SizeMb = x.SizeMb, Scope = x.Scope, ComplianceStatus = x.ComplianceStatus, LastSeenUtc = x.LastSeenUtc
            }).ToList(),
            Runs = runs.Select(r => new Models.AgentLogRow
            {
                Id = r.Id, Hostname = d.Hostname, StartedAtUtc = r.StartedAtUtc,
                StartedText = Fmt.Relative(r.StartedAtUtc, now) + " · " + r.StartedAtUtc.ToString("dd MMM HH:mm:ss", CultureInfo.InvariantCulture),
                DurationMs = r.DurationMs, Status = r.Status, AgentVersion = r.AgentVersion, CollectorsRun = r.CollectorsRun,
                ItemsSent = r.ItemsSent, OfflineQueueCount = r.OfflineQueueCount, CpuPercent = r.CpuPercent,
                MemoryMb = r.MemoryMb, ErrorMessage = r.ErrorMessage, TriggerSource = r.TriggerSource
            }).ToList(),
            CpuRamTrend = new Models.ChartModel
            {
                Labels = hist.Select(h => h.CollectedAtUtc.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture)).ToList(),
                Series = new List<Models.ChartSeries>
                {
                    new() { Name = "CPU %", Color = "#ef4d6f", Data = hist.Select(h => Fmt.N(h.CpuUsagePercent, 1)).ToList() },
                    new() { Name = "RAM %", Color = "#4f8cff", Data = hist.Select(h => Fmt.N(h.RamUsedPercent, 1)).ToList() }
                }
            },
            StorageTrend = new Models.ChartModel
            {
                Labels = hist.Select(h => h.CollectedAtUtc.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture)).ToList(),
                ValueSuffix = " GB",
                Series = new List<Models.ChartSeries>
                {
                    new() { Name = "Terpakai", Color = "#4f8cff", Data = hist.Select(h => Fmt.N(h.DiskUsedGb, 1)).ToList(), Fill = true },
                    new() { Name = "Free C", Color = "#22c9a5", Data = hist.Select(h => Fmt.N(h.CFreeGb, 1)).ToList() }
                }
            },
            UptimeTrend = new Models.ChartModel
            {
                Labels = hist.Select(h => h.CollectedAtUtc.ToString("dd/MM HH:mm", CultureInfo.InvariantCulture)).ToList(),
                ValueSuffix = " jam",
                Series = new List<Models.ChartSeries>
                {
                    new() { Name = "Uptime", Color = "#a06bff", Data = hist.Select(h => Fmt.N(h.UptimeSeconds / 3600.0, 1)).ToList(), Fill = true }
                }
            },
            Kpis = new List<Models.KpiCard>
            {
                new() { Title = "Status", Value = row.Status, Sub = "Terakhir melapor " + row.LastSeenText, Icon = "online", Tone = row.Status == "Online" ? "good" : row.Status == "Offline" ? "default" : "warn" },
                new() { Title = "Uptime", Value = row.UptimeText, Sub = row.UptimeCategory, Icon = "clock", Tone = "info" },
                new() { Title = "RAM", Value = d.RamTotalGb.ToString("0") + " GB", Sub = Fmt.Percent(row.RamUsedPercent) + " terpakai", Icon = "ram", Tone = row.RamUsedPercent >= _opt.RamPressurePercent ? "bad" : "good", Progress = row.RamUsedPercent },
                new() { Title = "Storage", Value = Fmt.Gb(d.TotalDiskGb), Sub = Fmt.Percent(row.DiskUsedPercent) + " terpakai · free " + Fmt.Gb(row.DiskFreeGb), Icon = "storage", Tone = row.DiskUsedPercent >= _opt.DiskCriticalPercent ? "bad" : row.DiskUsedPercent >= _opt.DiskWarningPercent ? "warn" : "good", Progress = row.DiskUsedPercent },
            }
        };
    }

    // ------------------------------------------------------------------
    // Ringkasan ringan untuk polling auto-refresh di topbar
    // ------------------------------------------------------------------

    public async Task<object> GetSummaryAsync()
    {
        var now = DateTime.UtcNow;
        var devices = await DevicesAsync();
        var snaps = await LatestSnapshotsAsync();
        var rows = devices.Select(d => { snaps.TryGetValue(d.Id, out var s); return ToRow(d, s, now); }).ToList();
        var total = devices.Sum(d => d.TotalDiskGb);
        var used = devices.Sum(d => d.UsedDiskGb);
        return new
        {
            totalDevices = devices.Count,
            online = rows.Count(r => r.Status == "Online"),
            offline = rows.Count(r => r.Status == "Offline"),
            warning = rows.Count(r => r.Status == "Warning" || r.Status == "Error"),
            storageTb = Fmt.N(total / 1024.0, 2),
            usedPercent = total > 0 ? Fmt.N(100.0 * used / total, 1) : 0,
            queued = rows.Sum(r => r.OfflineQueueCount),
            criticalDisk = rows.Count(r => r.DiskUsedPercent >= _opt.DiskCriticalPercent),
            generatedAtUtc = now.ToString("o", CultureInfo.InvariantCulture)
        };
    }

}
