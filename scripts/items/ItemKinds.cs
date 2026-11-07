using Godot;

namespace TheDeep;

/// <summary>
/// What each kind of item is to the eye: the color its pickup, its door and the map mark it with, and its word on the map's legend
/// (as <see cref="GateKinds"/> does for the abilities' gates). The colors are saturated and far apart in hue (a key's gold, a code's
/// green, a mechanism's chartreuse), none of them a biome's accent, so each reads in every biome's light, the brine's teal and the
/// furnace's red among them.
/// </summary>
public static class ItemKinds
{
    public static Color ColorOf(ItemKind kind) => kind switch
    {
        ItemKind.Key => new Color(1f, 0.68f, 0.26f),
        ItemKind.Code => new Color(0.32f, 1f, 0.42f),
        ItemKind.Mechanism => new Color(0.72f, 1f, 0.3f),
        _ => new Color(0.86f, 0.86f, 0.95f),
    };

    /// <summary>Whether the pilgrim carries it (a key, a code), so the map lists it; a mechanism's state stays in the world.</summary>
    public static bool Carried(ItemKind kind) => kind != ItemKind.Mechanism;

    /// <summary>The door's word on the map's legend.</summary>
    public static StringName MapWord(ItemKind kind) => kind switch
    {
        ItemKind.Key => "map.gate.lock",
        ItemKind.Code => "map.gate.seal",
        ItemKind.Mechanism => "map.gate.mechanism",
        _ => "map.gate.lock",
    };
}
