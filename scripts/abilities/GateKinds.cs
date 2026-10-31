using Godot;

namespace TheDeep;

/// <summary>
/// What each ability's gate is to the eye (metal, rings, cracked rock, deep water, heat): the color the map and the hints mark it
/// with, the line the pilgrim says on meeting it without the ability, and its word on the map's legend. A gate is always named by
/// what the pilgrim sees there, never by the ability it lacks.
/// </summary>
public static class GateKinds
{
    public static Color ColorOf(Ability ability) => ability switch
    {
        Ability.MagGrip => new Color(0.66f, 0.80f, 1f),
        Ability.Harpoon => new Color(1f, 0.84f, 0.46f),
        Ability.Drill => new Color(0.86f, 0.58f, 0.36f),
        Ability.Ballast => new Color(0.36f, 0.86f, 0.86f),
        Ability.HeatPlating => new Color(1f, 0.42f, 0.2f),
        _ => new Color(0.86f, 0.86f, 0.95f),
    };

    /// <summary>The line the pilgrim says meeting this gate without the ability.</summary>
    public static StringName HintLine(Ability ability) => $"hint.gate.{Key(ability)}";

    /// <summary>The gate's word on the map's legend.</summary>
    public static StringName MapWord(Ability ability) => $"map.gate.{Key(ability)}";

    private static string Key(Ability ability) => ability switch
    {
        Ability.MagGrip => "metal",
        Ability.Harpoon => "rings",
        Ability.Drill => "cracked",
        Ability.Ballast => "water",
        Ability.HeatPlating => "heat",
        _ => "none",
    };
}
