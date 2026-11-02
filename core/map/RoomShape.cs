using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// What the world check needs of one room, on its play plane (room positions): its solids (and the sound ones, without cracked
/// rock), its exits, where the pilgrim can wake (its shrines, and its spawn point in the start room), its anchors, lifts, swimmable
/// water, heat, relics and items (the box walking into takes one) and goals, its metal, and its spawn point.
/// </summary>
public sealed record RoomShape(
    List<Box> Solids, List<ExitShape> Exits, List<Vector2> Wakes, List<Vector2> Anchors, List<LiftPath> Lifts, List<Box> Sound,
    List<Box> Waters, List<Box> Heat, List<(Box Area, Ability Ability)> Relics, List<Box> Goals, List<Box> Metal, Vector2 Spawn)
{
    /// <summary>The items lying in the room (keys, codes), by id, with the box walking into takes each.</summary>
    public List<(Box Area, string Item)> Items { get; init; } = new();
}

/// <summary>
/// An exit as the world check sees it: its trigger box, its requirement, the side an arrival through a hatch is carried toward
/// (<see cref="ArrivalSide"/>: -1, 0, +1) and whether it is sealed (it leads to a room not built yet).
/// </summary>
public sealed record ExitShape(string Name, Vector2 Position, Vector2 Size, string Requires, int ArrivalSide, bool Sealed);

/// <summary>
/// The moveset's numbers the world check simulates (from <c>player_tuning.tres</c>): the base moveset, the Harpoon, Mag-grip,
/// water, and <see cref="Rise"/>, the upward speed (m/s) an arrival climbing up through a hatch keeps at least.
/// </summary>
public sealed record PlannerLimits(MoveLimits Move, HarpoonLimits Harpoon, GripLimits Grip, WaterLimits Water, float Rise);
