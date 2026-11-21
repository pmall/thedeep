namespace TheDeep;

/// <summary>
/// What the pilgrim's body shows as a whole, each from its own clips (<see cref="PilgrimClips"/>): the movement's
/// <see cref="MotionMode"/>, or a held <see cref="PilgrimMoment"/> over it. Picked by <see cref="PilgrimPoses.Of"/>.
/// </summary>
public enum PilgrimPose
{
    /// <summary>Standing or running: the idle and the run blended by speed.</summary>
    Ground,
    Air,
    Dash,
    AirDash,
    Bore,
    Grip,
    Swim,
    Sink,
    Rise,
    Dive,
    Throw,
    Zip,
    Rest,
    Relic,
    Death,
}
