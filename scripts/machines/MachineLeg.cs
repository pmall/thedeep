using Godot;

namespace TheDeep;

/// <summary>
/// A walking leg of a machine: two jointed segments solved by IK in the side plane so the foot steps along
/// the ground (<see cref="LegGait"/>): planted while the body passes over it, then swung forward. Its
/// <see cref="Upper"/> and <see cref="Lower"/> joints must not also be posed by drivers.
/// </summary>
[GlobalClass]
public partial class MachineLeg : Resource
{
    /// <summary>The hip (thigh's joint), the knee (shin's joint), and the foot at the end of the shin.</summary>
    [Export] public StringName Upper { get; set; } = "";
    [Export] public StringName Lower { get; set; } = "";
    [Export] public StringName Foot { get; set; } = "";

    /// <summary>Where this leg is in the gait cycle, 0 to 1 (alternate legs half a cycle apart).</summary>
    [Export] public float Phase { get; set; }

    /// <summary>Meters the body travels while the foot stays planted.</summary>
    [Export] public float Stride { get; set; } = 0.35f;

    /// <summary>How high the foot lifts in its swing, meters.</summary>
    [Export] public float Lift { get; set; } = 0.08f;

    /// <summary>Share of the cycle the foot is planted.</summary>
    [Export(PropertyHint.Range, "0.1,0.9")] public float Stance { get; set; } = 0.6f;

    /// <summary>+1 bends the knee forward (+X), -1 backward.</summary>
    [Export] public int Bend { get; set; } = 1;
}
