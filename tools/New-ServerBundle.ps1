<#
.SYNOPSIS
Assembles and zips a DayZ Mod Classic server bundle.

.DESCRIPTION
The deployable server bundle = heavy binaries (portable MySQL, the @dayzmodclassic
mod PBOs, @hive, the BattlEye fix) overlaid with the repo's current server\ tree
(mission, configs, scripts) so the published zip carries whatever the repo holds:
admin tools, the vehicle-fleet seeder, etc.

There is no source of the heavy binaries in git (they are 280+ MB), so you point
-GoldenDir at a previously assembled bundle to supply them. The repo server\ tree
always wins on overlap, so configs/mission stay source-of-truth from git.

Guards built in:
  * Asserts the golden mod PBOs + .bisign match installer\payload (the client set).
    A mismatch means clients would fail verifySignatures=2, so it throws.
  * Warns if the bundled hivemind DB is not empty (a populated object_data makes
    the vehicle seeder skip on first boot, and ships junk to new hosters).

Output: <OutDir>\server\ (the staged tree) and <OutDir>\DayZModClassic-Server-<Version>.zip
plus a .sha256 sidecar. Prints the scp deploy command and the downloads-page hash.

.EXAMPLE
.\New-ServerBundle.ps1 -Version 1.1.0 -GoldenDir C:\WorkDrive\arma2dayzmod\releases\1.0.0\server
.EXAMPLE
.\New-ServerBundle.ps1 -Version 1.1.0 -GoldenDir ..\..\arma2dayzmod\releases\1.0.0\server -OutDir D:\build\dzc-1.1.0
#>
#Requires -Version 7
param(
    [Parameter(Mandatory)] [string]$Version,
    [Parameter(Mandatory)] [string]$GoldenDir,
    [string]$RepoServerDir = (Join-Path $PSScriptRoot "..\server"),
    [string]$OutDir,
    [string]$WebHost = "administrator@85.239.231.196",
    [string]$WebDownloads = "C:/sites/dayzmodclassic.com/downloads/"
)

$ErrorActionPreference = 'Stop'
$RepoRoot      = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$GoldenDir     = (Resolve-Path $GoldenDir).Path
$RepoServerDir = (Resolve-Path $RepoServerDir).Path
if (-not $OutDir) { $OutDir = Join-Path $RepoRoot "..\dzc-server-build\$Version" }
$build = Join-Path $OutDir "server"
$zip   = Join-Path $OutDir "DayZModClassic-Server-$Version.zip"

function Invoke-Robocopy($src, $dst, $extra) {
    & robocopy $src $dst /E /NFL /NDL /NJH /NJS /R:1 /W:1 @extra | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy '$src' -> '$dst' failed ($LASTEXITCODE)" }
    $global:LASTEXITCODE = 0
}

# --- 1. Signature consistency: golden PBOs must equal the client payload --------
Write-Host "[1/5] Verifying mod PBO signatures match the client payload..." -ForegroundColor Cyan
$payloadAddons = Join-Path $RepoRoot "installer\payload\mod\@dayzmodclassic\AddOns"
$goldenAddons  = Join-Path $GoldenDir "@dayzmodclassic\AddOns"
if (-not (Test-Path $goldenAddons)) { throw "GoldenDir has no @dayzmodclassic\AddOns: $goldenAddons" }
$mismatch = @()
Get-ChildItem $payloadAddons -File | ForEach-Object {
    $g = Join-Path $goldenAddons $_.Name
    if (-not (Test-Path $g)) { $mismatch += "$($_.Name) (missing in golden)"; return }
    $ph = (Get-FileHash -Algorithm SHA256 $_.FullName).Hash
    $gh = (Get-FileHash -Algorithm SHA256 $g).Hash
    if ($ph -ne $gh) { $mismatch += "$($_.Name) (hash differs)" }
}
if ($mismatch.Count) {
    throw "Golden mod PBOs do not match installer\payload. Clients would fail verifySignatures=2:`n  " + ($mismatch -join "`n  ")
}
Write-Host "    all PBOs + .bisign match"

# --- 2. Stage: golden binaries base, then repo server\ overlay (repo wins) ------
Write-Host "[2/5] Staging build tree..." -ForegroundColor Cyan
New-Item -ItemType Directory -Force $build | Out-Null
Invoke-Robocopy $GoldenDir     $build @()
Invoke-Robocopy $RepoServerDir $build @()
# Mirror the mission so files removed in the repo do not linger from golden.
$repoMission  = Join-Path $RepoServerDir "MPMissions\dayz_1.Chernarus"
$buildMission = Join-Path $build "MPMissions\dayz_1.Chernarus"
if (Test-Path $repoMission) { Invoke-Robocopy $repoMission $buildMission @('/MIR') }
Write-Host ("    staged {0:N1} MB" -f ((Get-ChildItem $build -Recurse -File | Measure-Object Length -Sum).Sum / 1MB))

# --- 3. Sanitize HiveExt.ini from the tracked template -------------------------
Write-Host "[3/5] Writing sanitized HiveExt.ini..." -ForegroundColor Cyan
$example = Join-Path $build "cfgdayz\HiveExt.ini.example"
$live    = Join-Path $build "cfgdayz\HiveExt.ini"
if (-not (Test-Path $example)) { throw "Missing cfgdayz\HiveExt.ini.example in staged tree" }
Copy-Item $example $live -Force
Write-Host "    HiveExt.ini reset to the root/root localhost template"

# --- 4. Sanity: bundled DB should be empty so the vehicle seeder fires ---------
$objMyd = Get-ChildItem $build -Recurse -Filter "object_data.MYD" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($objMyd -and $objMyd.Length -gt 0) {
    Write-Warning "Bundled hivemind object_data.MYD is $($objMyd.Length) bytes (not empty). The vehicle seeder will SKIP on first boot and you are shipping leftover world objects. Ship a fresh DB."
}

# --- 5. Zip + hash -------------------------------------------------------------
Write-Host "[4/5] Zipping..." -ForegroundColor Cyan
if (Test-Path $zip) { Remove-Item $zip -Force }
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($build, $zip, [System.IO.Compression.CompressionLevel]::Optimal, $false)
$sha = (Get-FileHash -Algorithm SHA256 $zip).Hash.ToLowerInvariant()
Set-Content -Path "$zip.sha256" -Value $sha -Encoding ascii -NoNewline
Write-Host ("    {0}  ({1:N1} MB)" -f (Split-Path $zip -Leaf), ((Get-Item $zip).Length / 1MB))
Write-Host "    sha256 $sha"

Write-Host "[5/5] Done." -ForegroundColor Cyan
Write-Host ""
Write-Host "Update website\downloads\index.html hash block to (uppercase):" -ForegroundColor Yellow
Write-Host "    $($sha.ToUpperInvariant())"
Write-Host "Deploy:" -ForegroundColor Yellow
Write-Host "    scp `"$zip`" $WebHost`:$WebDownloads"
