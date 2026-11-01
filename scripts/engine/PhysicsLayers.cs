namespace TheDeep;

/// <summary>
/// Physics layer bits, mirroring [layer_names] in project.godot (layer N = bit N-1).
/// Rock versus metal is decided by these layers, never by visuals.
/// </summary>
public static class PhysicsLayers
{
    public const uint WorldRock = 1u << 0;
    public const uint WorldMetal = 1u << 1;
    public const uint Player = 1u << 2;
    public const uint Enemy = 1u << 3;
    public const uint Hazard = 1u << 4;
    public const uint Water = 1u << 5;
    public const uint Trigger = 1u << 6;

    /// <summary>Everything the player stands on or collides with.</summary>
    public const uint Solid = WorldRock | WorldMetal;
}
