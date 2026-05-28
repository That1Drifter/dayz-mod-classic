# DayZ Mod Classic 1.0.0 - Release Build Tree

Build artifacts for the v1.0.0 public release. Generated on 2026-05-26.

## Tree

```
releases\1.0.0\
+- server\                  Server bundle source (286 MB unzipped, 140 MB zipped)
|  +- @dayzmodclassic\      Mod PBOs (renamed from @dayz)
|  +- @hive\                HiveEXT.dll
|  +- Keys\                 dayz.bikey + that1drifter.bikey
|  +- MPMissions\dayz_1.Chernarus\
|  +- MySQL\                Portable MySQL 5.5.24
|  +- cfgdayz\              server.cfg (BE=1, sigs=2, hostname "DayZ Mod Classic 1.0 [Official]")
|  +- INSTALL_SERVER.ps1    Automated installer
|  +- INSTALL.md            Manual install guide
|  +- CLAUDE.md             Server-box agent context
|  +- *.bat                 Launchers (renamed for 1.0.0)
|
+- DayZModClassic-Server-1.0.0.zip   140 MB - the deployable bundle
|
+- launcher\                C# WinForms launcher source (~1400 LOC)
|  +- src\DayZModClassic.Launcher\
|  +- BUILD.md              dotnet publish instructions
|
+- installer\               Inno Setup client installer
|  +- DayZModClassic.iss    Inno Setup script
|  +- BUILD.md              ISCC compile instructions
|  +- payload\              Staged files (180 MB)
|     +- be-fix\            Dwarden Win11 24H2 BattlEye fix (5 files)
|     +- mod\               @dayzmodclassic + dayz.bikey + that1drifter.bikey
|
+- website\                 dayzmodclassic.com assets (~12 KB)
|  +- downloads\
|  |  +- index.html         Player + hoster download page
|  |  +- CHANGELOG.md
|  |  +- install-troubleshooting.md
|  |  +- host-your-own.md
|  +- servers.json          Server list (launcher polls this)
|  +- version.json          Update manifest
|
+- client\                  (Reserved for built installer .exe output)
+- _workspace\              (Scratch)
+- RELEASE_NOTES.md         This file
```

## Build chain

Order matters: launcher first, then installer.

```powershell
# 1. Launcher
cd D:\arma2dayzmod\releases\1.0.0\launcher\src\DayZModClassic.Launcher
dotnet publish -c Release -r win-x64 --self-contained true

# 2. Client installer
cd D:\arma2dayzmod\releases\1.0.0\installer
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" DayZModClassic.iss
# -> Output\DayZModClassic-Client-1.0.0-Setup-v5.exe (~200 MB)

# 3. Server bundle (already done in this build)
# D:\arma2dayzmod\releases\1.0.0\DayZModClassic-Server-1.0.0.zip
```

## Tooling prereqs

- `winget install Microsoft.DotNet.SDK.8`
- `winget install JRSoftware.InnoSetup`

## Distribution checklist

- [ ] Compile launcher (.exe)
- [ ] Compile installer (.exe)
- [ ] Compute SHA-256 of both .exe + server .zip
- [ ] Update `website\downloads\index.html` with the 3 hashes
- [ ] Render `*.md` guides in `website\downloads\` to `.html`
- [ ] Replace `[TBD]` placeholders (GitHub URL, Discord invite)
- [ ] Deploy `website\` to dayzmodclassic.com docroot
- [ ] Upload installers to dayzmodclassic.com/downloads/
- [ ] Deploy server bundle to Contabo VPS (Phase 7)
- [ ] Verify launcher connects to VPS (Phase 8)
- [ ] Tag v1.0.0 + GitHub release (Phase 9)
- [ ] Discord announcement

## Versioning

- User-facing: `1.0.0` (installer, launcher, server hostname, README)
- Dev internal: `1.0.0-dev+v8.16` (the patch series baked into this build)
- Mod folder: `@dayzmodclassic` (renamed from `@dayz`)
- Mod chain on server: `-mod=@dayzmodclassic;@hive`
- Mod chain on client: `-mod=<A2Base>;EXPANSION;CA;@dayzmodclassic`

## Brand assets

- Hostname: `DayZ Mod Classic 1.0 [Official]`
- Domain: `dayzmodclassic.com`
- Launcher title: `DayZ Mod Classic 1.0.0`
- Installer title: `DayZ Mod Classic 1.0.0 Setup`

## Risk notes

1. **BE fix licensing**: Dwarden's Win11 24H2 fix is bundled in `installer\payload\be-fix\`. Originally Discord-only. Credit Dwarden in installer README. Worth a courtesy ping to Dwarden before public release.
2. **SmartScreen**: Both .exe outputs are unsigned. First 50-100 installs trigger Windows warning. README + dayzmodclassic.com/downloads cover the "More info > Run anyway" path.
3. **Signature verification**: Server runs `verifySignatures=2`. Client PBOs must match server's exactly. Installer guarantees this since the same `@dayzmodclassic\` ships on both sides.
4. **MySQL credentials**: `cfgdayz\HiveExt.ini` ships with `root/root`. Localhost-only by design. INSTALL_SERVER.ps1 should be extended later to randomize on install (TODO).
5. **The `@dayz` -> `@dayzmodclassic` rename**: dev box still runs `@dayz` for LAN testing. The 192.168.1.3 LAN server has not been rebuilt to use the new folder yet. Phase 7 dogfood happens on the Contabo VPS first; LAN box gets renamed only after public is stable.
