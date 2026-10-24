using Godot;

namespace TheDeep;

/// <summary>One joint of a part assembly's rig: where it sits on its parent joint at rest. Rotations come from the animation.</summary>
[GlobalClass]
public partial class RigJoint : Resource
{
    /// <summary>Unique within the assembly. Humanoid rigs use the <see cref="HumanoidBone"/> names.</summary>
    [Export] public StringName Name { get; set; } = "";

    /// <summary>The joint this one hangs from; empty for a root joint (on the assembly's origin).</summary>
    [Export] public StringName Parent { get; set; } = "";

    /// <summary>Position on the parent joint at rest, meters (X forward, Y up, Z toward the camera when facing right).</summary>
    [Export] public Vector3 Offset { get; set; }
}
