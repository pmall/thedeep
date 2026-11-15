namespace TheDeep;

/// <summary>
/// Water's numbers (from <c>player_tuning.tres</c>, the Water group), as both the pilgrim and the map's geometry check use them:
/// swimming speed, where the body floats (its feet <see cref="FloatDepth"/> under the surface), how fast it rises and sinks
/// and how quickly water changes its vertical speed, how much of a plunge's speed the surface keeps, how deep the feet must
/// be to swim, and how near the float line a jump still leaves the water.
/// </summary>
public sealed record WaterLimits(
    float SwimSpeed, float SwimAcceleration, float SwimDeceleration, float FloatDepth, float FloatStiffness, float RiseSpeed,
    float SinkSpeed, float WaterAcceleration, float SplashKeep, float EnterDepth, float JumpWindow);
