using Godot;

namespace TheDeep;

/// <summary>
/// One rigid part of an assembly: a mesh and its material, placed on a joint. Any mesh works, so a
/// procedural part (PodMesh, PlateMesh, RingMesh, a kit piece) can be swapped for an authored one.
/// </summary>
[GlobalClass]
public partial class PartSlot : Resource
{
    /// <summary>Lets a variant assembly replace this part (<see cref="PartAssembly.Base"/>); empty for a part nothing replaces. Unique within the assembly.</summary>
    [Export] public StringName Name { get; set; } = Names.None;

    [Export] public StringName Joint { get; set; } = Names.None;
    [Export] public PartCategory Category { get; set; } = PartCategory.Chassis;
    [Export] public Mesh? Mesh { get; set; }
    [Export] public Material? Material { get; set; }

    /// <summary>Placement on the joint: position (m), rotation (degrees, XYZ Euler) and scale.</summary>
    [Export] public Vector3 Position { get; set; }
    [Export] public Vector3 Rotation { get; set; }
    [Export] public Vector3 Scale { get; set; } = Vector3.One;
}
