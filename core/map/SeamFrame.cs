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
/// Past a doorway's frame stands the wall it is cut in (<see cref="Wall"/>), each room's own.
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
    /// The frame's trim around the exit on <paramref name="side"/>, centered on its marker (origin), on the play plane: one quiet line,
    /// the same at both ends of the way; the doorway reads as a hole in a wall by its <see cref="Wall"/>, not by ornament. Visual only
    /// (docs/art-direction.md#visuals-never-gameplay): each piece stands proud of the frame's front face, never in the play plane, and
    /// runs into the frame, never sharing a face's plane with it. A doorway's sill takes a threshold band under the walked line; a
    /// hatch's slab, a plain curb at each edge of the hole.
    /// <paramref name="lined"/>: the frame is metal set in rock (a grip climb runs over it), so the faces the climb uses (a hatch's
    /// hole, a doorway's room-side ends) are lined with metal through the whole depth.
    /// <paramref name="bracket"/>: a doorway's sill hangs free into the room (no wall under it, <see cref="SillHangs"/>), so a single
    /// bracket carries its room-side end.
    /// </summary>
    public IReadOnlyList<TrimPiece> Trim(ExitSide side, bool lined, bool bracket = false)
    {
        float t = SeamCheck.Around;
        var pieces = new List<TrimPiece>();
        if (side is ExitSide.Down or ExitSide.Up)
        {
            float w = Opening * 0.5f, half = Approach * 0.5f;
            float room = side == ExitSide.Down ? half : -half, dir = MathF.Sign(room);
            float near = room - dir * 0.06f, far = -room - dir * 0.08f;
            foreach (float s in new[] { -1f, 1f })
            {
                pieces.Add(new TrimPiece(Span(s, w - 0.06f, w + 0.3f, near, far), 0.1f, lined));
                if (lined)
                {
                    pieces.Add(new TrimPiece(Span(s, w - 0.04f, w + 0.08f, -half - 0.02f, half + 0.02f), 0f, true, Through: true));
                }
            }

            return pieces;
        }

        float h = Approach * 0.5f + t, inward = side == ExitSide.West ? 1f : -1f;
        pieces.Add(new TrimPiece(new Box(-h + 0.02f, -0.28f, h - 0.02f, -0.06f), 0.1f, false));
        foreach (float s in new[] { -1f, 1f })
        {
            if (bracket && s == inward)
            {
                pieces.Add(new TrimPiece(Span(s, h - 0.9f, h - 0.02f, -t - 0.7f, -t + 0.1f), 0.08f, false, Sink: 0.5f));
            }

            if (lined && s == inward)
            {
                pieces.Add(new TrimPiece(Span(s, h - 0.08f, h + 0.04f, -t - 0.02f, 0.02f), 0f, true, Through: true));
                pieces.Add(new TrimPiece(Span(s, h - 0.08f, h + 0.04f, Opening - 0.02f, Opening + t + 0.02f), 0f, true, Through: true));
            }
        }

        return pieces;
    }

    /// <summary>
    /// The wall a doorway on <paramref name="side"/> at <paramref name="marker"/> stands in, past its frame, on the play plane: over the
    /// lintel up to what is solid above it, under the sill down to what is solid under it, column by column across the frame's span,
    /// each column stopping at the first of <paramref name="solids"/> (the room's own, its other exits' frames) or, with none, at the
    /// camera's <paramref name="bounds"/>. So the lintel is the bottom of a wall and the sill the top of one: the doorway reads as a hole
    /// in a wall by construction (docs/art-direction.md#look). Gameplay geometry, solid like the frame, outside what the seam check
    /// compares. A column that would end at one of <paramref name="holes"/> (the room's hatches) is a shaft to it and stays open.
    /// <paramref name="head"/> or <paramref name="foot"/> false leaves that side open, for a route that runs over the lintel or under
    /// the sill. A hatch's frame stands in a floor or a ceiling: no wall.
    /// </summary>
    public IReadOnlyList<Box> Wall(
        ExitSide side, Vector2 marker, IReadOnlyList<Box> solids, Box bounds, bool head = true, bool foot = true, IReadOnlyList<Box>? holes = null)
    {
        holes ??= Array.Empty<Box>();
        solids = solids.Concat(holes).ToList();
        if (side is ExitSide.Down or ExitSide.Up)
        {
            return Array.Empty<Box>();
        }

        IReadOnlyList<Box> frame = Solids(side, marker);
        Box sill = frame[0], lintel = frame[1];
        float[] cuts = solids.SelectMany(o => new[] { o.MinX, o.MaxX })
            .Where(x => x > lintel.MinX + Joint && x < lintel.MaxX - Joint)
            .Append(lintel.MinX).Append(lintel.MaxX).Distinct().Order().ToArray();
        var wall = new List<Box>();
        if (head)
        {
            wall.AddRange(Columns(cuts, lintel.MaxY, bounds.MaxY, solids));
        }

        if (foot)
        {
            wall.AddRange(Columns(cuts, sill.MinY, bounds.MinY, solids));
        }

        return wall.Where(w => !holes.Any(h => h.MinX < w.MaxX - Joint && w.MinX < h.MaxX - Joint
            && (MathF.Abs(h.MinY - w.MaxY) < Joint || MathF.Abs(h.MaxY - w.MinY) < Joint))).ToList();
    }

    /// <summary>Two faces closer than this are one joint, meters (<see cref="Wall"/>).</summary>
    private const float Joint = 0.05f;

    /// <summary>
    /// The columns between <paramref name="cuts"/> filled from <paramref name="from"/> toward <paramref name="limit"/> (up or down), each
    /// stopped by the first of <paramref name="solids"/> over it; neighbours as tall merge into one.
    /// </summary>
    private static IEnumerable<Box> Columns(float[] cuts, float from, float limit, IReadOnlyList<Box> solids)
    {
        float dir = MathF.Sign(limit - from);
        Box? run = null;
        for (int i = 0; i + 1 < cuts.Length; i++)
        {
            float a = cuts[i], b = cuts[i + 1], end = limit;
            foreach (Box o in solids.Where(o => o.MinX < b - Joint && o.MaxX > a + Joint))
            {
                if (dir > 0f && o.MaxY > from + Joint)
                {
                    end = MathF.Min(end, MathF.Max(o.MinY, from));
                }
                else if (dir < 0f && o.MinY < from - Joint)
                {
                    end = MathF.Max(end, MathF.Min(o.MaxY, from));
                }
            }

            if ((end - from) * dir < Joint)
            {
                if (run is { } done)
                {
                    yield return done;
                }

                run = null;
                continue;
            }

            Box column = new(a, MathF.Min(from, end), b, MathF.Max(from, end));
            if (run is { } r && r.MinY == column.MinY && r.MaxY == column.MaxY)
            {
                run = r with { MaxX = b };
            }
            else
            {
                if (run is { } before)
                {
                    yield return before;
                }

                run = column;
            }
        }

        if (run is { } last)
        {
            yield return last;
        }
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
    /// How far a standpipe's walls stop over its water's surface, and how far its column dips under it, meters (<see cref="Pipe"/>): a
    /// body's height, so a floater swims in under the mouth and floats up the column.
    /// </summary>
    public const float PipeMouth = 2f;

    /// <summary>How thick a standpipe's walls are, meters.</summary>
    public const float PipeWall = 1f;

    /// <summary>
    /// The standpipe hung from the hatch on <paramref name="side"/> at <paramref name="marker"/> (an up exit, a hole in the roof) into
    /// the highest of the room's swum <paramref name="waters"/> that lies under the whole hole, or null when none does: its column as
    /// wide as the hole, from the slab's top (where the flooded passage ends) down <see cref="PipeMouth"/> under that water's surface
    /// (no deeper than its floor), so the two are one water; its walls <see cref="PipeWall"/> thick either side of the hole, from the
    /// slab's underside down to <see cref="PipeMouth"/> over the surface.
    /// </summary>
    public Standpipe? Pipe(ExitSide side, Vector2 marker, IReadOnlyList<Box> waters)
    {
        if (side != ExitSide.Up)
        {
            throw new ArgumentException($"a standpipe hangs from a hatch in a roof, not from a {side} exit");
        }

        Box area = Exit(side, marker).Area;
        int? pool = null;
        for (int i = 0; i < waters.Count; i++)
        {
            Box w = waters[i];
            if (w.MinX <= area.MinX && w.MaxX >= area.MaxX && w.MaxY <= area.MinY && (pool is not { } best || w.MaxY > waters[best].MaxY))
            {
                pool = i;
            }
        }

        if (pool is not { } index)
        {
            return null;
        }

        Box water = waters[index];
        float mouth = water.MaxY + PipeMouth;
        if (mouth >= area.MinY)
        {
            throw new ArgumentException($"the water under the hatch at {marker} lies {area.MinY - water.MaxY} m under its slab: no room for a standpipe's mouth");
        }

        var column = new Box(area.MinX, MathF.Max(water.MaxY - PipeMouth, water.MinY), area.MaxX, area.MaxY);
        return new Standpipe(
            index, column, new Box(area.MinX - PipeWall, mouth, area.MinX, area.MinY), new Box(area.MaxX, mouth, area.MaxX + PipeWall, area.MinY));
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

