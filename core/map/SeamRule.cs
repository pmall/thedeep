
namespace TheDeep;

/// <summary>What must agree across an exit (docs/gameplay.md#leaving-a-room), one closed set: the rule a seam problem breaks.</summary>
public enum SeamRule
{
    /// <summary>The opening is as wide (a hatch) or as tall (a doorway) on both sides.</summary>
    Opening,

    /// <summary>Around the opening, what both rooms show is solid, water or open in both.</summary>
    Solids,

    /// <summary>Water rests on a floor, or continues through a hole into water on the other side.</summary>
    Water,

    /// <summary>A lift that rises through a hatch has its car over the matching hatch on the other side.</summary>
    Lift,
}
