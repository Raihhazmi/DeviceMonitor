using System.Text;
using DeviceMonitor.Shared.Entities;
using DeviceMonitor.Web.Data;
using DeviceMonitor.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeviceMonitor.Web.Controllers;

public class DataViewModel
{
    public string Provider { get; set; } = "SqlServer";
    public string Database { get; set; } = "-";
    public int Devices { get; set; }
    public int Disks { get; set; }
    public int Software { get; set; }
    public int Snapshots { get; set; }
    public int AgentRuns { get; set; }
    public int Rules { get; set; }
    public DateTime? LastIngestUtc { get; set; }
    public List<ComplianceRule> ComplianceRules { get; set; } = new();
    public List<CsvImportResult> ImportResults { get; set; } = new();
    public string? Message { get; set; }
    public string? MessageType { get; set; }
    public bool ApiKeyConfigured { get; set; }
}

/// <summary>Halaman Data &amp; Ingest: import CSV, seed demo, aturan compliance, dokumentasi API.</summary>
public class DataController : Controller
{
    private readonly MonitorDbContext _db;
    private readonly CsvImportService _csv;
    private readonly DemoDataSeeder _seeder;
    private readonly ComplianceService _compliance;
    private readonly IConfiguration _cfg;

    public DataController(MonitorDbContext db, CsvImportService csv, DemoDataSeeder seeder,
                          ComplianceService compliance, IConfiguration cfg)
    {
        _db = db;
        _csv = csv;
        _seeder = seeder;
        _compliance = compliance;
        _cfg = cfg;
    }

    private async Task<DataViewModel> BuildVmAsync()
    {
        var key = _cfg["Monitor:AgentApiKey"];
        return new DataViewModel
        {
            Provider = _cfg["Monitor:Provider"] ?? "SqlServer",
            Database = _db.Database.GetDbConnection().Database ?? "-",
            Devices = await _db.Devices.CountAsync(),
            Disks = await _db.DiskDrives.CountAsync(),
            Software = await _db.SoftwareInstalls.CountAsync(s => s.IsInstalled),
            Snapshots = await _db.DeviceSnapshots.CountAsync(),
            AgentRuns = await _db.AgentRuns.CountAsync(),
            Rules = await _db.ComplianceRules.CountAsync(),
            LastIngestUtc = await _db.Devices.MaxAsync(d => (DateTime?)d.LastSeenUtc),
            ComplianceRules = await _db.ComplianceRules.OrderBy(r => r.SoftwareName).ToListAsync(),
            ApiKeyConfigured = !string.IsNullOrWhiteSpace(key) && key != "GANTI-DENGAN-API-KEY-RAHASIA"
        };
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Data & Ingest";
        ViewData["PageCode"] = "DATA";
        ViewData["NavActive"] = "data";
        return View(await BuildVmAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(64 * 1024 * 1024)]
    public async Task<IActionResult> Import(string kind, IFormFile file)
    {
        var vm = await BuildVmAsync();
        ViewData["NavActive"] = "data";

        if (file is null || file.Length == 0)
        {
            vm.Message = "Pilih file CSV terlebih dahulu.";
            vm.MessageType = "warn";
            return View("Index", vm);
        }

        await using var stream = file.OpenReadStream();
        var result = kind.ToLowerInvariant() switch
        {
            "devices" => await _csv.ImportDevicesAsync(stream),
            "disks" => await _csv.ImportDisksAsync(stream),
            "software" => await _csv.ImportSoftwareAsync(stream),
            "snapshots" => await _csv.ImportSnapshotsAsync(stream),
            "agentruns" => await _csv.ImportAgentRunsAsync(stream),
            _ => new CsvImportResult { FileName = file.FileName, Error = $"Jenis import '{kind}' tidak dikenal." }
        };
        result.FileName = file.FileName;
        vm.ImportResults.Add(result);

        if (result.Success)
        {
            vm.Message = $"Import {result.FileName}: {result.RowsInserted} baris baru, {result.RowsUpdated} diperbarui, {result.RowsSkipped} dilewati.";
            vm.MessageType = result.Error is null ? "ok" : "warn";
        }
        else
        {
            vm.Message = $"Import gagal: {result.Error}";
            vm.MessageType = "err";
        }

        vm = await Refresh(vm);
        return View("Index", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SeedDemo(int deviceCount = 48, int historyDays = 14)
    {
        await _seeder.SeedAsync(Math.Clamp(deviceCount, 5, 300), Math.Clamp(historyDays, 3, 60));
        await _compliance.ReevaluateAllAsync();
        var vm = await Refresh(await BuildVmAsync());
        vm.Message = $"Data demo berhasil dibuat: {deviceCount} perangkat dengan riwayat {historyDays} hari.";
        vm.MessageType = "ok";
        ViewData["NavActive"] = "data";
        return View("Index", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset()
    {
        await _db.AgentRuns.ExecuteDeleteAsync();
        await _db.DeviceSnapshots.ExecuteDeleteAsync();
        await _db.SoftwareInstalls.ExecuteDeleteAsync();
        await _db.DiskDrives.ExecuteDeleteAsync();
        await _db.Devices.ExecuteDeleteAsync();
        await _db.ComplianceRules.ExecuteDeleteAsync();

        var vm = await Refresh(await BuildVmAsync());
        vm.Message = "Semua data transaksi dihapus. Database sekarang kosong.";
        vm.MessageType = "warn";
        ViewData["NavActive"] = "data";
        return View("Index", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reevaluate()
    {
        var changed = await _compliance.ReevaluateAllAsync();
        var vm = await Refresh(await BuildVmAsync());
        vm.Message = $"Compliance dihitung ulang: {changed} baris software berubah status.";
        vm.MessageType = "ok";
        ViewData["NavActive"] = "data";
        return View("Index", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRule(int? id, string softwareName, string? minimumVersion,
                                              string? prohibitedVersions, bool isMandatory, string? notes, bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(softwareName))
        {
            var vmErr = await Refresh(await BuildVmAsync());
            vmErr.Message = "Nama software wajib diisi.";
            vmErr.MessageType = "err";
            ViewData["NavActive"] = "data";
            return View("Index", vmErr);
        }

        ComplianceRule? rule = null;
        if (id is > 0) rule = await _db.ComplianceRules.FindAsync(id);
        rule ??= await _db.ComplianceRules.FirstOrDefaultAsync(r => r.SoftwareName == softwareName);
        if (rule is null)
        {
            rule = new ComplianceRule { SoftwareName = softwareName.Trim() };
            _db.ComplianceRules.Add(rule);
        }
        rule.SoftwareName = softwareName.Trim();
        rule.MinimumVersion = string.IsNullOrWhiteSpace(minimumVersion) ? null : minimumVersion.Trim();
        rule.ProhibitedVersions = string.IsNullOrWhiteSpace(prohibitedVersions) ? null : prohibitedVersions.Trim();
        rule.IsMandatory = isMandatory;
        rule.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        rule.IsEnabled = isEnabled;
        await _db.SaveChangesAsync();

        var changed = await _compliance.ReevaluateAllAsync();
        var vm = await Refresh(await BuildVmAsync());
        vm.Message = $"Aturan '{rule.SoftwareName}' disimpan. {changed} baris software diperbarui statusnya.";
        vm.MessageType = "ok";
        ViewData["NavActive"] = "data";
        return View("Index", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRule(int id)
    {
        var rule = await _db.ComplianceRules.FindAsync(id);
        if (rule is not null)
        {
            _db.ComplianceRules.Remove(rule);
            await _db.SaveChangesAsync();
        }
        var vm = await Refresh(await BuildVmAsync());
        vm.Message = "Aturan compliance dihapus.";
        vm.MessageType = "warn";
        ViewData["NavActive"] = "data";
        return View("Index", vm);
    }

    /// <summary>Unduh template CSV sesuai jenis data.</summary>
    [HttpGet]
    public IActionResult Template(string kind)
    {
        var (name, content) = CsvTemplates.Get(kind);
        return File(Encoding.UTF8.GetBytes(content), "text/csv", name);
    }

    private async Task<DataViewModel> Refresh(DataViewModel vm)
    {
        vm.Devices = await _db.Devices.CountAsync();
        vm.Disks = await _db.DiskDrives.CountAsync();
        vm.Software = await _db.SoftwareInstalls.CountAsync(s => s.IsInstalled);
        vm.Snapshots = await _db.DeviceSnapshots.CountAsync();
        vm.AgentRuns = await _db.AgentRuns.CountAsync();
        vm.Rules = await _db.ComplianceRules.CountAsync();
        vm.LastIngestUtc = await _db.Devices.MaxAsync(d => (DateTime?)d.LastSeenUtc);
        vm.ComplianceRules = await _db.ComplianceRules.OrderBy(r => r.SoftwareName).ToListAsync();
        return vm;
    }
}

/// <summary>Header &amp; contoh isi template CSV untuk import.</summary>
public static class CsvTemplates
{
    public static (string FileName, string Content) Get(string kind) => kind.ToLowerInvariant() switch
    {
        "disks" => ("template-disks.csv",
            "hostname,driveletter,label,drivetype,mediatype,filesystem,sizegb,freegb,usedgb,healthstatus,collectedatutc\n" +
            "PC-IT-01,C,System,Fixed,SSD,NTFS,476.94,82.11,394.83,Warning,2026-10-17T02:00:00Z\n" +
            "PC-IT-01,D,Data,Fixed,HDD,NTFS,931.51,410.22,521.29,Healthy,2026-10-17T02:00:00Z\n"),
        "software" => ("template-software.csv",
            "hostname,name,version,publisher,installdate,sizemb,scope\n" +
            "PC-IT-01,Google Chrome,129.0.6668.90,Google LLC,2026-09-02,312.4,PerMachine\n" +
            "PC-IT-01,Microsoft 365 Apps for enterprise,16.0.17531.20120,Microsoft Corporation,2026-01-14,3120.0,PerMachine\n"),
        "snapshots" => ("template-snapshots.csv",
            "hostname,collectedatutc,status,cpuusagepercent,ramusedgb,ramtotalgb,disktotalgb,diskusedgb,diskfreegb,cfreegb,uptimeseconds,processcount,agentcpupercent,agentmemorymb,offlinequeuecount\n" +
            "PC-IT-01,2026-10-17T02:00:00Z,Online,18.4,7.8,16,1408.45,915.12,493.33,82.11,284400,214,0.8,92.4,0\n"),
        "agentruns" => ("template-agentruns.csv",
            "hostname,startedatutc,finishedatutc,durationms,status,agentversion,collectorsrun,itemssent,offlinequeuecount,cpupercent,memorymb,errormessage,triggersource\n" +
            "PC-IT-01,2026-10-17T02:00:00Z,2026-10-17T02:00:03Z,3120.5,Success,1.0.0,\"hardware,disks,software,telemetry\",42,0,1.2,88.3,,schedule\n"),
        _ => ("template-devices.csv",
            "hostname,assettag,devicetype,manufacturer,model,serialnumber,osname,osversion,osbuild,osarchitecture,biosvendor,biosversion,cpuname,cpucores,cpulogicalprocessors,ramtotalgb,ipaddress,macaddress,domain,department,location,assigneduser,agentversion,status,totaldiskgb,useddiskgb,uptimeseconds,lastseenutc\n" +
            "PC-FIN-01,AST-2024-1001,Desktop,Dell,OptiPlex 7010,7X2AB12,Microsoft Windows 11 Pro,10.0.22631,22631.3447,64-bit,Dell Inc.,1.21.0,Intel(R) Core(TM) i5-12500 @ 3.00GHz,6,12,16,10.20.4.31,A4:B1:C2:D3:E4:F5,corp.local,Finance,HO - Lantai 2,andi.pratama,1.0.0,Online,1408.45,915.12,284400,2026-10-17T02:00:00Z\n")
    };
}
