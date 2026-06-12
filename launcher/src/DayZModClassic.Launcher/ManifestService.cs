using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DayZModClassic.Launcher;

public static class ManifestService
{
    // Shared client for all update/manifest traffic. Downloads of large blobs
    // use ResponseHeadersRead so the 100s default timeout only bounds headers,
    // not the body stream.
    public static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromSeconds(100) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"DayZModClassicLauncher/{AppInfo.Version}");
        return c;
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<VersionInfo?> FetchVersionAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            var json = await Http.GetStringAsync(url, cts.Token);
            var v = JsonSerializer.Deserialize<VersionInfo>(json, JsonOpts);
            if (v == null || string.IsNullOrEmpty(v.Latest))
            {
                Logger.Warn($"version fetch: unusable payload from {url}");
                return null;
            }
            return v;
        }
        catch (Exception ex)
        {
            Logger.Warn($"version fetch failed url={url}: {ex.Message}");
            return null;
        }
    }

    public static async Task<ModManifest?> FetchManifestAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            var json = await Http.GetStringAsync(url, cts.Token);
            var m = JsonSerializer.Deserialize<ModManifest>(json, JsonOpts);
            if (m == null || m.Files.Count == 0)
            {
                Logger.Warn($"manifest fetch: unusable payload from {url}");
                return null;
            }
            if (m.SchemaVersion != 1)
            {
                Logger.Warn($"manifest fetch: unsupported schemaVersion={m.SchemaVersion}");
                return null;
            }
            return m;
        }
        catch (Exception ex)
        {
            Logger.Warn($"manifest fetch failed url={url}: {ex.Message}");
            return null;
        }
    }
}
