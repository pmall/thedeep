using Godot;

namespace TheDeep;

/// <summary>Beats time until it sees the player, then stalks and strikes, twice every few strikes (<see cref="ConductorBrain"/>).</summary>
[GlobalClass]
public partial class ConductorBehavior : MachineBehavior
{
    [Export] public float SightRange { get; set; } = 14f;
    [Export] public float Reach { get; set; } = 2.8f;
    [Export] public float Band { get; set; } = 2f;
    [Export] public float ChestHeight { get; set; } = 1.6f;
    [Export] public float Speed { get; set; } = 1.6f;
    [Export] public float Lunge { get; set; } = 7f;
    [Export] public int Flurry { get; set; } = 3;
    [Export] public float BeatPeriod { get; set; } = 1.2f;

    [ExportGroup("Strike")]
    [Export] public float WindUp { get; set; } = 0.7f;
    [Export] public float Blow { get; set; } = 0.14f;
    [Export] public float Recover { get; set; } = 1.1f;
    [Export] public float Cooldown { get; set; } = 0.5f;

    /// <summary>The second strike of a flurry: a shorter wind-up, still readable.</summary>
    [ExportGroup("Quick strike")]
    [Export] public float QuickWindUp { get; set; } = 0.4f;
    [Export] public float QuickRecover { get; set; } = 1.3f;

    public override MachineBrain CreateBrain(MachineSpecies species, int seed) =>
        new ConductorBrain(
            new StrikeAction { WindUp = WindUp, Blow = Blow, Recover = Recover, Cooldown = Cooldown },
            new StrikeAction { WindUp = QuickWindUp, Blow = Blow, Recover = QuickRecover, Cooldown = Cooldown })
        {
            SightRange = SightRange, Reach = Reach, Band = Band, ChestHeight = ChestHeight, Speed = Speed, Lunge = Lunge, Flurry = Flurry,
            BeatPeriod = BeatPeriod * (0.95f + 0.1f * Jitter(seed, 1)),
        };
}
