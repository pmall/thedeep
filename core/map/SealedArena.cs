using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// A sealed arena (a boss's, an ambush's) as the room check holds it, pure; its rule in play is <see cref="ArenaFight"/>.
/// <see cref="Area"/> is the box on the play plane the pilgrim's feet step into to start the fight, <see cref="Gates"/> the boxes its
/// gates fill when shut, <see cref="Foes"/> where its foes stand as the room places them.
/// </summary>
public sealed record SealedArena(string Name, Box Area, IReadOnlyList<Box> Gates, IReadOnlyList<(string Name, Vector2 At)> Foes)
{
    /// <summary>
    /// What is wrong with the arena in its room (<paramref name="room"/>, its camera <paramref name="bounds"/>): a foe standing outside
    /// <see cref="Area"/>; the area reaching past the bounds; no floor in it; or, the gates shut, a way out of the room reached from it.
    /// The reach planner, simulating <paramref name="abilities"/> with <paramref name="limits"/> over the room with the gates as solids,
    /// starts on every floor (and float line) inside the area; any of <paramref name="waysOut"/> (an exit's trigger) it enters is a
    /// way out the gates leave open.
    /// </summary>
    public IEnumerable<string> Problems(RoomShape room, Box bounds, IReadOnlyList<(string Name, Box Trigger)> waysOut, PlannerLimits limits,
        Ability abilities)
    {
        foreach ((string foe, Vector2 at) in Foes.Where(f => !Area.Contains(f.At)))
        {
            yield return $"arena {Name}: its foe {foe} stands at ({at.X:0.##}, {at.Y:0.##}), outside its area {Describe(Area)}";
        }

        if (!bounds.Contains(Area))
        {
            yield return $"arena {Name}: its area {Describe(Area)} reaches past the room's camera bounds {Describe(bounds)}";
        }

        RoomShape shut = room with { Solids = room.Solids.Concat(Gates).ToList(), Sound = room.Sound.Concat(Gates).ToList() };
        ReachPlanner planner = WorldCheck.For(shut, limits, abilities);
        var starts = new List<(Vector2 At, Vector2 Velocity, int Steer)>();
        foreach (Ledge l in planner.Ledges.Where(l => l.Y >= Area.MinY && l.Y <= Area.MaxY))
        {
            float from = MathF.Max(l.MinX, Area.MinX), to = MathF.Min(l.MaxX, Area.MaxX);
            if (from <= to)
            {
                starts.Add((new Vector2(0.5f * (from + to), l.Y), Vector2.Zero, 0));
            }
        }

        if (starts.Count == 0)
        {
            yield return $"arena {Name}: no floor lies inside its area {Describe(Area)}, so the fight never starts";
            yield break;
        }

        HashSet<int> reached = planner.Reach(starts, waysOut.Select(w => w.Trigger).ToList()).Triggers;
        foreach (int way in reached.Order())
        {
            yield return $"arena {Name}: its gates shut, the pilgrim still leaves its area by exit {waysOut[way].Name} ({abilities}): " +
                         "a gate must bar every way out";
        }
    }

    private static string Describe(Box b) => $"({b.MinX:0.##}, {b.MinY:0.##})..({b.MaxX:0.##}, {b.MaxY:0.##})";
}
