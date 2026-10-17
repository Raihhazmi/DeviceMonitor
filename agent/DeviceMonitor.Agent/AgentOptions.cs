namespace DeviceMonitor.Agent;

public class AgentOptions
{
    public const string SectionName = "Agent";
    public const string AgentVersion = "1.0.0";

    /// <summary>URL dashboard, mis. http://srv-monitor:5000 atau https://monitor.kantor.local.</summary>
    public string ServerUrl { get; set; } = "http://localhost:5000";

    /// <summary>Harus sama dengan Monitor:AgentApiKey di server.</summary>
    public string ApiKey { get; set; } = "GANTI-DENGAN-API-KEY-RAHASIA";

    /// <summary>Interval pengumpulan data (menit). Default 6 jam.</summary>
    public int IntervalMinutes { get; set; } = 360;

    /// <summary>Jeda sebelum pengiriman pertama setelah service start (detik).</summary>
    public int InitialDelaySeconds { get; set; } = 30;

    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Kirim daftar software lengkap (mendeteksi uninstall).</summary>
    public bool FullSoftwareInventory { get; set; } = true;

    /// <summary>Kirim inventaris software hanya tiap N siklus (payload besar).</summary>
    public int SoftwareEveryCycles { get; set; } = 4;

    /// <summary>Folder antrian offline. Kosong = %ProgramData%/DeviceMonitor/queue.</summary>
    public string QueuePath { get; set; } = string.Empty;

    /// <summary>Label departemen/lokasi bila tidak terbaca dari AD/LDAP.</summary>
    public string? Department { get; set; }
    public string? Location { get; set; }
    public string? AssignedUser { get; set; }
}
