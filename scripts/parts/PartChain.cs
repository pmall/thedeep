using Godot;

namespace TheDeep;

/// <summary>
/// A flowing piece of an assembly (coat tail, hair, cable, a lantern on its chain): a <see cref="VerletChain"/>
/// hanging from a joint, drawn as a ribbon or a tube that tapers, with an optional rigid pendant at its end.
/// Secondary motion only: it never touches gameplay.
/// </summary>
[GlobalClass]
public partial class PartChain : Resource
{
    /// <summary>Lets code find a chain's end (a lantern's light follows it). Unique within the assembly.</summary>
    [Export] public StringName Name { get; set; } = "";

    [Export] public StringName Joint { get; set; } = "";
    [Export] public PartCategory Category { get; set; } = PartCategory.Ornament;

    /// <summary>Where the chain hangs from, on the joint (m).</summary>
    [Export] public Vector3 Offset { get; set; }

    /// <summary>Rest direction of the first link in the joint's frame.</summary>
    [Export] public Vector3 RestDirection { get; set; } = Vector3.Down;

    /// <summary>Bend per link at rest around the joint's Z axis, degrees (hair sweeping back).</summary>
    [Export] public float RestCurl { get; set; }

    [Export] public int Links { get; set; } = 6;

    /// <summary>Total length, meters.</summary>
    [Export] public float Length { get; set; } = 0.5f;

    [ExportGroup("Look")]
    [Export] public ChainShape Shape { get; set; } = ChainShape.Ribbon;
    [Export] public Material? Material { get; set; }

    /// <summary>Width at the root (a ribbon's width, a tube's diameter), meters.</summary>
    [Export] public float Width { get; set; } = 0.12f;

    /// <summary>Width at the tip as a share of <see cref="Width"/>.</summary>
    [Export] public float Taper { get; set; } = 0.6f;

    /// <summary>A ribbon's thickness, meters.</summary>
    [Export] public float Thickness { get; set; } = 0.012f;

    /// <summary>A rigid piece hanging at the end (a lantern, a weight), oriented along the last link.</summary>
    [Export] public Mesh? Pendant { get; set; }
    [Export] public Material? PendantMaterial { get; set; }

    [ExportGroup("Motion")]
    [Export] public float Gravity { get; set; } = 9.8f;

    /// <summary>Air drag, 1/s: higher streams further behind a run.</summary>
    [Export] public float Drag { get; set; } = 2f;

    /// <summary>Pull back to the rest shape, 1/s: 0 hangs freely, high holds the shape.</summary>
    [Export] public float Stiffness { get; set; } = 4f;

    /// <summary>Keeps the chain above the floor the body stands on.</summary>
    [Export] public bool StaysAboveFloor { get; set; } = true;
}
