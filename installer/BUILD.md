# DayZ Mod Classic 1.0.0 - Build Installer

Two-step build. First the launcher .exe, then the Inno Setup .exe.

## Prereqs

- .NET 8 SDK: `winget install Microsoft.DotNet.SDK.8`
- Inno Setup 6+: `winget install JRSoftware.InnoSetup`

## Step 1 - Build the launcher

```powershell
cd D:\arma2dayzmod\releases\1.0.0\launcher\src\DayZModClassic.Launcher
dotnet publish -c Release -r win-x64 --self-contained true
```

Output goes to `bin\Release\net8.0-windows\win-x64\publish\DayZModClassic.exe`.
The .iss script references that exact path.

## Step 2 - Compile the installer

```powershell
cd D:\arma2dayzmod\releases\1.0.0\installer
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" DayZModClassic.iss
```

Output: `Output\DayZModClassic-Client-1.0.0-Setup-v3.exe` (~200 MB).

## What the installer does

1. Detects Arma 2 OA via Steam library scan (registry + libraryfolders.vdf)
2. Fails fast if A2OA not installed (clear message: install via Steam)
3. Warns if Arma 2 base not installed (still proceeds)
4. Backs up existing BattlEye files to `<A2OA>\_be-fix-backup-<timestamp>\`
5. Installs Dwarden BE Win11 24H2 fix over the originals
6. Copies `@dayzmodclassic\` into `<A2OA>\@dayzmodclassic\`
7. Drops `dayz.bikey` + `that1drifter.bikey` into `<A2OA>\Keys\`
8. Installs launcher to `%LOCALAPPDATA%\DayZ Mod Classic\`
9. Writes `steam_appid.txt` into A2OA root (required for Steam identity)
10. Seeds `%APPDATA%\DayZModClassic\config.json` with detected paths
11. Creates Start Menu group + optional desktop shortcut

## After build

- Compute SHA-256: `Get-FileHash Output\DayZModClassic-Client-1.0.0-Setup-v3.exe -Algorithm SHA256`
- Update `..\website\downloads\index.html` with the hash
- Upload to dayzmodclassic.com/downloads/

## SmartScreen note

The .exe is unsigned. First ~50-100 installs trigger Windows SmartScreen warning.
README and dayzmodclassic.com/downloads cover the "More info > Run anyway" path.

## Uninstaller behavior

- Removes launcher from `%LOCALAPPDATA%\DayZ Mod Classic\`
- Removes `%APPDATA%\DayZModClassic\` config
- Does NOT remove `@dayzmodclassic\` or BE fix from A2OA (kept so the user can manually verify or reinstall without re-downloading 200 MB)
- TODO: optional "remove mod files too?" prompt in uninstaller (Pascal `[Code]`)
