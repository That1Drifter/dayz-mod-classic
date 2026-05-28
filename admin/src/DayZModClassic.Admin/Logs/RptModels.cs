namespace DayZModClassic.Admin.Logs;

public sealed record PlayerPos(string Name, string Uid, double X, double Y, bool Alive);

public sealed record PositionSnapshot(
    DateTimeOffset Time,
    int GameTime,
    IReadOnlyList<PlayerPos> Players);
