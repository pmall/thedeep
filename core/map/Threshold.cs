using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// An exit on its room's play plane (room positions): its trigger, where the body arrives through it, and what the room check holds
/// it to (docs/gameplay.md#leaving-a-room). The exit's <see cref="Area"/> is centered on its marker, <see cref="Size"/> wide: a
/// doorway's (<see cref="ExitSide.West"/>, <see cref="ExitSide.East"/>) is the last stretch of floor before the opening, its marker on
/// that floor, as tall as the opening; a hatch's (<see cref="ExitSide.Down"/>, <see cref="ExitSide.Up"/>) is the hole itself. The
/// <see cref="Trigger"/> covers the whole opening and reaches out past the room's edge: from a doorway's outer face, from a hatch's face
/// toward the room. A body touching it leaves through the exit, whatever it is doing; arrivals are placed clear of it.
/// </summary>
public readonly record struct Threshold(ExitSide Side, Vector2 Marker, Vector2 Size)
{
    /// <summary>How far clear of a trigger an arriving body is placed, meters.</summary>
    public const float Clearance = 0.1f;

    /// <summary>How far a trigger reaches out of its room, meters: past anything beyond the opening.</summary>
    private const float Reach = 50f;

    /// <summary>A hatch, crossed up or down (not a doorway).</summary>
    public bool IsHatch => Side is ExitSide.Down or ExitSide.Up;

    /// <summary>The way out of the room, a unit vector.</summary>
    public Vector2 Outward => Side switch
    {
        ExitSide.West => new Vector2(-1f, 0f),
        ExitSide.East => new Vector2(1f, 0f),
        ExitSide.Down => new Vector2(0f, -1f),
        _ => new Vector2(0f, 1f),
    };

    /// <summary>Where the trigger starts: a doorway's X (its area's outer face), a hatch's Y (its hole's face toward the room).</summary>
    public float Line => Side switch
    {
        ExitSide.West => Marker.X - Size.X * 0.5f,
        ExitSide.East => Marker.X + Size.X * 0.5f,
        ExitSide.Down => Marker.Y + Size.Y * 0.5f,
        _ => Marker.Y - Size.Y * 0.5f,
    };

    /// <summary>The exit's area: a doorway's stretch of floor before the opening, as tall as it; a hatch's hole, as wide as it.</summary>
    public Box Area => IsHatch
        ? Box.Around(Marker, Size)
        : new Box(Marker.X - Size.X * 0.5f, Marker.Y, Marker.X + Size.X * 0.5f, Marker.Y + Size.Y);

    /// <summary>The whole opening, from <see cref="Line"/> out past the room's edge: a body touching it leaves through the exit.</summary>
    public Box Trigger => Side switch
    {
        ExitSide.West => new Box(Line - Reach, Marker.Y, Line, Marker.Y + Size.Y),
        ExitSide.East => new Box(Line, Marker.Y, Line + Reach, Marker.Y + Size.Y),
        ExitSide.Down => new Box(Marker.X - Size.X * 0.5f, Line - Reach, Marker.X + Size.X * 0.5f, Line),
        _ => new Box(Marker.X - Size.X * 0.5f, Line, Marker.X + Size.X * 0.5f, Line + Reach),
    };

    /// <summary>Whether <paramref name="body"/> touches the trigger: it leaves the room through this exit.</summary>
    public bool Crossed(Box body) => body.Overlaps(Trigger);

    /// <summary>Whether two exits face opposite ways, as the two ends of one way do.</summary>
    public static bool Opposite(ExitSide a, ExitSide b) => a switch
    {
        ExitSide.West => b == ExitSide.East,
        ExitSide.East => b == ExitSide.West,
        ExitSide.Down => b == ExitSide.Up,
        _ => b == ExitSide.Down,
    };

    /// <summary>
    /// Where the feet of a body of <paramref name="body"/> size (width, height) arrive through this exit, clear of its trigger: through a
    /// doorway on its marker (or further in, for a shallow one); up through a hatch in a floor just above the hole; down through one in a
    /// roof with the head just below it. Across a hatch, at its middle, or the nearest place to it where the body fits among
    /// <paramref name="solids"/> (a ledge over part of the hole).
    /// </summary>
    public Vector2 Arrival(Vector2 body, IReadOnlyList<Box>? solids = null)
    {
        switch (Side)
        {
            case ExitSide.West:
                return new Vector2(MathF.Max(Marker.X, Line + body.X * 0.5f + Clearance), Marker.Y);
            case ExitSide.East:
                return new Vector2(MathF.Min(Marker.X, Line - body.X * 0.5f - Clearance), Marker.Y);
        }

        float y = Side == ExitSide.Down ? Line + Clearance : Line - body.Y - Clearance;
        float half = MathF.Max(0f, (Size.X - body.X) * 0.5f - Clearance);
        if (solids is not null)
        {
            for (float d = 0f; d <= half + 0.001f; d += 0.25f)
            {
                foreach (float x in new[] { Marker.X + d, Marker.X - d })
                {
                    var at = new Box(x - body.X * 0.5f, y, x + body.X * 0.5f, y + body.Y);
                    if (Hit(solids, at) is null)
                    {
                        return new Vector2(x, y);
                    }
                }
            }
        }

        return new Vector2(Marker.X, y);
    }

    /// <summary>
    /// The velocity an arrival through this exit keeps: the speed it crossed with, never back out, and coming up through a floor at
    /// least <paramref name="minRise"/> upward (m/s), so the body clears the hole.
    /// </summary>
    public Vector2 ArrivalVelocity(Vector2 velocity, float minRise)
    {
        Vector2 way = Outward;
        float back = Vector2.Dot(velocity, way);
        Vector2 kept = back > 0f ? velocity - way * back : velocity;
        return Side == ExitSide.Down ? kept with { Y = MathF.Max(kept.Y, minRise) } : kept;
    }

    /// <summary>
    /// What is wrong with this exit among its room's <paramref name="solids"/> (in its camera bounds, <paramref name="room"/>), for a body
    /// of <paramref name="body"/> size: the trigger
    /// must cover the whole opening (a doorway's marker on its floor, no room for a body over the trigger; no room for a body beside a
    /// hatch's), its area must be clear, and an arriving body must fit where it is placed. <paramref name="left"/>: whether the exit is
    /// left through (the far end of a one-way drop is only arrived through).
    /// </summary>
    public IEnumerable<string> Problems(IReadOnlyList<Box> solids, Box room, Vector2 body, bool left = true)
    {
        Vector2 feet = Arrival(body, solids);
        var arriving = new Box(feet.X - body.X * 0.5f, feet.Y, feet.X + body.X * 0.5f, feet.Y + body.Y);
        if (Hit(solids, arriving) is { } inArrival)
        {
            yield return $"a body arriving through it, at {Describe(arriving)}, is in a solid at {Describe(inArrival)}";
        }

        if (!left)
        {
            yield break;
        }

        Box area = Area;
        if (Hit(solids, area) is { } inArea)
        {
            yield return $"its area {Describe(area)} is blocked by a solid at {Describe(inArea)}";
        }

        if (!IsHatch)
        {
            // On its floor, from its marker out to the trigger; under a lintel low enough that no body passes over the trigger.
            float floor = Marker.Y, top = Marker.Y + Size.Y;
            foreach (float x in new[] { Marker.X, Line - Outward.X * 0.01f })
            {
                if (!solids.Any(s => s.MinX <= x && x <= s.MaxX && MathF.Abs(s.MaxY - floor) < 0.01f))
                {
                    yield return $"its marker is not on the floor of its opening: nothing stands at ({x:0.##}, {floor:0.##})";
                    yield break;
                }
            }

            // Somewhere out from the trigger's edge, the wall over the passage leaves no room for a body above the trigger.
            if (!Closed(solids, room, top, body.Y, horizontal: true))
            {
                yield return $"nothing closes its opening over the trigger (top {top:0.##}) within {Passage} m out: a body passes over " +
                             "it; make it as tall as the opening";
            }

            yield break;
        }

        // A hatch: somewhere out from the hole's face, the slab on each side of it leaves no room for a body beside the trigger.
        foreach (int side in new[] { -1, 1 })
        {
            if (!Closed(solids, room, side < 0 ? Area.MinX : Area.MaxX, body.X, horizontal: false, side))
            {
                yield return $"the hole is wider than the trigger on its {(side < 0 ? "west" : "east")} side: a body falls or climbs past it; " +
                             "make it as wide as the hole";
            }
        }
    }

    /// <summary>How far out from the trigger's edge the room check looks for what closes the opening around it, meters.</summary>
    private const float Passage = 6f;

    /// <summary>
    /// Whether, at some step out from the trigger's edge (<see cref="Line"/>) within <see cref="Passage"/>, the open stretch right past
    /// <paramref name="edge"/> is too small for a body: above a doorway's trigger (<paramref name="horizontal"/>, up to the room's top), or
    /// beside a hatch's toward <paramref name="side"/> (to the room's side).
    /// </summary>
    private bool Closed(IReadOnlyList<Box> solids, Box room, float edge, float body, bool horizontal, int side = 1)
    {
        float way = horizontal ? Outward.X : Outward.Y;
        int dir = horizontal ? 1 : side;
        float limit = horizontal ? room.MaxY : side > 0 ? room.MaxX : room.MinX;
        for (float d = 0.01f; d <= Passage; d += 0.25f)
        {
            float at = Line + way * d, open = MathF.Abs(limit - edge);
            foreach (Box s in solids)
            {
                if (!(horizontal ? s.MinX <= at && at <= s.MaxX : s.MinY <= at && at <= s.MaxY))
                {
                    continue;
                }

                float lo = horizontal ? s.MinY : s.MinX, hi = horizontal ? s.MaxY : s.MaxX;
                float near = dir > 0 ? lo : hi;
                if (lo <= edge && edge <= hi)
                {
                    open = 0f; // the slab is right at the edge
                }
                else if ((near - edge) * dir > 0f)
                {
                    open = MathF.Min(open, MathF.Abs(near - edge));
                }
            }

            if (open < body)
            {
                return true;
            }
        }

        return false;
    }

    private static Box? Hit(IReadOnlyList<Box> solids, Box box)
    {
        foreach (Box s in solids)
        {
            if (s.Overlaps(box))
            {
                return s;
            }
        }

        return null;
    }

    private static string Describe(Box b) => $"({b.MinX:0.##}, {b.MinY:0.##})..({b.MaxX:0.##}, {b.MaxY:0.##})";
}
