using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DeviceMonitor.Web.Data;

/// <summary>
/// Dipakai `dotnet ef` saat membuat migration tanpa menjalankan aplikasi.
/// Migration dibuat untuk SQL Server (provider utama), dan tetap bisa
/// diaplikasikan ke SQLite karena skemanya provider-agnostic.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<MonitorDbContext>
{
    public MonitorDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("DM_CONNECTION")
            ?? "Server=localhost,1433;Database=DeviceMonitor;User Id=sa;Password=Your_Strong_Pass123!;TrustServerCertificate=True;Encrypt=False";

        var options = new DbContextOptionsBuilder<MonitorDbContext>()
            .UseSqlServer(cs)
            .Options;

        return new MonitorDbContext(options);
    }
}
