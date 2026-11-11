using System;
using System.Collections.Generic;
using System.Linq;
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
    /// The frame's trim around the exit on <paramref name="side"/>, centered on its marker (origin), on the play plane: what makes the
    /// frame read as a portal built on purpose in any biome, the same at both ends of the way. Visual only (docs/art-direction.md#visuals-never-gameplay):
    /// molding stands proud of the frame's front face, never in the play plane, and each piece runs into the next or into the frame,
    /// never sharing a face's plane with it, so no two faces fight. A doorway's head is a cornice over a soffit band, a keystone
    /// straddling the seam, and a post at each end of the lintel and the sill that hides the joint against whatever wall the frame
    /// meets; its sill takes a threshold band under the walked line. A hatch's slab takes a collar
    /// post at each edge of the hole, a band under its room-side face and a post at each outer end.
    /// <paramref name="lined"/>: the frame is metal set in rock (a grip climb runs over it), so the faces the climb uses (a hatch's
    /// hole, a doorway's room-side ends) are lined with metal through the whole depth and their posts are metal.
    /// <paramref name="bracket"/>: a doorway's sill hangs free into the room (<see cref="SillHangs"/>), so a stepped corbel carries its
    /// room-side end.
    /// </summary>
    public IReadOnlyList<TrimPiece> Trim(ExitSide side, bool lined, bool bracket = false)
    {
        float t = SeamCheck.Around;
        var pieces = new List<TrimPiece>();
        if (side is ExitSide.Down or ExitSide.Up)
        {
            float w = Opening * 0.5f, e = w + t, half = Approach * 0.5f;
            float room = side == ExitSide.Down ? half : -half, dir = MathF.Sign(room);
            float near = room - dir * 0.06f, far = -room - dir * 0.08f;
            foreach (float s in new[] { -1f, 1f })
            {
                pieces.Add(new TrimPiece(Span(s, w - 0.08f, w + 0.4f, near, far), 0.22f, lined));
                pieces.Add(new TrimPiece(Span(s, w + 0.2f, e + 0.04f, room - dir * 0.3f, near), 0.14f, false));
                pieces.Add(new TrimPiece(Span(s, e - 0.4f, e + 0.08f, near, far), 0.18f, false));
                if (lined)
                {
                    pieces.Add(new TrimPiece(Span(s, w - 0.04f, w + 0.08f, -half - 0.02f, half + 0.02f), 0f, true, Through: true));
                }
            }

            return pieces;
        }

        float h = Approach * 0.5f + t, top = Opening + t, inward = side == ExitSide.West ? 1f : -1f;
        pieces.Add(new TrimPiece(new Box(-h - 0.15f, top - 0.3f, h + 0.15f, top + 0.06f), 0.22f, false));
        pieces.Add(new TrimPiece(new Box(-h - 0.06f, Opening - 0.1f, h + 0.06f, Opening + 0.22f), 0.12f, false));
        pieces.Add(new TrimPiece(new Box(-0.3f, Opening - 0.22f, 0.3f, top - 0.1f), 0.3f, false));
        pieces.Add(new TrimPiece(new Box(-h - 0.15f, -0.3f, h + 0.15f, -0.06f), 0.2f, false));
        foreach (float s in new[] { -1f, 1f })
        {
            bool liner = lined && s == inward;
            pieces.Add(new TrimPiece(Span(s, h - 0.4f, h + 0.08f, Opening - 0.16f, top - 0.2f), 0.16f, liner));
            pieces.Add(new TrimPiece(Span(s, h - 0.4f, h + 0.08f, -t - 0.08f, -0.2f), 0.14f, liner));
            if (bracket && s == inward)
            {
                pieces.Add(new TrimPiece(Span(s, h - 1.1f, h + 0.06f, -t - 0.3f, -t), 0.12f, false, Sink: 0.4f));
                pieces.Add(new TrimPiece(Span(s, h - 0.7f, h + 0.06f, -t - 0.65f, -t - 0.2f), 0.1f, false, Sink: 0.35f));
                pieces.Add(new TrimPiece(Span(s, h - 0.35f, h + 0.06f, -t - 1f, -t - 0.55f), 0.08f, false, Sink: 0.3f));
            }

            if (liner)
            {
                pieces.Add(new TrimPiece(Span(s, h - 0.08f, h + 0.04f, -t - 0.02f, 0.02f), 0f, true, Through: true));
                pieces.Add(new TrimPiece(Span(s, h - 0.08f, h + 0.04f, Opening - 0.02f, top + 0.02f), 0f, true, Through: true));
            }
        }

        return pieces;
    }

    /// <summary>
    /// Whether the sill of a doorway on <paramref name="side"/> at <paramref name="marker"/> hangs free into its room: none of the room's
    /// own <paramref name="solids"/> lies under its room-side end or meets the lower half of that end, so its trim brackets it.
    /// </summary>
    public bool SillHangs(ExitSide side, Vector2 marker, IReadOnlyList<Box> solids)
    {
        if (side is ExitSide.Down or ExitSide.Up)
        {
            return false;
        }

        Box sill = Solids(side, marker)[0];
        bool west = side == ExitSide.West;
        float end = west ? sill.MaxX : sill.MinX, inward = west ? 1f : -1f;
        Box under = new(MathF.Min(end, end - inward * 0.6f), sill.MinY - 0.1f, MathF.Max(end, end - inward * 0.6f), sill.MinY);
        Box beside = new(MathF.Min(end, end + inward * 0.1f), sill.MinY, MathF.Max(end, end + inward * 0.1f), sill.MinY + 0.5f * (sill.MaxY - sill.MinY));
        return !solids.Any(o => o.Overlaps(under) || o.Overlaps(beside));
    }

    /// <summary>
    /// Which of a room's own <paramref name="solids"/> the frame meets the most (the longest edge it shares with the frame's
    /// <paramref name="frame"/> solids), whose look the frame takes; null when none touches it.
    /// </summary>
    public static int? Neighbour(IReadOnlyList<Box> frame, IReadOnlyList<Box> solids)
    {
        const float touch = 0.05f;
        int? best = null;
        float longest = 0f;
        for (int i = 0; i < solids.Count; i++)
        {
            Box o = solids[i];
            float contact = 0f;
            foreach (Box f in frame)
            {
                if (!f.Grown(touch).Overlaps(o))
                {
                    continue;
                }

                float alongX = MathF.Min(f.MaxX, o.MaxX) - MathF.Max(f.MinX, o.MinX);
                float alongY = MathF.Min(f.MaxY, o.MaxY) - MathF.Max(f.MinY, o.MinY);
                contact += MathF.Max(alongX, alongY);
            }

            if (contact > longest)
            {
                (best, longest) = (i, contact);
            }
        }

        return best;
    }

    /// <summary>The span from <paramref name="from"/> to <paramref name="to"/> out from the center on the side <paramref name="s"/> (−1 or 1), between two heights.</summary>
    private static Box Span(float s, float from, float to, float y0, float y1) =>
        new(MathF.Min(s * from, s * to), MathF.Min(y0, y1), MathF.Max(s * from, s * to), MathF.Max(y0, y1));

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

/// <summary>
/// One piece of a seam frame's trim (<see cref="SeamFrame.Trim"/>), visual only: its <paramref name="Face"/> on the play plane, relative
/// to the exit's marker, standing <paramref name="Proud"/> meters out of the frame's front face; or, <paramref name="Through"/>, a
/// liner over a face running through the frame's whole depth. <paramref name="Lining"/>: metal, where a grip climb runs.
/// <paramref name="Sink"/>: how far behind the front face it starts, meters.
/// </summary>
public readonly record struct TrimPiece(Box Face, float Proud, bool Lining, bool Through = false, float Sink = 0.05f);

