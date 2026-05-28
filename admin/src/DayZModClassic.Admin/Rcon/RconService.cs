using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using DayZModClassic.Admin.Options;
using Microsoft.Extensions.Options;

namespace DayZModClassic.Admin.Rcon;

// Owns a single BeRconClient, keeps it connected, exposes high-level admin verbs and
// parsed views (players, bans). Server messages (chat + admin events) are kept in a
// capped ring buffer for the log/chat view.
public sealed partial class RconService : BackgroundService
{
    private readonly RconOptions _opts;
    private readonly ILogger<RconService> _log;

    private readonly object _lock = new();
    private BeRconClient? _client;

    private volatile IReadOnlyList<PlayerInfo> _players = Array.Empty<PlayerInfo>();
    private DateTimeOffset? _lastUpdate;

    private readonly ConcurrentQueue<ChatLine> _chat = new();
    private const int ChatCap = 500;

    public RconService(IOptions<AdminOptions> opts, ILogger<RconService> log)
    {
        _opts = opts.Value.Rcon;
        _log = log;
    }

    public bool Configured => !string.IsNullOrEmpty(_opts.Password);
    public bool IsConnected => _client?.IsConnected ?? false;

    public RconStatus Status => new(IsConnected, _players.Count, _lastUpdate);
    public IReadOnlyList<PlayerInfo> CachedPlayers => _players;

    public IReadOnlyList<ChatLine> RecentChat(int max = 200)
        => _chat.Reverse().Take(max).Reverse().ToList();

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!Configured)
        {
            _log.LogWarning("Admin:Rcon:Password not set - RCon features are disabled.");
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            BeRconClient? client = null;
            try
            {
                client = new BeRconClient(_opts.Host, _opts.Port, _opts.Password, _log);
                client.ServerMessage += OnServerMessage;

                if (await client.ConnectAsync(ct))
                {
                    lock (_lock) { _client = client; }
                    _log.LogInformation("RCon connected to {Host}:{Port}.", _opts.Host, _opts.Port);

                    while (!ct.IsCancellationRequested && client.IsConnected)
                    {
                        try { await RefreshPlayersAsync(ct); }
                        catch (Exception ex) { _log.LogDebug(ex, "Player refresh failed."); }
                        await Task.Delay(15_000, ct);
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.LogWarning(ex, "RCon connection error."); }

            lock (_lock) { _client = null; }
            _players = Array.Empty<PlayerInfo>();
            if (client is not null) await client.DisposeAsync();
            if (!ct.IsCancellationRequested) await Task.Delay(10_000, ct);
        }
    }

    private void OnServerMessage(string msg)
    {
        _chat.Enqueue(new ChatLine(DateTimeOffset.UtcNow, msg));
        while (_chat.Count > ChatCap && _chat.TryDequeue(out _)) { }
    }

    private BeRconClient Require()
    {
        var c = _client;
        if (c is null || !c.IsConnected)
            throw new InvalidOperationException("RCon is not connected.");
        return c;
    }

    public Task<string> SendRawAsync(string command, CancellationToken ct)
        => Require().SendCommandAsync(command, ct);

    // ---- Read views ----

    public async Task<IReadOnlyList<PlayerInfo>> RefreshPlayersAsync(CancellationToken ct)
    {
        var raw = await Require().SendCommandAsync("players", ct);
        var list = ParsePlayers(raw);
        _players = list;
        _lastUpdate = DateTimeOffset.UtcNow;
        return list;
    }

    public async Task<IReadOnlyList<BanEntry>> GetBansAsync(CancellationToken ct)
        => ParseBans(await Require().SendCommandAsync("bans", ct));

    // ---- Control verbs ----

    public Task<string> KickAsync(int playerId, string reason, CancellationToken ct)
        => SendRawAsync($"kick {playerId} {Clean(reason)}", ct);

    public Task<string> BanPlayerAsync(int playerId, int minutes, string reason, CancellationToken ct)
        => SendRawAsync($"ban {playerId} {minutes} {Clean(reason)}", ct);

    public Task<string> AddBanAsync(string guid, int minutes, string reason, CancellationToken ct)
        => SendRawAsync($"addBan {Clean(guid)} {minutes} {Clean(reason)}", ct);

    public async Task<string> RemoveBanAsync(int banIndex, CancellationToken ct)
    {
        var r = await SendRawAsync($"removeBan {banIndex}", ct);
        await SendRawAsync("writeBans", ct);
        return r;
    }

    public Task<string> SayGlobalAsync(string message, CancellationToken ct)
        => SendRawAsync($"say -1 {Clean(message)}", ct);

    public Task<string> SayPlayerAsync(int playerId, string message, CancellationToken ct)
        => SendRawAsync($"say {playerId} {Clean(message)}", ct);

    public Task<string> LockAsync(CancellationToken ct) => SendRawAsync("#lock", ct);
    public Task<string> UnlockAsync(CancellationToken ct) => SendRawAsync("#unlock", ct);
    public Task<string> ShutdownAsync(CancellationToken ct) => SendRawAsync("#shutdown", ct);
    public Task<string> RestartMissionAsync(CancellationToken ct) => SendRawAsync("#restart", ct);

    // Strip CR/LF so a crafted reason cannot inject a second RCon command line.
    private static string Clean(string s) => s.Replace("\r", " ").Replace("\n", " ").Trim();

    // ---- Parsing ----

    [GeneratedRegex(@"^(?<id>\d+)\s+(?<ip>[0-9.]+):(?<port>\d+)\s+(?<ping>[-\d]+)\s+(?<guid>[0-9a-fA-F]+)\((?<verif>OK|\?)\)\s*(?<name>.*)$")]
    private static partial Regex PlayerRegex();

    internal static IReadOnlyList<PlayerInfo> ParsePlayers(string raw)
    {
        var result = new List<PlayerInfo>();
        foreach (var line in raw.Split('\n'))
        {
            var m = PlayerRegex().Match(line.Trim());
            if (!m.Success) continue;

            var name = m.Groups["name"].Value.Trim();
            bool inLobby = name.EndsWith("(Lobby)", StringComparison.OrdinalIgnoreCase);
            if (inLobby) name = name[..^"(Lobby)".Length].Trim();

            int.TryParse(m.Groups["ping"].Value, out var ping);
            int.TryParse(m.Groups["port"].Value, out var port);

            result.Add(new PlayerInfo(
                int.Parse(m.Groups["id"].Value),
                m.Groups["ip"].Value,
                port,
                ping,
                m.Groups["guid"].Value,
                m.Groups["verif"].Value == "OK",
                name,
                inLobby));
        }
        return result;
    }

    [GeneratedRegex(@"^(?<idx>\d+)\s+(?<target>\S+)\s+(?<mins>\S+)\s*(?<reason>.*)$")]
    private static partial Regex BanRegex();

    internal static IReadOnlyList<BanEntry> ParseBans(string raw)
    {
        var result = new List<BanEntry>();
        string type = "guid";
        foreach (var rawLine in raw.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.StartsWith("GUID Bans", StringComparison.OrdinalIgnoreCase)) { type = "guid"; continue; }
            if (line.StartsWith("IP Bans", StringComparison.OrdinalIgnoreCase)) { type = "ip"; continue; }
            if (line.Length == 0 || line.StartsWith("[#]")) continue;

            var m = BanRegex().Match(line);
            if (!m.Success) continue;
            result.Add(new BanEntry(
                int.Parse(m.Groups["idx"].Value),
                type,
                m.Groups["target"].Value,
                m.Groups["mins"].Value,
                m.Groups["reason"].Value.Trim()));
        }
        return result;
    }
}
