# Installer: retired

The Inno Setup installer flow (`DayZModClassic.iss`) was retired in launcher 1.1.0.
The launcher now downloads and installs the mod itself from a per-file SHA-256
manifest, and self-updates. There is no client installer anymore.

What remains in this folder:

- `payload\` is still the **canonical mod payload source**. `tools\New-Release.ps1`
  reads it to generate the content-addressed download blobs and `manifest.json`
  that the launcher consumes. Update payload files here, then run the release script.

See:

- `..\tools\New-Release.ps1` for the release flow (blobs + manifest + launcher publish)
- `..\launcher\BUILD.md` for building the launcher
- git history for the old `DayZModClassic.iss` if it is ever needed again
