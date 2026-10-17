using DeviceMonitor.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeviceMonitor.Web.Controllers;

/// <summary>01 - Executive Overview.</summary>
public class OverviewController : Controller
{
    private readonly DashboardQueryService _q;
    public OverviewController(DashboardQueryService q) => _q = q;

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Executive Overview";
        ViewData["PageCode"] = "01";
        ViewData["NavActive"] = "overview";
        return View(await _q.GetOverviewAsync());
    }
}
