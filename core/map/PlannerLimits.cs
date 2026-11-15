namespace TheDeep;

/// <summary>
/// The moveset's numbers the world check simulates (from <c>player_tuning.tres</c>): the base moveset, the Harpoon, Mag-grip,
/// water, and <see cref="Rise"/>, the upward speed (m/s) an arrival climbing up through a hatch keeps at least.
/// </summary>
public sealed record PlannerLimits(MoveLimits Move, HarpoonLimits Harpoon, GripLimits Grip, WaterLimits Water, float Rise);
