using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// A body built from modular parts (docs/art-direction.md#shared-parts): a rig of joints, rigid parts on them,
/// and flowing chains. The pilgrim is one; machine species will be others, sharing the part library.
/// A <see cref="PartBody"/> node builds it; behavior code animates its joints.
/// <para>
/// A variant of another body (a boss that is a bigger rank-and-file machine, a rusted or glowing one) names its
/// <see cref="Base"/> and its differences instead of copying it: <see cref="Scale"/>, <see cref="MaterialSwaps"/>
/// and its own joints, parts and chains, which replace the base's of the same name or are added. Everything that
/// reads a body reads <see cref="Resolved"/>, so nothing downstream sees the difference.
/// </para>
/// </summary>
[GlobalClass]
public partial class PartAssembly : Resource
{
    private PartAssembly? _resolved;

    /// <summary>The body this one is a variant of, or null for a body of its own.</summary>
    [Export] public PartAssembly? Base { get; set; }

    /// <summary>With a <see cref="Base"/>: the factor the whole body (merged with this one's own entries) is scaled by, uniformly.</summary>
    [Export] public float Scale { get; set; } = 1f;

    /// <summary>With a <see cref="Base"/>: its materials replaced here (key to value) on the inherited parts and chains.</summary>
    [Export] public Godot.Collections.Dictionary<Material, Material> MaterialSwaps { get; set; } = new();

    /// <summary>Its own joints; with a <see cref="Base"/>, only those that replace one of the same <see cref="RigJoint.Name"/> or are new, in the base's size.</summary>
    [Export] public RigJoint[] Joints { get; set; } = System.Array.Empty<RigJoint>();

    /// <summary>Its own parts; with a <see cref="Base"/>, only those that replace one of the same <see cref="PartSlot.Name"/> or are new, in the base's size.</summary>
    [Export] public PartSlot[] Parts { get; set; } = System.Array.Empty<PartSlot>();

    /// <summary>Its own chains; with a <see cref="Base"/>, only those that replace one of the same <see cref="PartChain.Name"/> or are new, in the base's size.</summary>
    [Export] public PartChain[] Chains { get; set; } = System.Array.Empty<PartChain>();

    /// <summary>The body to build: this one itself, or with a <see cref="Base"/> the variant worked out once (a new assembly sharing the base's meshes).</summary>
    public PartAssembly Resolved => Base is null ? this : _resolved ??= Resolve(Base.Resolved);

    private PartAssembly Resolve(PartAssembly parent)
    {
        float scale = Scale;
        RigJoint[] joints = Overlay.Merge(parent.Joints, Joints, j => j.Name.ToString()).Select(j => ScaledJoint(j, scale)).ToArray();
        PartSlot[] parts = Overlay.Merge(parent.Parts.Select(Swapped), Parts, p => p.Name.ToString()).Select(p => ScaledSlot(p, scale)).ToArray();
        PartChain[] chains = Overlay.Merge(parent.Chains.Select(Swapped), Chains, c => c.Name.ToString()).Select(c => ScaledChain(c, scale)).ToArray();
        return new PartAssembly { Joints = joints, Parts = parts, Chains = chains };
    }

    private Material? Swap(Material? material) => material is not null && MaterialSwaps.TryGetValue(material, out Material? to) ? to : material;

    private PartSlot Swapped(PartSlot slot)
    {
        var copy = (PartSlot)slot.Duplicate();
        copy.Material = Swap(slot.Material);
        return copy;
    }

    private PartChain Swapped(PartChain chain)
    {
        var copy = (PartChain)chain.Duplicate();
        copy.Material = Swap(chain.Material);
        copy.PendantMaterial = Swap(chain.PendantMaterial);
        return copy;
    }

    private static RigJoint ScaledJoint(RigJoint joint, float scale)
    {
        var copy = (RigJoint)joint.Duplicate();
        copy.Offset = joint.Offset * scale;
        return copy;
    }

    private static PartSlot ScaledSlot(PartSlot slot, float scale)
    {
        var copy = (PartSlot)slot.Duplicate();
        copy.Position = slot.Position * scale;
        copy.Scale = slot.Scale * scale;
        return copy;
    }

    private static PartChain ScaledChain(PartChain chain, float scale)
    {
        var copy = (PartChain)chain.Duplicate();
        copy.Offset = chain.Offset * scale;
        copy.Length = chain.Length * scale;
        copy.Width = chain.Width * scale;
        copy.Thickness = chain.Thickness * scale;
        copy.PendantScale = chain.PendantScale * scale;
        return copy;
    }
}
