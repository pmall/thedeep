using Godot;

namespace TheDeep;

/// <summary>
/// A body built from modular parts (docs/long-term.md, objective 1): a rig of joints, rigid parts on them,
/// and flowing chains. The pilgrim is one; machine species will be others, sharing the part library.
/// A <see cref="PartBody"/> node builds it; behavior code animates its joints.
/// </summary>
[GlobalClass]
public partial class PartAssembly : Resource
{
    [Export] public RigJoint[] Joints { get; set; } = System.Array.Empty<RigJoint>();
    [Export] public PartSlot[] Parts { get; set; } = System.Array.Empty<PartSlot>();
    [Export] public PartChain[] Chains { get; set; } = System.Array.Empty<PartChain>();
}
