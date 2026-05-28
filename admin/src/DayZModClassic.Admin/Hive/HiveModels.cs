namespace DayZModClassic.Admin.Hive;

public sealed class CharacterRow
{
    public int CharacterId { get; set; }
    public string PlayerUid { get; set; } = "";
    public string? PlayerName { get; set; }
    public bool Alive { get; set; }
    public string InstanceId { get; set; } = "";
    public string Worldspace { get; set; } = "[]";
    public string Inventory { get; set; } = "[]";
    public string Backpack { get; set; } = "[]";
    public string Medical { get; set; } = "[]";
    public int Humanity { get; set; }
    public int KillsZ { get; set; }
    public int HeadshotsZ { get; set; }
    public int KillsH { get; set; }
    public int KillsB { get; set; }
    public string Model { get; set; } = "";
    public DateTime? LastLogin { get; set; }

    // Convenience for the map / list view.
    public double X { get; set; }
    public double Y { get; set; }
    public bool HasPosition { get; set; }
}

public sealed class ObjectRow
{
    public int ObjectId { get; set; }
    public string? ObjectUid { get; set; }
    public string? Instance { get; set; }
    public string? Classname { get; set; }
    public string? Damage { get; set; }
    public int? CharacterId { get; set; }
    public string? Worldspace { get; set; }
    public string? Inventory { get; set; }
    public string? Fuel { get; set; }

    public double X { get; set; }
    public double Y { get; set; }
    public bool HasPosition { get; set; }
}

// Editable fields only. Null = leave unchanged.
public sealed class CharacterUpdate
{
    public bool? Alive { get; set; }
    public string? Worldspace { get; set; }
    public string? Inventory { get; set; }
    public string? Backpack { get; set; }
    public string? Medical { get; set; }
    public int? Humanity { get; set; }
    public string? Model { get; set; }
}

public sealed class ObjectUpdate
{
    public string? Damage { get; set; }
    public string? Worldspace { get; set; }
    public string? Inventory { get; set; }
    public string? Fuel { get; set; }
}
