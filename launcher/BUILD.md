# DayZ Mod Classic Launcher: build instructions

Single-file, self-contained Windows x64 launcher. Built with .NET 8 WinForms.

Since 1.1.0 the launcher is also the installer and updater: it downloads the
mod from `https://dayzmodclassic.com/downloads/manifest.json` (per-file
SHA-256, only changed files on patches) and self-updates from `version.json`.

## Prerequisites

1. Install the .NET 8 SDK (8.0.x or later) from <https://dotnet.microsoft.com/download/dotnet/8.0>.
2. Verify on the command line:

   ```powershell
   dotnet --version
   ```

   Expect output like `8.0.xxx`.

## Build (debug, fast iteration)

From the launcher folder:

```powershell
cd C:\WorkDrive\dayz-mod-classic\launcher\src\DayZModClassic.Launcher
dotnet build -c Debug
```

The dev binary will be at:

```
bin\Debug\net8.0-windows\DayZModClassic.exe
```

This requires the .NET 8 runtime installed on the machine.

## Publish (release, self-contained single file)

This is the artifact you ship to end users. It bundles the .NET 8 runtime so no separate install is needed.

```powershell
cd C:\WorkDrive\dayz-mod-classic\launcher\src\DayZModClassic.Launcher
dotnet publish -c Release -r win-x64 --self-contained true
```

Final artifact:

```
bin\Release\net8.0-windows\win-x64\publish\DayZModClassic.exe
```

Expected size: roughly 69 MB (single file, self-contained WinForms + .NET runtime, compression on).

Normally you do not publish by hand: `..\..\tools\New-Release.ps1 -ModVersion X -LauncherVersion Y`
publishes, stages the exe into `website\downloads\`, and rewrites `version.json`.

## Versioning

Bump THREE places together (New-Release.ps1 asserts they match):

1. `DayZModClassic.Launcher.csproj` `<Version>` (+ FileVersion/AssemblyVersion)
2. `AppInfo.cs` `Version` constant
3. `-LauncherVersion` argument to New-Release.ps1

`version.json` semantics: `latest` newer than the running launcher = optional
update offer (Help menu); `minRequired` newer = forced update before play.

## Smoke test after publish

1. Copy `DayZModClassic.exe` to a clean test machine (or your dev machine).
2. Double-click it. The launcher window should open with the banner header and dark theme.
3. Health panel should detect Steam, Arma 2 OA, mod folder, and BE fix presence.
4. With no mod installed the big button reads INSTALL and downloads ~206 MB; with stale files it reads UPDATE and downloads only changed files.
5. Server list should fetch from <https://dayzmodclassic.com/servers.json> (or fall back to bundled list).
6. Fill in a player name, select the Official VPS row, click PLAY. Arma 2 OA should launch and connect.

For testing the update pipeline against a local fake site, stage one with
`tools\New-Release.ps1 -WebsiteDir C:\temp\dzc-site`, serve it
(`python -m http.server 8080`), and set `versionUrl`/`manifestUrl` in
`%APPDATA%\DayZModClassic\config.json` to `http://localhost:8080/...`.

## Key source map

- `ModInstaller.cs` two-phase install (download+verify to `<A2OA>\.dzc-staging`, then atomic moves)
- `ManifestService.cs` / `UpdateModels.cs` manifest + version.json fetch and models
- `HashCache.cs` `%APPDATA%\DayZModClassic\hashcache.json`, avoids rehashing the install every start
- `SelfUpdater.cs` rename-swap self-update (`--apply-update` / `--waitpid` / `--updated` args)
- `Ui\Theme.cs` dark palette (mirrors website CSS vars), `Ui\HeaderPanel.cs` banner, `Ui\FlatProgressBar.cs`
- `ShortcutService.cs` one-time desktop shortcut offer

## Notes

- The exe needs no installer. Drop-and-run.
- Config and launch log live at `%APPDATA%\DayZModClassic\`; launcher logs at `%LOCALAPPDATA%\DayZ Mod Classic\logs\`.
- The launcher reads Steam install via `HKCU\SOFTWARE\Valve\Steam` and parses `libraryfolders.vdf` to find Arma 2 OA (`appmanifest_33930.acf`). If A2OA is on a non-default Steam library, this will still find it.
- The launch path writes `steam_appid.txt` to the OA folder, ensures `Steam.exe -silent` is running, then starts `ArmA2OA_BE.exe` with the mod chain `A2Base;EXPANSION;CA;@dayzmodclassic`.

## Clean

```powershell
cd C:\WorkDrive\dayz-mod-classic\launcher\src\DayZModClassic.Launcher
dotnet clean
Remove-Item -Recurse -Force bin, obj -ErrorAction SilentlyContinue
```
