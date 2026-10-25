using Godot;

namespace TheDeep;

/// <summary>Once it has seen the player, follows at a distance, behind and above; never attacks (<see cref="FollowerBrain"/>).</summary>
[GlobalClass]
public partial class FollowerBehavior : MachineBehavior
{
    [Export] public float SightRange { get; set; } = 12f;
    [Export] public float Distance { get; set; } = 4f;
    [Export] public float Height { get; set; } = 1.6f;
    [Export] public float Shy { get; set; } = 2.2f;
    [Export] public float Speed { get; set; } = 6f;

    public override MachineBrain CreateBrain(MachineSpecies species, int seed) => new FollowerBrain
    {
        SightRange = SightRange, Distance = Distance * (0.9f + 0.2f * Jitter(seed, 1)), Height = Height, Shy = Shy, Speed = Speed,
    };
}
