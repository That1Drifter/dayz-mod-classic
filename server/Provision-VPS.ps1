# =============================================================================
# DayZ Mod Classic 1.0.0 - VPS Provisioning (one-shot prereq)
# =============================================================================
# Run on a fresh Windows Server (2016+) BEFORE INSTALL_SERVER.ps1.
# Installs SteamCMD and Arma 2 + Arma 2: Operation Arrowhead (apps 33900 + 33905)
# via a logged-in Steam account that owns both titles.
#
# After this completes, run INSTALL_SERVER.ps1 from the unpacked server
# bundle to deploy the mod + configs + scheduled task.
# =============================================================================

#Requires -RunAsAdministrator

param(
    [Parameter(Mandatory=$true)] [string]$SteamUser,
    [Parameter(Mandatory=$true)] [string]$SteamPassword,
    [string]$SteamGuardCode = ""
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Configurable paths
$SteamCmdRoot = "C:\steamcmd"
$A2OAServerRoot = "C:\arma2oa"
$SteamCmdZipUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip"

# App IDs (per dayz-classic-test-server-files/steamcmd-server-setup.md)
$AppA2 = 33900       # Arma 2 (Chernarus assets)
$AppA2OA = 33905     # Arma 2: Operation Arrowhead

Write-Host ""
Write-Host "==========================================================="
Write-Host "  DayZ Mod Classic - VPS Provisioning (A2OA Server)"
Write-Host "==========================================================="
Write-Host ""

# --- 1. SteamCMD --------------------------------------------------------------

Write-Host "[1/4] Installing SteamCMD..."
if (-not (Test-Path "$SteamCmdRoot\steamcmd.exe")) {
    New-Item -ItemType Directory -Path $SteamCmdRoot -Force | Out-Null
    $zipPath = Join-Path $SteamCmdRoot "steamcmd.zip"
    Write-Host "    Downloading from $SteamCmdZipUrl..."
    Invoke-WebRequest -Uri $SteamCmdZipUrl -OutFile $zipPath -UseBasicParsing
    Expand-Archive -Path $zipPath -DestinationPath $SteamCmdRoot -Force
    Remove-Item $zipPath
    Write-Host "    SteamCMD installed at $SteamCmdRoot"
} else {
    Write-Host "    SteamCMD already present at $SteamCmdRoot"
}

# --- 2. Run SteamCMD to self-update -------------------------------------------

Write-Host "[2/4] Updating SteamCMD..."
& "$SteamCmdRoot\steamcmd.exe" +quit | Out-Null
Write-Host "    SteamCMD updated."

# --- 3. Install Arma 2 base + Arma 2 OA (apps 33900 + 33905) -----------------

Write-Host "[3/4] Installing Arma 2 base ($AppA2) + Arma 2 OA ($AppA2OA)..."
Write-Host "    Combined download ~8 GB. Initial run takes 15-60 minutes."

if (-not (Test-Path $A2OAServerRoot)) {
    New-Item -ItemType Directory -Path $A2OAServerRoot -Force | Out-Null
}

# Build login string with optional Steam Guard code
$loginArg = "+login $SteamUser $SteamPassword"
if ($SteamGuardCode) { $loginArg += " $SteamGuardCode" }

# Run twice to absorb the self-update interrupt quirk
foreach ($pass in 1..2) {
    Write-Host "    Pass $pass of 2 (self-update absorber)..."
    $steamCmdArgs = @(
        "+force_install_dir `"$A2OAServerRoot`"",
        $loginArg,
        "+app_update $AppA2 validate",
        "+app_update $AppA2OA validate",
        "+quit"
    ) -join " "
    Start-Process -FilePath "$SteamCmdRoot\steamcmd.exe" -ArgumentList $steamCmdArgs -NoNewWindow -Wait
}

# --- 4. Verify ----------------------------------------------------------------

Write-Host "[4/4] Verifying install..."
$serverExe = Join-Path $A2OAServerRoot "arma2oaserver.exe"
if (-not (Test-Path $serverExe)) {
    Write-Host "    ERROR: arma2oaserver.exe not found at $serverExe" -ForegroundColor Red
    Write-Host "    SteamCMD may have failed. Check above output for errors." -ForegroundColor Red
    exit 1
}

$size = [math]::Round((Get-Item $serverExe).Length / 1MB, 1)
Write-Host "    arma2oaserver.exe present ($size MB)"

# Check for Chernarus + CA assets (A2 base) — critical for DayZ mission
$chernarusPbo = Get-ChildItem -Path $A2OAServerRoot -Filter "chernarus.pbo" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
$caPbo = Get-ChildItem -Path $A2OAServerRoot -Filter "ca.pbo" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1

if ($chernarusPbo) {
    Write-Host "    Chernarus assets present: $($chernarusPbo.FullName)"
} else {
    Write-Host "    WARN: chernarus.pbo NOT found anywhere under $A2OAServerRoot" -ForegroundColor Yellow
    Write-Host "    DayZ mission will fail to load Chernarus. Verify app 33900 installed correctly." -ForegroundColor Yellow
}

if ($caPbo) {
    Write-Host "    CA assets present: $($caPbo.FullName)"
}

# A2 base may be installed to a sibling Common dir by SteamCMD
$siblingA2 = "C:\arma2"
if (Test-Path "$siblingA2\AddOns\chernarus.pbo") {
    Write-Host "    A2 base detected at sibling path: $siblingA2"
}

Write-Host ""
Write-Host "==========================================================="
Write-Host "  Provisioning complete."
Write-Host ""
Write-Host "  A2OA Server installed to:"
Write-Host "    $A2OAServerRoot"
Write-Host ""
Write-Host "  Next step: extract the DayZ Mod Classic server bundle"
Write-Host "  into the same directory, then run INSTALL_SERVER.ps1"
Write-Host "==========================================================="
