using DeviceMonitor.Web.Data;
using DeviceMonitor.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DeviceMonitor.Web.Controllers.Api;

/// <summary>
/// Endpoint baca (read-only) untuk integrasi eksternal: Grafana / Power BI / skrip PowerShell.
/// </summary>
[ApiController]
[Route("api")]
public class DevicesController : ControllerBase
{
    private readonly MonitorDbContext _db;
    private readonly DashboardQueryService _q;

    public DevicesController(MonitorDbContext db, DashboardQueryService q)
    {
        _db = db;
        _q = q;
    }

    /// <summary>GET /api/devices - daftar perangkat + kondisi terkini.</summary>
    [HttpGet("devices")]
    public async Task<IActionResult> GetDevices([FromQuery] string? hostname = null, [FromQuery] string? department = null,
                                                [FromQuery] string? status = null)
    {
        var query = _db.Devices.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(hostname)) query = query.Where(d => d.Hostname.Contains(hostname));
        if (!string.IsNullOrWhiteSpace(department)) query = query.Where(d => d.Department == department);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(d => d.Status == status);

        var list = await query.OrderBy(d => d.Hostname).Select(d => new
        {
            d.Hostname,
            d.DeviceType,
            d.Manufacturer,
            d.Model,
            d.OsName,
            d.OsBuild,
            d.BiosVersion,
            d.RamTotalGb,
            d.TotalDiskGb,
            d.UsedDiskGb,
            d.UptimeSeconds,
            d.Status,
            d.Department,
            d.Location,
            d.AssignedUser,
            d.IpAddress,
            d.AgentVersion,
            d.LastSeenUtc
        }).ToListAsync();

        return Ok(list);
    }

    /// <summary>GET /api/summary - KPI ringkas untuk widget/Grafana.</summary>
    [HttpGet("summary")]
    public async Task<IActionResult> Summary() => Ok(await _q.GetSummaryAsync());

    /// <summary>GET /api/devices/{hostname}/disks - kondisi partisi terkini satu perangkat.</summary>
    [HttpGet("devices/{hostname}/disks")]
    public async Task<IActionResult> Disks(string hostname)
    {
        var device = await _db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Hostname == hostname);
        if (device is null) return NotFound(new { error = $"Perangkat '{hostname}' tidak ditemukan." });

        var disks = await _db.DiskDrives.AsNoTracking()
            .Where(d => d.DeviceId == device.Id)
            .OrderBy(d => d.DriveLetter)
            .Select(d => new { d.DriveLetter, d.Label, d.MediaType, d.FileSystem, d.SizeGb, d.UsedGb, d.FreeGb, d.UsedPercent, d.HealthStatus, d.CollectedAtUtc })
            .ToListAsync();

        return Ok(new { device.Hostname, disks });
    }

    /// <summary>GET /api/devices/{hostname}/software - inventaris software satu perangkat.</summary>
    [HttpGet("devices/{hostname}/software")]
    public async Task<IActionResult> Software(string hostname)
    {
        var device = await _db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Hostname == hostname);
        if (device is null) return NotFound(new { error = $"Perangkat '{hostname}' tidak ditemukan." });

        var sw = await _db.SoftwareInstalls.AsNoTracking()
            .Where(s => s.DeviceId == device.Id && s.IsInstalled)
            .OrderBy(s => s.Name)
            .Select(s => new { s.Name, s.Version, s.Publisher, s.InstallDate, s.SizeMb, s.ComplianceStatus })
            .ToListAsync();

        return Ok(new { device.Hostname, count = sw.Count, software = sw });
    }
}
