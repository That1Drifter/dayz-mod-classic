using DayZModClassic.Admin.Endpoints;
using DayZModClassic.Admin.Hive;
using DayZModClassic.Admin.Logs;
using DayZModClassic.Admin.Options;
using DayZModClassic.Admin.Rcon;
using DayZModClassic.Admin.Security;
using DayZModClassic.Admin.Server;

// Anchor content/web root at the exe directory so appsettings + wwwroot resolve
// regardless of the service's working directory.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection(AdminOptions.SectionName));

builder.Services.AddSingleton<RconService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RconService>());

builder.Services.AddSingleton<RptTailService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RptTailService>());

builder.Services.AddSingleton<RestartScheduler>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RestartScheduler>());

builder.Services.AddSingleton<HiveService>();

var app = builder.Build();

app.UseMiddleware<BasicAuthMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/healthz", () => Results.Ok("ok"));

// ----- Status -----
app.MapGet("/api/status", async (RconService rcon, HiveService hive, RestartScheduler sched, CancellationToken ct) => Results.Ok(new
{
    rconConfigured = rcon.Configured,
    rcon = rcon.Status,
    hiveConfigured = hive.Configured,
    hiveOnline = hive.Configured && await hive.PingAsync(ct),
    restart = sched.Status,
}));

// ----- Players (Phase 0 read + Phase 1 actions) -----
var players = app.MapGroup("/api/players");

players.MapGet("/", (RconService rcon) => Results.Ok(rcon.CachedPlayers));
players.MapPost("/refresh", (RconService rcon, CancellationToken ct) => RconResult(() => rcon.RefreshPlayersAsync(ct)));
players.MapPost("/{id:int}/kick", (int id, KickRequest req, RconService rcon, CancellationToken ct) =>
    RconText(() => rcon.KickAsync(id, req.Reason ?? "Kicked by admin", ct)));
players.MapPost("/{id:int}/ban", (int id, BanPlayerRequest req, RconService rcon, CancellationToken ct) =>
    RconText(() => rcon.BanPlayerAsync(id, req.Minutes, req.Reason ?? "Banned by admin", ct)));
players.MapPost("/{id:int}/say", (int id, SayRequest req, RconService rcon, CancellationToken ct) =>
    RconText(() => rcon.SayPlayerAsync(id, req.Message, ct)));

// ----- Bans -----
var bans = app.MapGroup("/api/bans");
bans.MapGet("/", (RconService rcon, CancellationToken ct) => RconResult(() => rcon.GetBansAsync(ct)));
bans.MapPost("/add", (AddBanRequest req, RconService rcon, CancellationToken ct) =>
    RconText(() => rcon.AddBanAsync(req.Guid, req.Minutes, req.Reason ?? "Banned by admin", ct)));
bans.MapDelete("/{index:int}", (int index, RconService rcon, CancellationToken ct) =>
    RconText(() => rcon.RemoveBanAsync(index, ct)));

// ----- Server control (Phase 1) -----
var server = app.MapGroup("/api/server");
server.MapPost("/say", (SayRequest req, RconService rcon, CancellationToken ct) => RconText(() => rcon.SayGlobalAsync(req.Message, ct)));
server.MapPost("/lock", (RconService rcon, CancellationToken ct) => RconText(() => rcon.LockAsync(ct)));
server.MapPost("/unlock", (RconService rcon, CancellationToken ct) => RconText(() => rcon.UnlockAsync(ct)));
server.MapPost("/shutdown", (RconService rcon, CancellationToken ct) => RconText(() => rcon.ShutdownAsync(ct)));
server.MapPost("/restart", (RconService rcon, CancellationToken ct) => RconText(() => rcon.RestartMissionAsync(ct)));
server.MapPost("/raw", (RawCommandRequest req, RconService rcon, CancellationToken ct) => RconText(() => rcon.SendRawAsync(req.Command, ct)));
server.MapPost("/schedule-restart", (ScheduleRestartRequest req, RestartScheduler sched) =>
{
    if (req.InMinutes <= 0) return Results.BadRequest("InMinutes must be positive.");
    sched.Arm(req.InMinutes, req.WarnMinutes);
    return Results.Ok(sched.Status);
});
server.MapPost("/schedule-restart/cancel", (RestartScheduler sched) => { sched.Cancel(); return Results.Ok(sched.Status); });

// ----- Logs + chat (Phase 0 read) -----
app.MapGet("/api/logs", (int? lines, RptTailService rpt) => Results.Ok(rpt.Tail(lines ?? 200)));
app.MapGet("/api/chat", (int? lines, RconService rcon) => Results.Ok(rcon.RecentChat(lines ?? 200)));

// ----- Live map (Phase 2) -----
app.MapGet("/api/map", async (RconService rcon, RptTailService rpt, HiveService hive, Microsoft.Extensions.Options.IOptions<AdminOptions> opts, CancellationToken ct) =>
{
    var snap = rpt.Snapshot;
    List<ObjectRow> vehicles = new();
    if (hive.Configured)
    {
        try { vehicles = (await hive.ListObjectsAsync(null, 1000, ct)).Where(o => o.HasPosition).ToList(); }
        catch { /* map still works without vehicle layer */ }
    }
    return Results.Ok(new
    {
        worldSize = opts.Value.Map.WorldSize,
        snapshotTime = snap.Time,
        gameTime = snap.GameTime,
        players = snap.Players,
        vehicles,
    });
});

// ----- Hive DB editor (Phase 3) -----
var dbc = app.MapGroup("/api/hive/characters");
dbc.MapGet("/", (bool? aliveOnly, string? search, int? limit, HiveService hive, CancellationToken ct) =>
    HiveResult(() => hive.ListCharactersAsync(aliveOnly ?? false, search, limit ?? 200, ct)));
dbc.MapGet("/{id:int}", async (int id, HiveService hive, CancellationToken ct) =>
{
    if (!hive.Configured) return Results.StatusCode(503);
    var c = await hive.GetCharacterAsync(id, ct);
    return c is null ? Results.NotFound() : Results.Ok(c);
});
dbc.MapPut("/{id:int}", async (int id, CharacterUpdate u, HiveService hive, RconService rcon, CancellationToken ct) =>
{
    if (!hive.Configured) return Results.StatusCode(503);
    if (!ValidateArrays(u, out var err)) return Results.BadRequest(err);

    var current = await hive.GetCharacterAsync(id, ct);
    if (current is null) return Results.NotFound();
    // Guardrail: never write a character whose owner is connected (hive would overwrite on save).
    if (rcon.CachedPlayers.Any(p => string.Equals(p.Guid, current.PlayerUid, StringComparison.OrdinalIgnoreCase)))
        return Results.Conflict("Player is online. Edit characters only while the owner is disconnected.");

    return await hive.UpdateCharacterAsync(id, u, ct) ? Results.Ok() : Results.NotFound();
});

var dbo = app.MapGroup("/api/hive/objects");
dbo.MapGet("/", (string? classname, int? limit, HiveService hive, CancellationToken ct) =>
    HiveResult(() => hive.ListObjectsAsync(classname, limit ?? 500, ct)));
dbo.MapGet("/{id:int}", async (int id, HiveService hive, CancellationToken ct) =>
{
    if (!hive.Configured) return Results.StatusCode(503);
    var o = await hive.GetObjectAsync(id, ct);
    return o is null ? Results.NotFound() : Results.Ok(o);
});
dbo.MapPut("/{id:int}", async (int id, ObjectUpdate u, HiveService hive, CancellationToken ct) =>
{
    if (!hive.Configured) return Results.StatusCode(503);
    if ((u.Worldspace is not null && !SqfArray.LooksBalanced(u.Worldspace)) ||
        (u.Inventory is not null && !SqfArray.LooksBalanced(u.Inventory)))
        return Results.BadRequest("Unbalanced brackets in an SQF array field.");
    return await hive.UpdateObjectAsync(id, u, ct) ? Results.Ok() : Results.NotFound();
});
dbo.MapDelete("/{id:int}", async (int id, HiveService hive, CancellationToken ct) =>
{
    if (!hive.Configured) return Results.StatusCode(503);
    return await hive.DeleteObjectAsync(id, ct) ? Results.Ok() : Results.NotFound();
});

app.Run();

// ----- helpers -----

static bool ValidateArrays(CharacterUpdate u, out string error)
{
    foreach (var (name, val) in new[]
    {
        ("Worldspace", u.Worldspace), ("Inventory", u.Inventory),
        ("Backpack", u.Backpack), ("Medical", u.Medical),
    })
    {
        if (val is not null && !SqfArray.LooksBalanced(val))
        {
            error = $"Unbalanced brackets in {name}.";
            return false;
        }
    }
    error = "";
    return true;
}

static async Task<IResult> RconResult<T>(Func<Task<T>> action)
{
    try { return Results.Ok(await action()); }
    catch (InvalidOperationException ex) { return Results.Json(new { error = ex.Message }, statusCode: 503); }
    catch (OperationCanceledException) { return Results.Json(new { error = "RCon command timed out." }, statusCode: 504); }
}

static async Task<IResult> RconText(Func<Task<string>> action)
{
    try { return Results.Ok(new { response = await action() }); }
    catch (InvalidOperationException ex) { return Results.Json(new { error = ex.Message }, statusCode: 503); }
    catch (OperationCanceledException) { return Results.Json(new { error = "RCon command timed out." }, statusCode: 504); }
}

static async Task<IResult> HiveResult<T>(Func<Task<T>> action)
{
    try { return Results.Ok(await action()); }
    catch (InvalidOperationException ex) { return Results.Json(new { error = ex.Message }, statusCode: 503); }
}
