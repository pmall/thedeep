using Godot;

namespace TheDeep;

/// <summary>
/// What each kind of item is to the eye: the color its pickup, its door and the map mark it with, and its word on the map's legend
/// (as <see cref="GateKinds"/> does for the abilities' gates).
/// </summary>
public static class ItemKinds
{
    public static Color ColorOf(ItemKind kind) => kind switch
    {
        ItemKind.Key => new Color(0.95f, 0.78f, 0.52f),
        ItemKind.Code => new Color(0.78f, 0.62f, 1f),
        ItemKind.Mechanism => new Color(0.55f, 0.95f, 0.72f),
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
