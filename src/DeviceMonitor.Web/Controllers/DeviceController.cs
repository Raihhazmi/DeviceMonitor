using DeviceMonitor.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeviceMonitor.Web.Controllers;

public class DeviceController : Controller
{
    private readonly DashboardQueryService _q;
    public DeviceController(DashboardQueryService q) => _q = q;

    public async Task<IActionResult> Details(string id)
    {
        var vm = await _q.GetDeviceAsync(id);
        if (vm is null) return NotFound();

        ViewData["Title"] = vm.Device.Hostname;
        ViewData["PageCode"] = "DETAIL";
        ViewData["NavActive"] = "overview";
        return View(vm);
    }
}
