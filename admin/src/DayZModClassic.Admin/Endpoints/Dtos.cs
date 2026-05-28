namespace DayZModClassic.Admin.Endpoints;

public sealed record RawCommandRequest(string Command);
public sealed record KickRequest(string? Reason);
public sealed record BanPlayerRequest(int Minutes, string? Reason);
public sealed record AddBanRequest(string Guid, int Minutes, string? Reason);
public sealed record SayRequest(string Message);
public sealed record ScheduleRestartRequest(int InMinutes, int[]? WarnMinutes);
