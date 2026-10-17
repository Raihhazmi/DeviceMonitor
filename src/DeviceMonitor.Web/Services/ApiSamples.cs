namespace DeviceMonitor.Web.Services;

/// <summary>
/// Contoh payload &amp; skrip untuk halaman Data &amp; Ingest (dokumentasi API).
/// Disimpan di C# (bukan di Razor) agar mudah dipelihara dan tidak bentrok dengan parser Razor.
/// </summary>
public static class ApiSamples
{
    public const string JsonPayload = """
{
  "device": {
    "hostname": "PC-FIN-01",
    "assetTag": "AST-2024-1001",
    "deviceType": "Desktop",
    "manufacturer": "Dell",
    "model": "OptiPlex 7010",
    "serialNumber": "7X2AB12",
    "osName": "Microsoft Windows 11 Pro",
    "osVersion": "10.0.22631",
    "osBuild": "22631.3447",
    "osArchitecture": "64-bit",
    "biosVendor": "Dell Inc.",
    "biosVersion": "1.21.0",
    "cpuName": "Intel(R) Core(TM) i5-12500 @ 3.00GHz",
    "cpuCores": 6,
    "cpuLogicalProcessors": 12,
    "ramTotalGb": 16,
    "ipAddress": "10.20.4.31",
    "macAddress": "A4:B1:C2:D3:E4:F5",
    "domain": "corp.local",
    "department": "Finance",
    "location": "HO - Lantai 2",
    "assignedUser": "andi.pratama",
    "agentVersion": "1.0.0",
    "uptimeSeconds": 284400,
    "collectedAtUtc": "2026-10-17T02:00:00Z"
  },
  "disks": [
    { "driveLetter": "C", "label": "System", "driveType": "Fixed", "mediaType": "SSD",
      "fileSystem": "NTFS", "sizeGb": 476.94, "freeGb": 82.11, "healthStatus": "Warning" },
    { "driveLetter": "D", "label": "Data", "driveType": "Fixed", "mediaType": "HDD",
      "fileSystem": "NTFS", "sizeGb": 931.51, "freeGb": 410.22, "healthStatus": "Healthy" }
  ],
  "software": [
    { "name": "Google Chrome", "version": "129.0.6668.90", "publisher": "Google LLC",
      "installDate": "2026-09-02", "sizeMb": 312.4, "scope": "PerMachine" },
    { "name": "Microsoft 365 Apps for enterprise", "version": "16.0.17531.20120",
      "publisher": "Microsoft Corporation", "sizeMb": 3120.0, "scope": "PerMachine" }
  ],
  "telemetry": {
    "collectedAtUtc": "2026-10-17T02:00:00Z",
    "status": "Online",
    "cpuUsagePercent": 18.4,
    "ramUsedGb": 7.8,
    "uptimeSeconds": 284400,
    "processCount": 214,
    "agentCpuPercent": 0.8,
    "agentMemoryMb": 92.4,
    "offlineQueueCount": 0
  },
  "agentRun": {
    "startedAtUtc": "2026-10-17T01:59:57Z",
    "finishedAtUtc": "2026-10-17T02:00:00Z",
    "durationMs": 3120.5,
    "status": "Success",
    "agentVersion": "1.0.0",
    "collectorsRun": "hardware,disks,software,telemetry",
    "itemsSent": 42,
    "offlineQueueCount": 0,
    "cpuPercent": 1.2,
    "memoryMb": 88.3,
    "triggerSource": "schedule"
  },
  "isFullSoftwareInventory": true
}
""";

    public const string EndpointList = """
POST /api/ingest                     kirim data perangkat (hardware, disk, software, telemetry, agent run)
GET  /api/ping                       cek konektivitas agent ke server
GET  /api/summary                    KPI ringkas (untuk widget / Grafana / Power BI)
GET  /api/devices                    daftar perangkat + kondisi terkini
GET  /api/devices/{hostname}/disks    partisi terkini satu perangkat
GET  /api/devices/{hostname}/software inventaris software satu perangkat
""";

    public const string PowerShellCollector = """
# ============================================================================
#  DeviceMonitor Collector - contoh pengumpul data untuk satu PC Windows
#  Jalankan manual, lewat Task Scheduler, atau GPO startup script.
# ============================================================================
$Server = "%BASE%/api/ingest"
$ApiKey = "GANTI-DENGAN-API-KEY-RAHASIA"

$os   = Get-CimInstance Win32_OperatingSystem
$cs   = Get-CimInstance Win32_ComputerSystem
$bios = Get-CimInstance Win32_BIOS
$cpu  = Get-CimInstance Win32_Processor | Select-Object -First 1

$disks = Get-CimInstance Win32_LogicalDisk -Filter "DriveType=3" | ForEach-Object {
    [pscustomobject]@{
        driveLetter = $_.DeviceID.TrimEnd(':')
        label       = $_.VolumeName
        driveType   = 'Fixed'
        fileSystem  = $_.FileSystem
        sizeGb      = [math]::Round($_.Size / 1GB, 2)
        freeGb      = [math]::Round($_.FreeSpace / 1GB, 2)
    }
}

$paths = @(
  'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*',
  'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\*'
)
$software = Get-ItemProperty $paths -ErrorAction SilentlyContinue |
    Where-Object { $_.DisplayName -and -not $_.SystemComponent } |
    Select-Object -Unique DisplayName | ForEach-Object {
        [pscustomobject]@{
            name        = $_.DisplayName
            version     = $_.DisplayVersion
            publisher   = $_.Publisher
            installDate = if ($_.InstallDate) { [datetime]::ParseExact($_.InstallDate, 'yyyyMMdd', $null) } else { $null }
            sizeMb      = [math]::Round($_.EstimatedSize / 1024, 1)
            scope       = 'PerMachine'
        }
    }

$usedRam = [math]::Round(($cs.TotalPhysicalMemory - ($os.FreePhysicalMemory * 1KB)) / 1GB, 2)
$uptime  = [math]::Round(((Get-Date) - $os.LastBootUpTime).TotalSeconds)
$cpuPct  = (Get-Counter '\Processor(_Total)\% Processor Time' -ErrorAction SilentlyContinue).CounterSamples.CookedValue

$body = @{
    device = @{
        hostname             = $env:COMPUTERNAME
        deviceType           = if ($cs.PCSystemType -eq 2) { 'Laptop' } else { 'Desktop' }
        manufacturer         = $cs.Manufacturer
        model                = $cs.Model
        serialNumber         = $bios.SerialNumber
        osName               = $os.Caption
        osVersion            = $os.Version
        osBuild              = $os.BuildNumber
        osArchitecture       = $os.OSArchitecture
        biosVendor           = $bios.Manufacturer
        biosVersion          = $bios.SMBIOSBIOSVersion
        cpuName              = $cpu.Name
        cpuCores             = $cpu.NumberOfCores
        cpuLogicalProcessors = $cpu.NumberOfLogicalProcessors
        ramTotalGb           = [math]::Round($cs.TotalPhysicalMemory / 1GB, 2)
        ipAddress            = (Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
                               Where-Object { $_.IPAddress -notlike '169.254*' -and $_.IPAddress -ne '127.0.0.1' } |
                               Select-Object -First 1).IPAddress
        macAddress           = (Get-NetAdapter -Physical -ErrorAction SilentlyContinue |
                               Select-Object -First 1).MacAddress
        domain               = $cs.Domain
        agentVersion         = '1.0.0'
        uptimeSeconds        = $uptime
        collectedAtUtc       = (Get-Date).ToUniversalTime().ToString('o')
    }
    disks     = $disks
    software  = $software
    telemetry = @{
        collectedAtUtc  = (Get-Date).ToUniversalTime().ToString('o')
        status          = 'Online'
        cpuUsagePercent = [math]::Round($cpuPct, 2)
        ramUsedGb       = $usedRam
        uptimeSeconds   = $uptime
        processCount    = (Get-Process).Count
        offlineQueueCount = 0
    }
    agentRun = @{
        startedAtUtc    = (Get-Date).ToUniversalTime().ToString('o')
        status          = 'Success'
        agentVersion    = '1.0.0'
        collectorsRun   = 'hardware,disks,software,telemetry'
        itemsSent       = 1 + $disks.Count + $software.Count
        triggerSource   = 'schedule'
    }
    isFullSoftwareInventory = $true
} | ConvertTo-Json -Depth 6

try {
    $resp = Invoke-RestMethod -Uri $Server -Method Post -Body $body -ContentType 'application/json; charset=utf-8' `
             -Headers @{ 'X-Agent-Key' = $ApiKey } -TimeoutSec 60
    Write-Host "OK - $($resp.hostname) ($($resp.status)) dalam $($resp.processedMs) ms"
}
catch {
    Write-Error "Gagal kirim data: $($_.Exception.Message)"
    exit 1
}
""";

    public const string TaskScheduler = """
# Daftarkan collector agar jalan tiap 6 jam (jalankan PowerShell sebagai Administrator)
$action  = New-ScheduledTaskAction -Execute 'powershell.exe' `
             -Argument '-NoProfile -ExecutionPolicy Bypass -File "C:\DeviceMonitor\collect.ps1"'
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) -RepetitionInterval (New-TimeSpan -Hours 6)
$principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -RunLevel Highest -LogonType ServiceAccount
Register-ScheduledTask -TaskName 'DeviceMonitor Collector' -Action $action -Trigger $trigger `
  -Principal $principal -Description 'Kirim inventaris & telemetry ke dashboard IT' -Force
""";

    public static string WithBase(string template, string baseUrl) => template.Replace("%BASE%", baseUrl);
}
