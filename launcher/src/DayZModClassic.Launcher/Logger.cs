using System;
using System.IO;
using System.Text;

namespace DayZModClassic.Launcher;

public static class Logger
{
    public static string LogDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DayZ Mod Classic", "logs");

    public static string CurrentLogPath =>
        Path.Combine(LogDir, $"launcher-{DateTime.Now:yyyy-MM-dd}.log");

    private static readonly object _gate = new();
    private static readonly string _userName = Environment.UserName;

    public static void Info(string message)  => Write("INFO ", message);
    public static void Warn(string message)  => Write("WARN ", message);
    public static void Error(string message) => Write("ERROR", message);

    public static void Exception(string context, Exception ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{context}: {ex.GetType().Name}: {ex.Message}");
        if (ex.StackTrace != null) sb.AppendLine(ex.StackTrace);
        var inner = ex.InnerException;
        while (inner != null)
        {
            sb.AppendLine($"caused by {inner.GetType().Name}: {inner.Message}");
            if (inner.StackTrace != null) sb.AppendLine(inner.StackTrace);
            inner = inner.InnerException;
        }
        Write("ERROR", sb.ToString().TrimEnd());
    }

    private static void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level} {Scrub(message)}{Environment.NewLine}";
            lock (_gate)
            {
                File.AppendAllText(CurrentLogPath, line);
            }
        }
        catch { /* swallow - logging never blocks UX */ }
    }

    // Replaces the current Windows username with <USER> so logs can be shared
    // without leaking the user's profile name. Best-effort; does not catch every
    // possible PII (e.g. custom paths users may have set).
    public static string Scrub(string s)
    {
        if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(_userName)) return s;
        return s.Replace(_userName, "<USER>", StringComparison.OrdinalIgnoreCase);
    }

    public static void PruneOldLogs(int keepDays = 7)
    {
        try
        {
            if (!Directory.Exists(LogDir)) return;
            var cutoff = DateTime.Now.AddDays(-keepDays);
            foreach (var f in Directory.EnumerateFiles(LogDir, "launcher-*.log"))
            {
                try
                {
                    if (File.GetLastWriteTime(f) < cutoff)
                        File.Delete(f);
                }
                catch { }
            }
        }
        catch { }
    }
}
