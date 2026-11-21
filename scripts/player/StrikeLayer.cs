using System;
using Godot;

namespace TheDeep;

/// <summary>
/// The sword strikes in the pilgrim's <see cref="AnimationTree"/> (built by <see cref="PlayerVisual"/>): each kind's clip
/// (<see cref="PilgrimClips.Of(StrikeKind)"/>, keyed over one second for its whole progress) scrubbed to the strike's
/// progress, laid over the pose twice: on the trunk and arms only (over a run, a jump) and on the whole body (standing,
/// the down and dash strikes), each by its share of the strike's weight (<see cref="PilgrimAnimation.StrikeWhole"/>).
/// </summary>
public sealed class StrikeLayer
{
    private static readonly StringName UpperNode = "strike_upper";
    private static readonly StringName WholeNode = "strike_whole";
    private static readonly StringName UpperAmount = "parameters/strike_upper/blend_amount";
    private static readonly StringName WholeAmount = "parameters/strike_whole/blend_amount";

    /// <summary>The bones a strike drives over a run or a jump: from the waist up.</summary>
    private static readonly string[] UpperBones =
    {
        "Spine", "Chest", "UpperChest", "Neck", "Head",
        "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
        "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
    };

    private readonly Source _upper = new("strike_upper_");
    private readonly Source _whole = new("strike_whole_");
    private StrikeKind? _shownKind;

    /// <summary>
    /// Adds the layer over <paramref name="under"/> to <paramref name="root"/>, its clips from <paramref name="clips"/> (the
    /// swing's for a kind without one), the trunk filtered by the bones of <paramref name="skeleton"/> (its path from the
    /// tree's root node). Returns the node on top.
    /// </summary>
    public StringName Build(AnimationNodeBlendTree root, StringName under, AnimationPlayer clips, string skeleton)
    {
        var upper = new AnimationNodeBlend2 { FilterEnabled = true };
        foreach (string bone in UpperBones)
        {
            using var path = new NodePath($"{skeleton}:{bone}");
            upper.SetFilterPath(path, true);
        }

        root.AddNode(UpperNode, upper);
        root.ConnectNode(UpperNode, 0, under);
        root.ConnectNode(UpperNode, 1, _upper.Build(root, clips));
        root.AddNode(WholeNode, new AnimationNodeBlend2());
        root.ConnectNode(WholeNode, 0, UpperNode);
        root.ConnectNode(WholeNode, 1, _whole.Build(root, clips));
        return WholeNode;
    }

    /// <summary>Shows <paramref name="strike"/>, driving <paramref name="whole"/> of the body (0 to 1).</summary>
    public void Show(AnimationTree tree, StrikeShow strike, float whole)
    {
        tree.Set(UpperAmount, strike.Weight * (1f - whole));
        tree.Set(WholeAmount, strike.Weight * whole);
        if (strike.Weight <= 0f)
        {
            return;
        }

        bool changed = _shownKind != strike.Kind;
        _shownKind = strike.Kind;
        _upper.Show(tree, strike, changed);
        _whole.Show(tree, strike, changed);
    }

    /// <summary>One copy of the strikes' clips (a node is fed into one input only: two would advance it twice).</summary>
    private sealed class Source
    {
        private readonly NameCache<string> _names = new(name => name);
        private readonly StringName _kindNode;
        private readonly StringName _kindRequest;
        private readonly NameCache<StrikeKind> _clipNodes;
        private readonly NameCache<StrikeKind> _seekNodes;
        private readonly NameCache<StrikeKind> _seekRequests;
        private readonly NameCache<StrikeKind> _clips = new(PilgrimClips.Of);
        private readonly NameCache<StrikeKind> _inputs = new(kind => kind.ToString());

        public Source(string prefix)
        {
            _kindNode = _names[prefix + "kind"];
            _kindRequest = _names[$"parameters/{prefix}kind/transition_request"];
            _clipNodes = new(kind => $"{prefix}clip_{kind}");
            _seekNodes = new(kind => $"{prefix}seek_{kind}");
            _seekRequests = new(kind => $"parameters/{prefix}seek_{kind}/seek_request");
        }

        public StringName Build(AnimationNodeBlendTree root, AnimationPlayer clips)
        {
            StrikeKind[] kinds = Enum.GetValues<StrikeKind>();
            var transition = new AnimationNodeTransition { XfadeTime = 0f, InputCount = kinds.Length };
            root.AddNode(_kindNode, transition);
            foreach (StrikeKind kind in kinds)
            {
                StringName clip = clips.HasAnimation(_clips[kind]) ? _clips[kind] : _clips[StrikeKind.Swing];
                root.AddNode(_clipNodes[kind], new AnimationNodeAnimation { Animation = clip });
                root.AddNode(_seekNodes[kind], new AnimationNodeTimeSeek());
                root.ConnectNode(_seekNodes[kind], 0, _clipNodes[kind]);
                transition.SetInputName((int)kind, _inputs[kind]);
                root.ConnectNode(_kindNode, (int)kind, _seekNodes[kind]);
            }

            return _kindNode;
        }

        public void Show(AnimationTree tree, StrikeShow strike, bool changed)
        {
            if (changed)
            {
                using Variant request = _inputs[strike.Kind];
                tree.Set(_kindRequest, request);
            }

            tree.Set(_seekRequests[strike.Kind], (double)strike.Progress);
        }
    }
}
