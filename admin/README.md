# DayZ Mod Classic - Admin Tool

A hybrid admin toolkit for the Arma 2: OA DayZ Mod Classic server, in the spirit of
DayZ Standalone's Community Online Tools, split across two halves:

- **Web panel** (`admin/src/DayZModClassic.Admin`): an ASP.NET Core 8 service that runs
  on the VPS behind Caddy. Handles everything that can be done from outside the running
  game: BattlEye RCon control, the live map, log monitoring, and offline hive DB editing.
- **In-game menu** (`server/MPMissions/dayz_1.Chernarus/admin/`): SQF loaded from the
  mission (no PBO re-sign needed), UID-gated, for live-world actions: teleport, spawn,
  godmode, heal, etc.

The two halves are independent - you can run either without the other.

## Capability split

| Action | Where | How |
|---|---|---|
| Players list, kick, ban, message | Web | BattlEye RCon |
| Ban list view / add / remove | Web | RCon `bans` / `addBan` / `removeBan` |
| Lock, restart, shutdown, broadcast | Web | RCon `#lock` / `#restart` / `#shutdown` / `say` |
| Scheduled restart with warnings | Web | timed `say` + `#shutdown` (watchdog relaunches) |
| Live player map | Web | mission reporter → RPT → backend tail |
| Vehicle/object map layer | Web | hive `object_data` last-known positions |
| Chat / connection / RPT log | Web | RCon server messages + RPT tail |
| Edit characters / vehicles offline | Web | direct MySQL (with pre-write backups) |
| Teleport / spawn / godmode / heal (live) | In-game | SQF dialog (F2), server-validated |

Live-world writes are deliberately *not* driven from the web panel (Arma 2 OA can't read
external commands without a callExtension DLL or a hive-poll bridge). Those are the
in-game menu's job.

## Web panel - build & deploy

The backend binds `127.0.0.1` only and is never exposed directly; Caddy terminates TLS
and adds a second basicauth layer.

1. **Publish** (on your workstation):
   ```powershell
   dotnet publish admin/src/DayZModClassic.Admin -c Release -o publish
   ```
2. **Copy** the `publish/` folder to the VPS (e.g. `scp -r publish administrator@85.239.231.196:C:/admin-panel-stage`).
3. **Secrets**: copy `appsettings.Production.json.example` → `appsettings.Production.json`
   in the publish folder and fill in:
   - `Admin:Rcon:Password` - the alphanumeric BE RCon password
   - `Admin:Hive:ConnectionString` - `Server=127.0.0.1;Port=3316;Database=hivemind;Uid=root;Pwd=root;`
   - `Admin:Auth:Password` - a strong panel password
   - `Admin:Paths:Rpt` - the live RPT, e.g. `C:\arma2oa\cfgdayz\arma2oaserver.RPT`

   (Or set these as env vars: `Admin__Rcon__Password`, etc. - they override the file.)
4. **Service**: run `deploy/Install-AdminPanel.ps1` as Administrator from the publish
   folder. Registers an NSSM service `DayZModClassic-Admin`, same pattern as Caddy.
5. **Caddy**: append `deploy/Caddyfile.admin.snippet` to the VPS Caddyfile, set the
   basicauth bcrypt hash (`caddy hash-password`), add the `admin.dayzmodclassic.com`
   DNS A record, and reload Caddy.

Open `https://admin.dayzmodclassic.com`.

### Config reference (`appsettings.json`)

| Key | Meaning |
|---|---|
| `Admin:Rcon:{Host,Port,Password}` | BE RCon endpoint. Port = game port (2302), not 2306. Password must be alphanumeric. |
| `Admin:Hive:ConnectionString` | MySQL connection. Empty = DB features disabled (panel still runs). |
| `Admin:Hive:InstanceId` | Mission instance (`dayZ_instance` = 222). Scopes DB queries. |
| `Admin:Paths:Rpt` | Server RPT to tail for logs + the live map feed. |
| `Admin:Paths:Backups` | Where pre-write row snapshots are saved before any DB edit. |
| `Admin:Auth:{Username,Password}` | Backend basicauth. Empty password = auth disabled (loopback dev only). |

## In-game menu - setup

1. **Set your admin UID.** Edit `server/MPMissions/dayz_1.Chernarus/admin/admin_init.sqf`
   and replace `CHANGEME_ADMIN_UID` in `DZAdmin_UIDs` with your BattlEye/Steam UID. You
   can read it off the web panel Players tab, or from the RPT on connect. Add more UIDs
   to the array for additional admins.
2. **Deploy the mission.** The admin SQF lives in the mission folder, so it ships with
   the mission - no PBO repack or re-sign. Just deploy the updated
   `dayz_1.Chernarus` folder to the server's `MPMissions`.
3. In game, an admin sees `[Admin] Tools loaded. Press F2 …`. **F2** opens the menu.

Security: the menu only arms for whitelisted UIDs, and `admin_server.sqf` re-checks the
UID on every command - a tampered client cannot run admin actions.

### Files

| File | Role |
|---|---|
| `admin_init.sqf` | Loader + UID whitelist; branches server/client. |
| `admin_positions.sqf` | Server: live position reporter for the web map. |
| `admin_server.sqf` | Server: validated command handler (spawn/teleport/heal/weather). |
| `admin_menu.sqf` | Client: menu logic + self/world actions. |
| `admin_dialog.hpp` | The F2 dialog (included from `description.ext`). |

## Status / caveats

- **Backend builds clean** (`dotnet build`, 0 warnings). RCon, hive, log tail, map feed,
  and DB editor are wired and ready for live config.
- **In-game SQF is written but needs in-engine validation** - it can't be compiled
  offline. Test the F2 menu and the position reporter on a live/test server before
  trusting them. The reporter (diag_log only) is low-risk; the dialog is the part most
  likely to need tweaks.
- Character edits are blocked while the owner is online (hive would overwrite on save).
- Every hive edit/delete writes a JSON backup to `Admin:Paths:Backups` first.
