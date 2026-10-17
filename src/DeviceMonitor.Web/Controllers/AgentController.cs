using DeviceMonitor.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeviceMonitor.Web.Controllers;

/// <summary>04 - Agent Health &amp; Telemetry.</summary>
public class AgentController : Controller
{
    private readonly DashboardQueryService _q;
    public AgentController(DashboardQueryService q) => _q = q;

    public async Task<IActionResult> Index(int page = 1, string? q = null, string? status = null, int size = 25)
    {
        ViewData["Title"] = "Agent Health & Telemetry";
        ViewData["PageCode"] = "04";
        ViewData["NavActive"] = "agent";
        var vm = await _q.GetAgentHealthAsync(page, size, q, status);
        return View(vm);
    }
}
