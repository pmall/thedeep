using System;
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
/// <param name="HeadRise">
/// For a hatch a lift rides up through: how far the shaft's head, the upper room's lift, carries the rider from the frame's top (where
/// the lower room's lift hands it over, where a body crossing up arrives) on up to the floor, meters (<see cref="Lift"/>).
/// 0: no head, the frame's top is the floor.
/// </param>
public sealed record SeamFrame(float Opening, float Approach, float HeadRise = 0f)
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

    /// <summary>
    /// The part of the opening a flooded way's water runs through, from the exit's marker out to the frame's outer face (a hatch's slab
    /// face, a doorway's far end), on the play plane: what the room's water must fill past what its own front shows, so the water
    /// continues through the opening into the other room's (docs/gameplay.md#seam-rule). The room side of the marker is the room's water.
    /// </summary>
    public Box Passage(ExitSide side, Vector2 marker)
    {
        Box area = Exit(side, marker).Area;
        float a = SeamCheck.Around;
        return side switch
        {
            ExitSide.Down => area with { MaxY = marker.Y },
            ExitSide.Up => area with { MinY = marker.Y },
            ExitSide.West => area with { MinX = area.MinX - a, MaxX = marker.X },
            _ => area with { MinX = marker.X, MaxX = area.MaxX + a },
        };
    }

    /// <summary>
    /// A column of heat rising through the hatch on <paramref name="side"/> at <paramref name="marker"/> (docs/gameplay.md#heat), on the
    /// play plane: as wide as the frame and centred on the hole, so the two rooms' columns are one column through the seam by
    /// construction, sealing the hole where the room's walls meet the frame. Under an up exit it rises from <paramref name="end"/> (its
    /// foot, the vent) to the slab's underside; over a down exit, from the slab's top to <paramref name="end"/> (the roof or hood over it).
    /// </summary>
    public Box Heat(ExitSide side, Vector2 marker, float end)
    {
        Box area = Exit(side, marker).Area;
        float a = SeamCheck.Around;
        return side switch
        {
            ExitSide.Up when end < area.MinY => new Box(area.MinX - a, end, area.MaxX + a, area.MinY),
            ExitSide.Down when end > area.MaxY => new Box(area.MinX - a, area.MaxY, area.MaxX + a, end),
            _ => throw new ArgumentException($"no heat rises through a {side} exit of this seam at {marker} to {end} m"),
        };
    }

    /// <summary>
    /// A lift whose cage rides up through the hatch on <paramref name="side"/> at <paramref name="marker"/>: its lower stop (the cage
    /// floor's center) and how far it rises (docs/gameplay.md#seam-rule). Under an up exit, the lower room's lift stands in line with the
    /// hole at <paramref name="floor"/> and rises to the hand-off, the frame's top, where the crossing body arrives; over a down exit,
    /// the shaft's head waits at the hand-off and rises <see cref="HeadRise"/> to the floor. The two markers meet on the map, so both rooms
    /// find the same hand-off: the shaft runs on through the seam as wide and in line by construction.
    /// </summary>
    /// <param name="floor">Under an up exit, the height of the lift's own lower stop (the floor of the shaft's foot); else unused.</param>
    public (Vector2 LowerStop, float Rise) Lift(ExitSide side, Vector2 marker, float floor)
    {
        float handOff = marker.Y + Approach * 0.5f;
        return side switch
        {
            ExitSide.Up when handOff > floor => (new Vector2(marker.X, floor), handOff - floor),
            ExitSide.Down when HeadRise > 0f => (new Vector2(marker.X, handOff), HeadRise),
            _ => throw new ArgumentException($"no lift rides through a {side} exit of this seam from {floor} m (head rise {HeadRise} m)"),
        };
    }
}
