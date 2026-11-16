namespace TheDeep;

/// <summary>How a room's back wall is dressed (<c>WallDressing</c>).</summary>
public enum WallStyle
{
    /// <summary>A foundry's wall: iron pilasters, girders, grated furnace mouths glowing, pipes, chains hanging (volcanic).</summary>
    Foundry,

    /// <summary>
    /// The deep works of the machine's cathedral: pistons on the bay lines, bands across, trains of meshed gears on their axles,
    /// banks of organ pipes stacked up the bays, glowing slits (the core).
    /// </summary>
    Machinery,

    /// <summary>An archive's wall: stone piers, iron shelves of server racks with gaps, icicles under the shelves (frozen).</summary>
    Archive,

    /// <summary>
    /// A cathedral's wall: a plinth, stone pilasters carrying a blind arcade of pointed arches (a rose in some tympana),
    /// string courses, tiers of lancet niches above, the odd arch fallen (flooded; the frozen layer's chapels).
    /// </summary>
    Nave,
}
