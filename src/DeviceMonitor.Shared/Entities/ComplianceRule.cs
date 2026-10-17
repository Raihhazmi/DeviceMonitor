namespace DeviceMonitor.Shared.Entities;

/// <summary>
/// Aturan kepatuhan software: versi minimum yang diizinkan / versi yang dilarang.
/// Dipakai untuk memberi label Compliant / Outdated / Prohibited pada SoftwareInstall.
/// </summary>
public class ComplianceRule
{
    public int Id { get; set; }

    /// <summary>Nama software (case-insensitive).</summary>
    public string SoftwareName { get; set; } = string.Empty;

    /// <summary>Versi minimum yang masih diizinkan. Kosong = tidak ada batas.</summary>
    public string? MinimumVersion { get; set; }

    /// <summary>Versi yang dilarang (dipisah koma). Kosong = tidak ada.</summary>
    public string? ProhibitedVersions { get; set; }

    /// <summary>true = software ini wajib ada di setiap perangkat.</summary>
    public bool IsMandatory { get; set; }

    public string? Notes { get; set; }
    public bool IsEnabled { get; set; } = true;
}
