using Godot;

namespace TheDeep;

/// <summary>
/// A kind of seam, as content: the one definition both rooms of a way frame their ends from (<see cref="SeamFrame"/>, where its
/// rule lives), so the two sides agree by construction. A world uses as many as it has kinds of opening (<c>resources/seams/</c>).
/// </summary>
[GlobalClass]
public partial class SeamProfile : Resource
{
    /// <summary>How tall a doorway's opening is (or how wide a hatch's hole), meters.</summary>
    [Export] public float Opening { get; set; } = 4f;

    /// <summary>How deep the exit's area runs along the way, meters: a doorway's stretch of floor before the opening, a hatch's slab.</summary>
    [Export] public float Approach { get; set; } = 1f;

    /// <summary>How deep the frame's solids reach across the play plane, meters (a block's Z size).</summary>
    [Export] public float Depth { get; set; } = 4f;

    /// <summary>The seam's rule in the core.</summary>
    public SeamFrame Frame => new(Opening, Approach);
}
