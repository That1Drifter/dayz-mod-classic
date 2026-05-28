using System.Net;
using System.Security.Cryptography;
using System.Text;
using DayZModClassic.Admin.Options;
using Microsoft.Extensions.Options;

namespace DayZModClassic.Admin.Security;

// HTTP Basic auth in front of every route except /healthz. Defence-in-depth: Caddy
// also enforces basicauth, but the backend must not be open if exposed directly.
public sealed class BasicAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly AuthOptions _auth;
    private readonly ILogger<BasicAuthMiddleware> _log;
    private bool _warned;

    public BasicAuthMiddleware(RequestDelegate next, IOptions<AdminOptions> opts, ILogger<BasicAuthMiddleware> log)
    {
        _next = next;
        _auth = opts.Value.Auth;
        _log = log;
    }

    public async Task Invoke(HttpContext ctx)
    {
        if (ctx.Request.Path.StartsWithSegments("/healthz"))
        {
            await _next(ctx);
            return;
        }

        if (string.IsNullOrEmpty(_auth.Password))
        {
            if (!_warned)
            {
                _log.LogWarning("Admin:Auth:Password is empty. Backend auth is DISABLED. Set it for any non-loopback exposure.");
                _warned = true;
            }
            await _next(ctx);
            return;
        }

        string? header = ctx.Request.Headers.Authorization;
        if (header is not null && header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var raw = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
                int sep = raw.IndexOf(':');
                if (sep > 0)
                {
                    var user = raw[..sep];
                    var pass = raw[(sep + 1)..];
                    if (FixedTimeEquals(user, _auth.Username) && FixedTimeEquals(pass, _auth.Password))
                    {
                        await _next(ctx);
                        return;
                    }
                }
            }
            catch (FormatException) { /* malformed header -> challenge */ }
        }

        ctx.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        ctx.Response.Headers.WWWAuthenticate = "Basic realm=\"DayZ Mod Classic Admin\"";
    }

    private static bool FixedTimeEquals(string a, string b)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
