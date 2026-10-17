using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using DeviceMonitor.Shared.Contracts;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace DeviceMonitor.Agent;

/// <summary>
/// Mengumpulkan data hardware, OS, BIOS, disk, software, dan telemetry dari mesin lokal.
/// Dirancang untuk Windows; di OS lain tetap mengirim data dasar agar agent tidak mati total.
/// </summary>
public class SystemInfoCollector
{
    private readonly AgentOptions _opt;
    private readonly ILogger<SystemInfoCollector> _log;

    public SystemInfoCollector(IOptions<AgentOptions> options, ILogger<SystemInfoCollector> log)
    {
        _opt = options.Value;
        _log = log;
    }

    private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    // ------------------------------------------------------------------ hardware

    public DevicePayload CollectDevice()
    {
        var p = new DevicePayload
        {
            Hostname = Environment.MachineName,
            AgentVersion = AgentOptions.AgentVersion,
            CollectedAtUtc = DateTime.UtcNow,
            Domain = IsWindows ? Safe(() => System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().DomainName) : null,
            DeviceType = "Desktop",
            Department = _opt.Department,
            Location = _opt.Location,
            AssignedUser = _opt.AssignedUser ?? Safe(() => Environment.UserName),
        };

        if (!IsWindows)
        {
            p.OsName = RuntimeInformation.OSDescription;
            p.OsArchitecture = RuntimeInformation.OSArchitecture.ToString();
            return p;
        }

        try
        {
            using var cs = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
            foreach (var o in cs.Get())
            {
                using var m = (ManagementObject)o;
                p.Manufacturer = m["Manufacturer"]?.ToString();
                p.Model = m["Model"]?.ToString();
                p.DeviceType = Equals(m["PCSystemType"]?.ToString(), "2") ? "Laptop" : "Desktop";
                if (ulong.TryParse(m["TotalPhysicalMemory"]?.ToString(), out var bytes))
                    p.RamTotalGb = Math.Round(bytes / 1073741824.0, 2);
            }
        }
        catch (Exception ex) { _log.LogWarning(ex, "Gagal membaca Win32_ComputerSystem"); }

        try
        {
            using var os = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
            foreach (var o in os.Get())
            {
                using var m = (ManagementObject)o;
                p.OsName = m["Caption"]?.ToString()?.Trim();
                p.OsVersion = m["Version"]?.ToString();
                p.OsBuild = m["BuildNumber"]?.ToString();
                p.OsArchitecture = m["OSArchitecture"]?.ToString();
                if (DateTime.TryParse(m["LastBootUpTime"]?.ToString(), out var boot))
                    p.UptimeSeconds = Math.Max(0, (DateTime.Now - boot).TotalSeconds);
            }
        }
        catch (Exception ex) { _log.LogWarning(ex, "Gagal membaca Win32_OperatingSystem"); }

        try
        {
            using var bios = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
            foreach (var o in bios.Get())
            {
                using var m = (ManagementObject)o;
                p.BiosVendor = m["Manufacturer"]?.ToString();
                p.BiosVersion = m["SMBIOSBIOSVersion"]?.ToString();
                p.SerialNumber = m["SerialNumber"]?.ToString();
                if (DateTime.TryParse(m["ReleaseDate"]?.ToString(), out var rd)) p.BiosReleaseDate = rd;
            }
        }
        catch (Exception ex) { _log.LogWarning(ex, "Gagal membaca Win32_BIOS"); }

        try
        {
            using var cpu = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            foreach (var o in cpu.Get())
            {
                using var m = (ManagementObject)o;
                p.CpuName ??= m["Name"]?.ToString()?.Trim();
                p.CpuCores += int.TryParse(m["NumberOfCores"]?.ToString(), out var c) ? c : 0;
                p.CpuLogicalProcessors += int.TryParse(m["NumberOfLogicalProcessors"]?.ToString(), out var l) ? l : 0;
            }
        }
        catch (Exception ex) { _log.LogWarning(ex, "Gagal membaca Win32_Processor"); }

        try
        {
            using var nic = new ManagementObjectSearcher(
                "SELECT IPAddress, MACAddress FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled = TRUE");
            foreach (var o in nic.Get())
            {
                using var m = (ManagementObject)o;
                if (m["IPAddress"] is string[] ips)
                    p.IpAddress ??= ips.FirstOrDefault(ip => ip.Contains('.') && !ip.StartsWith("169.254"));
                p.MacAddress ??= m["MACAddress"]?.ToString();
            }
        }
        catch (Exception ex) { _log.LogWarning(ex, "Gagal membaca konfigurasi jaringan"); }

        return p;
    }

    // ------------------------------------------------------------------ disks

    public List<DiskPayload> CollectDisks()
    {
        var list = new List<DiskPayload>();
        if (!IsWindows) return list;

        try
        {
            var media = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using var pm = new ManagementObjectSearcher(
                "SELECT DeviceID, MediaType FROM MSFT_PhysicalDisk", "root\\Microsoft\\Windows\\Storage");
            try
            {
                foreach (var o in pm.Get())
                {
                    using var m = (ManagementObject)o;
                    var id = m["DeviceID"]?.ToString();
                    var mt = m["MediaType"]?.ToString();
                    if (id is not null) media[id] = mt switch { "3" => "HDD", "4" => "SSD", "5" => "SSD", _ => "Unknown" };
                }
            }
            catch { /* namespace storage tidak tersedia di beberapa OS */ }

            using var disks = new ManagementObjectSearcher("SELECT * FROM Win32_LogicalDisk WHERE DriveType = 3");
            foreach (var o in disks.Get())
            {
                using var m = (ManagementObject)o;
                var letter = (m["DeviceID"]?.ToString() ?? "C").TrimEnd(':');
                if (!ulong.TryParse(m["Size"]?.ToString(), out var size) || size == 0) continue;
                ulong.TryParse(m["FreeSpace"]?.ToString(), out var free);
                var sizeGb = size / 1073741824.0;
                var freeGb = free / 1073741824.0;
                var usedPct = 100.0 * (sizeGb - freeGb) / sizeGb;

                list.Add(new DiskPayload
                {
                    DriveLetter = letter,
                    Label = m["VolumeName"]?.ToString(),
                    DriveType = "Fixed",
                    FileSystem = m["FileSystem"]?.ToString(),
                    MediaType = media.GetValueOrDefault(letter + "0", media.GetValueOrDefault(letter, "Unknown")),
                    SizeGb = Math.Round(sizeGb, 2),
                    FreeGb = Math.Round(freeGb, 2),
                    UsedGb = Math.Round(sizeGb - freeGb, 2),
                    HealthStatus = usedPct >= 90 ? "Critical" : usedPct >= 80 ? "Warning" : "Healthy"
                });
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Gagal membaca Win32_LogicalDisk");
        }

        return list;
    }

    // ------------------------------------------------------------------ software

    public List<SoftwarePayload> CollectSoftware()
    {
        var list = new List<SoftwarePayload>();
        if (!IsWindows) return list;

        var roots = new[]
        {
            RegistryView.Registry64, RegistryView.Registry32
        };
        var hives = new[]
        {
            (RegistryHive.LocalMachine, "PerMachine"),
            (RegistryHive.CurrentUser, "PerUser"),
        };

        foreach (var view in roots)
        foreach (var (hive, scope) in hives)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall");
                if (uninstall is null) continue;

                foreach (var sub in uninstall.GetSubKeyNames())
                {
                    using var key = uninstall.OpenSubKey(sub);
                    var name = key?.GetValue("DisplayName") as string;
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    if (key!.GetValue("SystemComponent") is int sc && sc == 1) continue;

                    var version = key.GetValue("DisplayVersion") as string ?? "unknown";
                    var publisher = key.GetValue("Publisher") as string;
                    var sizeKb = key.GetValue("EstimatedSize") as int? ?? 0;
                    DateTime? installDate = null;
                    if (key.GetValue("InstallDate") is string ds && ds.Length == 8 &&
                        DateTime.TryParseExact(ds, "yyyyMMdd", null, DateTimeStyles.None, out var d))
                        installDate = d;

                    // hindari duplikat 64/32 bit
                    if (list.Any(x => x.Name == name && x.Version == version)) continue;

                    list.Add(new SoftwarePayload
                    {
                        Name = name,
                        Version = version,
                        Publisher = publisher,
                        InstallDate = installDate,
                        SizeMb = Math.Round(sizeKb / 1024.0, 1),
                        Scope = scope
                    });
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Gagal membaca registry uninstall ({Hive}/{View})", hive, view);
            }
        }

        return list;
    }

    // ------------------------------------------------------------------ telemetry

    public TelemetryPayload CollectTelemetry()
    {
        var t = new TelemetryPayload
        {
            CollectedAtUtc = DateTime.UtcNow,
            Status = "Online",
            ProcessCount = SafeInt(() => System.Diagnostics.Process.GetProcesses().Length),
        };

        if (!IsWindows) return t;

        try
        {
            using var q = new ManagementObjectSearcher(
                "SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name = '_Total'");
            foreach (var o in q.Get())
            {
                using var m = (ManagementObject)o;
                if (ulong.TryParse(m["PercentProcessorTime"]?.ToString(), out var v))
                    t.CpuUsagePercent = Math.Min(100, v);
            }
        }
        catch (Exception ex) { _log.LogWarning(ex, "Gagal membaca performa CPU"); }

        try
        {
            using var os = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (var o in os.Get())
            {
                using var m = (ManagementObject)o;
                if (ulong.TryParse(m["TotalVisibleMemorySize"]?.ToString(), out var totalKb) &&
                    ulong.TryParse(m["FreePhysicalMemory"]?.ToString(), out var freeKb))
                {
                    t.RamTotalGb = Math.Round(totalKb / 1048576.0, 2);
                    t.RamUsedGb = Math.Round((totalKb - freeKb) / 1048576.0, 2);
                    t.RamUsedPercent = totalKb > 0 ? Math.Round(100.0 * (totalKb - freeKb) / totalKb, 2) : 0;
                }
            }
        }
        catch (Exception ex) { _log.LogWarning(ex, "Gagal membaca memori"); }

        // footprint proses agent
        try
        {
            using var proc = System.Diagnostics.Process.GetCurrentProcess();
            t.AgentMemoryMb = Math.Round(proc.WorkingSet64 / 1048576.0, 1);
            t.AgentCpuPercent = Math.Round(proc.TotalProcessorTime.TotalMilliseconds /
                                           Math.Max(1, Environment.ProcessorCount * proc.TotalProcessorTime.TotalMilliseconds + 1) * 100, 2);
            // estimasi sederhana: CPU agent sejak start dibagi waktu jalan
            var lifeMs = Math.Max(1, (DateTime.Now - proc.StartTime).TotalMilliseconds);
            t.AgentCpuPercent = Math.Round(100.0 * proc.TotalProcessorTime.TotalMilliseconds / (lifeMs * Environment.ProcessorCount), 2);
        }
        catch (Exception ex) { _log.LogWarning(ex, "Gagal membaca footprint agent"); }

        return t;
    }

    private static string? Safe(Func<string?> f)
    {
        try { return f(); } catch { return null; }
    }

    private static int SafeInt(Func<int> f)
    {
        try { return f(); } catch { return 0; }
    }
}
