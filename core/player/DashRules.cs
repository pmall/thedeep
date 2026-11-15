namespace TheDeep;

/// <summary>Which dash a press starts, pure (docs/gameplay.md#drill): the plain dash or the Drill's dive.</summary>
public static class DashRules
{
    /// <summary>
    /// Whether the press asks for the dive: the Drill, down held (<paramref name="moveY"/> below zero), the body upright, and in
    /// water only with Ballast's tanks full (<paramref name="sunk"/>).
    /// </summary>
    public static bool AsksForDive(bool hasDrill, int moveY, bool upright, bool inWater, bool sunk) =>
        hasDrill && moveY < 0 && upright && (!inWater || sunk);

    /// <summary>Whether a dive can start from here: from the air, or standing on cracked rock (<paramref name="crackedBelow"/>).</summary>
    public static bool CanDiveFrom(bool onFloor, bool crackedBelow) => !onFloor || crackedBelow;
}
