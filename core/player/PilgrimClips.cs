namespace TheDeep;

/// <summary>
/// The names of the pilgrim's animation clips, as the build names them (art/pilgrim/clips.py; Godot's importer drops a
/// looping clip's "_loop"). A pose's clip is its name in snake case (<see cref="Of"/>); the ground's are the idle and the run.
/// </summary>
public static class PilgrimClips
{
    public const string Idle = "idle";
    public const string Run = "run";

    /// <summary>The clip of a pose other than <see cref="PilgrimPose.Ground"/>.</summary>
    public static string Of(PilgrimPose pose) => pose switch
    {
        PilgrimPose.Ground => Idle,
        PilgrimPose.Air => "air",
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
}
