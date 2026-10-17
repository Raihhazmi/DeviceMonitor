using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using DeviceMonitor.Shared.Entities;
using DeviceMonitor.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DeviceMonitor.Web.Services;

public class CsvImportResult
{
    public string FileName { get; set; } = "";
    public int RowsRead { get; set; }
    public int RowsInserted { get; set; }
    public int RowsUpdated { get; set; }
    public int RowsSkipped { get; set; }
    public List<string> Warnings { get; set; } = new();
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Import data inventaris dari CSV (hasil export PDQ Inventory / Lansweeper / SCCM / Excel).
/// Header dikenali case-insensitive dan boleh memakai spasi atau underscore.
/// </summary>
public class CsvImportService
{
    private readonly MonitorDbContext _db;
    private readonly ILogger<CsvImportService> _log;

    public CsvImportService(MonitorDbContext db, ILogger<CsvImportService> log)
    {
        _db = db;
        _log = log;
    }

    private static CsvConfiguration Cfg() => new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        PrepareHeaderForMatch = args => Norm(args.Header),
        MissingFieldFound = null,
        HeaderValidated = null,
        BadDataFound = null,
        TrimOptions = TrimOptions.Trim
    };

    private static string Norm(string h)
        => h.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "").Replace(":", "");

    // ---------------------------------------------------------------- devices

    public async Task<CsvImportResult> ImportDevicesAsync(Stream csv, CancellationToken ct = default)
    {
        var res = new CsvImportResult { FileName = "devices.csv" };
        try
        {
            using var reader = new StreamReader(csv);
            using var parser = new CsvReader(reader, Cfg());
            parser.Read(); parser.ReadHeader();
            var headers = parser.HeaderRecord?.Select(Norm).ToList() ?? new List<string>();
            if (!headers.Contains("hostname") && !headers.Contains("computername") && !headers.Contains("devicename"))
            {
                res.Error = "Kolom 'hostname' (atau computername/devicename) tidak ditemukan.";
                return res;
            }

            var existing = await _db.Devices.ToListAsync(ct);
            var byHost = existing.ToDictionary(d => d.Hostname, StringComparer.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;

            while (await parser.ReadAsync())
            {
                res.RowsRead++;
                var host = Str(parser, "hostname", "computername", "devicename", "name");
                if (string.IsNullOrWhiteSpace(host)) { res.RowsSkipped++; continue; }

                if (!byHost.TryGetValue(host, out var d))
                {
                    d = new Device { Hostname = host, FirstSeenUtc = now, CreatedAtUtc = now };
                    _db.Devices.Add(d);
                    byHost[host] = d;
                    res.RowsInserted++;
                }
                else res.RowsUpdated++;

                d.AssetTag = Str(parser, "assettag", "tag", "kodeaset") ?? d.AssetTag;
                d.DeviceType = Str(parser, "devicetype", "type", "jenis") ?? d.DeviceType;
                d.Manufacturer = Str(parser, "manufacturer", "vendor", "pabrikan") ?? d.Manufacturer;
                d.Model = Str(parser, "model") ?? d.Model;
                d.SerialNumber = Str(parser, "serialnumber", "serial", "sn") ?? d.SerialNumber;
                d.OsName = Str(parser, "osname", "os", "operatingsystem", "sistemoperasi") ?? d.OsName;
                d.OsVersion = Str(parser, "osversion", "version") ?? d.OsVersion;
                d.OsBuild = Str(parser, "osbuild", "build") ?? d.OsBuild;
                d.OsArchitecture = Str(parser, "osarchitecture", "arch", "architecture") ?? d.OsArchitecture;
                d.BiosVendor = Str(parser, "biosvendor") ?? d.BiosVendor;
                d.BiosVersion = Str(parser, "biosversion", "bios") ?? d.BiosVersion;
                d.CpuName = Str(parser, "cpuname", "cpu", "processor") ?? d.CpuName;
                d.CpuCores = (int)(Num(parser, "cpucores", "cores") ?? d.CpuCores);
                d.CpuLogicalProcessors = (int)(Num(parser, "cpulogicalprocessors", "logicalprocessors", "threads") ?? d.CpuLogicalProcessors);

                var ram = Num(parser, "ramtotalgb", "ramgb", "ram", "totalramgb", "memorygb");
                if (ram is > 0) d.RamTotalGb = ram.Value > 2048 ? ram.Value / 1024.0 : ram.Value; // toleransi input MB

                d.IpAddress = Str(parser, "ipaddress", "ip", "ipv4") ?? d.IpAddress;
                d.MacAddress = Str(parser, "macaddress", "mac") ?? d.MacAddress;
                d.Domain = Str(parser, "domain", "workgroup") ?? d.Domain;
                d.Department = Str(parser, "department", "departemen", "divisi", "division") ?? d.Department;
                d.Location = Str(parser, "location", "lokasi", "site", "ruangan") ?? d.Location;
                d.AssignedUser = Str(parser, "assigneduser", "user", "pengguna", "username") ?? d.AssignedUser;
                d.AgentVersion = Str(parser, "agentversion", "agent") ?? d.AgentVersion;
                d.Status = Str(parser, "status") ?? d.Status;
                d.IsActive = true;

                var lastSeen = Date(parser, "lastseenutc", "lastseen", "terakhirterlihat");
                if (lastSeen is not null) d.LastSeenUtc = lastSeen;

                var total = Num(parser, "totaldiskgb", "disktotalgb", "totalstoragegb");
                if (total is > 0) d.TotalDiskGb = total.Value;
                var used = Num(parser, "useddiskgb", "diskusedgb", "usedstoragegb");
                if (used is > 0) d.UsedDiskGb = used.Value;
                var uptime = Num(parser, "uptimeseconds", "uptime");
                if (uptime is > 0) d.UptimeSeconds = uptime.Value;

                d.UpdatedAtUtc = now;
                if (string.IsNullOrWhiteSpace(d.DeviceType)) d.DeviceType = "Desktop";
                if (string.IsNullOrWhiteSpace(d.Status)) d.Status = "Offline";
                if (string.IsNullOrWhiteSpace(d.AgentVersion)) d.AgentVersion = "1.0.0";
            }

            await _db.SaveChangesAsync(ct);
            res.Success = true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Gagal import devices.csv");
            res.Error = ex.Message;
        }
        return res;
    }

    // ---------------------------------------------------------------- disks

    public async Task<CsvImportResult> ImportDisksAsync(Stream csv, CancellationToken ct = default)
    {
        var res = new CsvImportResult { FileName = "disks.csv" };
        try
        {
            using var reader = new StreamReader(csv);
            using var parser = new CsvReader(reader, Cfg());
            parser.Read(); parser.ReadHeader();

            var devices = await _db.Devices.ToListAsync(ct);
            var byHost = devices.ToDictionary(d => d.Hostname, StringComparer.OrdinalIgnoreCase);
            var disks = await _db.DiskDrives.ToListAsync(ct);
            var byKey = disks.ToDictionary(d => $"{d.DeviceId}|{d.DriveLetter.ToUpperInvariant()}");
            var now = DateTime.UtcNow;

            while (await parser.ReadAsync())
            {
                res.RowsRead++;
                var host = Str(parser, "hostname", "computername", "devicename");
                if (host is null || !byHost.TryGetValue(host, out var dev)) { res.RowsSkipped++; continue; }

                var letter = (Str(parser, "driveletter", "drive", "partisi", "letter") ?? "C").Trim().TrimEnd(':', '\\').ToUpperInvariant();
                var key = $"{dev.Id}|{letter}";
                if (!byKey.TryGetValue(key, out var disk))
                {
                    disk = new DiskDrive { DeviceId = dev.Id, DriveLetter = letter };
                    _db.DiskDrives.Add(disk);
                    byKey[key] = disk;
                    res.RowsInserted++;
                }
                else res.RowsUpdated++;

                disk.Label = Str(parser, "label", "volumename", "namapartisi") ?? disk.Label;
                disk.DriveType = Str(parser, "drivetype", "type") ?? disk.DriveType ?? "Fixed";
                disk.MediaType = Str(parser, "mediatype", "media") ?? disk.MediaType ?? "Unknown";
                disk.FileSystem = Str(parser, "filesystem", "fs") ?? disk.FileSystem;

                var size = Num(parser, "sizegb", "totalgb", "capacitygb", "kapasitasgb") ?? disk.SizeGb;
                var free = Num(parser, "freegb", "freespacegb", "sisagb") ?? disk.FreeGb;
                var used = Num(parser, "usedgb", "terpakaigb");
                disk.SizeGb = size;
                disk.FreeGb = Math.Min(free, size);
                disk.UsedGb = used is > 0 ? Math.Min(used.Value, size) : Math.Max(0, size - disk.FreeGb);
                disk.UsedPercent = size > 0 ? Math.Round(100.0 * disk.UsedGb / size, 2) : 0;
                disk.HealthStatus = Str(parser, "healthstatus", "health", "status")
                                    ?? (disk.UsedPercent >= 90 ? "Critical" : disk.UsedPercent >= 80 ? "Warning" : "Healthy");
                disk.CollectedAtUtc = Date(parser, "collectedatutc", "collectedat", "tanggal") ?? now;
            }

            // sinkronkan total storage di master perangkat
            var grouped = await _db.DiskDrives.GroupBy(d => d.DeviceId)
                .Select(g => new { DeviceId = g.Key, Total = g.Sum(x => x.SizeGb), Used = g.Sum(x => x.UsedGb) })
                .ToListAsync(ct);
            foreach (var g in grouped)
            {
                if (!byHost.Values.Any(d => d.Id == g.DeviceId)) continue;
                var dev = devices.First(d => d.Id == g.DeviceId);
                dev.TotalDiskGb = Math.Round(g.Total, 2);
                dev.UsedDiskGb = Math.Round(g.Used, 2);
            }

            await _db.SaveChangesAsync(ct);
            res.Success = true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Gagal import disks.csv");
            res.Error = ex.Message;
        }
        return res;
    }

    // ---------------------------------------------------------------- software

    public async Task<CsvImportResult> ImportSoftwareAsync(Stream csv, CancellationToken ct = default)
    {
        var res = new CsvImportResult { FileName = "software.csv" };
        try
        {
            using var reader = new StreamReader(csv);
            using var parser = new CsvReader(reader, Cfg());
            parser.Read(); parser.ReadHeader();

            var devices = await _db.Devices.ToListAsync(ct);
            var byHost = devices.ToDictionary(d => d.Hostname, StringComparer.OrdinalIgnoreCase);
            var sw = await _db.SoftwareInstalls.ToListAsync(ct);
            var byKey = sw.ToDictionary(s => $"{s.DeviceId}|{s.Name}|{s.Version}", StringComparer.OrdinalIgnoreCase);
            var rules = await ComplianceService.LoadRulesAsync(_db);
            var now = DateTime.UtcNow;

            while (await parser.ReadAsync())
            {
                res.RowsRead++;
                var host = Str(parser, "hostname", "computername", "devicename", "namakomputer");
                var name = Str(parser, "name", "softwarename", "namasoftware", "displayname", "aplikasi");
                if (host is null || name is null || !byHost.TryGetValue(host, out var dev)) { res.RowsSkipped++; continue; }

                var version = Str(parser, "version", "versi", "displayversion") ?? "unknown";
                if (string.IsNullOrWhiteSpace(version)) version = "unknown";
                var key = $"{dev.Id}|{name}|{version}";

                if (!byKey.TryGetValue(key, out var item))
                {
                    item = new SoftwareInstall { DeviceId = dev.Id, Name = name, Version = version, FirstSeenUtc = now };
                    _db.SoftwareInstalls.Add(item);
                    byKey[key] = item;
                    res.RowsInserted++;
                }
                else res.RowsUpdated++;

                item.Publisher = Str(parser, "publisher", "penerbit", "vendor") ?? item.Publisher;
                item.InstallDate = Date(parser, "installdate", "tanggalinstal", "installedon") ?? item.InstallDate;
                item.SizeMb = Num(parser, "sizemb", "size", "ukuranmb") ?? item.SizeMb;
                item.Scope = Str(parser, "scope", "installscope") ?? item.Scope;
                item.ComplianceStatus = ComplianceService.Evaluate(name, version, rules);
                item.LastSeenUtc = Date(parser, "lastseenutc", "lastseen") ?? now;
                item.IsInstalled = true;
            }

            await _db.SaveChangesAsync(ct);
            res.Success = true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Gagal import software.csv");
            res.Error = ex.Message;
        }
        return res;
    }

    // ---------------------------------------------------------------- snapshots & agent runs

    public async Task<CsvImportResult> ImportSnapshotsAsync(Stream csv, CancellationToken ct = default)
    {
        var res = new CsvImportResult { FileName = "snapshots.csv" };
        try
        {
            using var reader = new StreamReader(csv);
            using var parser = new CsvReader(reader, Cfg());
            parser.Read(); parser.ReadHeader();

            var devices = await _db.Devices.ToListAsync(ct);
            var byHost = devices.ToDictionary(d => d.Hostname, StringComparer.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;

            while (await parser.ReadAsync())
            {
                res.RowsRead++;
                var host = Str(parser, "hostname", "computername", "devicename");
                if (host is null || !byHost.TryGetValue(host, out var dev)) { res.RowsSkipped++; continue; }

                var s = new DeviceSnapshot
                {
                    DeviceId = dev.Id,
                    CollectedAtUtc = Date(parser, "collectedatutc", "collectedat", "timestamp", "waktu") ?? now,
                    Status = Str(parser, "status") ?? "Online",
                    CpuUsagePercent = Num(parser, "cpuusagepercent", "cpu", "cpupersen") ?? 0,
                    RamUsedGb = Num(parser, "ramusedgb", "ramterpakai", "ramused") ?? 0,
                    RamTotalGb = Num(parser, "ramtotalgb", "ramgb") ?? dev.RamTotalGb,
                    DiskTotalGb = Num(parser, "disktotalgb", "totalgb") ?? dev.TotalDiskGb,
                    DiskUsedGb = Num(parser, "diskusedgb", "usedgb") ?? dev.UsedDiskGb,
                    DiskFreeGb = Num(parser, "diskfreegb", "freegb") ?? 0,
                    CFreeGb = Num(parser, "cfreegb", "cfree") ?? 0,
                    UptimeSeconds = Num(parser, "uptimeseconds", "uptime") ?? 0,
                    ProcessCount = (long)(Num(parser, "processcount", "proses") ?? 0),
                    AgentCpuPercent = Num(parser, "agentcpupercent", "agentcpu") ?? 0,
                    AgentMemoryMb = Num(parser, "agentmemorymb", "agentmem") ?? 0,
                    OfflineQueueCount = (int)(Num(parser, "offlinequeuecount", "queue", "antrian") ?? 0),
                    NetworkRxBytes = (long)(Num(parser, "networkrxbytes", "rx") ?? 0),
                    NetworkTxBytes = (long)(Num(parser, "networktxbytes", "tx") ?? 0)
                };
                s.RamUsedPercent = s.RamTotalGb > 0 ? Math.Round(100 * s.RamUsedGb / s.RamTotalGb, 2) : Num(parser, "ramusedpercent", "rampersen") ?? 0;
                s.DiskUsedPercent = s.DiskTotalGb > 0 ? Math.Round(100 * s.DiskUsedGb / s.DiskTotalGb, 2) : Num(parser, "diskusedpercent", "diskpersen") ?? 0;
                if (s.DiskFreeGb <= 0) s.DiskFreeGb = Math.Max(0, s.DiskTotalGb - s.DiskUsedGb);

                _db.DeviceSnapshots.Add(s);
                res.RowsInserted++;

                if (dev.LastSeenUtc is null || s.CollectedAtUtc > dev.LastSeenUtc)
                {
                    dev.LastSeenUtc = s.CollectedAtUtc;
                    dev.Status = s.Status;
                    dev.UptimeSeconds = s.UptimeSeconds;
                    if (s.RamTotalGb > 0) dev.RamTotalGb = s.RamTotalGb;
                    if (s.DiskTotalGb > 0) dev.TotalDiskGb = s.DiskTotalGb;
                    if (s.DiskUsedGb > 0) dev.UsedDiskGb = s.DiskUsedGb;
                }
            }

            await _db.SaveChangesAsync(ct);
            res.Success = true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Gagal import snapshots.csv");
            res.Error = ex.Message;
        }
        return res;
    }

    public async Task<CsvImportResult> ImportAgentRunsAsync(Stream csv, CancellationToken ct = default)
    {
        var res = new CsvImportResult { FileName = "agentruns.csv" };
        try
        {
            using var reader = new StreamReader(csv);
            using var parser = new CsvReader(reader, Cfg());
            parser.Read(); parser.ReadHeader();

            var devices = await _db.Devices.ToListAsync(ct);
            var byHost = devices.ToDictionary(d => d.Hostname, StringComparer.OrdinalIgnoreCase);
            var now = DateTime.UtcNow;

            while (await parser.ReadAsync())
            {
                res.RowsRead++;
                var host = Str(parser, "hostname", "computername", "devicename");
                if (host is null || !byHost.TryGetValue(host, out var dev)) { res.RowsSkipped++; continue; }

                var started = Date(parser, "startedatutc", "startedat", "mulai") ?? now;
                var finished = Date(parser, "finishedatutc", "finishedat", "selesai");
                var duration = Num(parser, "durationms", "durasi", "duration");

                _db.AgentRuns.Add(new AgentRun
                {
                    DeviceId = dev.Id,
                    StartedAtUtc = started,
                    FinishedAtUtc = finished ?? started,
                    DurationMs = duration ?? (finished is null ? 0 : (finished.Value - started).TotalMilliseconds),
                    Status = Str(parser, "status") ?? "Success",
                    AgentVersion = Str(parser, "agentversion", "agent") ?? dev.AgentVersion,
                    CollectorsRun = Str(parser, "collectorsrun", "collectors"),
                    ItemsSent = (int)(Num(parser, "itemssent", "items") ?? 0),
                    OfflineQueueCount = (int)(Num(parser, "offlinequeuecount", "queue") ?? 0),
                    CpuPercent = Num(parser, "cpupercent", "cpu") ?? 0,
                    MemoryMb = Num(parser, "memorymb", "memory") ?? 0,
                    ErrorMessage = Str(parser, "errormessage", "error", "pesanerror"),
                    TriggerSource = Str(parser, "triggersource", "trigger") ?? "import"
                });
                res.RowsInserted++;
            }

            await _db.SaveChangesAsync(ct);
            res.Success = true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Gagal import agentruns.csv");
            res.Error = ex.Message;
        }
        return res;
    }

    // ---------------------------------------------------------------- helpers

    private static string? Str(CsvReader p, params string[] names)
    {
        foreach (var n in names)
        {
            try
            {
                if (p.TryGetField(n, out string? v) && !string.IsNullOrWhiteSpace(v)) return v.Trim();
            }
            catch { /* kolom tidak ada */ }
        }
        return null;
    }

    private static double? Num(CsvReader p, params string[] names)
    {
        var s = Str(p, names);
        if (s is null) return null;
        s = s.Replace("GB", "", StringComparison.OrdinalIgnoreCase)
             .Replace("MB", "", StringComparison.OrdinalIgnoreCase)
             .Replace("%", "", StringComparison.OrdinalIgnoreCase)
             .Replace(" ", "");
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.GetCultureInfo("id-ID"), out var v2)) return v2;
        return null;
    }

    private static DateTime? Date(CsvReader p, params string[] names)
    {
        var s = Str(p, names);
        if (s is null) return null;
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d)) return d;
        if (DateTime.TryParse(s, CultureInfo.GetCultureInfo("id-ID"), out var d2)) return d2;
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var oa))
        {
            try { return DateTime.FromOADate(oa); } catch { /* ignore */ }
        }
        return null;
    }
}
