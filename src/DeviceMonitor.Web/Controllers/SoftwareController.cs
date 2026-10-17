using DeviceMonitor.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeviceMonitor.Web.Controllers;

/// <summary>05 - Software Inventory &amp; Compliance.</summary>
public class SoftwareController : Controller
{
    private readonly DashboardQueryService _q;
    public SoftwareController(DashboardQueryService q) => _q = q;

    public async Task<IActionResult> Index(int page = 1, string? q = null, string? publisher = null,
                                             string? compliance = null, string sort = "name", int size = 25)
    {
        ViewData["Title"] = "Software Inventory & Compliance";
        ViewData["PageCode"] = "05";
        ViewData["NavActive"] = "software";
        var vm = await _q.GetSoftwareAsync(page, size, q, publisher, compliance, sort);
        return View(vm);
    }
}
