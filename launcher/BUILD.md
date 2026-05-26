# DayZ Mod Classic Launcher: build instructions

Single-file, self-contained Windows x64 launcher. Built with .NET 8 WinForms.

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
cd D:\arma2dayzmod\releases\1.0.0\launcher\src\DayZModClassic.Launcher
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
cd D:\arma2dayzmod\releases\1.0.0\launcher\src\DayZModClassic.Launcher
dotnet publish -c Release -r win-x64 --self-contained true
```

Final artifact:

```
bin\Release\net8.0-windows\win-x64\publish\DayZModClassic.exe
```

Expected size: roughly 10 to 15 MB compressed (single file, includes WinForms + runtime).

## Smoke test after publish

1. Copy `DayZModClassic.exe` to a clean test machine (or your dev machine).
2. Double-click it. The launcher window should open with title "DayZ Mod Classic 1.0.0".
3. Health panel should detect Steam, Arma 2 OA, mod folder, and BE fix presence.
4. Server list should fetch from <https://dayzmodclassic.com/servers.json> (or fall back to bundled list).
5. Fill in a player name, select the Official VPS row, click PLAY. Arma 2 OA should launch and connect.

## Adding an icon (optional, post-build)

1. Place a `.ico` file at `src\DayZModClassic.Launcher\app.ico`.
2. Add to the csproj `<PropertyGroup>`:

   ```xml
   <ApplicationIcon>app.ico</ApplicationIcon>
   ```

3. Rebuild.

## Notes

- The exe needs no installer. Drop-and-run.
- Config and launch log live at `%APPDATA%\DayZModClassic\`.
- The launcher reads Steam install via `HKCU\SOFTWARE\Valve\Steam` and parses `libraryfolders.vdf` to find Arma 2 OA (`appmanifest_33930.acf`). If A2OA is on a non-default Steam library, this will still find it.
- The launch path mirrors `D:\arma2dayzmod\CONNECT.ps1`: writes `steam_appid.txt` to the OA folder, ensures `Steam.exe -silent` is running, then starts `ArmA2OA_BE.exe` with the mod chain `A2Base;EXPANSION;CA;@dayzmodclassic`.

## Clean

```powershell
cd D:\arma2dayzmod\releases\1.0.0\launcher\src\DayZModClassic.Launcher
dotnet clean
Remove-Item -Recurse -Force bin, obj -ErrorAction SilentlyContinue
```
