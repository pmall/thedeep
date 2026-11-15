using Godot;

namespace TheDeep;

/// <summary>Hovers high over the player, then winds up and dives through where they stand, hanging low after (<see cref="SwoopBrain"/>).</summary>
[GlobalClass]
public partial class SwoopBehavior : MachineBehavior
{
    [Export] public float SightRange { get; set; } = 16f;
    [Export] public float Altitude { get; set; } = 6f;
    [Export] public float Offset { get; set; } = 3f;
    [Export] public float DriftSpeed { get; set; } = 4f;
    [Export] public float Interval { get; set; } = 2.4f;

    [ExportGroup("Dive")]
    [Export] public float WindUp { get; set; } = 0.7f;
    [Export] public float QuickWindUp { get; set; } = 0.4f;
    [Export] public float DiveSpeed { get; set; } = 15f;
    [Export] public float Overshoot { get; set; } = 2.5f;
    [Export] public float DiveTime { get; set; } = 1.1f;
    [Export] public float Hang { get; set; } = 1.4f;
    [Export] public int Flurry { get; set; } = 3;

    [ExportGroup("Bearing")]
    [Export] public float ArriveRadius { get; set; } = 2f;
    [Export] public float WindShudder { get; set; } = 0.6f;
    [Export] public float HangSag { get; set; } = 0.3f;
    [Export] public float HangAlert { get; set; } = 0.4f;
    [Export] public float AlertRate { get; set; } = 6f;
    [Export] public float BobRate { get; set; } = 1.3f;

    public override MachineBrain CreateBrain(MachineSpecies species, int seed) => new SwoopBrain
    {
        SightRange = SightRange,
        Altitude = Altitude,
        Offset = Offset,
        DriftSpeed = DriftSpeed,
        Interval = Interval * (0.95f + 0.1f * Jitter(seed, 1)),
        WindUp = WindUp,
        QuickWindUp = QuickWindUp,
        DiveSpeed = DiveSpeed,
        Overshoot = Overshoot,
        DiveTime = DiveTime,
        Hang = Hang,
        Flurry = Flurry,
        ArriveRadius = ArriveRadius,
        WindShudder = WindShudder,
        HangSag = HangSag,
        HangAlert = HangAlert,
        AlertRate = AlertRate,
        BobRate = BobRate,
    };
}
