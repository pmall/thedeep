namespace TheDeep;

/// <summary>
/// The names of the pilgrim's animation clips, as the build names them (art/pilgrim/clips.py; Godot's importer drops a
/// looping clip's "_loop"). A pose's clip is its name in snake case (<see cref="Of(PilgrimPose)"/>); the ground's are the idle and the run,
/// the air's the jump, the leap and the fall; the cues' and the strikes' are theirs (<see cref="Of(PilgrimCue)"/>,
/// <see cref="Of(StrikeKind)"/>).
/// </summary>
public static class PilgrimClips
{
    public const string Idle = "idle";
    public const string Run = "run";

    /// <summary>Rising from a standing jump: the knees tucked up.</summary>
    public const string Jump = "jump";

    /// <summary>Rising from a running jump: split into a leap.</summary>
    public const string Leap = "leap";

    /// <summary>Falling: reaching down for the ground.</summary>
    public const string Fall = "fall";

    /// <summary>The clip of a pose made of one (the ground and the air are made of several: their first).</summary>
    public static string Of(PilgrimPose pose) => pose switch
    {
        PilgrimPose.Ground => Idle,
        PilgrimPose.Air => Jump,
        PilgrimPose.Dash => "dash",
        PilgrimPose.AirDash => "air_dash",
        PilgrimPose.Bore => "bore",
        PilgrimPose.Grip => "grip",
        PilgrimPose.Swim => "swim",
        PilgrimPose.Sink => "sink",
        PilgrimPose.Rise => "rise",
        PilgrimPose.Dive => "dive",
        PilgrimPose.Throw => "throw",
        PilgrimPose.Zip => "zip",
        PilgrimPose.Rest => "rest",
        PilgrimPose.Relic => "relic",
        PilgrimPose.Death => "death",
        _ => Idle,
    };

    /// <summary>The clip of a cue.</summary>
    public static string Of(PilgrimCue cue) => cue switch
    {
        PilgrimCue.Land => "land",
        PilgrimCue.HardLand => "hard_land",
        PilgrimCue.Hurt => "hurt",
        PilgrimCue.Vault => "vault",
        _ => "none",
    };

    /// <summary>The clip of a sword strike, keyed over its whole progress (one second for all of it).</summary>
    public static string Of(StrikeKind kind) => kind switch
    {
        StrikeKind.Swing => "strike_swing",
        StrikeKind.Second => "strike_second",
        StrikeKind.Finisher => "strike_finisher",
        StrikeKind.Down => "strike_down",
        StrikeKind.Dash => "strike_dash",
        _ => "strike_swing",
    };
}
