namespace DeviceMonitor.Shared.Entities;

/// <summary>
/// Inventaris software terpasang per perangkat (latest state, di-diff saat ingest).
/// </summary>
public class SoftwareInstall
{
    public long Id { get; set; }

    public int DeviceId { get; set; }
    public Device? Device { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "unknown";
    public string? Publisher { get; set; }
    public DateTime? InstallDate { get; set; }
    public double SizeMb { get; set; }

    /// <summary>PerMachine / PerUser.</summary>
    public string? Scope { get; set; }

    /// <summary>Compliant / Outdated / Prohibited / Unmanaged.</summary>
    public string ComplianceStatus { get; set; } = "Unmanaged";

    public DateTime FirstSeenUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;

    /// <summary>false = sudah tidak ditemukan lagi di perangkat (uninstall).</summary>
    public bool IsInstalled { get; set; } = true;
}
