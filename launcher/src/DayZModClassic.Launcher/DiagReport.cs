using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace DayZModClassic.Launcher;

public static class DiagReport
{
    // Builds a zip on the user's Desktop containing recent launcher logs, the
    // latest Arma RPT, the launcher config (scrubbed), the installer diag if
    // present, and a system info text file. Returns the path to the zip.
    public static string Build(LauncherConfig cfg, HealthState health, string appVersion)
    {
        var stamp = DateTime.Now.ToString("yyyy-MM-dd-HHmm");
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var zipPath = Path.Combine(desktop, $"DayZModClassic-report-{stamp}.zip");

        using var fs = File.Create(zipPath);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);

        AddText(zip, "system-info.txt", BuildSystemInfo(cfg, health, appVersion));
        AddText(zip, "config-scrubbed.json", BuildScrubbedConfig(cfg));

        AddRecentLogs(zip);
        AddArmaRpt(zip);
        AddInstallerDiag(zip);

        return zipPath;
    }

    private static string BuildSystemInfo(LauncherConfig cfg, HealthState h, string appVersion)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== DayZ Mod Classic launcher diagnostic ===");
        sb.AppendLine("Attach this whole zip in our Discord. The contents are plain text;");
        sb.AppendLine("review before sending if you are uncomfortable sharing logs.");
        sb.AppendLine();
        sb.AppendLine($"timestamp: {DateTime.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine($"launcher_version: {appVersion}");
        sb.AppendLine($"os: {RuntimeInformation.OSDescription}");
        sb.AppendLine($"os_arch: {RuntimeInformation.OSArchitecture}");
        sb.AppendLine($"process_arch: {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine($"runtime: {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"culture: {System.Globalization.CultureInfo.CurrentCulture.Name}");
        sb.AppendLine();
        sb.AppendLine("[Health snapshot]");
        sb.AppendLine($"  steam_running: {h.SteamRunning}");
        sb.AppendLine($"  a2oa_installed: {h.A2oaInstalled}");
        sb.AppendLine($"  a2oa_path: {Logger.Scrub(h.A2oaPath ?? "")}");
        sb.AppendLine($"  mod_installed: {h.ModInstalled}");
        sb.AppendLine($"  battleye_fix: {h.BattlEyeFixPresent}");
        sb.AppendLine();
        sb.AppendLine("[Resolved paths]");
        sb.AppendLine($"  steam_path: {Logger.Scrub(cfg.SteamPath ?? "")}");
        sb.AppendLine($"  a2oa_path: {Logger.Scrub(cfg.A2oaPath ?? "")}");
        sb.AppendLine($"  a2_base_path: {Logger.Scrub(cfg.A2BasePath ?? "")}");
        return sb.ToString();
    }

    private static string BuildScrubbedConfig(LauncherConfig cfg)
    {
        // Don't reuse JsonSerializer config from ConfigStore - we want explicit
        // control over what fields go in and ensure every string path is scrubbed.
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine($"  \"playerName\": \"{Logger.Scrub(cfg.PlayerName ?? "")}\",");
        sb.AppendLine($"  \"lastServer\": \"{cfg.LastServer ?? ""}\",");
        sb.AppendLine($"  \"serversUrl\": \"{cfg.ServersUrl ?? ""}\",");
        sb.AppendLine($"  \"steamPath\": \"{EscapeJson(Logger.Scrub(cfg.SteamPath ?? ""))}\",");
        sb.AppendLine($"  \"a2oaPath\": \"{EscapeJson(Logger.Scrub(cfg.A2oaPath ?? ""))}\",");
        sb.AppendLine($"  \"a2BasePath\": \"{EscapeJson(Logger.Scrub(cfg.A2BasePath ?? ""))}\",");
        sb.AppendLine($"  \"customServerCount\": {cfg.CustomServers?.Count ?? 0}");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string EscapeJson(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static void AddRecentLogs(ZipArchive zip)
    {
        var dir = Logger.LogDir;
        if (!Directory.Exists(dir)) return;
        var cutoff = DateTime.Now.AddDays(-7);
        foreach (var f in Directory.EnumerateFiles(dir, "launcher-*.log"))
        {
            try
            {
                if (File.GetLastWriteTime(f) < cutoff) continue;
                var name = "logs/" + Path.GetFileName(f);
                AddCopy(zip, f, name);
            }
            catch { }
        }

        // Also include the legacy launch.log that ConfigStore writes.
        try
        {
            var legacy = ConfigStore.LaunchLogPath;
            if (File.Exists(legacy))
                AddCopy(zip, legacy, "logs/launch.log");
        }
        catch { }
    }

    private static void AddArmaRpt(ZipArchive zip)
    {
        try
        {
            var rpt = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ArmA 2 OA", "arma2oa.RPT");
            if (!File.Exists(rpt)) return;
            AddCopy(zip, rpt, "arma2oa.RPT");
        }
        catch { }
    }

    private static void AddInstallerDiag(ZipArchive zip)
    {
        try
        {
            var diag = Path.Combine(Path.GetTempPath(), "DayZModClassic-install-diag.txt");
            if (File.Exists(diag))
                AddCopy(zip, diag, "install-diag.txt");
        }
        catch { }
    }

    private static void AddCopy(ZipArchive zip, string sourcePath, string entryName)
    {
        try
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using var src = File.OpenRead(sourcePath);
            using var dst = entry.Open();
            src.CopyTo(dst);
        }
        catch { }
    }

    private static void AddText(ZipArchive zip, string entryName, string content)
    {
        try
        {
            var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
            using var dst = entry.Open();
            using var w = new StreamWriter(dst, new UTF8Encoding(false));
            w.Write(content);
        }
        catch { }
    }
}
