using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DayZModClassic.Launcher;

public static class ServerListService
{
    public static async Task<List<ServerEntry>> FetchAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DayZModClassicLauncher/1.0.0");
            var json = await http.GetStringAsync(url, ct);
            var list = JsonSerializer.Deserialize<List<ServerEntry>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
            if (list != null && list.Count > 0) return list;
        }
        catch { }

        return LoadFallback();
    }

    public static List<ServerEntry> LoadFallback()
    {
        try
        {
            var asm = Assembly.GetExecutingAssembly();
            // Resource name = <AssemblyName>.<RelativePath with dots>
            // csproj keeps RootNamespace = DayZModClassic.Launcher
            var resourceName = "DayZModClassic.Launcher.Resources.servers.fallback.json";
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                // Try scanning
                foreach (var n in asm.GetManifestResourceNames())
                {
                    if (n.EndsWith("servers.fallback.json", StringComparison.OrdinalIgnoreCase))
                    {
                        using var s2 = asm.GetManifestResourceStream(n);
                        if (s2 != null)
                        {
                            using var sr2 = new StreamReader(s2);
                            return Deserialize(sr2.ReadToEnd());
                        }
                    }
                }
                return HardcodedFallback();
            }
            using var sr = new StreamReader(stream);
            return Deserialize(sr.ReadToEnd());
        }
        catch
        {
            return HardcodedFallback();
        }
    }

    private static List<ServerEntry> Deserialize(string json)
    {
        var list = JsonSerializer.Deserialize<List<ServerEntry>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        return list ?? HardcodedFallback();
    }

    private static List<ServerEntry> HardcodedFallback() => new()
    {
        new ServerEntry
        {
            Name = "Official VPS",
            Host = "85.239.231.196",
            Port = 2302,
            Description = "Official DayZ Mod Classic server",
            Version = "1.0.0",
            Official = true
        }
    };

    /// <summary>
    /// Merge fetched + custom. Custom entries marked custom=true. Officials win on name collision.
    /// </summary>
    public static List<ServerEntry> Merge(List<ServerEntry> fetched, List<ServerEntry> custom)
    {
        var result = new List<ServerEntry>(fetched);
        foreach (var c in custom)
        {
            c.Custom = true;
            // Skip if name collides with an official entry
            bool collides = result.Exists(s =>
                string.Equals(s.Name, c.Name, StringComparison.OrdinalIgnoreCase) && s.Official);
            if (collides) continue;
            result.Add(c);
        }
        return result;
    }
}
