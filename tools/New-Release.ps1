<#
.SYNOPSIS
Stages a DayZ Mod Classic release: content-addressed mod blobs + manifest.json,
and optionally the launcher exe + version.json bump.

.DESCRIPTION
Reads the canonical payload (installer\payload), hashes every managed file,
copies new content to <website>\downloads\files\<sha256>.blob (idempotent,
dedupes across releases), and writes downloads\manifest.json plus an archived
manifest-<ver>.json.

With -LauncherVersion it also publishes the launcher (dotnet publish), stages
the exe at downloads\DayZModClassic.exe (+ versioned copy), and rewrites
version.json (latest, sha256, downloadUrl, released, manifestUrl, optionally
minRequired).

Deploy order matters: files\* first, then manifest.json, then the exe, then
version.json. The script prints the exact scp commands.

.EXAMPLE
.\New-Release.ps1 -ModVersion 1.0.0
.EXAMPLE
.\New-Release.ps1 -ModVersion 1.0.1 -LauncherVersion 1.1.0 -MinRequired 1.1.0
.EXAMPLE
.\New-Release.ps1 -ModVersion 1.0.0 -WebsiteDir C:\temp\dzc-site   # local test staging
#>
#Requires -Version 7
param(
    [Parameter(Mandatory)] [string]$ModVersion,
    [string]$LauncherVersion,
    [string]$MinRequired,
    [string]$PayloadDir = (Join-Path $PSScriptRoot "..\installer\payload"),
    [string]$WebsiteDir = (Join-Path $PSScriptRoot "..\website"),
    [switch]$Prune
)

$ErrorActionPreference = 'Stop'
$PayloadDir = (Resolve-Path $PayloadDir).Path
if (-not (Test-Path $WebsiteDir)) { New-Item -ItemType Directory -Force $WebsiteDir | Out-Null }
$WebsiteDir = (Resolve-Path $WebsiteDir).Path
$RepoRoot   = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

$downloadsDir = Join-Path $WebsiteDir "downloads"
$filesDir     = Join-Path $downloadsDir "files"
New-Item -ItemType Directory -Force $filesDir | Out-Null

$baseUrl = "https://dayzmodclassic.com/downloads/files/"

# --- 1. Destination mapping (mirrors the retired DayZModClassic.iss) ---------
# Each entry: source file under payload -> A2OA-root-relative install path.
function Get-PayloadMap {
    $map = @()
    $modRoot = Join-Path $PayloadDir "mod\@dayzmodclassic"
    Get-ChildItem $modRoot -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($modRoot.Length + 1) -replace '\\', '/'
        $map += [pscustomobject]@{ Source = $_.FullName; Path = "@dayzmodclassic/$rel"; Kind = "mod" }
    }
    Get-ChildItem (Join-Path $PayloadDir "mod") -File -Filter *.bikey | ForEach-Object {
        $map += [pscustomobject]@{ Source = $_.FullName; Path = "Keys/$($_.Name)"; Kind = "key" }
    }
    $map += [pscustomobject]@{ Source = (Join-Path $PayloadDir "be-fix\ArmA2OA_BE.exe"); Path = "ArmA2OA_BE.exe"; Kind = "befix" }
    Get-ChildItem (Join-Path $PayloadDir "be-fix\BattlEye") -File | ForEach-Object {
        $map += [pscustomobject]@{ Source = $_.FullName; Path = "BattlEye/$($_.Name)"; Kind = "befix" }
    }
    $map += [pscustomobject]@{ Source = (Join-Path $PayloadDir "steam_appid.txt"); Path = "steam_appid.txt"; Kind = "misc" }
    return $map
}

Write-Host "[1/5] Hashing payload..." -ForegroundColor Cyan
$entries = @()
foreach ($item in Get-PayloadMap) {
    if (-not (Test-Path $item.Source)) { throw "Payload file missing: $($item.Source)" }
    $hash = (Get-FileHash -Algorithm SHA256 -Path $item.Source).Hash.ToLowerInvariant()
    $size = (Get-Item $item.Source).Length
    $entries += [pscustomobject]@{
        path   = $item.Path
        kind   = $item.Kind
        size   = $size
        sha256 = $hash
        url    = "$hash.blob"
        source = $item.Source
    }
}
Write-Host ("    {0} files, {1:N1} MB total" -f $entries.Count, (($entries | Measure-Object size -Sum).Sum / 1MB))

# --- 2. Stage blobs (content-addressed, idempotent) ---------------------------
Write-Host "[2/5] Staging blobs..." -ForegroundColor Cyan
$newBlobs = 0; $newBytes = 0
foreach ($e in $entries) {
    $blob = Join-Path $filesDir "$($e.sha256).blob"
    if (-not (Test-Path $blob)) {
        Copy-Item $e.source $blob
        $newBlobs++; $newBytes += $e.size
    }
}
Write-Host ("    {0} new blob(s), {1:N1} MB (patch download size)" -f $newBlobs, ($newBytes / 1MB))

# --- 3. Manifest ---------------------------------------------------------------
Write-Host "[3/5] Writing manifest..." -ForegroundColor Cyan
$manifest = [ordered]@{
    schemaVersion = 1
    modVersion    = $ModVersion
    generated     = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    baseUrl       = $baseUrl
    files         = @($entries | Select-Object path, kind, size, sha256, url)
}
$manifestJson = $manifest | ConvertTo-Json -Depth 5
Set-Content -Path (Join-Path $downloadsDir "manifest.json") -Value $manifestJson -Encoding utf8NoBOM
Set-Content -Path (Join-Path $downloadsDir "manifest-$ModVersion.json") -Value $manifestJson -Encoding utf8NoBOM

# --- 4. Launcher publish + version.json ----------------------------------------
$exeSha = $null
if ($LauncherVersion) {
    Write-Host "[4/5] Publishing launcher $LauncherVersion..." -ForegroundColor Cyan
    $projDir  = Join-Path $RepoRoot "launcher\src\DayZModClassic.Launcher"
    $csproj   = Join-Path $projDir "DayZModClassic.Launcher.csproj"
    $appinfo  = Join-Path $projDir "AppInfo.cs"

    $csprojVer = ([xml](Get-Content $csproj)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
    if ($csprojVer -ne $LauncherVersion) {
        throw "csproj <Version> is '$csprojVer' but -LauncherVersion is '$LauncherVersion'. Bump the csproj (and AppInfo.cs) first."
    }
    if ((Get-Content $appinfo -Raw) -notmatch [regex]::Escape("`"$LauncherVersion`"")) {
        throw "AppInfo.cs Version constant does not match '$LauncherVersion'. Keep csproj and AppInfo.cs in sync."
    }

    dotnet publish $projDir -c Release -r win-x64 --self-contained true --nologo -v q
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

    $publishedExe = Join-Path $projDir "bin\Release\net8.0-windows\win-x64\publish\DayZModClassic.exe"
    Copy-Item $publishedExe (Join-Path $downloadsDir "DayZModClassic.exe") -Force
    Copy-Item $publishedExe (Join-Path $downloadsDir "DayZModClassic-$LauncherVersion.exe") -Force
    $exeSha = (Get-FileHash -Algorithm SHA256 -Path $publishedExe).Hash.ToLowerInvariant()
    Write-Host ("    exe staged, {0:N1} MB, sha256 {1}" -f ((Get-Item $publishedExe).Length / 1MB), $exeSha)
} else {
    Write-Host "[4/5] Skipping launcher publish (no -LauncherVersion)" -ForegroundColor DarkGray
}

$versionPath = Join-Path $WebsiteDir "version.json"
$version = if (Test-Path $versionPath) { Get-Content $versionPath -Raw | ConvertFrom-Json } else { [pscustomobject]@{} }
function Set-Prop($obj, $name, $value) {
    if ($obj.PSObject.Properties[$name]) { $obj.$name = $value }
    else { $obj | Add-Member -NotePropertyName $name -NotePropertyValue $value }
}
Set-Prop $version 'manifestUrl' "https://dayzmodclassic.com/downloads/manifest.json"
Set-Prop $version 'changelogUrl' "https://dayzmodclassic.com/downloads/CHANGELOG.md"
if ($LauncherVersion) {
    Set-Prop $version 'latest' $LauncherVersion
    Set-Prop $version 'released' (Get-Date -Format 'yyyy-MM-dd')
    Set-Prop $version 'downloadUrl' "https://dayzmodclassic.com/downloads/DayZModClassic.exe"
    Set-Prop $version 'sha256' $exeSha
}
if ($MinRequired) { Set-Prop $version 'minRequired' $MinRequired }
Set-Content -Path $versionPath -Value ($version | ConvertTo-Json) -Encoding utf8NoBOM
Write-Host "    version.json updated"

# --- 5. Prune + summary ---------------------------------------------------------
if ($Prune) {
    Write-Host "[5/5] Pruning unreferenced blobs..." -ForegroundColor Cyan
    $referenced = Get-ChildItem $downloadsDir -Filter "manifest*.json" |
        ForEach-Object { (Get-Content $_.FullName -Raw | ConvertFrom-Json).files.url } |
        Sort-Object -Unique
    $pruned = 0
    Get-ChildItem $filesDir -Filter *.blob | Where-Object { $referenced -notcontains $_.Name } | ForEach-Object {
        Remove-Item $_.FullName; $pruned++
    }
    Write-Host "    pruned $pruned blob(s)"
} else {
    Write-Host "[5/5] Prune skipped (pass -Prune to clean unreferenced blobs)" -ForegroundColor DarkGray
}

Write-Host ""
Write-Host "Deploy in THIS order:" -ForegroundColor Yellow
Write-Host "  scp -r `"$filesDir/*`" administrator@85.239.231.196:C:/sites/dayzmodclassic.com/downloads/files/"
Write-Host "  scp `"$downloadsDir/manifest.json`" `"$downloadsDir/manifest-$ModVersion.json`" administrator@85.239.231.196:C:/sites/dayzmodclassic.com/downloads/"
if ($LauncherVersion) {
    Write-Host "  scp `"$downloadsDir/DayZModClassic.exe`" `"$downloadsDir/DayZModClassic-$LauncherVersion.exe`" administrator@85.239.231.196:C:/sites/dayzmodclassic.com/downloads/"
}
Write-Host "  scp `"$versionPath`" administrator@85.239.231.196:C:/sites/dayzmodclassic.com/"
