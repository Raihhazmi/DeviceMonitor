using DeviceMonitor.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeviceMonitor.Web.Controllers;

/// <summary>02 - Hardware &amp; System Inventory.</summary>
public class HardwareController : Controller
{
    private readonly DashboardQueryService _q;
    public HardwareController(DashboardQueryService q) => _q = q;

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Hardware & System Inventory";
        ViewData["PageCode"] = "02";
        ViewData["NavActive"] = "hardware";
        return View(await _q.GetHardwareAsync());
    }
}
