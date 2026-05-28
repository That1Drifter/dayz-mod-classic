using System.Globalization;
using System.Text.RegularExpressions;

namespace DayZModClassic.Admin.Hive;

// Helpers for the SQF-literal text stored in hive columns. Worldspace is parsed
// structurally (it drives the map and the teleport editor); other arrays
// (Inventory/Medical/Backpack) are edited as raw text with only a balance check,
// because a full structural round-trip is risky and unnecessary.
public static partial class SqfArray
{
    [GeneratedRegex(@"\[\s*(-?\d+(?:\.\d+)?)\s*,\s*\[\s*(-?\d+(?:\.\d+)?)\s*,\s*(-?\d+(?:\.\d+)?)\s*(?:,\s*(-?\d+(?:\.\d+)?))?\s*\]")]
    private static partial Regex WorldspaceRegex();

    public static bool TryParseWorldspace(string? ws, out double dir, out double x, out double y, out double z)
    {
        dir = x = y = z = 0;
        if (string.IsNullOrWhiteSpace(ws)) return false;

        var m = WorldspaceRegex().Match(ws);
        if (!m.Success) return false;

        dir = ParseD(m.Groups[1].Value);
        x = ParseD(m.Groups[2].Value);
        y = ParseD(m.Groups[3].Value);
        z = m.Groups[4].Success ? ParseD(m.Groups[4].Value) : 0;
        return true;
    }

    public static string BuildWorldspace(double dir, double x, double y, double z)
        => string.Create(CultureInfo.InvariantCulture, $"[{dir:0.###},[{x:0.###},{y:0.###},{z:0.###}]]");

    // Cheap structural sanity check: brackets balanced and no stray nesting underflow.
    public static bool LooksBalanced(string? s)
    {
        if (s is null) return false;
        int depth = 0;
        foreach (var c in s)
        {
            if (c == '[') depth++;
            else if (c == ']') { depth--; if (depth < 0) return false; }
        }
        return depth == 0;
    }

    private static double ParseD(string s)
        => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);
}
