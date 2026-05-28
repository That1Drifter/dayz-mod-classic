<#
.SYNOPSIS
  Installs the DayZ Mod Classic admin panel as an NSSM Windows service on the VPS.

.DESCRIPTION
  Run on the VPS as Administrator, from a folder containing the published output
  (DayZModClassic.Admin.exe + appsettings*.json). Mirrors how Caddy is hosted
  (NSSM service). Does NOT open any firewall port: the panel binds 127.0.0.1 only
  and is reached through Caddy.

  Publish locally first, then copy the publish folder to the VPS:
    dotnet publish admin/src/DayZModClassic.Admin -c Release -o publish
    scp -r publish administrator@85.239.231.196:C:/admin-panel-stage

.PARAMETER SourceDir   Folder with the published files (default: current dir).
.PARAMETER InstallDir  Where the service runs from (default: C:\admin-panel).
.PARAMETER ServiceName NSSM service name (default: DayZModClassic-Admin).
.PARAMETER Port        Loopback port the panel listens on (default: 8088).
#>
[CmdletBinding()]
param(
    [string]$SourceDir = (Get-Location).Path,
    [string]$InstallDir = 'C:\admin-panel',
    [string]$ServiceName = 'DayZModClassic-Admin',
    [int]$Port = 8088
)

$ErrorActionPreference = 'Stop'

function Require-Cmd($name) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "$name not found on PATH. Install it first (NSSM ships with the Caddy setup on this box)."
    }
}

Require-Cmd nssm

$exe = Join-Path $SourceDir 'DayZModClassic.Admin.exe'
if (-not (Test-Path $exe)) {
    throw "DayZModClassic.Admin.exe not found in $SourceDir. Publish first and run this from the publish folder."
}

Write-Host "Copying published files to $InstallDir ..."
New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
Copy-Item -Path (Join-Path $SourceDir '*') -Destination $InstallDir -Recurse -Force

$prodCfg = Join-Path $InstallDir 'appsettings.Production.json'
if (-not (Test-Path $prodCfg)) {
    Write-Warning "appsettings.Production.json not present in $InstallDir."
    Write-Warning "Copy appsettings.Production.json.example to appsettings.Production.json and fill in RCon password, hive connection string, and admin auth password BEFORE starting the service."
}

$installedExe = Join-Path $InstallDir 'DayZModClassic.Admin.exe'

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    Write-Host "Service $ServiceName exists - stopping and reconfiguring."
    nssm stop $ServiceName confirm | Out-Null
} else {
    Write-Host "Installing service $ServiceName ..."
    nssm install $ServiceName $installedExe
}

nssm set $ServiceName Application $installedExe
nssm set $ServiceName AppDirectory $InstallDir
nssm set $ServiceName AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production" "ASPNETCORE_URLS=http://127.0.0.1:$Port"
nssm set $ServiceName Start SERVICE_AUTO_START
nssm set $ServiceName AppStdout (Join-Path $InstallDir 'service-out.log')
nssm set $ServiceName AppStderr (Join-Path $InstallDir 'service-err.log')
nssm set $ServiceName AppRotateFiles 1
nssm set $ServiceName AppRotateBytes 10485760

if (Test-Path $prodCfg) {
    Write-Host "Starting $ServiceName ..."
    nssm start $ServiceName
    Start-Sleep -Seconds 2
    Get-Service $ServiceName | Format-Table -AutoSize
    Write-Host "Health check:"
    try { (Invoke-WebRequest "http://127.0.0.1:$Port/healthz" -UseBasicParsing).Content } catch { Write-Warning $_.Exception.Message }
} else {
    Write-Warning "Not starting service until appsettings.Production.json exists. Fill it in, then: nssm start $ServiceName"
}

Write-Host ""
Write-Host "Next: add admin/deploy/Caddyfile.admin.snippet to the Caddyfile, set the basicauth hash, reload Caddy."
