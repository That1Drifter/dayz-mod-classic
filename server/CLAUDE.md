# CLAUDE.md - DayZ Mod Classic 1.0.0 Server Bundle

You are Claude Code, invoked on the server box where this bundle was deployed.
The human is the same one who staged this bundle.

## What this is

A self-contained server bundle for **DayZ Mod Classic 1.0.0**: a revival of the
original DayZ Mod v1.6 (May 2012) running on current Arma 2: Operation Arrowhead.
This bundle ships with the public v1.0.0 release.

## Operating context

- **OS**: Windows Server 2016+ (Win10/11 also supported)
- **Mode**: Public internet - BattlEye ON, signature verification ON (`verifySignatures=2`)
- **Persistence**: portable MySQL 5.5.24 on localhost:3316, database `hivemind`,
  root/root, NEVER exposed externally
- **Version**: 1.0.0 user-facing, dev internal patch level v8.16

## What the human will do first

1. Run `INSTALL_SERVER.ps1` as Administrator (handles everything) OR follow `INSTALL.md` manual steps.
2. Verify connect via the launcher (`DayZModClassic.exe`) from a client box.
3. Ask for help if any step fails.

## What you should do

Read `INSTALL.md` first. If `INSTALL_SERVER.ps1` was used, walk through what it
did (firewall rule, MySQL service, scheduled task) so you can troubleshoot.

### Likely first questions from human

| Question | Action |
|---|---|
| "Server won't start" | Check `cfgdayz\server_console.log` + `%LOCALAPPDATA%\ArmA 2 OA\arma2oaserver.RPT`. |
| "Client can't connect" | Verify UDP 2302-2305 open. Check `Get-NetFirewallRule -DisplayName 'DayZ Mod Classic*'`. Check signature verify isn't kicking valid clients. |
| "Bad signature in RPT" | Client has older PBOs. Confirm client ran the v1.0.0 installer; if not, point them at dayzmodclassic.com/downloads. |
| "Character won't spawn" | MySQL down, schema not patched, or HiveExt.ini Port mismatch. Run `APPLY_SCHEMA.bat`. Tail RPT for `[CHILD:101]` or `[CHILD:103]`. |
| "BattlEye loops" | Win11 24H2 issue. Apply Dwarden BE fix (same one bundled in client installer). Server box should be Win Server 2016 to avoid. |

### Critical files

- `@dayzmodclassic\AddOns\*.pbo` - mod PBOs (7 files + 7 bisigns; dayz_code signed with `that1drifter.bikey`, rest with `dayz.bikey`)
- `@hive\HiveEXT.dll` - DB bridge
- `Keys\dayz.bikey` + `Keys\that1drifter.bikey` - sig verification keys
- `cfgdayz\server.cfg` - Arma server config (BE=1, sigs=2, hostname "DayZ Mod Classic 1.0 [Official]")
- `cfgdayz\HiveExt.ini` - HiveEXT config (localhost:3316, db hivemind, root/root)
- `schema-patch.sql` - DB schema fixes
- `MPMissions\dayz_1.Chernarus\` - mission

### Known gotchas (already mitigated)

1. GameSpy dead since 2014. `reportingIP=""`. Launcher does discovery, not master server.
2. Modern Arma 2 OA build (131129). `requiredBuild=0` accepts any.
3. `dayz_code.pbo` re-signed with `that1drifter.bikey` (post-patch). Other 6 PBOs still use `dayz.bikey`. Both keys in `Keys/`.
4. Win11 24H2 BE - if running on Win11 24H2, Dwarden BE fix must be applied to A2OA root.
5. MySQL `my.ini` paths fixed to relative `./` (verified booting from `MySQL\bin\mysqld.exe` with CWD=`MySQL\`).

## Boundaries

- BE and sigs are ON intentionally (public deploy). Don't disable without explicit ask.
- DO NOT open MySQL port 3316 externally. Localhost-only.
- DO NOT change `passwordAdmin` without telling the human.
- DO NOT commit `cfgdayz\HiveExt.ini` or anything with the MySQL credentials to a public repo.

## Quick verify commands

```powershell
# Is MySQL up?
.\MySQL\bin\mysql.exe -h 127.0.0.1 -P 3316 -u root -proot -e "SELECT VERSION();"

# Did schema patch apply?
.\MySQL\bin\mysql.exe -h 127.0.0.1 -P 3316 -u root -proot hivemind -e "DESC character_data;"

# Firewall rule active?
Get-NetFirewallRule -DisplayName 'DayZ Mod Classic*'

# Server external IP
Invoke-RestMethod -Uri 'https://api.ipify.org'

# Players connected?
Get-Content cfgdayz\server_console.log -Wait -Tail 20

# Auto-restart task status?
Get-ScheduledTask -TaskName 'DayZModClassic-Server' | Select-Object State,LastRunTime,LastTaskResult
```

## Patch history (internal)

This v1.0.0 release bundles dev iteration v8.16. The v7 (session-ID medical
loop) and v8 (ZFSM with LOS gates + 0.5s cooldown) patches are baked in.
RPT debug tags `[DBG-HEALTH]`, `[DBG-ZFSM]`, `[DBG-MED]` may appear and are
benign telemetry.
