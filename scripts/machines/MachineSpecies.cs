using Godot;

namespace TheDeep;

/// <summary>
/// A machine species as data (docs/long-term.md, objective 1): who it is, its body (parts from the shared
/// library), how it moves, its stats, its palette, its behavior and its animation. Machines in rooms are
/// all the same <see cref="Machine"/> node pointing at a species; adding a species is one .tres in
/// <see cref="Folder"/>, plus a brain only when its behavior is truly new.
/// </summary>
[GlobalClass]
public partial class MachineSpecies : Resource
{
    public const string Folder = "res://resources/machines";

    /// <summary>Stable id (the file name): neighbors recognize their kind by it, the bestiary keys on it.</summary>
    [Export] public StringName Id { get; set; } = "";

    [Export] public string DisplayName { get; set; } = "";
    [Export] public DepthLayer Layer { get; set; } = DepthLayer.Flooded;

    /// <summary>Can it hurt the player? About a third of species should not (docs/long-term.md).</summary>
    [Export] public bool Hostile { get; set; }

    /// <summary>Id of its creed, a line in <see cref="LineBook.Path"/> (placeholder text, stored as data for voicing later).</summary>
    [Export] public StringName CreedLine { get; set; } = "";

    [ExportGroup("Body")]
    [Export] public PartAssembly Assembly { get; set; } = null!;
    [Export] public Locomotion Locomotion { get; set; } = Locomotion.Walk;

    /// <summary>Collision box (width, height), meters; it stands on the origin, or is centered on it for flyers.</summary>
    [Export] public Vector2 BodySize { get; set; } = new(0.8f, 1f);

    /// <summary>Height of its eyes above its origin, meters: where its line of sight starts.</summary>
    [Export] public float SensorHeight { get; set; } = 0.8f;

    /// <summary>How fast it turns around, 1/s.</summary>
    [Export] public float TurnSharpness { get; set; } = 8f;

    [ExportGroup("Stats")]
    [Export] public float MaxSpeed { get; set; } = 3f;

    /// <summary>m/s².</summary>
    [Export] public float Acceleration { get; set; } = 20f;

    /// <summary>Hits it takes (combat comes with M6).</summary>
    [Export] public int Health { get; set; } = 3;

    /// <summary>Health a landed blow takes from the pilgrim.</summary>
    [Export] public int Damage { get; set; } = 1;

    /// <summary>How long a hit stuns its brain (a wind-up is lost), seconds; 0 for a machine that shrugs hits off (a boss).</summary>
    [Export] public float StaggerTime { get; set; } = 0.35f;

    /// <summary>How far a hit pushes it, as a share of the swing's knockback (a heavy machine barely moves).</summary>
    [Export(PropertyHint.Range, "0,1")] public float Heft { get; set; } = 1f;

    /// <summary>How far from the player's chest, measured from its sensors, a landing blow still counts, meters.</summary>
    [Export] public float StrikeRange { get; set; } = 2.6f;

    /// <summary>Rust on its edges and in its grooves, 0 to 1. Its colors come from the biome palette through its materials.</summary>
    [ExportGroup("Palette")]
    [Export(PropertyHint.Range, "0,1")] public float Wear { get; set; } = 0.3f;

    /// <summary>Lighter or darker than the palette, per species.</summary>
    [Export(PropertyHint.Range, "0,2")] public float Tone { get; set; } = 1f;

    [ExportGroup("Behavior")]
    [Export] public MachineBehavior Behavior { get; set; } = null!;

    [ExportGroup("Animation")]
    [Export] public JointDriver[] Drivers { get; set; } = System.Array.Empty<JointDriver>();
    [Export] public MachineLeg[] Legs { get; set; } = System.Array.Empty<MachineLeg>();
}
