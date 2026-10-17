using DeviceMonitor.Shared.Entities;
using DeviceMonitor.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace DeviceMonitor.Web.Services;

/// <summary>
/// Menilai status kepatuhan software berdasarkan ComplianceRule.
/// Dipanggil saat ingest dan bisa dipicu ulang manual dari halaman Data & Ingest.
/// </summary>
public class ComplianceService
{
    private readonly MonitorDbContext _db;

    public ComplianceService(MonitorDbContext db) => _db = db;

    public static async Task<List<ComplianceRule>> LoadRulesAsync(MonitorDbContext db)
        => await db.ComplianceRules.AsNoTracking().Where(r => r.IsEnabled).ToListAsync();

    /// <summary>Compliant / Outdated / Prohibited / Unmanaged.</summary>
    public static string Evaluate(string name, string version, IReadOnlyList<ComplianceRule> rules)
    {
        var rule = rules.FirstOrDefault(r => r.SoftwareName.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (rule is null) return "Unmanaged";

        // Versi dilarang: cocok persis atau cocok pola wildcard "8.0.*"
        if (!string.IsNullOrWhiteSpace(rule.ProhibitedVersions))
        {
            var banned = rule.ProhibitedVersions
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var b in banned)
            {
                if (b.EndsWith('*'))
                {
                    if (version.StartsWith(b.TrimEnd('*'), StringComparison.OrdinalIgnoreCase)) return "Prohibited";
                }
                else if (b.Equals(version, StringComparison.OrdinalIgnoreCase))
                {
                    return "Prohibited";
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(rule.MinimumVersion) && Fmt.CompareVersions(version, rule.MinimumVersion) < 0)
            return "Outdated";

        return "Compliant";
    }

    /// <summary>Hitung ulang status compliance untuk seluruh baris SoftwareInstall.</summary>
    public async Task<int> ReevaluateAllAsync()
    {
        var rules = await _db.ComplianceRules.Where(r => r.IsEnabled).ToListAsync();
        var items = await _db.SoftwareInstalls.Where(s => s.IsInstalled).ToListAsync();
        var changed = 0;
        foreach (var s in items)
        {
            var status = Evaluate(s.Name, s.Version, rules);
            if (s.ComplianceStatus != status)
            {
                s.ComplianceStatus = status;
                changed++;
            }
        }
        if (changed > 0) await _db.SaveChangesAsync();
        return changed;
    }
}
