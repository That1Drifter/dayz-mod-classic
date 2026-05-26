using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace DayZModClassic.Launcher;

public record DetectionResult(string? SteamPath, string? A2oaPath, string? A2BasePath);

public static class SteamDetector
{
    // App IDs
    public const string A2OA_APPID = "33930"; // Arma 2: Operation Arrowhead
    public const string A2_APPID   = "33900"; // Arma 2

    public static DetectionResult Detect()
    {
        var steamPath = ReadSteamPath();
        if (string.IsNullOrEmpty(steamPath) || !Directory.Exists(steamPath))
            return new DetectionResult(null, null, null);

        var libraries = ReadSteamLibraries(steamPath);

        string? a2oaPath = null;
        string? a2BasePath = null;

        foreach (var lib in libraries)
        {
            var steamapps = Path.Combine(lib, "steamapps");
            if (!Directory.Exists(steamapps)) continue;

            if (a2oaPath == null && File.Exists(Path.Combine(steamapps, $"appmanifest_{A2OA_APPID}.acf")))
            {
                var candidate = Path.Combine(steamapps, "common", "Arma 2 Operation Arrowhead");
                if (Directory.Exists(candidate)) a2oaPath = candidate;
            }

            if (a2BasePath == null && File.Exists(Path.Combine(steamapps, $"appmanifest_{A2_APPID}.acf")))
            {
                var candidate = Path.Combine(steamapps, "common", "Arma 2");
                if (Directory.Exists(candidate)) a2BasePath = candidate;
            }
        }

        return new DetectionResult(steamPath, a2oaPath, a2BasePath);
    }

    public static string? ReadSteamPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Valve\Steam");
            var v = key?.GetValue("SteamPath") as string;
            if (!string.IsNullOrEmpty(v)) return Path.GetFullPath(v.Replace('/', Path.DirectorySeparatorChar));
        }
        catch { }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam");
            var v = key?.GetValue("InstallPath") as string;
            if (!string.IsNullOrEmpty(v)) return Path.GetFullPath(v);
        }
        catch { }

        return null;
    }

    public static List<string> ReadSteamLibraries(string steamPath)
    {
        var result = new List<string> { steamPath };
        var vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (!File.Exists(vdf)) return result;

        try
        {
            var text = File.ReadAllText(vdf);
            // Match every "path"\t\t"C:\\..." entry (Valve VDF format).
            var rx = new Regex("\"path\"\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
            foreach (Match m in rx.Matches(text))
            {
                var p = m.Groups[1].Value.Replace("\\\\", "\\");
                if (!string.IsNullOrEmpty(p) && !result.Contains(p, StringComparer.OrdinalIgnoreCase))
                    result.Add(p);
            }
        }
        catch { }

        return result;
    }

    public static bool IsSteamRunning()
    {
        try
        {
            var procs = System.Diagnostics.Process.GetProcessesByName("steam");
            return procs.Length > 0;
        }
        catch { return false; }
    }
}
