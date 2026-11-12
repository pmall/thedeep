using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// What the world check needs of one room, on its play plane (room positions): its solids (and the sound ones, without cracked
/// rock), its exits, where the pilgrim can wake (its shrines, and its spawn point in the start room), its anchors, lifts, swimmable
/// water, heat, slag, relics and items (the box walking into takes one) and goals, its metal, and its spawn point.
/// </summary>
public sealed record RoomShape(
    List<Box> Solids, List<ExitShape> Exits, List<Vector2> Wakes, List<Vector2> Anchors, List<LiftPath> Lifts, List<Box> Sound,
    List<Box> Waters, List<Box> Heat, List<(Box Area, Ability Ability)> Relics, List<Box> Goals, List<Box> Metal, Vector2 Spawn)
{
    /// <summary>The items lying in the room (keys, codes), by id, with the box walking into takes each.</summary>
    public List<(Box Area, string Item)> Items { get; init; } = new();

    /// <summary>Slag (`docs/gameplay.md#slag`): space no planner enters, plated or not; a way over it is a way over its surface.</summary>
    public List<Box> Slag { get; init; } = new();
}

/// <summary>
/// An exit as the world check sees it: its trigger and arrival (<see cref="Threshold"/>), its requirement, the side an arrival up
/// through a hatch is carried toward (<see cref="ArrivalSide"/>: -1, 0, +1) and whether it is sealed (it leads to a room not built yet).
/// </summary>
public sealed record ExitShape(string Name, Threshold Threshold, string Requires, int ArrivalSide, bool Sealed)
{
    /// <summary>
    /// How a body arriving through this exit enters the room, placed as room travel places it (<see cref="Threshold.Arrival"/>, among
    /// <paramref name="solids"/>): through a doorway standing, up through a hatch in a floor rising (carried toward
    /// <see cref="ArrivalSide"/>, the steer), down through one in a roof falling.
    /// </summary>
    public (Vector2 At, Vector2 Velocity, int Steer) Entry(PlannerLimits limits, IReadOnlyList<Box> solids)
    {
        Vector2 velocity = Threshold.Side switch
        {
            ExitSide.Down => Threshold.ArrivalVelocity(Vector2.Zero, limits.Rise),
            ExitSide.Up => new Vector2(0f, -5f),
            _ => Vector2.Zero,
        };
        return (Threshold.Arrival(new Vector2(limits.Move.Width, limits.Move.Height), solids), velocity,
            Threshold.Side == ExitSide.Down ? ArrivalSide : 0);
    }
}

/// <summary>
/// The moveset's numbers the world check simulates (from <c>player_tuning.tres</c>): the base moveset, the Harpoon, Mag-grip,
/// water, and <see cref="Rise"/>, the upward speed (m/s) an arrival climbing up through a hatch keeps at least.
/// </summary>
public sealed record PlannerLimits(MoveLimits Move, HarpoonLimits Harpoon, GripLimits Grip, WaterLimits Water, float Rise);
