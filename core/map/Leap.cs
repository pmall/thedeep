using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// One move as the planner flies it from a ledge (<see cref="ReachPlanner.Reach(IEnumerable{ValueTuple{Vector2, Vector2, int}}, IReadOnlyList{Box}, Action{Leap})"/>): the ledge it starts on (<see cref="From"/>), the
/// take-off point, the move (its dash tick the one it came at), the feet tick by tick from the take-off (<see cref="Path"/>, walking
/// to the edge first for a walk-off), the ledge it landed on (<see cref="Lands"/>, −1: none in time), and whether the flight was clean:
/// landed on a floor touching no wall or ceiling on the way, no ledge assist, no water, no hazard.
/// </summary>
public sealed record Leap(int From, Vector2 TakeOff, GroundMove Move, IReadOnlyList<Vector2> Path, int Lands, bool Clean);
