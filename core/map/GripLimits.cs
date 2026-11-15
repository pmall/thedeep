namespace TheDeep;

/// <summary>
/// Mag-grip's numbers (from <c>player_tuning.tres</c>), for a planner that may also walk on metal: the leap off a wall (speed away,
/// share of a jump's take-off upward), how long after letting go the body can't grip again, the downward speed of a drop from under
/// a ceiling and the speed away from the surface on letting go (m/s).
/// </summary>
public sealed record GripLimits(float LeapAwaySpeed, float LeapUpFactor, float RegrabDelay, float DropSpeed, float LetGoSpeed);
