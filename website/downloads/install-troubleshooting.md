# Install troubleshooting

Common problems hitting first-time players, and how to fix each one.

## "Installer says Arma 2 OA not detected"

The installer looks for Arma 2: Operation Arrowhead in your Steam library. If it can't find it:

1. Install Steam if you haven't already.
2. Buy and install both **Arma 2** and **Arma 2: Operation Arrowhead** on Steam.
3. Launch Arma 2: Operation Arrowhead once from Steam to confirm it works.
4. Re-run `DayZModClassic-Client-1.0.0-Setup-v5.exe`.

If you believe A2OA is installed and the installer still fails to detect it, share the diagnostic file the installer writes to `%TEMP%\DayZModClassic-install-diag.txt`. Paste it into Discord and we will help.

## "Launcher says BattlEye fix not present"

The installer attempts to drop in the Win11 24H2 BattlEye compatibility files. If A2OA was missing at install time, this step is skipped.

Fix: re-run `DayZModClassic-Client-1.0.0-Setup-v5.exe` after A2OA is fully installed. You can run the installer on top of an existing install; it will repair missing files.

## "Connect fails: Wait for host"

This usually means the server is empty and is booting on first connect (the official VPS boots lazily to save resources).

1. Wait about 30 seconds.
2. Click PLAY again from the launcher.
3. If it keeps failing, check the server list at `https://dayzmodclassic.com/servers.json` to see whether the official server is online.

## "Connect fails: Player without identity"

Steam isn't running, so Arma can't load your Steam identity.

Fix: start Steam, sign in, then click PLAY again from the launcher.

## Game crashes on startup

Open the Arma report log:

```
%LOCALAPPDATA%\ArmA 2 OA\arma2oa.RPT
```

Paste the **last 50 lines** of that file into the `#help` channel on Discord. Someone will help diagnose it.

## Still stuck?

From the launcher, open **Help ▾ → Save diagnostic report...**. It writes a zip on your Desktop with launcher logs, the Arma RPT, your scrubbed config, and the installer diagnostic (if present). Drop the zip into our Discord and we will dig in.

- Discord: https://discord.gg/rgGpjayRMv
- GitHub issues: https://github.com/That1Drifter/dayz-mod-classic/issues
