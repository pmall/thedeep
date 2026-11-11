using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// One kind of seam, shared by the rooms on both sides of a way (docs/gameplay.md#seam-rule): how big its opening is and how deep the
/// stretch before it, and the frame of solids it builds around one end. Each room frames its end of the way from this one definition,
/// centered on its exit's marker; the two markers meet at one place on the map, so the two frames lie on each other and the seam agrees
/// by construction. The frame fills everything around the opening that <see cref="SeamCheck"/> compares (<see cref="SeamCheck.Around"/>
/// past the exit's area, within both rooms' camera bounds): what lies beyond is each room's own.
/// A doorway's frame is a sill under the opening and a lintel over it, both running along the way. A hatch's is the slab either side of
/// the hole, as thick as the exit's area: past the slab lie the two rooms' own spaces, each bounding its camera at the slab's far face.
/// </summary>
/// <param name="Opening">How tall a doorway's opening is, or how wide a hatch's hole, meters.</param>
/// <param name="Approach">How deep the exit's area runs along the way, meters: a doorway's stretch of floor, a hatch's slab.</param>
public sealed record SeamFrame(float Opening, float Approach)
{
    /// <summary>The size (width, height) of an exit framed by this seam on <paramref name="side"/> (<see cref="Threshold.Size"/>).</summary>
    public Vector2 ExitSize(ExitSide side) =>
        side is ExitSide.Down or ExitSide.Up ? new Vector2(Opening, Approach) : new Vector2(Approach, Opening);

    /// <summary>The exit this seam makes on <paramref name="side"/>, its marker at <paramref name="marker"/>.</summary>
    public Threshold Exit(ExitSide side, Vector2 marker) => new(side, marker, ExitSize(side));

    /// <summary>
    /// The frame's solids around the exit on <paramref name="side"/> at <paramref name="marker"/>, on the play plane: everything the seam
    /// check compares there except the passage through the opening, which runs the whole way through.
    /// </summary>
    public IReadOnlyList<Box> Solids(ExitSide side, Vector2 marker)
    {
        Box area = Exit(side, marker).Area;
        float a = SeamCheck.Around;
        var near = new Box(area.MinX - a, area.MinY - a, area.MaxX + a, area.MaxY + a);
        return side is ExitSide.Down or ExitSide.Up
            ? new[] { area with { MinX = near.MinX, MaxX = area.MinX }, area with { MinX = area.MaxX, MaxX = near.MaxX } }
            : new[] { near with { MaxY = area.MinY }, near with { MinY = area.MaxY } };
    }
}
