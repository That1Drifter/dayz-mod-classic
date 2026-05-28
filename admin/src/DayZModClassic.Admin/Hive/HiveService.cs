using System.Data;
using System.Text.Json;
using DayZModClassic.Admin.Options;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace DayZModClassic.Admin.Hive;

// Read/write access to the HiveExt MySQL database (localhost only). Every mutating
// call snapshots the affected row to a JSON backup file first, so a bad edit is
// recoverable without touching the live schema.
public sealed class HiveService
{
    private readonly string _cs;
    private readonly string _instanceId;
    private readonly string _backupDir;
    private readonly ILogger<HiveService> _log;

    public HiveService(IOptions<AdminOptions> opts, IHostEnvironment env, ILogger<HiveService> log)
    {
        var o = opts.Value;
        _cs = o.Hive.ConnectionString;
        _instanceId = o.Hive.InstanceId.ToString();
        _backupDir = Path.IsPathRooted(o.Paths.Backups)
            ? o.Paths.Backups
            : Path.Combine(env.ContentRootPath, o.Paths.Backups);
        _log = log;
    }

    public bool Configured => !string.IsNullOrWhiteSpace(_cs);

    private async Task<MySqlConnection> OpenAsync(CancellationToken ct)
    {
        if (!Configured) throw new InvalidOperationException("Hive connection string is not configured.");
        var conn = new MySqlConnection(_cs);
        await conn.OpenAsync(ct);
        return conn;
    }

    // ---- Characters ----

    public async Task<List<CharacterRow>> ListCharactersAsync(bool aliveOnly, string? search, int limit, CancellationToken ct)
    {
        const string baseSql = @"
SELECT c.CharacterID, c.PlayerUID, p.PlayerName, c.Alive, c.InstanceID, c.Worldspace,
       c.Inventory, c.Backpack, c.Medical, c.Humanity, c.KillsZ, c.HeadshotsZ,
       c.KillsH, c.KillsB, c.Model, c.LastLogin
FROM character_data c
LEFT JOIN player_data p ON p.PlayerUID = c.PlayerUID
WHERE c.InstanceID = @inst {alive} {search}
ORDER BY c.LastLogin DESC
LIMIT @limit";

        string aliveClause = aliveOnly ? "AND c.Alive = 1" : "";
        string searchClause = string.IsNullOrWhiteSpace(search) ? "" : "AND (p.PlayerName LIKE @q OR c.PlayerUID LIKE @q)";
        string sql = baseSql.Replace("{alive}", aliveClause).Replace("{search}", searchClause);

        await using var conn = await OpenAsync(ct);
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@inst", _instanceId);
        cmd.Parameters.AddWithValue("@limit", limit);
        if (!string.IsNullOrWhiteSpace(search))
            cmd.Parameters.AddWithValue("@q", "%" + search + "%");

        var list = new List<CharacterRow>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            list.Add(MapCharacter(rd));
        return list;
    }

    public async Task<CharacterRow?> GetCharacterAsync(int id, CancellationToken ct)
    {
        const string sql = @"
SELECT c.CharacterID, c.PlayerUID, p.PlayerName, c.Alive, c.InstanceID, c.Worldspace,
       c.Inventory, c.Backpack, c.Medical, c.Humanity, c.KillsZ, c.HeadshotsZ,
       c.KillsH, c.KillsB, c.Model, c.LastLogin
FROM character_data c
LEFT JOIN player_data p ON p.PlayerUID = c.PlayerUID
WHERE c.CharacterID = @id";

        await using var conn = await OpenAsync(ct);
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        return await rd.ReadAsync(ct) ? MapCharacter(rd) : null;
    }

    public async Task<bool> UpdateCharacterAsync(int id, CharacterUpdate u, CancellationToken ct)
    {
        var current = await GetCharacterAsync(id, ct);
        if (current is null) return false;
        await BackupAsync($"character-{id}", current, ct);

        var sets = new List<string>();
        await using var conn = await OpenAsync(ct);
        await using var cmd = new MySqlCommand { Connection = conn };

        if (u.Alive is not null) { sets.Add("Alive = @alive"); cmd.Parameters.AddWithValue("@alive", u.Alive.Value ? 1 : 0); }
        if (u.Worldspace is not null) { sets.Add("Worldspace = @ws"); cmd.Parameters.AddWithValue("@ws", u.Worldspace); }
        if (u.Inventory is not null) { sets.Add("Inventory = @inv"); cmd.Parameters.AddWithValue("@inv", u.Inventory); }
        if (u.Backpack is not null) { sets.Add("Backpack = @bp"); cmd.Parameters.AddWithValue("@bp", u.Backpack); }
        if (u.Medical is not null) { sets.Add("Medical = @med"); cmd.Parameters.AddWithValue("@med", u.Medical); }
        if (u.Humanity is not null) { sets.Add("Humanity = @hum"); cmd.Parameters.AddWithValue("@hum", u.Humanity.Value); }
        if (u.Model is not null) { sets.Add("Model = @mdl"); cmd.Parameters.AddWithValue("@mdl", u.Model); }

        if (sets.Count == 0) return true;

        cmd.CommandText = $"UPDATE character_data SET {string.Join(", ", sets)} WHERE CharacterID = @id";
        cmd.Parameters.AddWithValue("@id", id);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    // ---- Objects (vehicles, tents, deployables) ----

    public async Task<List<ObjectRow>> ListObjectsAsync(string? classnameFilter, int limit, CancellationToken ct)
    {
        string sql = @"
SELECT ObjectID, ObjectUID, Instance, Classname, Damage, CharacterID, Worldspace, Inventory, Fuel
FROM object_data
WHERE Instance = @inst {cls}
ORDER BY ObjectID
LIMIT @limit";
        string clsClause = string.IsNullOrWhiteSpace(classnameFilter) ? "" : "AND Classname LIKE @cls";
        sql = sql.Replace("{cls}", clsClause);

        await using var conn = await OpenAsync(ct);
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@inst", _instanceId);
        cmd.Parameters.AddWithValue("@limit", limit);
        if (!string.IsNullOrWhiteSpace(classnameFilter))
            cmd.Parameters.AddWithValue("@cls", "%" + classnameFilter + "%");

        var list = new List<ObjectRow>();
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            list.Add(MapObject(rd));
        return list;
    }

    public async Task<ObjectRow?> GetObjectAsync(int id, CancellationToken ct)
    {
        const string sql = @"
SELECT ObjectID, ObjectUID, Instance, Classname, Damage, CharacterID, Worldspace, Inventory, Fuel
FROM object_data WHERE ObjectID = @id";
        await using var conn = await OpenAsync(ct);
        await using var cmd = new MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        return await rd.ReadAsync(ct) ? MapObject(rd) : null;
    }

    public async Task<bool> UpdateObjectAsync(int id, ObjectUpdate u, CancellationToken ct)
    {
        var current = await GetObjectAsync(id, ct);
        if (current is null) return false;
        await BackupAsync($"object-{id}", current, ct);

        var sets = new List<string>();
        await using var conn = await OpenAsync(ct);
        await using var cmd = new MySqlCommand { Connection = conn };

        if (u.Damage is not null) { sets.Add("Damage = @dmg"); cmd.Parameters.AddWithValue("@dmg", u.Damage); }
        if (u.Worldspace is not null) { sets.Add("Worldspace = @ws"); cmd.Parameters.AddWithValue("@ws", u.Worldspace); }
        if (u.Inventory is not null) { sets.Add("Inventory = @inv"); cmd.Parameters.AddWithValue("@inv", u.Inventory); }
        if (u.Fuel is not null) { sets.Add("Fuel = @fuel"); cmd.Parameters.AddWithValue("@fuel", u.Fuel); }

        if (sets.Count == 0) return true;

        cmd.CommandText = $"UPDATE object_data SET {string.Join(", ", sets)} WHERE ObjectID = @id";
        cmd.Parameters.AddWithValue("@id", id);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> DeleteObjectAsync(int id, CancellationToken ct)
    {
        var current = await GetObjectAsync(id, ct);
        if (current is null) return false;
        await BackupAsync($"object-delete-{id}", current, ct);

        await using var conn = await OpenAsync(ct);
        await using var cmd = new MySqlCommand("DELETE FROM object_data WHERE ObjectID = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<bool> PingAsync(CancellationToken ct)
    {
        try
        {
            await using var conn = await OpenAsync(ct);
            await using var cmd = new MySqlCommand("SELECT 1", conn);
            await cmd.ExecuteScalarAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogDebug(ex, "Hive ping failed.");
            return false;
        }
    }

    private async Task BackupAsync(string label, object row, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(_backupDir);
            var name = $"{label}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.json";
            var json = JsonSerializer.Serialize(row, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(Path.Combine(_backupDir, name), json, ct);
        }
        catch (Exception ex)
        {
            // A failed backup must not silently allow a destructive write.
            throw new InvalidOperationException($"Pre-write backup failed for {label}; aborting edit.", ex);
        }
    }

    private static CharacterRow MapCharacter(IDataRecord r)
    {
        var row = new CharacterRow
        {
            CharacterId = r.GetInt32(0),
            PlayerUid = GetStr(r, 1),
            PlayerName = IsNull(r, 2) ? null : r.GetString(2),
            Alive = r.GetInt32(3) != 0,
            InstanceId = GetStr(r, 4),
            Worldspace = GetStr(r, 5, "[]"),
            Inventory = GetStr(r, 6, "[]"),
            Backpack = GetStr(r, 7, "[]"),
            Medical = GetStr(r, 8, "[]"),
            Humanity = IsNull(r, 9) ? 0 : Convert.ToInt32(r.GetValue(9)),
            KillsZ = Convert.ToInt32(r.GetValue(10)),
            HeadshotsZ = Convert.ToInt32(r.GetValue(11)),
            KillsH = Convert.ToInt32(r.GetValue(12)),
            KillsB = Convert.ToInt32(r.GetValue(13)),
            Model = GetStr(r, 14),
            LastLogin = IsNull(r, 15) ? null : r.GetDateTime(15),
        };
        ApplyPosition(row.Worldspace, p => { row.X = p.x; row.Y = p.y; row.HasPosition = true; });
        return row;
    }

    private static ObjectRow MapObject(IDataRecord r)
    {
        var row = new ObjectRow
        {
            ObjectId = r.GetInt32(0),
            ObjectUid = IsNull(r, 1) ? null : r.GetString(1),
            Instance = IsNull(r, 2) ? null : r.GetString(2),
            Classname = IsNull(r, 3) ? null : r.GetString(3),
            Damage = IsNull(r, 4) ? null : r.GetString(4),
            CharacterId = IsNull(r, 5) ? null : Convert.ToInt32(r.GetValue(5)),
            Worldspace = IsNull(r, 6) ? null : r.GetString(6),
            Inventory = IsNull(r, 7) ? null : r.GetString(7),
            Fuel = IsNull(r, 8) ? null : r.GetString(8),
        };
        ApplyPosition(row.Worldspace, p => { row.X = p.x; row.Y = p.y; row.HasPosition = true; });
        return row;
    }

    private static void ApplyPosition(string? ws, Action<(double x, double y)> set)
    {
        if (SqfArray.TryParseWorldspace(ws, out _, out var x, out var y, out _))
            set((x, y));
    }

    private static bool IsNull(IDataRecord r, int i) => r.IsDBNull(i);
    private static string GetStr(IDataRecord r, int i, string fallback = "")
        => r.IsDBNull(i) ? fallback : r.GetString(i);
}
