using Godot;

namespace TheDeep;

/// <summary>
/// The seven materials a <see cref="WallDressing"/> is built with, as one resource a world swaps whole
/// (resources/stage/wall_palette.tres is the prototype's; wall_palette_machinery.tres darkens the plating for machinery walls,
/// where the core's pale bronze outshone the play plane and its grippable metal).
/// </summary>
[GlobalClass]
public partial class WallPalette : Resource
{
    /// <summary>Iron: pilasters, girders, pipes, chains, shelves.</summary>
    [Export] public Material Iron { get; set; } = null!;

    /// <summary>Plating: panels, wheels, pistons, racks, hubs.</summary>
    [Export] public Material Plate { get; set; } = null!;

    /// <summary>What glows in a furnace mouth.</summary>
    [Export] public Material Molten { get; set; } = null!;

    /// <summary>A slit's light.</summary>
    [Export] public Material Glow { get; set; } = null!;

    /// <summary>Masonry: an archive's piers, a nave's arcades, plinths and courses.</summary>
    [Export] public Material Stone { get; set; } = null!;

    /// <summary>Ice: icicles under shelves and courses.</summary>
    [Export] public Material Ice { get; set; } = null!;

    /// <summary>The shadowed backs of a nave's niches, blind arches and rose.</summary>
    [Export] public Material Recess { get; set; } = null!;
}
