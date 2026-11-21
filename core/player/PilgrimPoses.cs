namespace TheDeep;

/// <summary>Which <see cref="PilgrimPose"/> the body shows, and what stands in for a pose whose clip isn't made yet.</summary>
public static class PilgrimPoses
{
    /// <summary>A held moment, if any, over the movement's mode.</summary>
    public static PilgrimPose Of(MotionMode mode, PilgrimMoment moment) => moment switch
    {
        PilgrimMoment.Rest => PilgrimPose.Rest,
        PilgrimMoment.Relic => PilgrimPose.Relic,
        PilgrimMoment.Death => PilgrimPose.Death,
        _ => mode switch
        {
            MotionMode.Ground => PilgrimPose.Ground,
            MotionMode.Grip => PilgrimPose.Grip,
            MotionMode.Air => PilgrimPose.Air,
            MotionMode.Dash => PilgrimPose.Dash,
            MotionMode.AirDash => PilgrimPose.AirDash,
            MotionMode.Bore => PilgrimPose.Bore,
            MotionMode.Swim => PilgrimPose.Swim,
            MotionMode.Sink => PilgrimPose.Sink,
            MotionMode.Rise => PilgrimPose.Rise,
            MotionMode.Dive => PilgrimPose.Dive,
            MotionMode.Throw => PilgrimPose.Throw,
            MotionMode.Zip => PilgrimPose.Zip,
            _ => PilgrimPose.Ground,
        },
    };

    /// <summary>
    /// The nearest pose to show in place of <paramref name="pose"/> while it has no clip: every chain ends at
    /// <see cref="PilgrimPose.Ground"/>, which has none (it is the idle and the run).
    /// </summary>
    public static PilgrimPose? Fallback(PilgrimPose pose) => pose switch
    {
        PilgrimPose.Ground => null,
        PilgrimPose.AirDash or PilgrimPose.Bore => PilgrimPose.Dash,
        PilgrimPose.Sink or PilgrimPose.Rise => PilgrimPose.Swim,
        PilgrimPose.Swim or PilgrimPose.Dive or PilgrimPose.Throw or PilgrimPose.Zip => PilgrimPose.Air,
        _ => PilgrimPose.Ground,
    };

    /// <summary>
    /// How long the body takes to go from <paramref name="from"/> to <paramref name="to"/>, seconds: slow out of a held
    /// moment (standing up from a rest), quick into a dash (it lasts a fraction of a second), brisk otherwise.
    /// </summary>
    public static float Crossfade(PilgrimPose from, PilgrimPose to) => (from, to) switch
    {
        (PilgrimPose.Rest or PilgrimPose.Relic or PilgrimPose.Death, _) => 0.4f,
        (_, PilgrimPose.Rest or PilgrimPose.Relic or PilgrimPose.Death) => 0.25f,
        (_, PilgrimPose.Dash or PilgrimPose.AirDash or PilgrimPose.Bore) => 0.04f,
        _ => 0.1f,
    };
}
