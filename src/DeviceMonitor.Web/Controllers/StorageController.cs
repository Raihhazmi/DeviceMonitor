using DeviceMonitor.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeviceMonitor.Web.Controllers;

/// <summary>03 - Storage &amp; Disk Health.</summary>
public class StorageController : Controller
{
    private readonly DashboardQueryService _q;
    public StorageController(DashboardQueryService q) => _q = q;

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Storage & Disk Health";
        ViewData["PageCode"] = "03";
        ViewData["NavActive"] = "storage";
        return View(await _q.GetStorageAsync());
    }
}
