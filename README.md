# DayZ Mod Classic

The original 2012 DayZ Mod (v1.6), revived on modern Arma 2: Operation Arrowhead.

**Website:** [dayzmodclassic.com](https://dayzmodclassic.com)
**Discord:** [discord.gg/rgGpjayRMv](https://discord.gg/rgGpjayRMv)

---

## For players

Download the client installer from [dayzmodclassic.com/downloads](https://dayzmodclassic.com/downloads). It bundles the mod, the Dwarden BattlEye Win11 24H2 fix, and the launcher app. Requires Arma 2 + Arma 2: Operation Arrowhead from Steam.

## For server hosts

Download the server bundle from [dayzmodclassic.com/downloads](https://dayzmodclassic.com/downloads). Run `INSTALL_SERVER.ps1` as Administrator on a Windows Server 2016+ box. See `server/INSTALL.md`.

To get your server on the official launcher list, open an issue here with your server details or ping #server-list in Discord.

## What's in this repo

```
launcher/    C# WinForms .NET 8 launcher source
installer/   Inno Setup .iss for the client installer
server/      Server config templates, install script, mission, schema
website/     dayzmodclassic.com static site assets
```

Binary artifacts (PBOs, compiled installers, MySQL bundle) are attached to GitHub releases, not stored in git.

## Building from source

See `launcher/BUILD.md` and `installer/BUILD.md`.

Short version:
```powershell
# Launcher
cd launcher/src/DayZModClassic.Launcher
dotnet publish -c Release -r win-x64 --self-contained true

# Installer (requires Inno Setup 6 + payload binaries staged separately)
cd installer
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" DayZModClassic.iss
```

## Credits

- **Dean "Rocket" Hall** + the original DayZ Mod community for v1.6 (2012)
- **Dwarden** for the Arma 2 OA BattlEye Win11 24H2 compatibility fix
- **pwnoz0r** for early server install scaffolding

## License

See `LICENSE`. Mod content (PBOs) retains its original Bohemia Interactive ARMA Public License (APL). This repository's wrapper code (launcher, installer scripts, install automation) is MIT licensed.
