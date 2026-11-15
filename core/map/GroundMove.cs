namespace TheDeep;

/// <summary>
/// One move tried from a take-off point (<see cref="ReachPlanner.GroundMoves"/>): held toward <see cref="Dir"/> (−1, 0, +1) from a run at
/// that speed, a jump held for <see cref="JumpTicks"/> (−1: to the apex; 0: no jump, walking off the end), an air dash toward
/// <see cref="Dir"/> (right when 0) at <see cref="DashTick"/> (−1: none), ticks counted from the take-off.
/// </summary>
public readonly record struct GroundMove(int Dir, int JumpTicks, int DashTick);
