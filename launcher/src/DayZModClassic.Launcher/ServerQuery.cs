using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DayZModClassic.Launcher;

/// <summary>
/// Steam A2S_INFO query (Source Engine Query protocol).
/// Used by Arma 2 / DayZ servers exposed via Steam GameSpy bridge.
/// Header: 0xFF 0xFF 0xFF 0xFF 'T' "Source Engine Query\0"
/// </summary>
public static class ServerQuery
{
    private static readonly byte[] A2S_INFO = BuildRequest();

    private static byte[] BuildRequest()
    {
        var prefix = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, (byte)'T' };
        var payload = Encoding.ASCII.GetBytes("Source Engine Query\0");
        var buf = new byte[prefix.Length + payload.Length];
        Buffer.BlockCopy(prefix, 0, buf, 0, prefix.Length);
        Buffer.BlockCopy(payload, 0, buf, prefix.Length, payload.Length);
        return buf;
    }

    public record QueryResult(bool Ok, int? PlayerCount, int? MaxPlayers, int? PingMs, string Status);

    public static async Task<QueryResult> QueryAsync(string host, int port, int timeoutMs = 1000, CancellationToken ct = default)
    {
        // Arma 2 / DayZ Mod servers respond to A2S_INFO on the SERVER PORT + 1 (Steam query port).
        // The classic DayZ servers historically use port and port+1. Try port+1 first, fall back to port.
        var stopwatch = Stopwatch.StartNew();
        foreach (var qp in new[] { port + 1, port })
        {
            try
            {
                using var udp = new UdpClient();
                udp.Client.ReceiveTimeout = timeoutMs;
                udp.Client.SendTimeout = timeoutMs;

                IPEndPoint? remote;
                try
                {
                    var ips = await Dns.GetHostAddressesAsync(host, ct);
                    if (ips.Length == 0) return new QueryResult(false, null, null, null, "error");
                    remote = new IPEndPoint(ips[0], qp);
                }
                catch
                {
                    return new QueryResult(false, null, null, null, "error");
                }

                udp.Connect(remote);

                stopwatch.Restart();
                await udp.SendAsync(A2S_INFO, A2S_INFO.Length).WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), ct);

                var recvTask = udp.ReceiveAsync();
                var winner = await Task.WhenAny(recvTask, Task.Delay(timeoutMs, ct));
                if (winner != recvTask) continue; // timeout, try next port
                stopwatch.Stop();

                var resp = recvTask.Result.Buffer;
                var parsed = ParseInfo(resp);
                if (parsed.Ok)
                    return new QueryResult(true, parsed.PlayerCount, parsed.MaxPlayers, (int)stopwatch.ElapsedMilliseconds, "ok");
            }
            catch (OperationCanceledException)
            {
                return new QueryResult(false, null, null, null, "timeout");
            }
            catch
            {
                // try the other port
            }
        }
        return new QueryResult(false, null, null, null, "timeout");
    }

    private static (bool Ok, int? PlayerCount, int? MaxPlayers) ParseInfo(byte[] resp)
    {
        // Expected: 0xFF 0xFF 0xFF 0xFF (header) then 'I' (0x49) for Source response.
        // Some legacy GoldSrc returns 'm' (0x6D). We support both.
        try
        {
            if (resp.Length < 6) return (false, null, null);
            int i = 0;
            if (!(resp[0] == 0xFF && resp[1] == 0xFF && resp[2] == 0xFF && resp[3] == 0xFF))
                return (false, null, null);
            i = 4;
            byte header = resp[i++];

            if (header == 0x49) // 'I' - Source response
            {
                i += 1; // protocol byte
                i = SkipString(resp, i); // server name
                i = SkipString(resp, i); // map
                i = SkipString(resp, i); // folder
                i = SkipString(resp, i); // game
                if (i + 4 > resp.Length) return (false, null, null);
                i += 2; // appid (short)
                int players = resp[i++];
                int max = resp[i++];
                return (true, players, max);
            }
            if (header == 0x6D) // 'm' - GoldSrc response
            {
                i = SkipString(resp, i); // address
                i = SkipString(resp, i); // name
                i = SkipString(resp, i); // map
                i = SkipString(resp, i); // folder
                i = SkipString(resp, i); // game
                if (i + 2 > resp.Length) return (false, null, null);
                int players = resp[i++];
                int max = resp[i++];
                return (true, players, max);
            }
            return (false, null, null);
        }
        catch
        {
            return (false, null, null);
        }
    }

    private static int SkipString(byte[] buf, int start)
    {
        int i = start;
        while (i < buf.Length && buf[i] != 0) i++;
        return Math.Min(buf.Length, i + 1);
    }
}
