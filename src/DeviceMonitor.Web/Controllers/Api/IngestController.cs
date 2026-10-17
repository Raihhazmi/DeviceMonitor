using DeviceMonitor.Shared.Contracts;
using DeviceMonitor.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DeviceMonitor.Web.Controllers.Api;

/// <summary>
/// Validasi header <c>X-Agent-Key</c> untuk endpoint ingest.
/// Dimatikan bila Agent:RequireApiKey = false (hanya untuk lab/dev).
/// </summary>
public class ApiKeyFilter : IAsyncActionFilter
{
    public const string HeaderName = "X-Agent-Key";

    private readonly IConfiguration _cfg;
    private readonly ILogger<ApiKeyFilter> _log;

    public ApiKeyFilter(IConfiguration cfg, ILogger<ApiKeyFilter> log)
    {
        _cfg = cfg;
        _log = log;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var require = _cfg.GetValue<bool?>("Agent:RequireApiKey") ?? true;
        if (require)
        {
            var expected = _cfg["Monitor:AgentApiKey"];
            if (string.IsNullOrWhiteSpace(expected) || expected == "GANTI-DENGAN-API-KEY-RAHASIA")
            {
                _log.LogWarning("Monitor:AgentApiKey belum diganti. Ingest ditolak sampai API key diisi.");
                context.Result = new ObjectResult(new
                {
                    error = "AgentApiKey belum dikonfigurasi di server. Isi Monitor:AgentApiKey di appsettings.json."
                })
                { StatusCode = StatusCodes.Status503ServiceUnavailable };
                return;
            }

            if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided)
                || !string.Equals(provided.ToString(), expected, StringComparison.Ordinal))
            {
                context.Result = new ObjectResult(new { error = "API key tidak valid." })
                { StatusCode = StatusCodes.Status401Unauthorized };
                return;
            }
        }

        await next();
    }
}

/// <summary>
/// Endpoint penerima data dari agent di setiap PC/laptop kantor.
/// </summary>
[ApiController]
[Route("api")]
public class IngestController : ControllerBase
{
    private readonly IngestService _ingest;
    private readonly ILogger<IngestController> _log;

    public IngestController(IngestService ingest, ILogger<IngestController> log)
    {
        _ingest = ingest;
        _log = log;
    }

    /// <summary>POST /api/ingest - kirim data perangkat (hardware, disk, software, telemetry, agent run).</summary>
    [HttpPost("ingest")]
    [ServiceFilter(typeof(ApiKeyFilter))]
    [RequestSizeLimit(32 * 1024 * 1024)]
    public async Task<ActionResult<IngestResult>> Ingest([FromBody] IngestPayload payload, CancellationToken ct)
    {
        if (payload?.Device is null || string.IsNullOrWhiteSpace(payload.Device.Hostname))
            return BadRequest(new IngestResult { Success = false, Error = "Field device.hostname wajib diisi." });

        var result = await _ingest.IngestAsync(payload, ct);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>GET /api/ping - cek konektivitas agent ke server.</summary>
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new
    {
        status = "ok",
        serverTimeUtc = DateTime.UtcNow.ToString("o"),
        service = "DeviceMonitor Ingest API"
    });
}
