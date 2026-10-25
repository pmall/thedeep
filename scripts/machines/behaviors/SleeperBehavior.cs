using Godot;

namespace TheDeep;

/// <summary>Sleeps until a hard impact nearby, then hunts toward it and strikes; dozes off again when nothing happens (<see cref="SleeperBrain"/>).</summary>
[GlobalClass]
public partial class SleeperBehavior : MachineBehavior
{
    [Export] public float HearingRange { get; set; } = 10f;
    [Export] public float WakeThreshold { get; set; } = 7f;
    [Export] public float TrackRange { get; set; } = 9f;
    [Export] public float Speed { get; set; } = 3f;
    [Export] public float Reach { get; set; } = 1.3f;
    [Export] public float WakeTime { get; set; } = 0.7f;
    [Export] public float Patience { get; set; } = 5f;

    [ExportGroup("Strike")]
    [Export] public float WindUp { get; set; } = 0.35f;
    [Export] public float Blow { get; set; } = 0.1f;
    [Export] public float Recover { get; set; } = 0.5f;
    [Export] public float Cooldown { get; set; } = 0.9f;

    public override MachineBrain CreateBrain(MachineSpecies species, int seed) =>
        new SleeperBrain(new StrikeAction { WindUp = WindUp, Blow = Blow, Recover = Recover, Cooldown = Cooldown })
        {
            HearingRange = HearingRange, WakeThreshold = WakeThreshold, TrackRange = TrackRange, Speed = Speed,
            Reach = Reach, WakeTime = WakeTime, Patience = Patience,
        };
}
