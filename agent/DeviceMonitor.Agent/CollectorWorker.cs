using System.Diagnostics;
using System.Net.Http.Json;
using DeviceMonitor.Shared.Contracts;
using Microsoft.Extensions.Options;

namespace DeviceMonitor.Agent;

/// <summary>
/// Siklus kerja agent:
///  1. kumpulkan data (hardware/disk/software/telemetry),
///  2. kirim ke POST /api/ingest,
///  3. bila gagal -> simpan ke antrian offline,
///  4. tiap siklus coba kirim ulang antrian tertua lebih dulu.
/// </summary>
public class CollectorWorker : BackgroundService
{
    private readonly AgentOptions _opt;
    private readonly SystemInfoCollector _collector;
    private readonly OfflineQueue _queue;
    private readonly HttpClient _http;
    private readonly ILogger<CollectorWorker> _log;
    private int _cycle;

    public CollectorWorker(IOptions<AgentOptions> options, SystemInfoCollector collector,
                           OfflineQueue queue, HttpClient http, ILogger<CollectorWorker> log)
    {
        _opt = options.Value;
        _collector = collector;
        _queue = queue;
        _http = http;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("DeviceMonitor Agent {Version} mulai. Server: {Server}", AgentOptions.AgentVersion, _opt.ServerUrl);

        if (string.IsNullOrWhiteSpace(_opt.ApiKey) || _opt.ApiKey == "GANTI-DENGAN-API-KEY-RAHASIA")
            _log.LogWarning("Agent:ApiKey masih nilai bawaan. Server akan menolak payload (401).");

        try { await Task.Delay(TimeSpan.FromSeconds(_opt.InitialDelaySeconds), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(5, _opt.IntervalMinutes)));
        do
        {
            var sw = Stopwatch.StartNew();
            await RunCycleAsync(stoppingToken);
            _log.LogInformation("Siklus #{Cycle} selesai dalam {Ms} ms. Antrian offline: {Queue}",
                _cycle, sw.ElapsedMilliseconds, _queue.Count);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false) == true);
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        _cycle++;
        var started = DateTime.UtcNow;

        var payload = new IngestPayload
        {
            Device = _collector.CollectDevice(),
            Disks = _collector.CollectDisks(),
            Telemetry = _collector.CollectTelemetry(),
            IsFullSoftwareInventory = _opt.FullSoftwareInventory
        };

        if (_cycle == 1 || _cycle % Math.Max(1, _opt.SoftwareEveryCycles) == 0)
            payload.Software = _collector.CollectSoftware();

        payload.Telemetry.OfflineQueueCount = _queue.Count;
        payload.AgentRun = new AgentRunPayload
        {
            StartedAtUtc = started,
            AgentVersion = AgentOptions.AgentVersion,
            CollectorsRun = payload.Software is { Count: > 0 }
                ? "hardware,disks,software,telemetry"
                : "hardware,disks,telemetry",
            TriggerSource = "schedule",
            OfflineQueueCount = _queue.Count
        };

        var sent = await SendAsync(payload, ct);

        payload.AgentRun.FinishedAtUtc = DateTime.UtcNow;
        payload.AgentRun.DurationMs = (DateTime.UtcNow - started).TotalMilliseconds;
        payload.AgentRun.Status = sent ? "Success" : "Queued";
        payload.AgentRun.ItemsSent = sent ? 1 + (payload.Disks?.Count ?? 0) + (payload.Software?.Count ?? 0) : 0;

        if (!sent)
        {
            await _queue.EnqueueAsync(payload, ct);
        }

        await FlushQueueAsync(ct);
        return;
    }

    private async Task<bool> SendAsync(IngestPayload payload, CancellationToken ct)
    {
        try
        {
            var url = _opt.ServerUrl.TrimEnd('/') + "/api/ingest";
            using var resp = await _http.PostAsJsonAsync(url, payload, ct);
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _log.LogInformation("Payload {Host} diterima server: {Body}", payload.Device?.Hostname, body);
                return true;
            }

            // 4xx selain kegagalan jaringan: jangan di-retry terus menerus
            if (resp.StatusCode is >= System.Net.HttpStatusCode.BadRequest and < System.Net.HttpStatusCode.InternalServerError
                && resp.StatusCode != System.Net.HttpStatusCode.RequestTimeout)
            {
                _log.LogError("Server menolak payload ({Code}): {Msg}", (int)resp.StatusCode,
                    await resp.Content.ReadAsStringAsync(ct));
                return true; // anggap selesai supaya tidak menumpuk di antrian
            }

            _log.LogWarning("Gagal kirim ({Code}). Akan diantrikan.", (int)resp.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Tidak bisa menghubungi server {Server}", _opt.ServerUrl);
            return false;
        }
    }

    private async Task FlushQueueAsync(CancellationToken ct)
    {
        var pending = await _queue.PeekAsync(10, ct);
        if (pending.Count == 0) return;

        _log.LogInformation("Mencoba mengirim {N} payload tertunda…", pending.Count);
        foreach (var (file, payload) in pending)
        {
            payload.Telemetry ??= new TelemetryPayload();
            payload.Telemetry.OfflineQueueCount = _queue.Count;
            if (await SendAsync(payload, ct)) _queue.Remove(file);
            else break; // server masih belum sehat, coba lagi siklus depan
        }
    }
}
