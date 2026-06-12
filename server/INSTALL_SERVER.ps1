# =============================================================================
# DayZ Mod Classic 1.0.0 - Automated Server Installer
# =============================================================================
# Run as Administrator from the bundle directory. Does:
#   1. Verify Arma 2 OA installed (Steam library scan)
#   2. Copy bundle into A2OA install root
#   3. Open UDP 2302-2305 in Windows Firewall
#   4. Boot MySQL, apply schema, leave running
#   5. Register Arma server as Scheduled Task with auto-restart on crash
#   6. Prompt for admin password override + MOTD edit
# =============================================================================

#Requires -RunAsAdministrator

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"
$BundleRoot = $PSScriptRoot

Write-Host ""
Write-Host "==========================================================="
Write-Host "  DayZ Mod Classic 1.0.0 - Automated Server Installer"
Write-Host "==========================================================="
Write-Host ""

# --- 1. Find Arma 2 OA install --------------------------------------------------

Write-Host "[1/6] Locating Arma 2: Operation Arrowhead install..."

function Find-A2OA {
    # Search order: explicit common VPS path -> Steam library scan
    $explicitCandidates = @("C:\arma2oa", "C:\arma2", "C:\Program Files\Arma 2 Operation Arrowhead")
    foreach ($c in $explicitCandidates) {
        if (Test-Path (Join-Path $c "arma2oaserver.exe")) { return $c }
    }

    # Steam library scan (for player boxes with normal Steam install)
    $steamReg = Get-ItemProperty "HKCU:\SOFTWARE\Valve\Steam" -ErrorAction SilentlyContinue
    if (-not $steamReg) { return $null }
    $steamPath = $steamReg.SteamPath
    $vdf = Join-Path $steamPath "steamapps\libraryfolders.vdf"
    if (-not (Test-Path $vdf)) { return $null }
    $libs = @($steamPath)
    Get-Content $vdf | ForEach-Object {
        if ($_ -match '"path"\s+"([^"]+)"') {
            $libs += ($matches[1] -replace '\\\\','\')
        }
    }
    foreach ($lib in $libs) {
        $candidate = Join-Path $lib "steamapps\common\Arma 2 Operation Arrowhead"
        if (Test-Path (Join-Path $candidate "arma2oaserver.exe")) { return $candidate }
    }
    return $null
}

$A2OA = Find-A2OA

if (-not $A2OA) {
    Write-Host "ERROR: Arma 2: Operation Arrowhead not found. Install via Steam first." -ForegroundColor Red
    exit 1
}
Write-Host "    Found: $A2OA"

# --- 2. Copy bundle into A2OA root ----------------------------------------------

Write-Host "[2/6] Copying bundle into A2OA install root..."

$copyItems = @(
    "@dayzmodclassic", "@hive", "Keys", "MPMissions", "MySQL", "cfgdayz",
    "libmysql.dll", "mysqlcppconn.dll", "schema-patch.sql",
    "START_MYSQL.bat", "APPLY_SCHEMA.bat", "START_SERVER.bat"
)
foreach ($item in $copyItems) {
    $src = Join-Path $BundleRoot $item
    if (-not (Test-Path $src)) {
        Write-Host "    WARN: $item missing from bundle, skipping" -ForegroundColor Yellow
        continue
    }
    $dst = Join-Path $A2OA $item
    Write-Host "    -> $item"
    if ((Get-Item $src).PSIsContainer) {
        robocopy $src $dst /E /NFL /NDL /NJH /NJS /NP | Out-Null
    } else {
        Copy-Item $src $dst -Force
    }
}

# HiveExt.ini is gitignored (carries the MySQL password); seed it from the
# template on a fresh checkout so the server has a working config.
$hiveIni = Join-Path $A2OA "cfgdayz\HiveExt.ini"
$hiveExample = Join-Path $A2OA "cfgdayz\HiveExt.ini.example"
if (-not (Test-Path $hiveIni) -and (Test-Path $hiveExample)) {
    Copy-Item $hiveExample $hiveIni -Force
    Write-Host "    Seeded cfgdayz\HiveExt.ini from template (edit the Password before launch)." -ForegroundColor Yellow
}

# --- 3. Firewall --------------------------------------------------------------

Write-Host "[3/6] Configuring Windows Firewall..."
$ruleName = "DayZ Mod Classic UDP 2302-2305"
try { Remove-NetFirewallRule -DisplayName $ruleName -ErrorAction Stop } catch {}
New-NetFirewallRule -DisplayName $ruleName `
    -Direction Inbound -Protocol UDP -LocalPort 2302-2305 -Action Allow | Out-Null
Write-Host "    UDP 2302-2305 inbound allowed."

# --- 4. MySQL + schema --------------------------------------------------------

Write-Host "[4/6] Booting MySQL + applying schema..."
$mysqlBin = Join-Path $A2OA "MySQL\bin\mysqld.exe"
if (-not (Test-Path $mysqlBin)) {
    Write-Host "    ERROR: MySQL binary missing at $mysqlBin" -ForegroundColor Red
    exit 1
}

# Start MySQL in background (will detach via START_MYSQL.bat)
Start-Process -FilePath (Join-Path $A2OA "START_MYSQL.bat") -WorkingDirectory $A2OA -WindowStyle Minimized
Start-Sleep -Seconds 6

# Apply schema
$mysqlClient = Join-Path $A2OA "MySQL\bin\mysql.exe"
$schemaSql = Join-Path $A2OA "schema-patch.sql"
& $mysqlClient -h 127.0.0.1 -P 3316 -u root -proot hivemind -e "source $schemaSql" 2>&1 | Out-Null
Write-Host "    Schema patch applied (DROP INDEX errors on first run are harmless)."

# --- 5. Scheduled Task with auto-restart --------------------------------------

Write-Host "[5/6] Registering Arma server as Scheduled Task..."
$taskName = "DayZModClassic-Server"
try { Unregister-ScheduledTask -TaskName $taskName -Confirm:$false -ErrorAction Stop } catch {}

$action = New-ScheduledTaskAction -Execute (Join-Path $A2OA "START_SERVER.bat") -WorkingDirectory $A2OA
$trigger = New-ScheduledTaskTrigger -AtStartup
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet `
    -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries `
    -RestartCount 99 -RestartInterval (New-TimeSpan -Minutes 1) `
    -ExecutionTimeLimit (New-TimeSpan -Days 365)

Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger `
    -Principal $principal -Settings $settings -Description "DayZ Mod Classic 1.0 dedicated server" | Out-Null
Write-Host "    Task '$taskName' registered (boots at startup, restarts on crash)."

# --- 6. Interactive config polish --------------------------------------------

Write-Host "[6/6] Final config..."
$serverCfg = Join-Path $A2OA "cfgdayz\server.cfg"
$cfgContent = Get-Content $serverCfg -Raw

$adminPw = Read-Host -Prompt "    Set admin password (blank to keep current)"
if ($adminPw) {
    $cfgContent = $cfgContent -replace 'passwordAdmin\s*=\s*"[^"]*"', "passwordAdmin = `"$adminPw`""
    Set-Content $serverCfg -Value $cfgContent -NoNewline
    Write-Host "    Admin password updated."
}

$hostName = Read-Host -Prompt "    Server hostname (blank to keep 'DayZ Mod Classic 1.0 [Official]')"
if ($hostName) {
    $cfgContent = $cfgContent -replace 'hostname\s*=\s*"[^"]*"', "hostname = `"$hostName`""
    Set-Content $serverCfg -Value $cfgContent -NoNewline
    Write-Host "    Hostname updated."
}

Write-Host ""
Write-Host "==========================================================="
Write-Host "  Install complete."
Write-Host ""
Write-Host "  Next steps:"
Write-Host "    1. Start the server now:  Start-ScheduledTask -TaskName '$taskName'"
Write-Host "    2. Or just reboot - it autostarts."
Write-Host "    3. Verify with launcher from a client box."
Write-Host ""
Write-Host "  Server logs:"
Write-Host "    $A2OA\cfgdayz\server_console.log"
Write-Host "    `$env:LOCALAPPDATA\ArmA 2 OA\arma2oaserver.RPT"
Write-Host ""
Write-Host "  To get on the official server list: see INSTALL.md section 'Get listed'."
Write-Host "==========================================================="
