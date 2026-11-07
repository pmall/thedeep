using Godot;

namespace TheDeep;

/// <summary>What each stat a power-up raises is to the eye and in words: the color its crystal glows (a warm coral for strength, an azure
/// for defense: apart in hue and both saturated; the shapes differ too, <see cref="PowerUp"/>), and its lines (its name, and what it
/// does, shown when taken).</summary>
public static class PowerStats
{
    public static Color ColorOf(PowerStat stat) => stat switch
    {
        PowerStat.Strength => new Color(1f, 0.4f, 0.3f),
        PowerStat.Defense => new Color(0.18f, 0.5f, 1f),
        _ => new Color(0.86f, 0.86f, 0.95f),
    };

    public static StringName NameLine(PowerStat stat) => $"powerup.{Key(stat)}.name";

    public static StringName HintLine(PowerStat stat) => $"powerup.{Key(stat)}.hint";

    private static string Key(PowerStat stat) => stat switch
    {
        PowerStat.Strength => "strength",
        PowerStat.Defense => "defense",
        _ => "none",
    };
}
