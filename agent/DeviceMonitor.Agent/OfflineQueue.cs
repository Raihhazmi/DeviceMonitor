using DeviceMonitor.Shared.Contracts;
using Microsoft.Extensions.Options;

namespace DeviceMonitor.Agent;

/// <summary>
/// Antrian payload saat server tidak bisa dihubungi.
/// Payload disimpan sebagai file JSON sehingga tidak hilang walau mesin restart.
/// </summary>
public class OfflineQueue
{
    private readonly string _path;
    private readonly ILogger<OfflineQueue> _log;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public OfflineQueue(IOptions<AgentOptions> options, ILogger<OfflineQueue> log)
    {
        _log = log;
        _path = string.IsNullOrWhiteSpace(options.Value.QueuePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "DeviceMonitor", "queue")
            : options.Value.QueuePath;
        Directory.CreateDirectory(_path);
    }

    public int Count => Directory.Exists(_path) ? Directory.GetFiles(_path, "*.json").Length : 0;

    public async Task EnqueueAsync(IngestPayload payload, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var name = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json";
            var file = Path.Combine(_path, name);
            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            await File.WriteAllTextAsync(file, json, ct);
            _log.LogWarning("Server tidak terjangkau. Payload {Host} masuk antrian ({File}).",
                payload.Device?.Hostname, name);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Ambil hingga <paramref name="max"/> payload tertua tanpa menghapusnya.</summary>
    public async Task<List<(string File, IngestPayload Payload)>> PeekAsync(int max = 10, CancellationToken ct = default)
    {
        var result = new List<(string, IngestPayload)>();
        await _lock.WaitAsync(ct);
        try
        {
            foreach (var file in Directory.GetFiles(_path, "*.json").OrderBy(f => f).Take(max))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file, ct);
                    var payload = System.Text.Json.JsonSerializer.Deserialize<IngestPayload>(json);
                    if (payload is not null) result.Add((file, payload));
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "File antrian rusak: {File}. Dibuang.", file);
                    try { File.Delete(file); } catch { /* ignore */ }
                }
            }
        }
        finally
        {
            _lock.Release();
        }
        return result;
    }

    public void Remove(string file)
    {
        try { if (File.Exists(file)) File.Delete(file); }
        catch (Exception ex) { _log.LogError(ex, "Gagal menghapus file antrian {File}", file); }
    }
}
