using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace TheDeep;

/// <summary>A connection as the layout sees it: the exit's marker in its room and the arrival marker in the target room.</summary>
public sealed record MapLink(string From, string FromExit, Vector2 FromPosition, string To, string ToExit, Vector2 ToPosition);

/// <summary>
/// Places the rooms on one map from their connections, pure (the same rule as <see cref="ExitMath.RoomOffset"/>:
/// a departure marker and its arrival marker are the same place), and checks the result: every way of
/// reaching a room must put it in the same place, and rooms must not overlap beyond the shared doorway
/// margin. Rooms are rectangles (their camera bounds).
/// </summary>
public static class MapLayout
{
    /// <summary>Two placements of the same room closer than this agree, meters.</summary>
    public const float Tolerance = 0.01f;

    public sealed record Result(IReadOnlyDictionary<string, Vector2> Offsets, IReadOnlyList<string> Problems);

    /// <param name="bounds">Each room's camera bounds (min, max), room-local.</param>
    /// <param name="overlapMargin">How far two rooms' bounds may overlap (their walls at a shared doorway), meters.</param>
    public static Result Place(string start, IEnumerable<MapLink> links, IReadOnlyDictionary<string, (Vector2 Min, Vector2 Max)> bounds,
        float overlapMargin)
    {
        var offsets = new Dictionary<string, Vector2> { [start] = Vector2.Zero };
        var problems = new List<string>();
        List<MapLink> all = links.ToList();
        var queue = new Queue<string>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            string room = queue.Dequeue();
            // Both directions place rooms: a one-way drop still fixes where its target lies.
            foreach (MapLink link in all.Where(l => l.From == room || l.To == room))
            {
                bool forward = link.From == room;
                string other = forward ? link.To : link.From;
                Vector2 here = forward ? link.FromPosition : link.ToPosition;
                Vector2 there = forward ? link.ToPosition : link.FromPosition;
                Vector2 placed = ExitMath.RoomOffset(offsets[room], here, there);
                if (offsets.TryGetValue(other, out Vector2 known))
                {
                    if (Vector2.Distance(known, placed) > Tolerance)
                    {
                        problems.Add($"{link.From}:{link.FromExit} → {link.To}:{link.ToExit} puts {other} at {Fmt(placed)}, " +
                                     $"but another way puts it at {Fmt(known)} (the exits around this loop don't line up)");
                    }
                }
                else
                {
                    offsets[other] = placed;
                    queue.Enqueue(other);
                }
            }
        }

        // Overlaps, among the rooms placed.
        List<string> placedRooms = offsets.Keys.Where(bounds.ContainsKey).OrderBy(r => r, StringComparer.Ordinal).ToList();
        for (int i = 0; i < placedRooms.Count; i++)
        {
            for (int j = i + 1; j < placedRooms.Count; j++)
            {
                (Vector2 aMin, Vector2 aMax) = Shrink(bounds[placedRooms[i]], offsets[placedRooms[i]], overlapMargin * 0.5f);
                (Vector2 bMin, Vector2 bMax) = Shrink(bounds[placedRooms[j]], offsets[placedRooms[j]], overlapMargin * 0.5f);
                if (aMin.X < bMax.X && bMin.X < aMax.X && aMin.Y < bMax.Y && bMin.Y < aMax.Y)
                {
                    problems.Add($"{placedRooms[i]} and {placedRooms[j]} overlap on the map (more than {overlapMargin} m)");
                }
            }
        }

        return new Result(offsets, problems);
    }

    private static (Vector2, Vector2) Shrink((Vector2 Min, Vector2 Max) b, Vector2 offset, float by) =>
        (b.Min + offset + new Vector2(by), b.Max + offset - new Vector2(by));

    private static string Fmt(Vector2 v) => $"({v.X:0.##}, {v.Y:0.##})";
}
