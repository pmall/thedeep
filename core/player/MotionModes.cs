namespace TheDeep;

/// <summary>Which <see cref="MotionMode"/> a movement state shows, from what it knows of the body: the choices states share.</summary>
public static class MotionModes
{
    /// <summary>Rising faster than this share of the water's rise speed reads as the buoyant rise, not floating's bob.</summary>
    public const float RiseShare = 0.5f;

    /// <summary>A dash: the bore once it meets cracked rock with the Drill, else by where it began.</summary>
    public static MotionMode Dash(bool startedGrounded, bool boring) =>
        boring ? MotionMode.Bore : startedGrounded ? MotionMode.Dash : MotionMode.AirDash;

    /// <summary>
    /// In water: sunk on the bottom walks, sunk off it sinks; with the tanks blown, rising at more than <see cref="RiseShare"/>
    /// of <paramref name="riseSpeed"/> (m/s, up positive like <paramref name="velocityY"/>) rises, otherwise it floats.
    /// </summary>
    public static MotionMode Water(bool sunk, bool onFloor, float velocityY, float riseSpeed) =>
        sunk ? onFloor ? MotionMode.Ground : MotionMode.Sink
        : velocityY > riseSpeed * RiseShare ? MotionMode.Rise : MotionMode.Swim;

    /// <summary>The Harpoon: the throw while the line flies, the zip once it has bitten.</summary>
    public static MotionMode Harpoon(bool bitten) => bitten ? MotionMode.Zip : MotionMode.Throw;
}
