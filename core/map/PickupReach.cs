using System.Numerics;

namespace TheDeep;

/// <summary>
/// How near the feet must come to take a relic or an item (a circle on the play plane, in the game), and the trigger box the map's
/// proof gives that reach (<see cref="Area"/>): one source for both, so retuning the reach retunes the proof.
/// </summary>
public static class PickupReach
{
    /// <summary>How close, meters on the play plane, the feet must come to take a relic or an item.</summary>
    public const float Take = 1.2f;

    /// <summary>How tall the proof's box is, meters: a band around the feet, as the circle is taken on the plane whatever the height.</summary>
    public const float Band = 0.4f;

    /// <summary>How far the proof's box is raised over the thing's own position, meters.</summary>
    public const float Lift = 0.1f;

    /// <summary>The proof's trigger box for a pickup at <paramref name="at"/> taken within <paramref name="range"/> meters.</summary>
    public static Box Area(Vector2 at, float range) => Box.Around(at + new Vector2(0f, Lift), new Vector2(2f * range, Band));
}
