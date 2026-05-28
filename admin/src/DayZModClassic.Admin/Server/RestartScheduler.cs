using DayZModClassic.Admin.Rcon;

namespace DayZModClassic.Admin.Server;

// A single armed restart: broadcasts countdown warnings, then issues #shutdown.
// The server watchdog relaunches automatically (~30s), so this is the full restart.
public sealed class RestartScheduler : BackgroundService
{
    private readonly RconService _rcon;
    private readonly ILogger<RestartScheduler> _log;
    private readonly object _lock = new();

    private DateTimeOffset? _targetUtc;
    private SortedSet<int> _warnMinutes = new();
    private readonly HashSet<int> _sentWarnings = new();

    public RestartScheduler(RconService rcon, ILogger<RestartScheduler> log)
    {
        _rcon = rcon;
        _log = log;
    }

    public RestartStatus Status
    {
        get
        {
            lock (_lock)
            {
                return new RestartStatus(
                    _targetUtc is not null,
                    _targetUtc,
                    _targetUtc is null ? null : (int)Math.Max(0, (_targetUtc.Value - DateTimeOffset.UtcNow).TotalSeconds));
            }
        }
    }

    public void Arm(int inMinutes, IEnumerable<int>? warnMinutes)
    {
        lock (_lock)
        {
            _targetUtc = DateTimeOffset.UtcNow.AddMinutes(inMinutes);
            _warnMinutes = new SortedSet<int>((warnMinutes ?? new[] { 15, 10, 5, 1 }).Where(m => m > 0 && m < inMinutes));
            _sentWarnings.Clear();
        }
        _log.LogInformation("Restart armed for {Target} UTC.", _targetUtc);
    }

    public void Cancel()
    {
        lock (_lock)
        {
            _targetUtc = null;
            _warnMinutes.Clear();
            _sentWarnings.Clear();
        }
        _log.LogInformation("Scheduled restart cancelled.");
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(5_000, ct);
            DateTimeOffset target;
            lock (_lock)
            {
                if (_targetUtc is null) continue;
                target = _targetUtc.Value;
            }

            var remaining = target - DateTimeOffset.UtcNow;
            if (remaining <= TimeSpan.Zero)
            {
                lock (_lock) { _targetUtc = null; _sentWarnings.Clear(); }
                try
                {
                    await _rcon.SayGlobalAsync("Server is restarting now. Reconnect in ~1 minute.", ct);
                    await Task.Delay(1500, ct);
                    await _rcon.ShutdownAsync(ct);
                    _log.LogInformation("Scheduled restart fired (#shutdown sent).");
                }
                catch (Exception ex) { _log.LogWarning(ex, "Failed to fire scheduled restart."); }
                continue;
            }

            int minsLeft = (int)Math.Ceiling(remaining.TotalMinutes);
            bool fire;
            lock (_lock) { fire = _warnMinutes.Contains(minsLeft) && _sentWarnings.Add(minsLeft); }
            if (fire)
            {
                try { await _rcon.SayGlobalAsync($"Server restart in {minsLeft} minute(s).", ct); }
                catch (Exception ex) { _log.LogDebug(ex, "Warning broadcast failed."); }
            }
        }
    }
}

public sealed record RestartStatus(bool Armed, DateTimeOffset? TargetUtc, int? SecondsLeft);
