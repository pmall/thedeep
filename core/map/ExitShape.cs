using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// An exit as the world check sees it: its trigger and arrival (<see cref="Threshold"/>), its requirement, the side an arrival up
/// through a hatch is carried toward (<see cref="ArrivalSide"/>: -1, 0, +1) and whether it is sealed (it leads to a room not built yet).
/// </summary>
public sealed record ExitShape(string Name, Threshold Threshold, string Requires, int ArrivalSide, bool Sealed)
{
    /// <summary>The downward speed an arrival falling in through a hatch in the roof is taken to come with, m/s.</summary>
    public const float FallInSpeed = 5f;

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
            ExitSide.Up => new Vector2(0f, -FallInSpeed),
            _ => Vector2.Zero,
        };
        return (Threshold.Arrival(new Vector2(limits.Move.Width, limits.Move.Height), solids), velocity,
            Threshold.Side == ExitSide.Down ? ArrivalSide : 0);
    }
}
