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
    private static readonly StringName LockWord = "map.gate.lock";
    private static readonly StringName SealWord = "map.gate.seal";
    private static readonly StringName MechanismWord = "map.gate.mechanism";

    /// <summary>The color of an item of <paramref name="kind"/> and of the lock it opens, on the pickup, the door and
    /// the map.</summary>
    public static Color ColorOf(ItemKind kind) => kind switch
    {
        ItemKind.Key => new Color(1f, 0.68f, 0.26f),
        ItemKind.Code => new Color(0.32f, 1f, 0.42f),
        ItemKind.Mechanism => new Color(0.72f, 1f, 0.3f),
        _ => throw new System.ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>The door's word on the map's legend.</summary>
    public static StringName MapWord(ItemKind kind) => kind switch
    {
        ItemKind.Key => LockWord,
        ItemKind.Code => SealWord,
        ItemKind.Mechanism => MechanismWord,
        _ => throw new System.ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
