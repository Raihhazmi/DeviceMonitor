#Requires -Version 5.1
<#
.SYNOPSIS
    Install DeviceMonitor Agent sebagai Windows Service atau Scheduled Task.
.PARAMETER Mode
    Service (default) atau ScheduledTask.
.EXAMPLE
    powershell -ExecutionPolicy Bypass -File install-agent.ps1 -ServerUrl http://srv-monitor:5000 -ApiKey XYZ
#>
param(
    [ValidateSet('Service', 'ScheduledTask')]
    [string]$Mode = 'Service',

    [Parameter(Mandatory = $true)]
    [string]$ServerUrl,

    [Parameter(Mandatory = $true)]
    [string]$ApiKey,

    [string]$InstallDir = 'C:\DeviceMonitor',
    [string]$SourceDir = '',          # kosong = publish otomatis dari repo
    [int]$IntervalMinutes = 360
)

$ErrorActionPreference = 'Stop'
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
        ).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Jalankan PowerShell sebagai Administrator.'
}

$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)   # DeviceMonitor/
if (-not $SourceDir) {
    Write-Host "Publish agent dari $repo ..."
    $SourceDir = Join-Path $env:TEMP 'dm-agent-publish'
    dotnet publish (Join-Path $repo 'agent\DeviceMonitor.Agent\DeviceMonitor.Agent.csproj') `
        -c Release -r win-x64 --self-contained false -o $SourceDir | Out-Null
}

Write-Host "Menyalin agent ke $InstallDir ..."
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item -Path (Join-Path $SourceDir '*') -Destination $InstallDir -Recurse -Force

$cfgPath = Join-Path $InstallDir 'appsettings.json'
$cfg = Get-Content $cfgPath -Raw | ConvertFrom-Json
$cfg.Agent.ServerUrl = $ServerUrl
$cfg.Agent.ApiKey = $ApiKey
$cfg.Agent.IntervalMinutes = $IntervalMinutes
$cfg | ConvertTo-Json -Depth 6 | Set-Content $cfgPath -Encoding UTF8

$exe = Join-Path $InstallDir 'DeviceMonitor.Agent.exe'

if ($Mode -eq 'Service') {
    $svc = Get-Service -Name 'DeviceMonitorAgent' -ErrorAction SilentlyContinue
    if ($svc) { Stop-Service DeviceMonitorAgent -Force -ErrorAction SilentlyContinue }
    if (-not $svc) {
        New-Service -Name 'DeviceMonitorAgent' -BinaryPathName $exe `
            -DisplayName 'DeviceMonitor Agent' `
            -Description 'Mengirim inventaris & telemetry perangkat ke dashboard IT' `
            -StartupType Automatic | Out-Null
    }
    Start-Service DeviceMonitorAgent
    Write-Host "Service DeviceMonitorAgent berjalan: $((Get-Service DeviceMonitorAgent).Status)"
}
else {
    $action = New-ScheduledTaskAction -Execute $exe -WorkingDirectory $InstallDir
    $trigger = New-ScheduledTaskTrigger -Once -At (Get-Date) `
        -RepetitionInterval (New-TimeSpan -Minutes $IntervalMinutes)
    $principal = New-ScheduledTaskPrincipal -UserId 'SYSTEM' -RunLevel Highest -LogonType ServiceAccount
    Register-ScheduledTask -TaskName 'DeviceMonitor Agent' -Action $action -Trigger $trigger `
        -Principal $principal -Force | Out-Null
    Start-ScheduledTask -TaskName 'DeviceMonitor Agent'
    Write-Host 'Scheduled task DeviceMonitor Agent terdaftar & dijalankan.'
}

Write-Host @"

Selesai. Verifikasi:
  - Dashboard: halaman 04 Agent Health (log run muncul dalam ±1 menit)
  - Antrian offline: %ProgramData%\DeviceMonitor\queue
"@
