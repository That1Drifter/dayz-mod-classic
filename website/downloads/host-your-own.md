# Host your own DayZ Mod Classic server

This guide walks you through running an official-compatible server on your own box. Players using the standard launcher will be able to connect if your server is reachable on the public internet (and listed in `servers.json`, see below).

## Hardware minimums

- **2 vCPU** (more helps with player count above 20)
- **4 GB RAM**
- **20 GB disk**
- **Reliable upstream**: 5+ Mbps up, low latency
- **Recommended OS**: Windows Server 2016 or newer (Windows 10/11 also works for LAN/small servers)

## Prerequisites

1. **Windows Server 2016+** (or Windows 10/11). Linux is not currently supported (Arma 2 OA dedicated server is Windows-only).
2. **MySQL** 5.7 or 8.0. The server bundle includes a portable MySQL distribution, or you can point at an existing system MySQL instance via `config\hive.ini`.
3. **Open ports** (UDP, inbound):
   - `2302` (game)
   - `2303` (VON voice)
   - `2304` (BattlEye)
   - `2305` (server steam query)
4. **Administrator** access to install services and modify the firewall.

## Install steps

1. Download `DayZModClassic-Server-1.1.0.zip` from the [downloads page](/downloads/).
2. Extract to a path with no spaces, for example `C:\dayzmodclassic-server\`.
3. Open PowerShell **as Administrator** in the extracted folder.
4. Run:

   ```powershell
   .\INSTALL_SERVER.ps1
   ```

   The script will:
   - Open UDP 2302-2305 in the Windows firewall
   - Register a Windows service named `DayZModClassicServer`
   - Initialize the MySQL data directory and import the schema
   - Start the service

5. Verify the server is running by checking the Services control panel, or by connecting from a client using the launcher's "Custom server" entry pointed at `<your-ip>:2302`.

## Configuration

Key files under your install dir:

- `config\server.cfg`, hostname, max players, password, message of the day
- `config\hive.ini`, MySQL connection (host, port, db, user, password)
- `mpmissions\dayz_mission\`, mission files (rarely need to be edited)

After editing config, restart the service:

```powershell
Restart-Service DayZModClassicServer
```

## Getting listed in the launcher

Once your server is up, reach out on [Discord](https://discord.gg/rgGpjayRMv) and we'll add an entry to `https://dayzmodclassic.com/servers.json`. Players using the standard launcher will then see your server in the browser.

Include:
- Server name
- Public IP and port
- Region (NA / EU / SA / OCE / ASIA)
- Short description
- Contact (Discord handle) in case the server goes down

## Troubleshooting

- **Server not visible to clients**: confirm UDP 2302-2305 are forwarded at the router and allowed in the Windows firewall. Try connecting from outside your network.
- **MySQL connection refused**: confirm MySQL is running and `hive.ini` matches. The bundled MySQL listens on port 3306 by default.
- **High CPU on idle**: normal for A2OA dedicated server. Watch for sustained 100% on a single core during peak; that's the cap.

For deeper issues, post in `#hosting` on Discord with your server's `arma2oaserver.RPT` log.
