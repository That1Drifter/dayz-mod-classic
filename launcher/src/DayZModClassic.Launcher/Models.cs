using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DayZModClassic.Launcher;

public class ServerEntry
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("host")] public string Host { get; set; } = "";
    [JsonPropertyName("port")] public int Port { get; set; } = 2302;
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("version")] public string Version { get; set; } = "";
    [JsonPropertyName("official")] public bool Official { get; set; }
    [JsonPropertyName("custom")] public bool Custom { get; set; }

    // Runtime-only, not serialized to disk in the same shape
    [JsonIgnore] public int? PlayerCount { get; set; }
    [JsonIgnore] public int? MaxPlayers { get; set; }
    [JsonIgnore] public int? PingMs { get; set; }
    [JsonIgnore] public string? QueryStatus { get; set; } // null | "ok" | "timeout" | "error"
}

public class LauncherConfig
{
    [JsonPropertyName("playerName")] public string PlayerName { get; set; } = "";
    [JsonPropertyName("lastServer")] public string LastServer { get; set; } = "Official VPS";
    [JsonPropertyName("serversUrl")] public string ServersUrl { get; set; } = "https://dayzmodclassic.com/servers.json";
    [JsonPropertyName("a2oaPath")] public string A2oaPath { get; set; } = "";
    [JsonPropertyName("a2BasePath")] public string A2BasePath { get; set; } = "";
    [JsonPropertyName("steamPath")] public string SteamPath { get; set; } = "";
    [JsonPropertyName("customServers")] public List<ServerEntry> CustomServers { get; set; } = new();
    [JsonPropertyName("versionUrl")] public string VersionUrl { get; set; } = "https://dayzmodclassic.com/version.json";
    // Empty = take manifestUrl from version.json; set explicitly for local testing.
    [JsonPropertyName("manifestUrl")] public string ManifestUrl { get; set; } = "";
    [JsonPropertyName("shortcutOffered")] public bool ShortcutOffered { get; set; }
    [JsonPropertyName("playAnywayWarned")] public bool PlayAnywayWarned { get; set; }
}
