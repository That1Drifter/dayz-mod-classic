# DayZ Mod Classic, Changelog

## 1.1.0, 2026-06-19

### Changed
- The launcher now installs and updates the mod itself. The standalone installer is retired: one download, and the launcher handles first-time setup, signature-correct PBOs, and every future update.

### Added
- In-game admin menu plus a web admin panel with a live player map (teleport, vehicle picker, Satellite / Topographic / Grid map background).
- Server vehicle fleet seeding. Vehicles now spawn automatically on a fresh database (about 50 across Chernarus, with stock-feel partial damage and low fuel), fixing the long-standing "no vehicles ever spawn" issue on private and LAN servers.

### Notes
- Server operators: update to the 1.1.0 server bundle (or drop the new `MPMissions\dayz_1.Chernarus\` over your existing mission) to enable vehicle spawning. Seeding self-guards: it only fills an empty fleet, so it will not duplicate vehicles on restart.

## 1.0.0, 2026-05-26

Initial public release.

### Features
- Original DayZ Mod 1.6 (May 2012) running on modern Arma 2: Operation Arrowhead
- Bundled BattlEye Win11 24H2 compatibility fix (Dwarden)
- One-click installer with Steam path detection
- Launcher app with server list, health checks, and one-click connect
- Official server hosted at 85.239.231.196:2302

### Notes
- Requires Arma 2 + Arma 2: Operation Arrowhead purchased separately on Steam
- Windows 10/11 only (Win11 24H2 supported via bundled BE fix)
