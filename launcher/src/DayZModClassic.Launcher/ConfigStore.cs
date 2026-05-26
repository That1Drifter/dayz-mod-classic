using System;
using System.IO;
using System.Text.Json;

namespace DayZModClassic.Launcher;

public static class ConfigStore
{
    public static string ConfigDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DayZModClassic");

    public static string ConfigPath => Path.Combine(ConfigDir, "config.json");
    public static string LaunchLogPath => Path.Combine(ConfigDir, "launch.log");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static LauncherConfig Load()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            if (!File.Exists(ConfigPath)) return new LauncherConfig();
            var text = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<LauncherConfig>(text, JsonOpts) ?? new LauncherConfig();
        }
        catch
        {
            return new LauncherConfig();
        }
    }

    public static void Save(LauncherConfig cfg)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(cfg, JsonOpts));
        }
        catch { /* swallow */ }
    }

    public static void AppendLaunchLog(string line)
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            File.AppendAllText(LaunchLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {line}{Environment.NewLine}");
        }
        catch { /* swallow */ }
    }
}
