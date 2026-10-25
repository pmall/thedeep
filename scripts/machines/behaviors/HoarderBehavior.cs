using Godot;

namespace TheDeep;

/// <summary>Gleans scrap around its home and bolts from the player with it; never fights (<see cref="HoarderBrain"/>).</summary>
[GlobalClass]
public partial class HoarderBehavior : MachineBehavior
{
    [Export] public float HomeRange { get; set; } = 4f;
    [Export] public float WalkSpeed { get; set; } = 1.3f;
    [Export] public float FleeSpeed { get; set; } = 6.5f;
    [Export] public float Wary { get; set; } = 4.5f;
    [Export] public float Safe { get; set; } = 10f;
    [Export] public float MinWork { get; set; } = 1.5f;
    [Export] public float MaxWork { get; set; } = 3.5f;

    public override MachineBrain CreateBrain(MachineSpecies species, int seed) => new HoarderBrain(seed)
    {
        HomeRange = HomeRange, WalkSpeed = WalkSpeed, FleeSpeed = FleeSpeed, Wary = Wary, Safe = Safe,
        MinWork = MinWork, MaxWork = MaxWork,
    };
}
