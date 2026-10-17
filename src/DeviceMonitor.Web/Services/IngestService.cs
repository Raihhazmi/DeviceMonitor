using DeviceMonitor.Shared.Contracts;
using DeviceMonitor.Shared.Entities;
using DeviceMonitor.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DeviceMonitor.Web.Services;

/// <summary>
/// Menyimpan payload agent ke database (upsert master + latest state disk/software
/// + append snapshot telemetry + append log agent run).
/// </summary>
public class IngestService
{
    private readonly MonitorDbContext _db;
    private readonly ILogger<IngestService> _log;

    public IngestService(MonitorDbContext db, ILogger<IngestService> log)
    {
        _db = db;
        _log = log;
    }

    public async Task<IngestResult> IngestAsync(IngestPayload payload, CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        var result = new IngestResult { ReceivedAtUtc = started };

        var hostname = payload.Device?.Hostname?.Trim();
        if (string.IsNullOrWhiteSpace(hostname))
        {
            result.Success = false;
            result.Error = "Field device.hostname wajib diisi.";
            return result;
        }

        var now = DateTime.UtcNow;
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.Hostname == hostname, ct);
        var isNew = device is null;
        if (device is null)
        {
            device = new Device
            {
                Hostname = hostname,
                FirstSeenUtc = now,
                CreatedAtUtc = now
            };
            _db.Devices.Add(device);
        }

        // ---------- 1. Master perangkat ----------
        var dp = payload.Device!;
        device.AssetTag = Pick(dp.AssetTag, device.AssetTag);
        device.DeviceType = Pick(dp.DeviceType, device.DeviceType, "Desktop")!;
        device.Manufacturer = Pick(dp.Manufacturer, device.Manufacturer);
        device.Model = Pick(dp.Model, device.Model);
        device.SerialNumber = Pick(dp.SerialNumber, device.SerialNumber);
        device.OsName = Pick(dp.OsName, device.OsName);
        device.OsVersion = Pick(dp.OsVersion, device.OsVersion);
        device.OsBuild = Pick(dp.OsBuild, device.OsBuild);
        device.OsArchitecture = Pick(dp.OsArchitecture, device.OsArchitecture);
        device.BiosVendor = Pick(dp.BiosVendor, device.BiosVendor);
        device.BiosVersion = Pick(dp.BiosVersion, device.BiosVersion);
        if (dp.BiosReleaseDate is not null) device.BiosReleaseDate = dp.BiosReleaseDate;
        device.CpuName = Pick(dp.CpuName, device.CpuName);
        if (dp.CpuCores > 0) device.CpuCores = dp.CpuCores;
        if (dp.CpuLogicalProcessors > 0) device.CpuLogicalProcessors = dp.CpuLogicalProcessors;
        if (dp.RamTotalGb > 0) device.RamTotalGb = dp.RamTotalGb;
        device.IpAddress = Pick(dp.IpAddress, device.IpAddress);
        device.MacAddress = Pick(dp.MacAddress, device.MacAddress);
        device.Domain = Pick(dp.Domain, device.Domain);
        device.Department = Pick(dp.Department, device.Department);
        device.Location = Pick(dp.Location, device.Location);
        device.AssignedUser = Pick(dp.AssignedUser, device.AssignedUser);
        device.AgentVersion = Pick(dp.AgentVersion, device.AgentVersion, "1.0.0")!;
        if (dp.UptimeSeconds is > 0) device.UptimeSeconds = dp.UptimeSeconds.Value;
        device.IsActive = true;
        device.UpdatedAtUtc = now;

        var collectedAt = dp.CollectedAtUtc ?? payload.Telemetry?.CollectedAtUtc ?? now;
        device.LastSeenUtc = collectedAt > now ? now : collectedAt;

        // Simpan master perangkat lebih dulu agar DeviceId tersedia untuk relasi di bawah.
        if (isNew) await _db.SaveChangesAsync(ct);

        // ---------- 2. Disk / partisi (latest state) ----------
        if (payload.Disks is { Count: > 0 })
        {
            var existing = await _db.DiskDrives.Where(d => d.DeviceId == device.Id).ToListAsync(ct);
            var byLetter = existing.ToDictionary(d => d.DriveLetter.ToUpperInvariant(), d => d);

            foreach (var p in payload.Disks)
            {
                var letter = (p.DriveLetter ?? "C").Trim().TrimEnd(':', '\\').ToUpperInvariant();
                if (letter.Length == 0) letter = "C";
                var size = Math.Max(0, p.SizeGb);
                var free = p.FreeGb >= 0 ? Math.Min(p.FreeGb, size) : 0;
                var used = p.UsedGb is > 0 ? Math.Min(p.UsedGb.Value, size) : size - free;

                if (!byLetter.TryGetValue(letter, out var disk))
                {
                    disk = new DiskDrive { DeviceId = device.Id, DriveLetter = letter };
                    _db.DiskDrives.Add(disk);
                    byLetter[letter] = disk;
                }

                disk.Label = p.Label ?? disk.Label;
                disk.DriveType = p.DriveType ?? disk.DriveType ?? "Fixed";
                disk.MediaType = p.MediaType ?? disk.MediaType ?? "Unknown";
                disk.FileSystem = p.FileSystem ?? disk.FileSystem;
                disk.SizeGb = size;
                disk.FreeGb = free;
                disk.UsedGb = used;
                disk.UsedPercent = size > 0 ? Math.Round(100.0 * used / size, 2) : 0;
                disk.HealthStatus = p.HealthStatus ?? HealthOf(disk.UsedPercent);
                disk.CollectedAtUtc = collectedAt;
                result.DisksUpserted++;
            }

            // partisi yang tidak dilaporkan lagi dianggap dilepas
            var reported = payload.Disks
                .Select(p => (p.DriveLetter ?? "C").Trim().TrimEnd(':', '\\').ToUpperInvariant())
                .ToHashSet();
            foreach (var gone in existing.Where(e => !reported.Contains(e.DriveLetter.ToUpperInvariant())).ToList())
                _db.DiskDrives.Remove(gone);

            device.TotalDiskGb = Math.Round(byLetter.Values.Sum(d => d.SizeGb), 2);
            device.UsedDiskGb = Math.Round(byLetter.Values.Sum(d => d.UsedGb), 2);
        }

        // ---------- 3. Software inventory ----------
        var rules = await ComplianceService.LoadRulesAsync(_db);
        if (payload.Software is { Count: > 0 })
        {
            var existing = await _db.SoftwareInstalls.Where(s => s.DeviceId == device.Id).ToListAsync(ct);
            var key = new Dictionary<string, SoftwareInstall>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in existing) key[$"{e.Name}|{e.Version}"] = e;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in payload.Software)
            {
                var name = (p.Name ?? "").Trim();
                if (name.Length == 0) continue;
                var version = string.IsNullOrWhiteSpace(p.Version) ? "unknown" : p.Version.Trim();
                var k = $"{name}|{version}";
                seen.Add(k);

                if (!key.TryGetValue(k, out var sw))
                {
                    sw = new SoftwareInstall
                    {
                        DeviceId = device.Id,
                        Name = name,
                        Version = version,
                        FirstSeenUtc = now
                    };
                    _db.SoftwareInstalls.Add(sw);
                    key[k] = sw;
                }

                sw.Publisher = p.Publisher ?? sw.Publisher;
                sw.InstallDate = p.InstallDate ?? sw.InstallDate;
                sw.SizeMb = p.SizeMb > 0 ? p.SizeMb : sw.SizeMb;
                sw.Scope = p.Scope ?? sw.Scope;
                sw.ComplianceStatus = ComplianceService.Evaluate(name, version, rules);
                sw.LastSeenUtc = now;
                sw.IsInstalled = true;
                result.SoftwareUpserted++;
            }

            if (payload.IsFullSoftwareInventory)
            {
                foreach (var e in existing.Where(e => e.IsInstalled && !seen.Contains($"{e.Name}|{e.Version}")))
                {
                    e.IsInstalled = false;
                    result.SoftwareRemoved++;
                }
            }
        }

        // ---------- 4. Telemetry snapshot ----------
        var t = payload.Telemetry;
        var snapshot = new DeviceSnapshot
        {
            DeviceId = device.Id,
            CollectedAtUtc = t?.CollectedAtUtc ?? collectedAt,
            CpuUsagePercent = Clamp(t?.CpuUsagePercent ?? 0, 0, 100),
            RamTotalGb = t?.RamTotalGb is > 0 ? t.RamTotalGb.Value : device.RamTotalGb,
            RamUsedGb = Math.Max(0, t?.RamUsedGb ?? 0),
            UptimeSeconds = t?.UptimeSeconds ?? device.UptimeSeconds,
            ProcessCount = t?.ProcessCount ?? 0,
            AgentCpuPercent = Clamp(t?.AgentCpuPercent ?? 0, 0, 100),
            AgentMemoryMb = Math.Max(0, t?.AgentMemoryMb ?? 0),
            OfflineQueueCount = Math.Max(0, t?.OfflineQueueCount ?? 0),
            NetworkRxBytes = Math.Max(0, t?.NetworkRxBytes ?? 0),
            NetworkTxBytes = Math.Max(0, t?.NetworkTxBytes ?? 0)
        };
        snapshot.RamUsedPercent = snapshot.RamTotalGb > 0
            ? Math.Round(Clamp(100.0 * snapshot.RamUsedGb / snapshot.RamTotalGb, 0, 100), 2)
            : (t?.RamUsedPercent is > 0 ? Math.Round(Clamp(t.RamUsedPercent.Value, 0, 100), 2) : 0);

        if (payload.Disks is { Count: > 0 })
        {
            snapshot.DiskTotalGb = device.TotalDiskGb;
            snapshot.DiskUsedGb = device.UsedDiskGb;
        }
        else
        {
            snapshot.DiskTotalGb = t?.DiskTotalGb ?? device.TotalDiskGb;
            snapshot.DiskUsedGb = t?.DiskUsedGb ?? device.UsedDiskGb;
        }
        snapshot.DiskFreeGb = t?.DiskFreeGb is > 0 ? t.DiskFreeGb.Value : Math.Max(0, snapshot.DiskTotalGb - snapshot.DiskUsedGb);
        snapshot.DiskUsedPercent = t?.DiskUsedPercent is > 0
            ? Math.Round(Clamp(t.DiskUsedPercent.Value, 0, 100), 2)
            : (snapshot.DiskTotalGb > 0 ? Math.Round(100.0 * snapshot.DiskUsedGb / snapshot.DiskTotalGb, 2) : 0);

        var cDrive = payload.Disks?.FirstOrDefault(d =>
            (d.DriveLetter ?? "").Trim().TrimEnd(':', '\\').Equals("C", StringComparison.OrdinalIgnoreCase));
        snapshot.CFreeGb = cDrive?.FreeGb ?? t?.CFreeGb ?? 0;

        snapshot.Status = ResolveStatus(t?.Status, snapshot, now);
        device.Status = snapshot.Status;

        _db.DeviceSnapshots.Add(snapshot);

        // ---------- 5. Agent run log ----------
        var ar = payload.AgentRun;
        if (ar is not null || !string.IsNullOrWhiteSpace(t?.Status))
        {
            var run = new AgentRun
            {
                DeviceId = device.Id,
                StartedAtUtc = ar?.StartedAtUtc ?? collectedAt,
                FinishedAtUtc = ar?.FinishedAtUtc ?? now,
                DurationMs = ar?.DurationMs ?? Math.Max(0, (now - (ar?.StartedAtUtc ?? collectedAt)).TotalMilliseconds),
                Status = ar?.Status ?? (snapshot.Status == "Offline" ? "Queued" : "Success"),
                AgentVersion = ar?.AgentVersion ?? device.AgentVersion,
                CollectorsRun = ar?.CollectorsRun ?? DefaultCollectors(payload),
                ItemsSent = ar?.ItemsSent ?? (1 + result.DisksUpserted + result.SoftwareUpserted),
                OfflineQueueCount = ar?.OfflineQueueCount ?? snapshot.OfflineQueueCount,
                CpuPercent = ar?.CpuPercent ?? snapshot.AgentCpuPercent,
                MemoryMb = ar?.MemoryMb ?? snapshot.AgentMemoryMb,
                ErrorMessage = Trunc(ar?.ErrorMessage, 1024),
                TriggerSource = ar?.TriggerSource ?? "schedule"
            };
            _db.AgentRuns.Add(run);
        }

        await _db.SaveChangesAsync(ct);

        result.Success = true;
        result.DeviceId = device.Id;
        result.Hostname = device.Hostname;
        result.Status = isNew ? "created" : "updated";
        result.ProcessedMs = Math.Round((DateTime.UtcNow - started).TotalMilliseconds, 1);
        _log.LogInformation("Ingest {Host} ({Status}) dalam {Ms} ms", device.Hostname, result.Status, result.ProcessedMs);
        return result;
    }

    private static string ResolveStatus(string? reported, DeviceSnapshot s, DateTime now)
    {
        if (!string.IsNullOrWhiteSpace(reported))
        {
            var r = reported.Trim();
            return r.ToLowerInvariant() switch
            {
                "online" => "Online",
                "offline" => "Offline",
                "warning" => "Warning",
                "error" => "Error",
                _ => "Online"
            };
        }

        if (s.CpuUsagePercent >= 95 || s.RamUsedPercent >= 95) return "Warning";
        return "Online";
    }

    private static string DefaultCollectors(IngestPayload p)
    {
        var list = new List<string> { "telemetry" };
        if (p.Device is not null) list.Add("hardware");
        if (p.Disks is { Count: > 0 }) list.Add("disks");
        if (p.Software is { Count: > 0 }) list.Add("software");
        return string.Join(",", list);
    }

    private static string HealthOf(double usedPercent)
        => usedPercent >= 90 ? "Critical" : usedPercent >= 80 ? "Warning" : "Healthy";

    private static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;

    private static string? Pick(string? incoming, string? current, string? fallback = null)
    {
        if (!string.IsNullOrWhiteSpace(incoming)) return incoming.Trim();
        if (!string.IsNullOrWhiteSpace(current)) return current;
        return fallback;
    }

    private static string? Trunc(string? s, int max)
        => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s[..max]);
}
