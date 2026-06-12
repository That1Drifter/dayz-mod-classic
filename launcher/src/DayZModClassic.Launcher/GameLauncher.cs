using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace DayZModClassic.Launcher;

public record HealthState(
    bool SteamRunning,
    bool A2oaInstalled,
    string? A2oaPath,
    bool ModInstalled,
    bool BattlEyeFixPresent
);

public static class GameLauncher
{
    public const string ModFolder = "@dayzmodclassic";
    public const string ModPboRelative = @"@dayzmodclassic\AddOns\dayz.pbo";
    public const long BE_EXE_MIN_BYTES = 1 * 1024 * 1024;       // 1 MB
    public const long BE_SVC_MIN_BYTES = 15 * 1024 * 1024;      // 15 MB

    public static HealthState ComputeHealth(LauncherConfig cfg)
    {
        bool steam = SteamDetector.IsSteamRunning();
        bool a2oaInstalled = !string.IsNullOrEmpty(cfg.A2oaPath) && Directory.Exists(cfg.A2oaPath);
        bool mod = a2oaInstalled && File.Exists(Path.Combine(cfg.A2oaPath, ModPboRelative));
        bool be = a2oaInstalled && CheckBattlEyeFix(cfg.A2oaPath);
        return new HealthState(steam, a2oaInstalled, cfg.A2oaPath, mod, be);
    }

    public static bool CheckBattlEyeFix(string a2oaRoot)
    {
        try
        {
            var beExe = Path.Combine(a2oaRoot, "ArmA2OA_BE.exe");
            var beSvc = Path.Combine(a2oaRoot, "BattlEye", "BEService_x64.exe");
            if (!File.Exists(beExe)) return false;
            if (new FileInfo(beExe).Length <= BE_EXE_MIN_BYTES) return false;
            if (!File.Exists(beSvc)) return false;
            if (new FileInfo(beSvc).Length <= BE_SVC_MIN_BYTES) return false;
            return true;
        }
        catch { return false; }
    }

    public class LaunchException : Exception
    {
        public LaunchException(string message) : base(message) { }
    }

    public static async Task LaunchAsync(LauncherConfig cfg, ServerEntry server, string playerName)
    {
        if (string.IsNullOrWhiteSpace(cfg.A2oaPath) || !Directory.Exists(cfg.A2oaPath))
            throw new LaunchException("Arma 2: Operation Arrowhead is not installed (path not detected).");

        var a2oaRoot = NormalizeWinPath(cfg.A2oaPath);
        var beExe = Path.Combine(a2oaRoot, "ArmA2OA_BE.exe");
        var modPbo = Path.Combine(a2oaRoot, ModPboRelative);
        var appidFile = Path.Combine(a2oaRoot, "steam_appid.txt");

        if (!File.Exists(beExe))
            throw new LaunchException("BE fix not installed. Click INSTALL in the launcher first.");
        if (!File.Exists(modPbo))
            throw new LaunchException("Mod not installed. Click INSTALL in the launcher first.");

        // Steam identity trick: steam_appid.txt with 33930 in cwd of the game exe.
        try
        {
            if (!File.Exists(appidFile))
                File.WriteAllText(appidFile, SteamDetector.A2OA_APPID);
        }
        catch (Exception ex)
        {
            throw new LaunchException($"Could not write steam_appid.txt: {ex.Message}");
        }

        // Ensure Steam is running
        if (!SteamDetector.IsSteamRunning())
        {
            var steamExe = string.IsNullOrEmpty(cfg.SteamPath)
                ? null
                : Path.Combine(cfg.SteamPath, "Steam.exe");
            if (!string.IsNullOrEmpty(steamExe) && File.Exists(steamExe))
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = steamExe,
                        Arguments = "-silent",
                        UseShellExecute = true
                    });
                    await Task.Delay(5000);
                }
                catch { /* keep going, Arma will complain if needed */ }
            }
        }

        // Build mod chain: A2Base ; EXPANSION ; CA ; @dayzmodclassic
        // Arma's -mod parser expects backslashes + uppercase drive letter on
        // absolute paths. Mixed slashes (c:/foo\bar) cause silent A2-base mount
        // failures => "Missing addons: chernarus, dayz_code, ..." => server kick.
        var a2Base = string.IsNullOrEmpty(cfg.A2BasePath) ? "" : NormalizeWinPath(cfg.A2BasePath);
        var modChain = $"{a2Base};EXPANSION;CA;{ModFolder}";

        var args =
            $"\"-mod={modChain}\" " +
            $"-connect={server.Host}:{server.Port} " +
            $"-name=\"{EscapeArg(playerName)}\" " +
            $"-noPause " +
            $"-skipIntro";

        // UseShellExecute=true (ShellExecute API) detaches the spawned process
        // so it's not a child of this launcher. CONNECT.ps1 on MainPC uses
        // PowerShell Start-Process which is equivalent. Without this, the
        // Steam API attach in ArmA2OA.exe can fail (parent process chain
        // doesn't include Steam.exe), producing "Player without identity"
        // on server connect.
        var psi = new ProcessStartInfo
        {
            FileName = beExe,
            Arguments = args,
            WorkingDirectory = a2oaRoot,
            UseShellExecute = true
        };

        try
        {
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            throw new LaunchException($"Failed to start ArmA2OA_BE.exe: {ex.Message}");
        }

        ConfigStore.AppendLaunchLog($"launch server=\"{server.Name}\" addr={server.Host}:{server.Port} player=\"{playerName}\"");
    }

    private static string EscapeArg(string s) => (s ?? "").Replace("\"", "");

    // Normalize a Windows path: forward slashes -> backslashes, uppercase drive letter.
    // Arma 2 OA's -mod parser silently fails on mixed slashes / lowercase drive in
    // absolute mod paths; symptom is "Missing addons: chernarus, dayz_code, ...".
    private static string NormalizeWinPath(string p)
    {
        if (string.IsNullOrEmpty(p)) return p;
        var normalized = p.Replace('/', '\\').TrimEnd('\\');
        if (normalized.Length >= 2 && normalized[1] == ':')
        {
            normalized = char.ToUpperInvariant(normalized[0]) + normalized.Substring(1);
        }
        return normalized;
    }
}
