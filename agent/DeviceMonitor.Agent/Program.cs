using DeviceMonitor.Agent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// Bisa dijalankan sebagai console maupun Windows Service (sc create / Register-ScheduledTask).
builder.Services.AddWindowsService(o => o.ServiceName = "DeviceMonitorAgent");

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
builder.Services.AddSingleton<OfflineQueue>();
builder.Services.AddSingleton<HttpClient>(sp =>
{
    var opt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentOptions>>().Value;
    var client = new HttpClient { Timeout = TimeSpan.FromSeconds(Math.Max(15, opt.TimeoutSeconds)) };
    client.DefaultRequestHeaders.Add("X-Agent-Key", opt.ApiKey);
    client.DefaultRequestHeaders.Add("User-Agent", $"DeviceMonitorAgent/{AgentOptions.AgentVersion}");
    return client;
});
builder.Services.AddSingleton<SystemInfoCollector>();
builder.Services.AddHostedService<CollectorWorker>();

var host = builder.Build();
host.Run();
