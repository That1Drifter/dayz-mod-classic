namespace DayZModClassic.Admin.Rcon;

public sealed record PlayerInfo(
    int Id,
    string Ip,
    int Port,
    int Ping,
    string Guid,
    bool Verified,
    string Name,
    bool InLobby);

public sealed record BanEntry(
    int Index,
    string Type, // "guid" | "ip"
    string Target,
    string MinutesLeft,
    string Reason);

public sealed record ChatLine(DateTimeOffset Time, string Text);

public sealed record RconStatus(
    bool Connected,
    int PlayerCount,
    DateTimeOffset? LastUpdate);
