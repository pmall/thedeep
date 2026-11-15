namespace TheDeep;

/// <summary>
/// The base moveset's numbers (copied from <c>player_tuning.tres</c>, the single source), as the planner simulates them, with the
/// body's size (<see cref="Width"/>, <see cref="Height"/>, meters: the pilgrim's capsule).
/// </summary>
public sealed record MoveLimits(
    float RunSpeed, float AirAcceleration, float AirDeceleration, float AirDrag, float AirCarrySpeed, float JumpHeight, float JumpTimeToApex, float JumpCutFactor,
    float FallGravityMultiplier, float ApexHangThreshold, float ApexGravityMultiplier, float MaxFallSpeed,
    float DashDistance, float DashDuration, float DashExitSpeedFactor, int AirDashes,
    float Width, float Height, float EdgeMargin = 0f, float LedgeAssist = 0f)
{
    /// <summary>
    /// A clumsier pilgrim, for the slack report: jumps 18% lower, dashes 35% shorter, runs 20% slower, never takes off from the very
    /// end of a ledge, and gets no ledge assist (it still climbs 2.5 m steps and crosses 7.5 m gaps). A way only these limits miss asks
    /// for too exact play: jumps must not be exact (owner).
    /// </summary>
    public MoveLimits Clumsy() => this with
    {
        JumpHeight = JumpHeight * 0.82f,
        DashDistance = DashDistance * 0.65f,
        RunSpeed = RunSpeed * 0.8f,
        EdgeMargin = 0.3f,
        LedgeAssist = 0f,
    };

    /// <summary>The gravity of a rising jump, m/s² (positive down): what makes it peak at the jump height after the time to
    /// apex.</summary>
    public float Gravity => JumpMath.Gravity(JumpHeight, JumpTimeToApex);
    /// <summary>The upward speed a jump starts with, m/s.</summary>
    public float TakeOff => JumpMath.TakeOffSpeed(JumpHeight, JumpTimeToApex);
}
