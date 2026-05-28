using System.Collections.Concurrent;
using System.Globalization;
using DayZModClassic.Admin.Options;
using Microsoft.Extensions.Options;

namespace DayZModClassic.Admin.Logs;

// Tails the server RPT by polling (the watchdog rotates the RPT on relaunch, so a
// shrinking/replaced file is expected and handled by resetting the read offset).
// Keeps a capped buffer of recent lines for the log viewer, and parses the mission's
// ADMINPOS lines into the latest live-position snapshot for the map.
public sealed class RptTailService : BackgroundService
{
    // Two-line snapshot protocol emitted by the mission reporter:
    //   ADMINPOS|<gameTime>|<count>            (header, starts a snapshot)
    //   ADMINPOSP|<uid>|<x>|<y>|<alive>|<name> (one per player; name is the remainder)
    private const string HeaderMarker = "ADMINPOS|";
    private const string PlayerMarker = "ADMINPOSP|";
    private const int LineCap = 3000;

    private readonly string _path;
    private readonly ILogger<RptTailService> _log;

    private readonly ConcurrentQueue<string> _lines = new();
    private volatile PositionSnapshot _snapshot = new(DateTimeOffset.MinValue, 0, Array.Empty<PlayerPos>());

    // In-progress snapshot being assembled across lines.
    private int _buildGameTime;
    private int _buildExpected;
    private bool _buildActive;
    private List<PlayerPos> _build = new();

    private long _offset;
    private long _lastKnownLength = -1;

    public RptTailService(IOptions<AdminOptions> opts, ILogger<RptTailService> log)
    {
        _path = opts.Value.Paths.Rpt;
        _log = log;
    }

    public PositionSnapshot Snapshot => _snapshot;

    public IReadOnlyList<string> Tail(int max = 200)
        => _lines.Reverse().Take(max).Reverse().ToList();

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_path))
        {
            _log.LogWarning("Admin:Paths:Rpt not set - log/map feed disabled.");
            return;
        }

        while (!ct.IsCancellationRequested)
        {
            try { Poll(); }
            catch (Exception ex) { _log.LogDebug(ex, "RPT poll error."); }
            await Task.Delay(2000, ct);
        }
    }

    private void Poll()
    {
        if (!File.Exists(_path)) return;

        long length = new FileInfo(_path).Length;

        // Rotation / truncation: file replaced or shrank -> start over from the top.
        if (length < _lastKnownLength || (_lastKnownLength < 0 && _offset > length))
            _offset = 0;
        _lastKnownLength = length;

        if (length <= _offset)
        {
            if (length < _offset) _offset = 0;
            return;
        }

        using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fs.Seek(_offset, SeekOrigin.Begin);
        using var reader = new StreamReader(fs);

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            Ingest(line);
        }
        _offset = fs.Position;
    }

    private void Ingest(string line)
    {
        _lines.Enqueue(line);
        while (_lines.Count > LineCap && _lines.TryDequeue(out _)) { }

        int pi = line.IndexOf(PlayerMarker, StringComparison.Ordinal);
        if (pi >= 0) { HandlePlayerLine(line[(pi + PlayerMarker.Length)..]); return; }

        int hi = line.IndexOf(HeaderMarker, StringComparison.Ordinal);
        if (hi >= 0) HandleHeaderLine(line[(hi + HeaderMarker.Length)..]);
    }

    private void HandleHeaderLine(string payload)
    {
        // A new header flushes whatever was assembled (covers dropped/late lines).
        if (_buildActive) Publish();

        var f = payload.TrimEnd('"', ' ', '\t').Split('|');
        int.TryParse(f.ElementAtOrDefault(0), NumberStyles.Integer, CultureInfo.InvariantCulture, out _buildGameTime);
        int.TryParse(f.ElementAtOrDefault(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out _buildExpected);
        _build = new List<PlayerPos>();
        _buildActive = true;

        if (_buildExpected <= 0) Publish(); // empty server -> publish empty snapshot now
    }

    private void HandlePlayerLine(string payload)
    {
        if (!_buildActive) return;

        // uid | x | y | alive | name(remainder, may contain '|')
        var f = payload.TrimEnd('"', ' ', '\t').Split('|', 5);
        if (f.Length < 4) return;
        double.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x);
        double.TryParse(f[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var y);
        bool alive = f[3] is "1" or "true" or "True";
        // name is the remainder; SQF logs it wrapped in quotes, so strip them.
        var name = (f.Length >= 5 ? f[4] : f[0]).Trim().Trim('"');
        _build.Add(new PlayerPos(name, f[0], x, y, alive));

        if (_build.Count >= _buildExpected) Publish();
    }

    private void Publish()
    {
        _snapshot = new PositionSnapshot(DateTimeOffset.UtcNow, _buildGameTime, _build);
        _buildActive = false;
    }
}
