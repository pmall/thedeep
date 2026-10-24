using Godot;

namespace TheDeep;

/// <summary>
/// One rigid part of an assembly: a mesh and its material, placed on a joint. Any mesh works, so a
/// procedural part (PodMesh, PlateMesh, RingMesh, a kit piece) can be swapped for an authored one.
/// </summary>
[GlobalClass]
public partial class PartSlot : Resource
{
    [Export] public StringName Joint { get; set; } = "";
    [Export] public PartCategory Category { get; set; } = PartCategory.Chassis;
    [Export] public Mesh? Mesh { get; set; }
    [Export] public Material? Material { get; set; }

    /// <summary>Placement on the joint: position (m), rotation (degrees, XYZ Euler) and scale.</summary>
    [Export] public Vector3 Position { get; set; }
    [Export] public Vector3 Rotation { get; set; }
    [Export] public Vector3 Scale { get; set; } = Vector3.One;
}
