namespace TheDeep;

/// <summary>The architecture standing behind a room, by depth layer (a <see cref="BiomeProfile"/> choice).</summary>
public enum BackdropStyle
{
    /// <summary>Cathedral naves: clustered piers carrying pointed arches, slimmer arcaded tiers above, a few shafts running
    /// up the piers' faces (the flooded layer).</summary>
    Nave,

    /// <summary>Archive stacks: towers of racks, walkways between them fringed with icicles (the frozen layer).</summary>
    Stacks,

    /// <summary>A foundry: furnaces with their stacks, gantries and ducts between the stacks, tanks on the gantries, chains
    /// hanging from them, a few furnace mouths glowing in the farthest row (the volcanic layer).</summary>
    Foundry,

    /// <summary>Ancient machinery: great gears on their axles, pistons between them (the core).</summary>
    Machinery,
}
