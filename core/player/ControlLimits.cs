namespace TheDeep;

/// <summary>
/// The numbers of the pilgrim's controls (copied from <c>player_tuning.tres</c>, the single source), as <see cref="PilgrimControl"/>
/// and <see cref="MoveRules"/> read them: running and falling (m/s, m/s²), the grace windows that forgive a press made a little
/// early or late (seconds), the air dashes, how speed above a run bleeds off in the air (m/s²) and the fastest it is carried (m/s), and how a blow throws the pilgrim (m/s) and takes control away (seconds).
/// </summary>
public sealed record ControlLimits(
    float RunSpeed, float GroundAcceleration, float GroundDeceleration, float AirAcceleration, float AirDeceleration, float AirDrag, float AirCarrySpeed,
    float Gravity, float ApexHangThreshold, float ApexGravityMultiplier, float FallGravityMultiplier, float MaxFallSpeed,
    float CoyoteTime, float JumpBufferTime, float DashBufferTime, float DashCooldown, float HarpoonBufferTime, float GripRegrabDelay,
    int AirDashes, float HurtKnockback, float HurtLift, float HurtStun);
