namespace TheDeep;

/// <summary>
/// The Harpoon's numbers (from <c>player_tuning.tres</c>), for a planner that may also zip to anchors: range from the body's
/// center, the nearest an anchor may be, the pull's top speed and acceleration, and the release (speed kept, lift).
/// </summary>
public sealed record HarpoonLimits(float Range, float MinDistance, float ZipSpeed, float ZipAcceleration, float ExitSpeedFactor, float ExitLift);
