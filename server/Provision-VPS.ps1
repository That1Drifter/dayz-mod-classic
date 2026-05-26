# =============================================================================
# DayZ Mod Classic 1.0.0 - VPS Provisioning (one-shot prereq)
# =============================================================================
# Run on a fresh Windows Server (2016+) BEFORE INSTALL_SERVER.ps1.
# Installs SteamCMD and Arma 2: Operation Arrowhead Dedicated Server
# (Steam app 33935, anonymous-login free download).
#
# After this completes, run INSTALL_SERVER.ps1 from the unpacked server
# bundle to deploy the mod + configs + scheduled task.
# =============================================================================

#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Configurable paths
$SteamCmdRoot = "C:\steamcmd"
$A2OAServerRoot = "C:\Program Files (x86)\Steam\steamapps\common\Arma 2 Operation Arrowhead"
$SteamCmdZipUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip"

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

# --- 3. Install Arma 2 OA Dedicated Server (app 33935, anonymous) ------------

Write-Host "[3/4] Installing Arma 2: OA Dedicated Server (Steam app 33935)..."
Write-Host "    This downloads ~3 GB. Initial run takes 10-30 minutes."

if (-not (Test-Path $A2OAServerRoot)) {
    New-Item -ItemType Directory -Path $A2OAServerRoot -Force | Out-Null
}

$steamCmdArgs = @(
    "+force_install_dir `"$A2OAServerRoot`"",
    "+login anonymous",
    "+app_update 33935 validate",
    "+quit"
) -join " "

Start-Process -FilePath "$SteamCmdRoot\steamcmd.exe" -ArgumentList $steamCmdArgs -NoNewWindow -Wait

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

# Check for Chernarus assets - critical for DayZ
$chernarusPbo = Join-Path $A2OAServerRoot "Addons\chernarus.pbo"
$cawPbo = Join-Path $A2OAServerRoot "Addons\ca.pbo"
if (Test-Path $chernarusPbo) {
    Write-Host "    Chernarus assets present (chernarus.pbo)"
} else {
    Write-Host "    WARN: chernarus.pbo NOT found. DayZ mission will fail to load." -ForegroundColor Yellow
    Write-Host "    The OA dedicated server may not include Chernarus assets." -ForegroundColor Yellow
    Write-Host "    Workaround options:" -ForegroundColor Yellow
    Write-Host "      a) Install Steam + Arma 2 base game (app 33900) via Steam GUI (RDP needed)" -ForegroundColor Yellow
    Write-Host "      b) Copy chernarus PBOs from a licensed install over SCP" -ForegroundColor Yellow
}

if (Test-Path $cawPbo) {
    Write-Host "    CA assets present (ca.pbo)"
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
