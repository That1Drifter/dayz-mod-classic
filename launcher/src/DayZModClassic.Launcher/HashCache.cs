using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DayZModClassic.Launcher;

// Caches SHA-256 of installed files keyed by A2OA-relative path so startup
// checks are stat calls instead of rehashing ~171 MB. A size or mtime
// mismatch falls back to a full rehash; the cache can be slow, never wrong.
public sealed class HashCache
{
    public class Entry
    {
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("mtimeUtc")] public long MtimeUtcTicks { get; set; }
        [JsonPropertyName("sha256")] public string Sha256 { get; set; } = "";
    }

    public static string CachePath => Path.Combine(ConfigStore.ConfigDir, "hashcache.json");

    private readonly Dictionary<string, Entry> _entries;

    private HashCache(Dictionary<string, Entry> entries) => _entries = entries;

    public static HashCache Load()
    {
        try
        {
            if (File.Exists(CachePath))
            {
                var map = JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(CachePath));
                if (map != null) return new HashCache(map);
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"hashcache load failed: {ex.Message}");
        }
        return new HashCache(new Dictionary<string, Entry>());
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigStore.ConfigDir);
            File.WriteAllText(CachePath, JsonSerializer.Serialize(_entries));
        }
        catch (Exception ex)
        {
            Logger.Warn($"hashcache save failed: {ex.Message}");
        }
    }

    public static string NormalizeKey(string relPath) => relPath.Replace('\\', '/').ToLowerInvariant();

    // Returns the SHA-256 (lowercase hex) of the file, from cache when
    // size+mtime match, hashing and recording otherwise.
    public string GetSha256(string fullPath, string relKey)
    {
        var fi = new FileInfo(fullPath);
        var key = NormalizeKey(relKey);
        if (_entries.TryGetValue(key, out var e) &&
            e.Size == fi.Length &&
            e.MtimeUtcTicks == fi.LastWriteTimeUtc.Ticks &&
            !string.IsNullOrEmpty(e.Sha256))
        {
            return e.Sha256;
        }

        var sha = HashFile(fullPath);
        Record(relKey, fi.Length, fi.LastWriteTimeUtc, sha);
        return sha;
    }

    public void Record(string relKey, long size, DateTime mtimeUtc, string sha256)
    {
        _entries[NormalizeKey(relKey)] = new Entry
        {
            Size = size,
            MtimeUtcTicks = mtimeUtc.Ticks,
            Sha256 = sha256,
        };
    }

    public void Invalidate(string relKey) => _entries.Remove(NormalizeKey(relKey));

    public int Count => _entries.Count;

    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
