using Godot;

namespace TheDeep;

/// <summary>Prays in place, in step with its kind; strikes only who passes in front of it (<see cref="PrayerBrain"/>).</summary>
[GlobalClass]
public partial class PrayerBehavior : MachineBehavior
{
    /// <summary>Seconds per bow (each machine varies it by up to 10%).</summary>
    [Export] public float Period { get; set; } = 3.4f;

    [Export] public float Reach { get; set; } = 2f;
    [Export] public float Band { get; set; } = 1.4f;
    [Export] public float ChestHeight { get; set; } = 1f;
    [Export] public float Congregation { get; set; } = 8f;
    [Export] public float Calm { get; set; } = 1.6f;

    [ExportGroup("Strike")]
    [Export] public float WindUp { get; set; } = 0.45f;
    [Export] public float Blow { get; set; } = 0.12f;
    [Export] public float Recover { get; set; } = 0.7f;
    [Export] public float Cooldown { get; set; } = 0.6f;

    public override MachineBrain CreateBrain(MachineSpecies species, int seed) =>
        new PrayerBrain(species.Id, Period * (0.9f + 0.2f * Jitter(seed, 1)), Jitter(seed, 2),
            new StrikeAction { WindUp = WindUp, Blow = Blow, Recover = Recover, Cooldown = Cooldown })
        {
            Reach = Reach, Band = Band, ChestHeight = ChestHeight, Congregation = Congregation, Calm = Calm,
        };
}
