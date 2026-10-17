using System.Globalization;
using DeviceMonitor.Web.Models;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace DeviceMonitor.Web.Services;

/// <summary>Helper format untuk Razor view (selalu invariant culture agar aman di server id-ID).</summary>
public static class V
{
    public static string N0(double v) => v.ToString("#,0", CultureInfo.InvariantCulture);
    public static string N1(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);
    public static string N2(double v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    public static string Pct(double v) => v.ToString("0.0", CultureInfo.InvariantCulture) + "%";
    public static string Gb(double v) => Fmt.Gb(v);
    public static string Tb(double v) => Fmt.Tb(v);
    public static string Mb(double v) => Fmt.Mb(v);
    public static string Ms(double v) => Fmt.Ms(v);
    public static string Dur(double seconds) => Fmt.Duration(seconds);
    public static string When(DateTime? utc) => Fmt.DateTimeShort(utc);
    public static string Ago(DateTime? utc) => Fmt.Relative(utc);
    public static string D(DateTime? d) => d is null || d == default ? "-" : d.Value.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);
    public static string Or(this string? s, string fallback = "-") => string.IsNullOrWhiteSpace(s) ? fallback : s;

    /// <summary>Warna inline untuk nilai persen.</summary>
    public static string ToneColor(double percent, double warn = 75, double bad = 90)
        => percent >= bad ? "var(--bad)" : percent >= warn ? "var(--warn)" : "var(--good)";

    /// <summary>Kelas badge untuk status kesehatan disk.</summary>
    public static string HealthTone(string? health) => (health ?? "").ToLowerInvariant() switch
    {
        "healthy" => "good",
        "warning" => "warn",
        "critical" => "bad",
        _ => "muted"
    };

    /// <summary>ViewData siap pakai untuk partial _Chart.</summary>
    public static ViewDataDictionary Chart(ViewDataDictionary baseViewData, string id, string type = "bar",
                                           int height = 230, string? center = null, bool legend = true)
    {
        var vd = new ViewDataDictionary(baseViewData)
        {
            { "chartId", id },
            { "chartType", type },
            { "chartHeight", height },
            { "chartLegend", legend }
        };
        if (center != null) vd["chartCenter"] = center;
        return vd;
    }

    /// <summary>ViewData untuk partial _Notice.</summary>
    public static ViewDataDictionary Notice(ViewDataDictionary baseViewData, string type)
        => new(baseViewData) { { "type", type } };

    /// <summary>ViewData untuk partial _Bar dengan tone eksplisit.</summary>
    public static ViewDataDictionary Tone(ViewDataDictionary baseViewData, string tone)
        => new(baseViewData) { { "tone", tone } };
}
