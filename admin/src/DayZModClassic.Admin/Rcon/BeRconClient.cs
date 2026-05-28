using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace DayZModClassic.Admin.Rcon;

// Minimal async BattlEye RCon client.
//
// Wire format (all packets): 'B' 'E' <crc32 LE of body> <body>
//   body = 0xFF <type> <payload>
//   type 0x00 = login, 0x01 = command, 0x02 = server message
//
// Arma 2 OA multiplexes RCon over the game port (see project notes); connect to the
// game port, not 2306.
public sealed class BeRconClient : IAsyncDisposable
{
    private static readonly Encoding Enc = Encoding.Latin1;

    private readonly string _host;
    private readonly int _port;
    private readonly string _password;
    private readonly ILogger _log;

    private UdpClient? _udp;
    private CancellationTokenSource? _cts;
    private Task? _receiveLoop;
    private Task? _keepAliveLoop;

    private byte _sequence;
    private readonly object _seqLock = new();
    private readonly Dictionary<byte, PendingCommand> _pending = new();
    private readonly object _pendingLock = new();
    private long _lastSentTicks = DateTime.UtcNow.Ticks;
    private volatile bool _loggedIn;

    public event Action<string>? ServerMessage;
    public event Action<bool>? ConnectionChanged;

    public bool IsConnected => _loggedIn;

    public BeRconClient(string host, int port, string password, ILogger log)
    {
        _host = host;
        _port = port;
        _password = password;
        _log = log;
    }

    private sealed class PendingCommand
    {
        public readonly TaskCompletionSource<string> Tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly SortedDictionary<int, string> Parts = new();
        public int ExpectedParts = -1;
    }

    public async Task<bool> ConnectAsync(CancellationToken ct)
    {
        await DisposeSocketAsync();

        _udp = new UdpClient();
        _udp.Connect(_host, _port);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var packet = BuildPacket(0x00, Enc.GetBytes(_password));
        await _udp.SendAsync(packet, packet.Length);

        var recv = _udp.ReceiveAsync();
        var done = await Task.WhenAny(recv, Task.Delay(5000, ct));
        if (done != recv)
        {
            _log.LogWarning("RCon login timed out talking to {Host}:{Port}", _host, _port);
            return false;
        }

        var resp = (await recv).Buffer;
        // login response body: 0xFF 0x00 <0x01 ok | 0x00 fail>
        if (resp.Length >= 9 && resp[6] == 0xFF && resp[7] == 0x00 && resp[8] == 0x01)
        {
            _loggedIn = true;
            _receiveLoop = Task.Run(() => ReceiveLoop(_cts.Token));
            _keepAliveLoop = Task.Run(() => KeepAliveLoop(_cts.Token));
            ConnectionChanged?.Invoke(true);
            return true;
        }

        _log.LogWarning("RCon login rejected (bad password or RestrictRCon).");
        return false;
    }

    public async Task<string> SendCommandAsync(string command, CancellationToken ct, int timeoutMs = 8000)
    {
        var udp = _udp;
        if (udp is null || !_loggedIn)
            throw new InvalidOperationException("RCon not connected.");

        byte seq;
        var pending = new PendingCommand();
        lock (_seqLock) { seq = _sequence; _sequence = (byte)(_sequence + 1); }
        lock (_pendingLock) { _pending[seq] = pending; }

        var cmdBytes = Enc.GetBytes(command);
        var payload = new byte[1 + cmdBytes.Length];
        payload[0] = seq;
        cmdBytes.CopyTo(payload, 1);

        var packet = BuildPacket(0x01, payload);
        await udp.SendAsync(packet, packet.Length);
        Interlocked.Exchange(ref _lastSentTicks, DateTime.UtcNow.Ticks);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeoutMs);
        await using (timeoutCts.Token.Register(() => pending.Tcs.TrySetCanceled()).ConfigureAwait(false))
        {
            try { return await pending.Tcs.Task; }
            finally { lock (_pendingLock) { _pending.Remove(seq); } }
        }
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        var udp = _udp!;
        while (!ct.IsCancellationRequested)
        {
            UdpReceiveResult res;
            try { res = await udp.ReceiveAsync(ct); }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { _log.LogWarning(ex, "RCon receive loop ended."); break; }
            try { HandlePacket(res.Buffer); }
            catch (Exception ex) { _log.LogDebug(ex, "Failed to handle RCon packet."); }
        }
        _loggedIn = false;
        ConnectionChanged?.Invoke(false);
        FailAllPending(new InvalidOperationException("RCon disconnected."));
    }

    private void HandlePacket(byte[] buf)
    {
        if (buf.Length < 8 || buf[0] != (byte)'B' || buf[1] != (byte)'E' || buf[6] != 0xFF)
            return;

        switch (buf[7])
        {
            case 0x02: // server message -> must be acknowledged
            {
                byte seq = buf[8];
                var ack = BuildPacket(0x02, new[] { seq });
                _ = _udp!.SendAsync(ack, ack.Length);
                if (buf.Length > 9)
                    ServerMessage?.Invoke(Enc.GetString(buf, 9, buf.Length - 9));
                break;
            }
            case 0x01: // command response (possibly multipart)
            {
                byte seq = buf[8];
                PendingCommand? p;
                lock (_pendingLock) { _pending.TryGetValue(seq, out p); }
                if (p is null) return; // keepalive echo or stale sequence

                if (buf.Length >= 12 && buf[9] == 0x00)
                {
                    int total = buf[10];
                    int index = buf[11];
                    var data = buf.Length > 12 ? Enc.GetString(buf, 12, buf.Length - 12) : "";
                    p.Parts[index] = data;
                    p.ExpectedParts = total;
                    if (p.Parts.Count >= total)
                    {
                        var sb = new StringBuilder();
                        foreach (var part in p.Parts.Values) sb.Append(part);
                        p.Tcs.TrySetResult(sb.ToString());
                    }
                }
                else
                {
                    var data = buf.Length > 9 ? Enc.GetString(buf, 9, buf.Length - 9) : "";
                    p.Tcs.TrySetResult(data);
                }
                break;
            }
        }
    }

    private async Task KeepAliveLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(25_000, ct);
                var idle = DateTime.UtcNow - new DateTime(Interlocked.Read(ref _lastSentTicks), DateTimeKind.Utc);
                if (idle.TotalSeconds < 20) continue;

                byte seq;
                lock (_seqLock) { seq = _sequence; _sequence = (byte)(_sequence + 1); }
                var packet = BuildPacket(0x01, new[] { seq }); // empty command = keepalive
                try
                {
                    await _udp!.SendAsync(packet, packet.Length);
                    Interlocked.Exchange(ref _lastSentTicks, DateTime.UtcNow.Ticks);
                }
                catch { /* receive loop will surface the disconnect */ }
            }
        }
        catch (OperationCanceledException) { }
    }

    private static byte[] BuildPacket(byte type, ReadOnlySpan<byte> payload)
    {
        var body = new byte[2 + payload.Length];
        body[0] = 0xFF;
        body[1] = type;
        payload.CopyTo(body.AsSpan(2));

        uint crc = Crc32.Compute(body);
        var packet = new byte[6 + body.Length];
        packet[0] = (byte)'B';
        packet[1] = (byte)'E';
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(2, 4), crc);
        body.CopyTo(packet.AsSpan(6));
        return packet;
    }

    private void FailAllPending(Exception ex)
    {
        lock (_pendingLock)
        {
            foreach (var p in _pending.Values) p.Tcs.TrySetException(ex);
            _pending.Clear();
        }
    }

    private async Task DisposeSocketAsync()
    {
        try { if (_cts is not null) await _cts.CancelAsync(); } catch { }
        try { _udp?.Dispose(); } catch { }
        _udp = null;
        _loggedIn = false;
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeSocketAsync();
        FailAllPending(new ObjectDisposedException(nameof(BeRconClient)));
    }
}
