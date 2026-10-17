using DeviceMonitor.Shared.Entities;
using DeviceMonitor.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DeviceMonitor.Web.Services;

/// <summary>
/// Mengisi database dengan data fleet kantor yang realistis (deterministik per seed)
/// supaya seluruh panel dashboard bisa langsung divalidasi tanpa menunggu agent asli.
/// </summary>
public class DemoDataSeeder
{
    private readonly MonitorDbContext _db;
    private readonly ILogger<DemoDataSeeder> _log;
    private readonly Random _r;

    public DemoDataSeeder(MonitorDbContext db, ILogger<DemoDataSeeder> log, int seed = 20261017)
    {
        _db = db;
        _log = log;
        _r = new Random(seed);
    }

    // ------------------------------------------------------------------ catalogs

    private record Dept(string Name, string Prefix, int Weight);
    private static readonly Dept[] Depts =
    {
        new("Finance", "FIN", 7), new("Marketing", "MKT", 6), new("HR & GA", "HRGA", 5),
        new("IT", "IT", 8), new("Operations", "OPS", 7), new("Sales", "SLS", 6),
        new("Engineering", "ENG", 5), new("Legal", "LGL", 3), new("Procurement", "PROC", 3),
        new("Direksi", "BOD", 2)
    };

    private record ModelSpec(string Manufacturer, string Model, string Type, string BiosVendor, string[] Bios);
    private static readonly ModelSpec[] Models =
    {
        new("Dell", "Latitude 5440", "Laptop", "Dell Inc.", new[] { "1.9.0", "1.12.1", "1.14.0" }),
        new("Dell", "OptiPlex 7010", "Desktop", "Dell Inc.", new[] { "1.18.2", "1.21.0" }),
        new("Lenovo", "ThinkPad T14 Gen 4", "Laptop", "LENOVO", new[] { "N3AET64W (1.32)", "N3AET72W (1.40)" }),
        new("Lenovo", "ThinkCentre M70q", "Desktop", "LENOVO", new[] { "M1AKT4FA", "M1AKT5BA" }),
        new("HP", "EliteBook 840 G9", "Laptop", "HP", new[] { "U81 Ver. 01.07.01", "U81 Ver. 01.09.01" }),
        new("HP", "ProDesk 400 G7", "Desktop", "HP", new[] { "S05 Ver. 02.14.01" }),
        new("ASUS", "ExpertBook B1400", "Laptop", "ASUSTeK COMPUTER INC.", new[] { "B1400CEAE.302", "B1400CEAE.306" }),
        new("Acer", "Veriton X4660G", "Desktop", "Acer", new[] { "R01-A2", "R01-A4" }),
        new("Dell", "Precision 3660", "Workstation", "Dell Inc.", new[] { "1.6.0", "1.8.1" }),
    };

    private record CpuSpec(string Name, int Cores);
    private static readonly CpuSpec[] Cpus =
    {
        new("Intel(R) Core(TM) i5-1235U @ 1.30GHz", 10),
        new("Intel(R) Core(TM) i5-1335U @ 1.30GHz", 10),
        new("Intel(R) Core(TM) i7-1265U @ 1.80GHz", 10),
        new("Intel(R) Core(TM) i7-1355U @ 1.70GHz", 10),
        new("Intel(R) Core(TM) i3-1215U @ 1.20GHz", 6),
        new("Intel(R) Core(TM) i9-12900H @ 2.50GHz", 14),
        new("AMD Ryzen 5 PRO 6650U with Radeon Graphics", 6),
        new("AMD Ryzen 7 PRO 6850U with Radeon Graphics", 8),
        new("Intel(R) Xeon(R) E-2334 @ 3.40GHz", 4),
    };

    private record OsSpec(string Name, string Version, string Build, string Arch);
    private static readonly OsSpec[] Oss =
    {
        new("Microsoft Windows 11 Pro", "10.0.22631", "22631.3447", "64-bit"),
        new("Microsoft Windows 11 Pro", "10.0.22621", "22621.3527", "64-bit"),
        new("Microsoft Windows 11 Enterprise", "10.0.22631", "22631.3294", "64-bit"),
        new("Microsoft Windows 10 Pro", "10.0.19045", "19045.4291", "64-bit"),
        new("Microsoft Windows 10 Pro", "10.0.19044", "19044.4170", "64-bit"),
        new("Microsoft Windows 10 Enterprise", "10.0.19045", "19045.4046", "64-bit"),
    };

    private static readonly string[] Locations =
        { "HO - Lantai 2", "HO - Lantai 3", "HO - Lantai 5", "Cabang Surabaya", "Cabang Medan", "Pabrik - Cikarang" };

    private static readonly string[] FirstNames =
        { "Andi", "Siti", "Budi", "Rina", "Agus", "Dewi", "Rizky", "Maya", "Fajar", "Nadia", "Hendra", "Putri", "Yoga", "Laras", "Bayu", "Intan", "Dimas", "Ratna", "Eko", "Wulan" };

    private static readonly string[] LastNames =
        { "Pratama", "Wijaya", "Saputra", "Halim", "Nugroho", "Santoso", "Kurniawan", "Setiawan", "Maharani", "Permata" };

    private record SwSpec(string Name, string Publisher, string[] Versions, int MinMb, int MaxMb, string Scope);
    private static readonly SwSpec[] Catalog =
    {
        new("Microsoft 365 Apps for enterprise", "Microsoft Corporation", new[] { "16.0.16928.20220", "16.0.17328.20184", "16.0.17531.20120" }, 2800, 3400, "PerMachine"),
        new("Google Chrome", "Google LLC", new[] { "118.0.5993.118", "121.0.6167.140", "124.0.6367.91", "126.0.6478.127", "129.0.6668.90" }, 240, 330, "PerMachine"),
        new("Mozilla Firefox", "Mozilla", new[] { "119.0", "124.0.2", "128.0.3", "131.0.2" }, 180, 240, "PerMachine"),
        new("Zoom Workplace", "Zoom Video Communications, Inc.", new[] { "5.16.10", "5.17.9", "6.0.10", "6.1.35" }, 60, 130, "PerMachine"),
        new("Microsoft Teams", "Microsoft Corporation", new[] { "23306.3309.2502.6798", "24021.1404.2742.0", "24165.1206.2959.0" }, 200, 320, "PerUser"),
        new("VLC media player", "VideoLAN", new[] { "3.0.18", "3.0.20", "3.0.21" }, 130, 160, "PerMachine"),
        new("7-Zip 23.01 (x64)", "Igor Pavlov", new[] { "23.01", "24.05", "24.08" }, 5, 8, "PerMachine"),
        new("Adobe Acrobat Reader", "Adobe Inc.", new[] { "23.006.20360", "24.001.20602", "24.003.20112" }, 350, 520, "PerMachine"),
        new("Notepad++ (64-bit x64)", "Notepad++ Team", new[] { "8.6", "8.6.6", "8.7" }, 12, 18, "PerMachine"),
        new("Git", "The Git Development Community", new[] { "2.42.0", "2.44.0", "2.46.2" }, 280, 340, "PerMachine"),
        new("Node.js", "OpenJS Foundation", new[] { "18.19.0", "20.11.1", "20.17.0", "22.9.0" }, 60, 90, "PerMachine"),
        new("Python 3.11 (64-bit)", "Python Software Foundation", new[] { "3.11.5150.0", "3.11.7150.0", "3.11.9150.0" }, 90, 120, "PerMachine"),
        new("Java(TM) 8 Update 351 (64-bit)", "Oracle Corporation", new[] { "8.0.3510.10", "8.0.3610.9", "8.0.4010.13" }, 200, 260, "PerMachine"),
        new("WinRAR 6.24 (64-bit)", "win.rar GmbH", new[] { "6.24.0", "7.00.0" }, 12, 16, "PerMachine"),
        new("TeamViewer", "TeamViewer Germany GmbH", new[] { "15.50.5", "15.55.3" }, 60, 80, "PerMachine"),
        new("AnyDesk", "AnyDesk Software GmbH", new[] { "8.0.8", "8.11.3" }, 20, 30, "PerMachine"),
        new("Docker Desktop", "Docker Inc.", new[] { "4.26.1", "4.29.0", "4.33.2" }, 900, 1200, "PerMachine"),
        new("Visual Studio Code", "Microsoft Corporation", new[] { "1.85.2", "1.88.1", "1.92.2", "1.94.1" }, 320, 400, "PerUser"),
        new("Microsoft Edge", "Microsoft Corporation", new[] { "119.0.2151.97", "122.0.2365.92", "126.0.2592.102", "129.0.2792.89" }, 450, 620, "PerMachine"),
        new("Microsoft Edge WebView2 Runtime", "Microsoft Corporation", new[] { "119.0.2151.97", "124.0.2478.80", "129.0.2792.89" }, 500, 700, "PerMachine"),
        new("SQL Server Management Studio 19", "Microsoft Corporation", new[] { "19.2.56.2", "19.3.54.4" }, 1400, 1700, "PerMachine"),
        new("SAP GUI", "SAP SE", new[] { "7.70", "8.00" }, 1100, 1400, "PerMachine"),
        new("Odoo Desktop", "Odoo S.A.", new[] { "16.0.1", "17.0.2" }, 300, 420, "PerMachine"),
        new("K-Lite Codec Pack Basic", "KLCP", new[] { "17.8.5", "18.2.0" }, 40, 60, "PerMachine"),
        new("Foxit PDF Reader", "Foxit Software Inc.", new[] { "12.1.2", "2024.2.0" }, 260, 340, "PerMachine"),
        new("Canva", "Canva", new[] { "1.62.0", "1.77.0" }, 180, 220, "PerUser"),
        new("Spotify", "Spotify AB", new[] { "1.2.26.1187", "1.2.45.454" }, 160, 200, "PerUser"),
        new("Telegram Desktop", "Telegram FZ-LLC", new[] { "4.14.3", "5.5.5" }, 90, 130, "PerUser"),
        new("WhatsApp", "WhatsApp LLC", new[] { "2.2349.9.0", "2.2435.6.0" }, 200, 260, "PerUser"),
        new("CCleaner", "Piriform Software Ltd", new[] { "6.20", "6.28" }, 40, 60, "PerMachine"),
        new("RealVNC Viewer", "RealVNC Ltd", new[] { "7.8.0", "7.13.1" }, 30, 45, "PerMachine"),
        new("AutoCAD LT 2024", "Autodesk, Inc.", new[] { "24.3.12.0", "24.3.60.0" }, 2100, 2600, "PerMachine"),
        new("Wireshark 4.2.0 (64-bit)", "Wireshark development team", new[] { "4.2.0", "4.4.0" }, 200, 260, "PerMachine"),
        new("Postman", "Postman Inc.", new[] { "10.21.9", "11.12.0" }, 220, 300, "PerUser"),
        new("PowerToys (Preview)", "Microsoft Corporation", new[] { "0.79.0", "0.84.0" }, 180, 220, "PerMachine"),
    };

    /// <summary>Aplikasi yang selalu ada di semua perangkat (termasuk agent monitoring).</summary>
    private static readonly (string Name, string Publisher, string Version, double SizeMb)[] CommonApps =
    {
        ("DeviceMonitor Agent", "PT Internal IT", "1.0.0", 28),
        ("Microsoft Defender Antivirus", "Microsoft Corporation", "4.18.24090.11", 180),
        ("Windows Feature Experience Pack", "Microsoft Corporation", "1000.22684.1000.0", 12),
        ("Intel(R) Graphics Command Center", "Intel Corporation", "1.100.3408.0", 240),
        ("Realtek High Definition Audio Driver", "Realtek Semiconductor Corp.", "6.0.9599.1", 90),
    };

    private static readonly (string Name, string? MinVersion, string? Prohibited, bool Mandatory, string Notes)[] RuleSeed =
    {
        ("Google Chrome", "124.0.6367.91", null, false, "Minimum Chrome 124 - versi lama punya CVE aktif"),
        ("Mozilla Firefox", "124.0", null, false, "Minimum Firefox 124"),
        ("7-Zip 23.01 (x64)", "24.05", null, false, "Standar kompresi kantor"),
        ("WinRAR 6.24 (64-bit)", "7.00.0", null, false, "WinRAR < 7 dilarang (celah keamanan aktif)"),
        ("Java(TM) 8 Update 351 (64-bit)", null, "8.0.*,8.0.3510.10,8.0.3610.9", false, "Java 8 sudah EOL, migrasi ke JDK 17/21"),
        ("VLC media player", "3.0.20", null, false, "Minimum VLC 3.0.20"),
        ("TeamViewer", "15.55.0", null, false, "Remote support wajib versi terbaru"),
        ("DeviceMonitor Agent", "1.0.0", null, true, "Agent monitoring wajib terpasang di semua perangkat"),
        ("Microsoft Defender Antivirus", "4.18", null, true, "Antivirus wajib aktif"),
        ("Adobe Acrobat Reader", "24.001.20602", null, false, "Versi 23 sudah tidak didukung"),
        ("Zoom Workplace", "6.0.0", null, false, "Minimum Zoom 6"),
        ("CCleaner", null, "6.20", false, "CCleaner 6.20 tidak boleh dipakai (bundling adware)"),
    };

    // ------------------------------------------------------------------ seeding

    public async Task<bool> SeedIfEmptyAsync(int deviceCount = 48, int historyDays = 14, CancellationToken ct = default)
    {
        if (await _db.Devices.AnyAsync(ct)) return false;
        await SeedAsync(deviceCount, historyDays, ct);
        return true;
    }

    public async Task SeedAsync(int deviceCount = 48, int historyDays = 14, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var start = now.Date.AddDays(-historyDays);

        if (!await _db.ComplianceRules.AnyAsync(ct))
        {
            _db.ComplianceRules.AddRange(RuleSeed.Select(r => new ComplianceRule
            {
                SoftwareName = r.Name,
                MinimumVersion = r.MinVersion,
                ProhibitedVersions = r.Prohibited,
                IsMandatory = r.Mandatory,
                Notes = r.Notes,
                IsEnabled = true
            }));
            await _db.SaveChangesAsync(ct);
        }
        var rules = await _db.ComplianceRules.Where(x => x.IsEnabled).ToListAsync(ct);

        var deptPool = Depts.SelectMany(d => Enumerable.Repeat(d, d.Weight)).ToArray();

        var allDevices = new List<Device>();
        var allDisks = new List<DiskDrive>();
        var allSoftware = new List<SoftwareInstall>();
        var allSnapshots = new List<DeviceSnapshot>();
        var allRuns = new List<AgentRun>();
        var usedHostnames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < deviceCount; i++)
        {
            var dept = deptPool[_r.Next(deptPool.Length)];
            var model = Models[_r.Next(Models.Length)];
            var os = Oss[Pick(new[] { 14, 10, 8, 22, 14, 10 })];
            var cpu = Cpus[Pick(new[] { 20, 18, 12, 12, 8, 4, 12, 10, 4 })];
            var ram = new double[] { 4, 8, 8, 16, 16, 16, 32, 64 }[Pick(new[] { 3, 14, 14, 24, 20, 12, 9, 4 })];
            if (model.Type == "Workstation") ram = Math.Max(32, ram);

            var prefix = model.Type == "Laptop" ? "NB" : model.Type == "Workstation" ? "WS" : "PC";
            var hostname = NewHostname(prefix, dept.Prefix);

            var user = $"{FirstNames[_r.Next(FirstNames.Length)]}.{LastNames[_r.Next(LastNames.Length)]}".ToLowerInvariant();

            // profil kondisi perangkat
            var profile = _r.Next(100);
            var heavyUser = profile < 22;                 // disk & RAM tertekan
            var mostlyOff = profile >= 92;                // sering offline
            var stale = profile >= 85 && profile < 92;    // uptime > 30 hari

            var dev = new Device
            {
                Hostname = hostname,
                AssetTag = $"AST-{2021 + _r.Next(0, 5)}-{1000 + i}",
                DeviceType = model.Type,
                Manufacturer = model.Manufacturer,
                Model = model.Model,
                SerialNumber = RandomSerial(model.Manufacturer),
                OsName = os.Name,
                OsVersion = os.Version,
                OsBuild = os.Build,
                OsArchitecture = os.Arch,
                BiosVendor = model.BiosVendor,
                BiosVersion = model.Bios[_r.Next(model.Bios.Length)],
                BiosReleaseDate = now.AddDays(-_r.Next(120, 900)).Date,
                CpuName = cpu.Name,
                CpuCores = cpu.Cores,
                CpuLogicalProcessors = cpu.Cores * (_r.Next(100) < 70 ? 2 : 1),
                RamTotalGb = ram,
                IpAddress = $"10.{_r.Next(10, 40)}.{_r.Next(0, 20)}.{_r.Next(11, 250)}",
                MacAddress = RandomMac(),
                Domain = _r.Next(100) < 80 ? "corp.local" : "WORKGROUP",
                Department = dept.Name,
                Location = Locations[_r.Next(Locations.Length)],
                AssignedUser = user,
                AgentVersion = new[] { "1.0.0", "1.0.0", "1.0.0", "0.9.5", "1.1.0-beta" }[_r.Next(5)],
                FirstSeenUtc = start.AddDays(-_r.Next(10, 260)),
                CreatedAtUtc = start,
                UpdatedAtUtc = now,
                IsActive = true
            };
            allDevices.Add(dev);

            // ---------------- disk / partisi ----------------
            var devDisks = new List<DiskDrive>();
            var cSize = model.Type == "Workstation" ? Pick(new double[] { 512, 512, 1024 })
                      : model.Type == "Laptop" ? Pick(new double[] { 238, 256, 476, 512 })
                      : Pick(new double[] { 238, 476, 512 });
            var c = MakeDisk("C", "System", cSize, heavyUser ? Between(88, 98) : Between(45, 88),
                             _r.Next(100) < 82 ? "SSD" : "HDD", "Fixed", now);
            devDisks.Add(c);

            if (_r.Next(100) < (model.Type == "Desktop" ? 62 : 34))
            {
                var dSize = Pick(new double[] { 476, 931, 1024, 2048 });
                devDisks.Add(MakeDisk("D", "Data", dSize, heavyUser ? Between(75, 96) : Between(15, 82),
                                      _r.Next(100) < 45 ? "SSD" : "HDD", "Fixed", now));
            }

            if (_r.Next(100) < 10)
            {
                var letter = new[] { "E", "F" }[_r.Next(2)];
                var size = Pick(new double[] { 931, 2048, 4096 });
                devDisks.Add(MakeDisk(letter, letter == "E" ? "Backup" : "Archive", size, Between(20, 88),
                                      "HDD", _r.Next(100) < 30 ? "Network" : "Fixed", now));
            }

            dev.TotalDiskGb = Math.Round(devDisks.Sum(x => x.SizeGb), 2);
            dev.UsedDiskGb = Math.Round(devDisks.Sum(x => x.UsedGb), 2);
            foreach (var d in devDisks) { d.Device = dev; allDisks.Add(d); }

            // ---------------- software ----------------
            var devSoftware = new List<SoftwareInstall>();
            var chosen = Catalog.OrderBy(_ => _r.Next()).Take(_r.Next(14, 26)).ToList();

            var appList = CommonApps
                .Select(a => (a.Name, a.Publisher, Version: a.Version, SizeMb: a.SizeMb, Scope: "PerMachine"))
                .Concat(chosen.Select(x => (x.Name, x.Publisher, Version: x.Versions[_r.Next(x.Versions.Length)],
                                            SizeMb: (double)_r.Next(x.MinMb, x.MaxMb), Scope: x.Scope)));

            foreach (var app in appList)
            {
                if (!IsPlausible(app.Name, dept.Name)) continue;
                devSoftware.Add(new SoftwareInstall
                {
                    Name = app.Name,
                    Version = app.Version,
                    Publisher = app.Publisher,
                    InstallDate = start.AddDays(-_r.Next(5, 500)).Date,
                    SizeMb = Math.Round(app.SizeMb * Between(0.92, 1.14), 1),
                    Scope = app.Scope,
                    ComplianceStatus = ComplianceService.Evaluate(app.Name, app.Version, rules),
                    FirstSeenUtc = start.AddDays(-_r.Next(0, 60)),
                    LastSeenUtc = now,
                    IsInstalled = true,
                    Device = dev
                });
            }
            allSoftware.AddRange(devSoftware);

            // ---------------- riwayat snapshot + agent run ----------------
            var uptimeSeed = stale ? Between(31, 120) * 86400
                           : _r.Next(100) < 25 ? Between(2, 20) * 3600
                           : Between(1, 9) * 86400 + Between(0, 20) * 3600;
            var bootTime = now.AddSeconds(-uptimeSeed);
            // Boot sebelumnya: dipakai untuk snapshot yang diambil sebelum bootTime saat ini,
            // sehingga tren uptime terlihat natural (bukan nol lalu melonjak).
            var prevBoot = bootTime.AddDays(-Between(4, 45));
            DateTime? offlineFrom = mostlyOff ? start.AddDays(_r.Next(0, historyDays)) : null;

            var devSnapshots = new List<DeviceSnapshot>();
            var devRuns = new List<AgentRun>();

            for (var day = 0; day <= historyDays; day++)
            {
                for (var slot = 0; slot < 4; slot++)
                {
                    var ts = start.AddDays(day).AddHours(1 + slot * 6).AddMinutes(_r.Next(0, 40));
                    if (ts > now) continue;

                    var inOfflineWindow = offlineFrom is not null && ts >= offlineFrom && ts <= offlineFrom.Value.AddDays(3);
                    var isOffline = inOfflineWindow || (mostlyOff && _r.Next(100) < 55);

                    var uptime = ts >= bootTime ? (ts - bootTime).TotalSeconds : Math.Max(60, (ts - prevBoot).TotalSeconds);

                    var cpuBase = heavyUser ? Between(35, 82) : Between(6, 48);
                    var ramPct = heavyUser ? Between(72, 96) : Between(38, 82);
                    var diskTotal = devDisks.Sum(x => x.SizeGb);
                    var diskUsed = Math.Max(1, devDisks.Sum(x => x.UsedGb) - (historyDays - day) * Between(0.05, 0.45));
                    var cFree = Math.Max(0.6, c.FreeGb + (historyDays - day) * Between(0.05, 0.35));

                    var snap = new DeviceSnapshot
                    {
                        CollectedAtUtc = ts,
                        Status = isOffline ? "Offline" : (cpuBase > 88 || ramPct > 93 ? "Warning" : "Online"),
                        CpuUsagePercent = isOffline ? 0 : Math.Round(Math.Clamp(cpuBase + Between(-6, 12), 0, 100), 2),
                        RamUsedGb = isOffline ? 0 : Math.Round(ram * ramPct / 100.0, 2),
                        RamTotalGb = ram,
                        RamUsedPercent = isOffline ? 0 : Math.Round(Math.Clamp(ramPct + Between(-4, 6), 0, 100), 2),
                        DiskTotalGb = Math.Round(diskTotal, 2),
                        DiskUsedGb = Math.Round(Math.Min(diskUsed, diskTotal * 0.99), 2),
                        CFreeGb = Math.Round(cFree, 2),
                        UptimeSeconds = Math.Round(uptime, 0),
                        ProcessCount = _r.Next(120, 340),
                        AgentCpuPercent = isOffline ? 0 : Math.Round(Between(0.2, 2.4), 2),
                        AgentMemoryMb = isOffline ? 0 : Math.Round(Between(45, 175), 1),
                        OfflineQueueCount = isOffline ? _r.Next(1, 12) : (_r.Next(100) < 8 ? _r.Next(1, 4) : 0),
                        NetworkRxBytes = _r.Next(500_000, 90_000_000),
                        NetworkTxBytes = _r.Next(200_000, 40_000_000),
                        Device = dev
                    };
                    snap.DiskFreeGb = Math.Round(Math.Max(0, snap.DiskTotalGb - snap.DiskUsedGb), 2);
                    snap.DiskUsedPercent = snap.DiskTotalGb > 0 ? Math.Round(100.0 * snap.DiskUsedGb / snap.DiskTotalGb, 2) : 0;
                    devSnapshots.Add(snap);

                    var failed = !isOffline && _r.Next(100) < 6;
                    var duration = failed ? Between(9000, 45000) : Between(700, 5200) + devSoftware.Count * Between(6, 14);
                    devRuns.Add(new AgentRun
                    {
                        StartedAtUtc = ts,
                        FinishedAtUtc = ts.AddMilliseconds(duration),
                        DurationMs = Math.Round(duration, 1),
                        Status = isOffline ? "Queued" : failed ? (_r.Next(100) < 60 ? "Failed" : "Partial") : "Success",
                        AgentVersion = dev.AgentVersion,
                        CollectorsRun = slot == 0 ? "hardware,disks,software,telemetry" : "telemetry,disks",
                        ItemsSent = isOffline ? 0 : 1 + devDisks.Count + (slot == 0 ? devSoftware.Count : 0),
                        OfflineQueueCount = snap.OfflineQueueCount,
                        CpuPercent = Math.Round(Between(0.2, 3.1), 2),
                        MemoryMb = Math.Round(Between(45, 180), 1),
                        ErrorMessage = failed ? Pick(new[]
                        {
                            "Timeout menghubungi SQL Server setelah 30 detik",
                            "WMI query Win32_OperatingSystem gagal: 0x80041010",
                            "HTTP 503 dari endpoint /api/ingest (retry ke-3)",
                            "Gagal membaca registry Uninstall (Access Denied)",
                            "Payload melebihi 8 MB, software inventory dipotong"
                        }) : null,
                        TriggerSource = slot == 0 ? "schedule" : (_r.Next(100) < 85 ? "schedule" : "manual"),
                        Device = dev
                    });
                }
            }

            // status terakhir perangkat
            DateTime lastTs;
            if (mostlyOff) lastTs = now.AddHours(-Between(3, 50));
            else if (_r.Next(100) < 12) lastTs = now.AddMinutes(-Between(35, 180));
            else lastTs = now.AddMinutes(-Between(1, 12));

            dev.LastSeenUtc = lastTs;
            dev.UptimeSeconds = Math.Round(Math.Max(60, (lastTs - bootTime).TotalSeconds), 0);
            dev.Status = (lastTs < now.AddMinutes(-30)) ? "Offline"
                       : (heavyUser && _r.Next(100) < 55) ? "Warning" : "Online";

            var lastSnap = new DeviceSnapshot
            {
                CollectedAtUtc = lastTs,
                Status = dev.Status,
                CpuUsagePercent = Math.Round(heavyUser ? Between(40, 88) : Between(5, 45), 2),
                RamUsedGb = Math.Round(ram * (heavyUser ? Between(75, 95) : Between(40, 80)) / 100.0, 2),
                RamTotalGb = ram,
                RamUsedPercent = Math.Round(heavyUser ? Between(75, 95) : Between(40, 80), 2),
                DiskTotalGb = dev.TotalDiskGb,
                DiskUsedGb = dev.UsedDiskGb,
                DiskFreeGb = Math.Round(dev.TotalDiskGb - dev.UsedDiskGb, 2),
                DiskUsedPercent = dev.TotalDiskGb > 0 ? Math.Round(100.0 * dev.UsedDiskGb / dev.TotalDiskGb, 2) : 0,
                CFreeGb = c.FreeGb,
                UptimeSeconds = dev.UptimeSeconds,
                ProcessCount = _r.Next(130, 330),
                AgentCpuPercent = Math.Round(Between(0.2, 2.2), 2),
                AgentMemoryMb = Math.Round(Between(48, 170), 1),
                OfflineQueueCount = mostlyOff ? _r.Next(3, 22) : (_r.Next(100) < 10 ? _r.Next(1, 6) : 0),
                NetworkRxBytes = _r.Next(500_000, 60_000_000),
                NetworkTxBytes = _r.Next(200_000, 30_000_000),
                Device = dev
            };
            devSnapshots.Add(lastSnap);

            devRuns.Add(new AgentRun
            {
                StartedAtUtc = lastTs.AddSeconds(-3),
                FinishedAtUtc = lastTs,
                DurationMs = Math.Round(Between(800, 5200), 1),
                Status = dev.Status == "Offline" ? "Queued" : "Success",
                AgentVersion = dev.AgentVersion,
                CollectorsRun = "hardware,disks,software,telemetry",
                ItemsSent = 1 + devDisks.Count + devSoftware.Count,
                OfflineQueueCount = lastSnap.OfflineQueueCount,
                CpuPercent = Math.Round(Between(0.2, 2.6), 2),
                MemoryMb = Math.Round(Between(50, 160), 1),
                TriggerSource = "schedule",
                Device = dev
            });

            allSnapshots.AddRange(devSnapshots);
            allRuns.AddRange(devRuns);
        }

        _db.Devices.AddRange(allDevices);
        await _db.SaveChangesAsync(ct);          // FK device terisi otomatis lewat navigation property

        _db.DiskDrives.AddRange(allDisks);
        _db.SoftwareInstalls.AddRange(allSoftware);
        await _db.SaveChangesAsync(ct);

        _db.DeviceSnapshots.AddRange(allSnapshots);
        await _db.SaveChangesAsync(ct);

        _db.AgentRuns.AddRange(allRuns);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation(
            "Demo data siap: {Devices} perangkat, {Disks} partisi, {Software} software, {Snaps} snapshot, {Runs} agent run",
            allDevices.Count, allDisks.Count, allSoftware.Count, allSnapshots.Count, allRuns.Count);
    }

    // ------------------------------------------------------------------ helpers

    private string NewHostname(string prefix, string deptPrefix)
    {
        for (var attempt = 0; attempt < 500; attempt++)
        {
            var h = $"{prefix}-{deptPrefix}-{_r.Next(1, 99):00}";
            if (!UsedHostnames.Add(h)) continue;
            return h;
        }
        return $"{prefix}-{deptPrefix}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
    }

    private HashSet<string> UsedHostnames { get; } = new(StringComparer.OrdinalIgnoreCase);

    private DiskDrive MakeDisk(string letter, string label, double sizeGb, double usedPercent,
                               string media, string driveType, DateTime collectedAt)
    {
        var usedPct = Math.Round(Math.Clamp(usedPercent, 1, 99), 2);
        var used = Math.Round(sizeGb * usedPct / 100.0, 2);
        return new DiskDrive
        {
            DriveLetter = letter,
            Label = label,
            DriveType = driveType,
            MediaType = media,
            FileSystem = "NTFS",
            SizeGb = sizeGb,
            UsedGb = used,
            FreeGb = Math.Round(sizeGb - used, 2),
            UsedPercent = usedPct,
            HealthStatus = usedPct >= 90 ? "Critical" : usedPct >= 80 ? "Warning" : "Healthy",
            CollectedAtUtc = collectedAt
        };
    }

    /// <summary>Menyaring aplikasi agar masuk akal per departemen.</summary>
    private bool IsPlausible(string app, string dept)
    {
        var chance = _r.Next(100);
        return app switch
        {
            "SAP GUI" => dept is "Finance" or "Procurement" or "Operations" ? chance < 70 : chance < 12,
            "AutoCAD LT 2024" => dept == "Engineering" ? chance < 80 : chance < 6,
            "SQL Server Management Studio 19" => dept == "IT" ? chance < 85 : chance < 8,
            "Docker Desktop" => dept is "IT" or "Engineering" ? chance < 70 : chance < 5,
            "Wireshark 4.2.0 (64-bit)" => dept == "IT" ? chance < 60 : chance < 4,
            "Postman" => dept is "IT" or "Engineering" ? chance < 65 : chance < 5,
            "Git" => dept is "IT" or "Engineering" ? chance < 85 : chance < 15,
            "Node.js" => dept is "IT" or "Engineering" ? chance < 75 : chance < 10,
            "Python 3.11 (64-bit)" => dept is "IT" or "Engineering" or "Finance" ? chance < 60 : chance < 10,
            "Odoo Desktop" => dept is "Operations" or "Sales" or "Procurement" ? chance < 75 : chance < 15,
            "Visual Studio Code" => dept is "IT" or "Engineering" ? chance < 80 : chance < 18,
            "Canva" => dept is "Marketing" or "Sales" ? chance < 85 : chance < 25,
            "Spotify" or "Telegram Desktop" or "WhatsApp" => chance < 55,
            "CCleaner" or "K-Lite Codec Pack Basic" or "RealVNC Viewer" => chance < 22,
            "TeamViewer" or "AnyDesk" => dept == "IT" ? chance < 80 : chance < 35,
            "Notepad++ (64-bit x64)" => chance < 70,
            "Java(TM) 8 Update 351 (64-bit)" => chance < 28,
            "WinRAR 6.24 (64-bit)" => chance < 35,
            "7-Zip 23.01 (x64)" => chance < 80,
            "VLC media player" => chance < 65,
            "Foxit PDF Reader" => chance < 40,
            "Adobe Acrobat Reader" => chance < 85,
            "PowerToys (Preview)" => chance < 40,
            _ => true
        };
    }

    private string RandomSerial(string vendor)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var len = vendor == "LENOVO" ? 8 : vendor == "Dell Inc." ? 7 : 10;
        return new string(Enumerable.Range(0, len).Select(_ => chars[_r.Next(chars.Length)]).ToArray());
    }

    private string RandomMac()
        => string.Join(":", Enumerable.Range(0, 6).Select(_ => _r.Next(0, 256).ToString("X2")));

    private int Pick(int[] weights)
    {
        var n = _r.Next(weights.Sum());
        for (var i = 0; i < weights.Length; i++) { n -= weights[i]; if (n < 0) return i; }
        return weights.Length - 1;
    }

    private T Pick<T>(T[] items) => items[_r.Next(items.Length)];

    private double Between(double min, double max) => min + _r.NextDouble() * (max - min);
}
