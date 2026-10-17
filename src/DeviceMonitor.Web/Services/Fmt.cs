using System.Globalization;

namespace DeviceMonitor.Web.Services;

/// <summary>Kumpulan helper format angka/waktu/warna supaya konsisten di semua halaman.</summary>
public static class Fmt
{
    public static readonly string[] Palette =
    {
        "#4f8cff", "#22c9a5", "#f5a524", "#ef4d6f", "#a06bff",
        "#37c6f4", "#f47b3b", "#8bd450", "#e86bd0", "#6b7fd7",
        "#00b8a9", "#ff7a59", "#9aa5b1", "#c9a227", "#5d8bf4"
    };

    public static string Color(int i) => Palette[Math.Abs(i) % Palette.Length];

    /// <summary>Warna status perangkat.</summary>
    public static string StatusColor(string? status) => (status ?? "").ToLowerInvariant() switch
    {
        "online" => "#22c9a5",
        "offline" => "#9aa5b1",
        "warning" => "#f5a524",
        "error" => "#ef4d6f",
        _ => "#4f8cff"
    };

    public static string HealthColor(string? health) => (health ?? "").ToLowerInvariant() switch
    {
        "healthy" => "#22c9a5",
        "warning" => "#f5a524",
        "critical" => "#ef4d6f",
        _ => "#9aa5b1"
    };

    public static string ComplianceColor(string? status) => (status ?? "").ToLowerInvariant() switch
    {
        "compliant" => "#22c9a5",
        "outdated" => "#f5a524",
        "prohibited" => "#ef4d6f",
        _ => "#9aa5b1"
    };

    public static double N(double v, int digits = 1) => Math.Round(v, digits, MidpointRounding.AwayFromZero);

    public static string Gb(double gb)
    {
        if (Math.Abs(gb) >= 1024) return (gb / 1024.0).ToString("0.00", CultureInfo.InvariantCulture) + " TB";
        return gb.ToString("0.0", CultureInfo.InvariantCulture) + " GB";
    }

    public static string Tb(double tb) => tb.ToString("0.00", CultureInfo.InvariantCulture) + " TB";

    public static string Percent(double p) => p.ToString("0.0", CultureInfo.InvariantCulture) + "%";

    public static string Mb(double mb)
    {
        if (Math.Abs(mb) >= 1024) return (mb / 1024.0).ToString("0.00", CultureInfo.InvariantCulture) + " GB";
        return mb.ToString("0", CultureInfo.InvariantCulture) + " MB";
    }

    /// <summary>3d 4h 12m / 5h 20m / 42m 10s.</summary>
    public static string Duration(double seconds)
    {
        if (seconds <= 0 || double.IsNaN(seconds)) return "-";
        var ts = TimeSpan.FromSeconds(seconds);
        if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        return $"{(int)ts.TotalSeconds}s";
    }

    public static string Ms(double ms)
    {
        if (ms >= 1000) return (ms / 1000.0).ToString("0.00", CultureInfo.InvariantCulture) + " s";
        return ms.ToString("0", CultureInfo.InvariantCulture) + " ms";
    }

    /// <summary>Waktu relatif: "baru saja", "5 mnt lalu", "3 jam lalu", "2 hari lalu".</summary>
    public static string Relative(DateTime? utc, DateTime? nowUtc = null)
    {
        if (utc is null || utc == default) return "-";
        var now = nowUtc ?? DateTime.UtcNow;
        var span = now - utc.Value;
        if (span.TotalSeconds < 0) span = TimeSpan.Zero;
        if (span.TotalSeconds < 60) return "baru saja";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} mnt lalu";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} jam lalu";
        if (span.TotalDays < 30) return $"{(int)span.TotalDays} hari lalu";
        return utc.Value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
    }

    /// <summary>Format jam lokal untuk tampilan (UTC -> local server/browser tidak diubah, hanya format).</summary>
    public static string DateTimeShort(DateTime? utc)
        => utc is null || utc == default ? "-" : utc.Value.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture) + " UTC";

    public static string Day(DateTime d) => d.ToString("dd MMM", CultureInfo.InvariantCulture);

    /// <summary>Bandingkan versi dot-separated: 1 = a lebih baru, -1 = a lebih lama, 0 = sama.</summary>
    public static int CompareVersions(string? a, string? b)
    {
        var pa = Split(a); var pb = Split(b);
        var len = Math.Max(pa.Length, pb.Length);
        for (var i = 0; i < len; i++)
        {
            var x = i < pa.Length ? pa[i] : 0;
            var y = i < pb.Length ? pb[i] : 0;
            if (x != y) return x > y ? 1 : -1;
        }
        return 0;

        static int[] Split(string? v)
        {
            if (string.IsNullOrWhiteSpace(v)) return Array.Empty<int>();
            return v.Split('.', '-', '_')
                    .Select(p => new string(p.TakeWhile(char.IsDigit).ToArray()))
                    .Select(p => int.TryParse(p, out var n) ? n : 0)
                    .ToArray();
        }
    }

    public static string UptimeCategoryOf(double uptimeSeconds)
    {
        var hours = uptimeSeconds / 3600.0;
        if (hours <= 0) return "Tanpa Data";
        if (hours < 24) return "< 24 jam";
        if (hours < 24 * 7) return "1 - 7 hari";
        if (hours < 24 * 30) return "7 - 30 hari";
        return "> 30 hari";
    }

    public static List<DateTime> LastDays(int days, DateTime? todayUtc = null)
    {
        var today = (todayUtc ?? DateTime.UtcNow).Date;
        return Enumerable.Range(0, days).Select(i => today.AddDays(-(days - 1 - i))).ToList();
    }

    /// <summary>Isi hari yang tidak punya data dengan 0 agar grafik tetap kontinu.</summary>
    public static List<double> FillSeries(List<DateTime> days, Dictionary<DateTime, double> values, int decimals = 1)
        => days.Select(d => values.TryGetValue(d, out var v) ? Math.Round(v, decimals, MidpointRounding.AwayFromZero) : 0d).ToList();
}
