#Requires -Version 5.1
<#
.SYNOPSIS
    Buat repo GitHub baru (private) + push seluruh proyek DeviceMonitor.
.DESCRIPTION
    Cukup siapkan Personal Access Token (classic: scope `repo`, atau fine-grained:
    Contents = Read & write + Administration = Read & write bila repo perlu dibuat).
    Token TIDAK disimpan: hanya dipakai di perintah push, lalu remote dibersihkan
    kembali ke URL tanpa token.
.EXAMPLE
    .\push-to-github.ps1 -Token ghp_xxxx
    .\push-to-github.ps1            # token dibaca dari env GITHUB_TOKEN atau ditanyakan
#>
param(
    [string]$Token = $env:GITHUB_TOKEN,
    [string]$RepoName = 'DeviceMonitor',
    [string]$Description = 'Dashboard monitoring perangkat kantor - ASP.NET Core 8 + SQL Server',
    [switch]$Public
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')

if (-not $Token) {
    $Token = Read-Host 'Personal Access Token GitHub' -AsSecureString
    $Token = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Token))
}

$headers = @{
    Authorization = "Bearer $Token"
    Accept        = 'application/vnd.github+json'
    'User-Agent'  = 'devicemonitor-push'
}

Write-Host 'Mengambil username GitHub...'
$me = Invoke-RestMethod -Uri 'https://api.github.com/user' -Headers $headers
Write-Host "  user: $($me.login)"

$repoUrl = "https://api.github.com/repos/$($me.login)/$RepoName"
$repo = $null
try { $repo = Invoke-RestMethod -Uri $repoUrl -Headers $headers } catch { $repo = $null }

if (-not $repo) {
    Write-Host "Membuat repo $($me.login)/$RepoName ..."
    $body = @{ name = $RepoName; private = (-not $Public.IsPresent); description = $Description; auto_init = $false }
    $repo = Invoke-RestMethod -Uri 'https://api.github.com/user/repos' -Method Post `
        -Headers $headers -Body ($body | ConvertTo-Json) -ContentType 'application/json'
}
else {
    Write-Host "Repo $($me.login)/$RepoName sudah ada, push ke repo tersebut."
}

Push-Location $root
try {
    git remote remove origin 2>$null
    git remote add origin "https://x-access-token:$Token@github.com/$($me.login)/$RepoName.git"
    git push -u origin main
    if ($LASTEXITCODE -ne 0) { throw "git push gagal (exit $LASTEXITCODE)" }
    git remote set-url origin "https://github.com/$($me.login)/$RepoName.git"
}
finally {
    Pop-Location
}

Write-Host @"

SELESAI  ->  https://github.com/$($me.login)/$RepoName
Remote sudah dibersihkan dari token. Bila token sempat masuk clipboard/history,
segera revoke di GitHub > Settings > Developer settings > Personal access tokens.
"@
