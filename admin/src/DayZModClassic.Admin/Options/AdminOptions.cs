namespace DayZModClassic.Admin.Options;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    public RconOptions Rcon { get; set; } = new();
    public HiveOptions Hive { get; set; } = new();
    public PathOptions Paths { get; set; } = new();
    public AuthOptions Auth { get; set; } = new();
    public MapOptions Map { get; set; } = new();
}

public sealed class RconOptions
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 2302;
    public string Password { get; set; } = "";
}

public sealed class HiveOptions
{
    public string ConnectionString { get; set; } = "";

    // Instance the mission runs under (dayZ_instance in init.sqf). Used to scope hive queries.
    public int InstanceId { get; set; } = 222;
}

public sealed class PathOptions
{
    public string Rpt { get; set; } = "";
    public string BeBans { get; set; } = "";

    // Directory for pre-write row snapshots taken before any hive edit/delete.
    public string Backups { get; set; } = "backups";
}

public sealed class AuthOptions
{
    public string Username { get; set; } = "admin";
    public string Password { get; set; } = "";
}

public sealed class MapOptions
{
    public int WorldSize { get; set; } = 15360;
}
