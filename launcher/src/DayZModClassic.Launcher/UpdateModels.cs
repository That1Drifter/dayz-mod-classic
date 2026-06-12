using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DayZModClassic.Launcher;

public class ModManifest
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; set; }
    [JsonPropertyName("modVersion")] public string ModVersion { get; set; } = "";
    [JsonPropertyName("generated")] public string Generated { get; set; } = "";
    [JsonPropertyName("baseUrl")] public string BaseUrl { get; set; } = "";
    [JsonPropertyName("files")] public List<ManifestFile> Files { get; set; } = new();
}

public class ManifestFile
{
    // A2OA-root-relative, forward slashes (e.g. "@dayzmodclassic/AddOns/dayz.pbo")
    [JsonPropertyName("path")] public string Path { get; set; } = "";
    [JsonPropertyName("kind")] public string Kind { get; set; } = "";
    [JsonPropertyName("size")] public long Size { get; set; }
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = "";
    // Relative to manifest baseUrl
    [JsonPropertyName("url")] public string Url { get; set; } = "";
}

public class VersionInfo
{
    [JsonPropertyName("latest")] public string Latest { get; set; } = "";
    [JsonPropertyName("released")] public string Released { get; set; } = "";
    [JsonPropertyName("downloadUrl")] public string DownloadUrl { get; set; } = "";
    [JsonPropertyName("sha256")] public string Sha256 { get; set; } = "";
    [JsonPropertyName("changelogUrl")] public string ChangelogUrl { get; set; } = "";
    [JsonPropertyName("minRequired")] public string MinRequired { get; set; } = "";
    [JsonPropertyName("manifestUrl")] public string ManifestUrl { get; set; } = "";
}

public enum InstallState
{
    NotInstalled,    // dayz.pbo absent
    UpdateAvailable, // some manifest files missing or hash-mismatched
    UpToDate,
    Unknown,         // manifest unavailable (offline) but mod present
}

public record UpdateCheckResult(
    InstallState State,
    string ModVersion,
    IReadOnlyList<ManifestFile> ToDownload,
    long DownloadBytes);

public record InstallProgress(
    string Phase,        // "verify" | "download" | "commit"
    string CurrentFile,
    int FileIndex,
    int FileCount,
    long BytesDone,
    long BytesTotal,
    double BytesPerSec);
