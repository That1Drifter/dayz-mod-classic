# DayZ Mod Classic 1.0.0 - Server Install Guide

Public-internet-ready server bundle. BattlEye on, signature verification on.
For LAN-only testing, edit `cfgdayz\server.cfg` and set `BattlEye=0` + `verifySignatures=0`.

## Server box requirements

- Windows Server 2016+ recommended (Windows 10/11 also work)
- Steam account that owns **Arma 2** AND **Arma 2: Operation Arrowhead**
- 2 vCPU, 4 GB RAM, 20 GB disk minimum
- UDP ports 2302-2305 open to the internet (or LAN, depending on deploy)
- Optional: static public IP (most VPS providers default to this)

## Bundle contents (this folder)

```
@dayzmodclassic\    1.0.0 client PBOs (the mod)
@hive\HiveEXT.dll   Hive extension (DB bridge)
Keys\               .bikey files (server-side signing keys)
MPMissions\         dayz_1.Chernarus mission
MySQL\              portable MySQL 5.5.24 (port 3316)
cfgdayz\            server.cfg + HiveExt.ini
libmysql.dll        HiveEXT runtime dep
mysqlcppconn.dll    HiveEXT runtime dep
schema-patch.sql    DB schema fixes
START_MYSQL.bat     launches MySQL
APPLY_SCHEMA.bat    applies schema-patch.sql (run once)
START_SERVER.bat    launches Arma 2 OA dedicated server
INSTALL.md          this file
INSTALL_SERVER.ps1  one-shot automated installer (PowerShell)
```

## Automated install (recommended)

Open PowerShell **as Administrator** in this directory and run:
```powershell
.\INSTALL_SERVER.ps1
```
The script:
1. Verifies Arma 2 OA is installed
2. Copies bundle into the A2OA install root
3. Opens UDP 2302-2305 in Windows Firewall
4. Sets up MySQL service + applies schema
5. Registers the Arma server as a scheduled task with auto-restart
6. Prompts for admin password + MOTD overrides

Skip the manual steps below if you used the script.

## Manual install

### 1. Install Arma 2 + Arma 2: Operation Arrowhead via Steam

Both required. OA is the standalone expansion the dedicated server uses.
Default install path:
```
C:\Program Files (x86)\Steam\steamapps\common\Arma 2 Operation Arrowhead\
```

Verify `arma2oaserver.exe` exists in that directory after install.

### 2. Copy bundle contents into Arma 2 OA install root

Copy everything inside this bundle directly into the Arma 2 OA install root.
After copying you should see, side-by-side:
```
<arma 2 oa install>\
+- arma2oaserver.exe          (from Steam)
+- arma2oa.exe                (from Steam)
+- @dayzmodclassic\           (from bundle)
+- @hive\                     (from bundle)
+- Keys\                      (merged with Steam's Keys)
+- MPMissions\dayz_1.Chernarus\
+- MySQL\
+- cfgdayz\
+- libmysql.dll
+- mysqlcppconn.dll
+- schema-patch.sql
+- START_MYSQL.bat
+- APPLY_SCHEMA.bat
+- START_SERVER.bat
```

### 3. Set admin password

Open `cfgdayz\server.cfg` in a text editor. Find:
```
passwordAdmin = "CHANGE_ME_BEFORE_LAUNCH";
```
Change to a strong random password. In-game use `#login <password>` for admin access.

### 4. Open Windows Firewall ports

PowerShell as Administrator:
```powershell
New-NetFirewallRule -DisplayName "DayZ Mod Classic UDP 2302-2305" `
    -Direction Inbound -Protocol UDP -LocalPort 2302-2305 -Action Allow
```

DO NOT open MySQL port 3316 to the network. Localhost-only.

### 5. First boot - MySQL + schema patch

1. Double-click `START_MYSQL.bat`. Leave it running.
2. Wait 5 seconds.
3. Double-click `APPLY_SCHEMA.bat`. First run shows a few `DROP INDEX` errors that are harmless.
4. Verify:
   ```
   MySQL\bin\mysql.exe -h 127.0.0.1 -P 3316 -u root -proot hivemind -e "DESC character_data;"
   ```
   Expect `PlayerUID varchar(32)`, `Worldspace varchar(128)`, `Medical varchar(256)`.

### 6. Boot Arma server

Double-click `START_SERVER.bat`. Wait for:
```
Server: ... World name: Chernarus
```

Logs:
- `cfgdayz\server_console.log` - Arma server output
- `%LOCALAPPDATA%\ArmA 2 OA\arma2oaserver.RPT` - script errors
- `MySQL\data\<hostname>.err` - MySQL errors

## Client connect

Each player runs the `DayZModClassic-Client-1.0.0-Setup-v5.exe` installer on their PC.
That bundles the mod + launcher. They connect via the launcher's server list.

For testers without the installer (manual setup):
```
arma2oa.exe -mod=@dayzmodclassic -connect=<your-server-IP>:2302
```

## Stop sequence

1. Arma server window: type `#shutdown` (after `#login`) or close window.
2. MySQL window: Ctrl+C, wait for clean shutdown line.

## Troubleshooting

### Server boots but client can't connect
- `arma2oaserver.RPT` shows nothing on connect attempt = firewall blocking
- Server console shows player joining then leaving immediately = mod mismatch (client and server PBOs differ)
- "Bad signature" in RPT = client has different PBO version; tell client to reinstall via launcher

### Client connects, character won't spawn (stuck black screen)
- MySQL not running
- `HiveExt.ini` Port mismatch with MySQL (must be 3316)
- Schema not patched (run `APPLY_SCHEMA.bat`)
- Tail RPT for `[CHILD:101]` / `[CHILD:103]` errors

### "HiveEXT.dll" failed to load
- Right-click `HiveEXT.dll` -> Properties -> Unblock (SmartScreen)
- Visual C++ 2010 redistributable missing - install from Microsoft
- `libmysql.dll` or `mysqlcppconn.dll` not in Arma 2 OA root (re-copy from bundle)

### BattlEye not loading (Win11 24H2)
- The official server runs on Windows Server 2016 (no BE 24H2 issue).
- If hosting on Win11 24H2 personally: install Dwarden's BE Win11 fix
  (same one bundled in the client installer's BE-fix folder; copy to server's A2OA root + BattlEye/).

## Get listed on dayzmodclassic.com

To be added to the official server list (visible to all launcher users):
1. Verify your server boots and accepts connections
2. Open an issue at https://github.com/That1Drifter/dayz-mod-classic with: server name, IP:port, region, description, contact
3. Or ping #server-list in Discord at https://discord.gg/rgGpjayRMv

The official launcher polls `https://dayzmodclassic.com/servers.json` for the current list.
