using DeviceMonitor.Web.Data;
using DeviceMonitor.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- configuration
builder.Services.Configure<MonitorOptions>(builder.Configuration.GetSection(MonitorOptions.SectionName));
var monitorOptions = builder.Configuration.GetSection(MonitorOptions.SectionName).Get<MonitorOptions>() ?? new MonitorOptions();

// ---------------------------------------------------------------- database
// Provider utama: SQL Server (MSSQL). Untuk uji coba cepat tanpa SQL Server,
// ubah "Monitor:Provider" menjadi "Sqlite" di appsettings.json.
var provider = builder.Configuration["Monitor:Provider"] ?? "SqlServer";
var sqlServerCs = builder.Configuration.GetConnectionString("SqlServerDb")
                  ?? throw new InvalidOperationException("ConnectionStrings:SqlServerDb belum diisi.");
var sqliteCs = builder.Configuration.GetConnectionString("SqliteDb") ?? "Data Source=devicemonitor.db";

builder.Services.AddDbContext<MonitorDbContext>(opt =>
{
    if (string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
        opt.UseSqlite(sqliteCs);
    else
        opt.UseSqlServer(sqlServerCs, sql =>
        {
            sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
            sql.CommandTimeout(60);
        });
});

// ---------------------------------------------------------------- services
builder.Services.AddControllersWithViews()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddScoped<DashboardQueryService>();
builder.Services.AddScoped<IngestService>();
builder.Services.AddScoped<CsvImportService>();
builder.Services.AddScoped<ComplianceService>();
builder.Services.AddScoped<DemoDataSeeder>();
builder.Services.AddScoped<DeviceMonitor.Web.Controllers.Api.ApiKeyFilter>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddResponseCompression(o => o.EnableForHttps = true);

var app = builder.Build();

// ---------------------------------------------------------------- pipeline
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Home/Error");

app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl = "public,max-age=300";
    }
});

app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ---------------------------------------------------------------- startup: migrate + seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MonitorDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        var autoMigrate = app.Configuration.GetValue<bool?>("Monitor:AutoMigrate") ?? true;
        var isSqlite = string.Equals(provider, "Sqlite", StringComparison.OrdinalIgnoreCase);

        if (isSqlite)
        {
            // Mode demo/lokal: skema dibuat langsung oleh provider SQLite
            // (migration EF di proyek ini ditujukan untuk SQL Server).
            db.Database.EnsureCreated();
        }
        else if (autoMigrate)
        {
            // Produksi SQL Server: terapkan migration EF secara otomatis.
            if (db.Database.GetPendingMigrations().Any() || db.Database.GetAppliedMigrations().Any())
                db.Database.Migrate();
            else
                db.Database.EnsureCreated();
        }
        else
        {
            logger.LogInformation("Monitor:AutoMigrate = false. Skema diasumsikan sudah dibuat manual (lihat db/migration.sql).");
        }

        var seedCfg = app.Configuration.GetSection("Monitor:Seed");
        if (seedCfg.GetValue<bool?>("OnStartup") ?? true)
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DemoDataSeeder>();
            var created = await seeder.SeedIfEmptyAsync(
                seedCfg.GetValue<int?>("DeviceCount") ?? 48,
                seedCfg.GetValue<int?>("HistoryDays") ?? monitorOptions.TrendDays);
            if (created) logger.LogInformation("Seed data demo selesai dibuat.");
        }
        logger.LogInformation("Database siap. Provider = {Provider}", provider);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Gagal menyiapkan database. Periksa koneksi SQL Server / Monitor:Provider di appsettings.json.");
        if (!app.Environment.IsDevelopment()) throw;
    }
}

app.Run();
