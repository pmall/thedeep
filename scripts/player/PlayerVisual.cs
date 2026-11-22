using System;
using Godot;

namespace TheDeep;

/// <summary>
/// The pilgrim's body: the model built in Blender (art/pilgrim/build.py, imported from assets/pilgrim/pilgrim.glb), its
/// clips played by an <see cref="AnimationTree"/> from what the core's <see cref="PilgrimAnimation"/> decides each physics
/// tick: the pose (crossfaded), the idle giving way to the run, the run's rate matched to the speed. A pose whose clip isn't
/// made yet shows its <see cref="PilgrimPoses.Fallback"/>'s; a change of pose crossfades for as long as
/// <see cref="PilgrimPoses.Crossfade"/> says. The tree is built here from the clips the model holds, and
/// advanced on physics ticks (<see cref="Animate"/>), so a hit stop, which skips them, holds the body still. The clips'
/// travel is on the skeleton's root bone, taken out as root motion (the movement moves the body): the run's sets its speed.
/// A strike draws the blade out of the near forearm (<see cref="Blade"/>, slid out along its length) and its sweep leaves
/// the <see cref="Slash"/>. Footfalls are read from where the soles are (<see cref="Footfalls"/>). The body turns smoothly
/// through a glance at the camera when the facing flips, and leans into a change of speed (<see cref="PilgrimAnimation.Lean"/>); the hair swings after it on springs. Pivot is at the feet.
/// </summary>
public partial class PlayerVisual : Node3D
{
    private static readonly StringName PoseNode = "pose";
    private static readonly StringName GaitNode = "gait";
    private static readonly StringName RunRateNode = "run_rate";
    private static readonly StringName RunNode = "run";
    private static readonly StringName IdleNode = "idle";
    private static readonly StringName OutputNode = "output";
    private static readonly StringName JumpNode = "jump";
    private static readonly StringName LeapNode = "leap";
    private static readonly StringName AirLeapNode = "air_leap";
    private static readonly StringName TreadNode = "tread";
    private static readonly StringName StrokeNode = "stroke";
    private static readonly StringName TreadClip = PilgrimClips.Tread;
    private static readonly StringName StrokeParameter = "parameters/stroke/blend_amount";
    private static readonly StringName JumpSeekNode = "jump_seek";
    private static readonly StringName LeapSeekNode = "leap_seek";
    private static readonly StringName JumpClip = PilgrimClips.Jump;
    private static readonly StringName LeapClip = PilgrimClips.Leap;
    private static readonly StringName LeapParameter = "parameters/air_leap/blend_amount";
    private static readonly StringName JumpSeekParameter = "parameters/jump_seek/seek_request";
    private static readonly StringName LeapSeekParameter = "parameters/leap_seek/seek_request";
    private static readonly StringName GaitParameter = "parameters/gait/blend_amount";
    private static readonly StringName RunRateParameter = "parameters/run_rate/scale";
    private static readonly StringName PoseParameter = "parameters/pose/transition_request";
    private static readonly StringName HurtParameter = "hurt";
    private static readonly StringName IdleClip = PilgrimClips.Idle;
    private static readonly StringName RunClip = PilgrimClips.Run;
    private static readonly NameCache<PilgrimPose> PoseNames = new(pose => pose.ToString());
    private static readonly NameCache<PilgrimPose> ClipNodeNames = new(pose => "clip_" + pose);
    private static readonly NameCache<PilgrimPose> ClipNames = new(PilgrimClips.Of);
    private static readonly NameCache<PilgrimPose> RateNodeNames = new(pose => "rate_" + pose);
    private static readonly NameCache<PilgrimPose> RateParameters = new(pose => $"parameters/rate_{pose}/scale");
    private static readonly NameCache<PilgrimCue> CueClips = new(PilgrimClips.Of);
    private static readonly NameCache<PilgrimCue> CueNodes = new(cue => "cue_" + cue);
    private static readonly NameCache<PilgrimCue> CueRequests = new(cue => $"parameters/cue_{cue}/request");
    private bool[] _cueShown = Array.Empty<bool>();

    /// <summary>The model's player, which holds the clips (inside the imported scene).</summary>
    [Export] public AnimationPlayer Clips { get; set; } = null!;

    /// <summary>The tree that plays them, built in <see cref="_Ready"/>.</summary>
    [Export] public AnimationTree Tree { get; set; } = null!;

    /// <summary>The model's skeleton.</summary>
    [Export] public Skeleton3D Skeleton { get; set; } = null!;

    /// <summary>The model's mesh, which shimmers while the pilgrim can't be hurt.</summary>
    [Export] public GeometryInstance3D Mesh { get; set; } = null!;

    /// <summary>The track of the skeleton's root bone in the clips, relative to the tree's root node: the clips' travel.</summary>
    [Export] public NodePath RootTrack { get; set; } = new("Pilgrim/Skeleton3D:Root");

    /// <summary>What the blade swings around: the near shoulder.</summary>
    [Export] public Node3D Shoulder { get; set; } = null!;

    /// <summary>The blade, on the near forearm, hanging down its -Y axis.</summary>
    [Export] public Node3D Blade { get; set; } = null!;

    /// <summary>Turns the near upper arm toward <see cref="ReachTarget"/> while on the Harpoon's line (its influence faded in and out).</summary>
    [Export] public LookAtModifier3D Aim { get; set; } = null!;

    /// <summary>What <see cref="Aim"/> looks at: placed along the line from the shoulder.</summary>
    [Export] public Node3D ReachTarget { get; set; } = null!;

    /// <summary>The slash the blade's sweep leaves, or none.</summary>
    [Export] public SlashTrail? Slash { get; set; }

    /// <summary>How long a cue (a landing, a recoil) takes to come in over the pose and to give it back, seconds.</summary>
    [Export] public float CueFadeIn { get; set; } = 0.05f;

    [Export] public float CueFadeOut { get; set; } = 0.2f;

    /// <summary>How fast the body turns around when the facing flips, 1/s (higher is snappier).</summary>
    [Export] public float TurnSharpness { get; set; } = 20f;

    /// <summary>How fast the body eases into a new pose after the collider snapped (a Mag-grip corner), 1/s.</summary>
    [Export] public float CarrySharpness { get; set; } = 16f;

    /// <summary>The soles' height over their bones at rest (ankle, ball of the foot), meters: the model's (art/pilgrim/skeleton.py).</summary>
    [Export] public float AnkleHeight { get; set; } = 0.085f;

    [Export] public float BallHeight { get; set; } = 0.025f;

    /// <summary>How long each shown pose holds before the next, seconds: 0 shows every tick (smooth), more shows the pose on
    /// twos or threes as drawn animation is (set by a look: <see cref="ToonLook"/>). The body still travels every tick.</summary>
    public float PoseHoldSeconds
    {
        get => _poseClock.HoldSeconds;
        set => _poseClock.HoldSeconds = value;
    }

    private readonly PoseClock _poseClock = new();

    /// <summary>The locks of the hair (art/pilgrim/skeleton.py: LOCKS), each two bones swung on springs.</summary>
    private static readonly string[] Locks = { "HairBack", "HairLeft", "HairRight" };

    /// <summary>How the hair swings: how fast a lock springs back to where the clip holds it, how much its swing is damped,
    /// and how hard it hangs down.</summary>
    [Export] public float HairStiffness { get; set; } = 0.7f;

    [Export] public float HairDrag { get; set; } = 0.35f;

    [Export] public float HairGravity { get; set; } = 0.4f;

    private SpringBoneSimulator3D? _hair;

    private static readonly StringName FootR = "RightFoot";
    private static readonly StringName FootL = "LeftFoot";
    private static readonly StringName ToesR = "RightToes";
    private static readonly StringName ToesL = "LeftToes";

    private PilgrimAnimation? _animation;
    private readonly Footfalls _footfalls = new();
    private readonly StrikeLayer _strikes = new();
    private AnimationNodeTransition? _transition;
    private PilgrimPose _shownPose;
    private int _footR = -1, _footL = -1, _toesR = -1, _toesL = -1;
    private float _bladeLength;
    private int _facing = 1;
    private float _yaw;
    private int _blinkTime;
    private float _shimmer;

    /// <summary>Where the visible body still is relative to the collider after a snap; eases back to identity.</summary>
    private Transform3D _carry = Transform3D.Identity;

    /// <summary>A foot was planted at this world position, the right (+1) or the left (-1).</summary>
    public event Action<Vector3, int>? Footfall;

    /// <summary>How much the run shows, 0 standing to 1 running.</summary>
    public float Gait => _animation?.Gait ?? 0f;

    private PilgrimAnimation Animation => _animation ??= new PilgrimAnimation(
        new ClipSpeeds(ClipSpeed(RunClip), ClipSpeed(ClipNames[PilgrimPose.Grip]), ClipSpeed(ClipNames[PilgrimPose.Swim])));

    public override void _Ready()
    {
        _footR = Skeleton.FindBone(FootR);
        _footL = Skeleton.FindBone(FootL);
        _toesR = Skeleton.FindBone(ToesR);
        _toesL = Skeleton.FindBone(ToesL);
        foreach (Node child in Blade.Children())
        {
            if (child is MeshInstance3D part && part.Mesh is not null)
            {
                _bladeLength = Mathf.Max(_bladeLength, part.Mesh.GetAabb().Size.Y);
            }
        }

        BuildTree();
        BuildHair();
        ShowBlade(0f);
    }

    /// <summary>The hair's locks swung on springs over the clips, after them (a modifier added last runs last).</summary>
    private void BuildHair()
    {
        if (Skeleton.FindBone(Locks[0] + "1") < 0)
        {
            return;
        }

        _hair = new SpringBoneSimulator3D { SettingCount = Locks.Length };
        for (int i = 0; i < Locks.Length; i++)
        {
            _hair.SetRootBoneName(i, Locks[i] + "1");
            _hair.SetEndBoneName(i, Locks[i] + "2");
            _hair.SetExtendEndBone(i, true);
            _hair.SetEndBoneDirection(i, SkeletonModifier3D.BoneDirection.PlusY);
            _hair.SetEndBoneLength(i, 0.05f);
            _hair.SetStiffness(i, HairStiffness);
            _hair.SetDrag(i, HairDrag);
            _hair.SetGravity(i, HairGravity);
            _hair.SetRadius(i, 0.015f);
        }

        Skeleton.AddChild(_hair);
    }

    /// <summary>
    /// The tree: the idle and the rated run blended by the gait for the ground; the jump and the leap, both scrubbed along
    /// the arc (<see cref="PilgrimAnimation.Climb"/>), blended by the speed across for the air; the swim's stroke over treading
    /// water by the speed; a clip for every other pose (its fallback's while it has none); all into one crossfading transition, under the strikes (<see cref="StrikeLayer"/>) and a one-shot for
    /// each cue that has a clip.
    /// </summary>
    private void BuildTree()
    {
        var root = new AnimationNodeBlendTree();
        root.AddNode(IdleNode, new AnimationNodeAnimation { Animation = IdleClip });
        root.AddNode(RunNode, new AnimationNodeAnimation { Animation = RunClip });
        root.AddNode(RunRateNode, new AnimationNodeTimeScale());
        root.AddNode(GaitNode, new AnimationNodeBlend2());
        root.ConnectNode(RunRateNode, 0, RunNode);
        root.ConnectNode(GaitNode, 0, IdleNode);
        root.ConnectNode(GaitNode, 1, RunRateNode);

        root.AddNode(JumpNode, new AnimationNodeAnimation { Animation = JumpClip });
        root.AddNode(LeapNode, new AnimationNodeAnimation { Animation = Clips.HasAnimation(LeapClip) ? LeapClip : JumpClip });
        root.AddNode(JumpSeekNode, new AnimationNodeTimeSeek());
        root.AddNode(LeapSeekNode, new AnimationNodeTimeSeek());
        root.AddNode(AirLeapNode, new AnimationNodeBlend2());
        root.ConnectNode(JumpSeekNode, 0, JumpNode);
        root.ConnectNode(LeapSeekNode, 0, LeapNode);
        root.ConnectNode(AirLeapNode, 0, JumpSeekNode);
        root.ConnectNode(AirLeapNode, 1, LeapSeekNode);

        var transition = new AnimationNodeTransition();
        _transition = transition;
        PilgrimPose[] poses = Enum.GetValues<PilgrimPose>();
        transition.InputCount = poses.Length;
        root.AddNode(PoseNode, transition);
        foreach (PilgrimPose pose in poses)
        {
            transition.SetInputName((int)pose, PoseNames[pose]);
            if (pose is PilgrimPose.Ground or PilgrimPose.Air)
            {
                root.ConnectNode(PoseNode, (int)pose, pose == PilgrimPose.Ground ? GaitNode : AirLeapNode);
                continue;
            }

            // Each input its own clip node: a node fed into two inputs would be advanced twice.
            root.AddNode(ClipNodeNames[pose], new AnimationNodeAnimation { Animation = ShownClip(pose) });
            root.AddNode(RateNodeNames[pose], new AnimationNodeTimeScale());
            root.ConnectNode(RateNodeNames[pose], 0, ClipNodeNames[pose]);
            if (pose == PilgrimPose.Swim && Clips.HasAnimation(TreadClip))
            {
                // Still in the water, the stroke fades into treading it.
                root.AddNode(TreadNode, new AnimationNodeAnimation { Animation = TreadClip });
                root.AddNode(StrokeNode, new AnimationNodeBlend2());
                root.ConnectNode(StrokeNode, 0, TreadNode);
                root.ConnectNode(StrokeNode, 1, RateNodeNames[pose]);
                root.ConnectNode(PoseNode, (int)pose, StrokeNode);
                continue;
            }

            root.ConnectNode(PoseNode, (int)pose, RateNodeNames[pose]);
        }

        StringName under = _strikes.Build(root, PoseNode, Clips, RootTrack.GetConcatenatedNames());
        PilgrimCue[] cues = Enum.GetValues<PilgrimCue>();
        _cueShown = new bool[cues.Length];
        foreach (PilgrimCue cue in cues)
        {
            if (cue == PilgrimCue.None || !Clips.HasAnimation(CueClips[cue]))
            {
                continue;
            }

            _cueShown[(int)cue] = true;
            root.AddNode(CueClips[cue], new AnimationNodeAnimation { Animation = CueClips[cue] });
            root.AddNode(CueNodes[cue], new AnimationNodeOneShot { FadeInTime = CueFadeIn, FadeOutTime = CueFadeOut });
            root.ConnectNode(CueNodes[cue], 0, under);
            root.ConnectNode(CueNodes[cue], 1, CueClips[cue]);
            under = CueNodes[cue];
        }

        root.ConnectNode(OutputNode, 0, under);
        Tree.TreeRoot = root;
        Tree.RootMotionTrack = RootTrack;
        Tree.CallbackModeProcess = AnimationMixer.AnimationCallbackModeProcess.Manual;
        Tree.Active = true;
    }

    /// <summary>The clip a pose shows: its own, or its nearest fallback's that exists (the idle in the end).</summary>
    private StringName ShownClip(PilgrimPose pose)
    {
        for (PilgrimPose? p = pose; p is { } shown; p = PilgrimPoses.Fallback(shown))
        {
            if (Clips.HasAnimation(ClipNames[shown]))
            {
                return ClipNames[shown];
            }
        }

        return IdleClip;
    }

    /// <summary>How fast a clip travels at its own rate, m/s: its root bone's travel over its length (0 for none, or no clip).</summary>
    private float ClipSpeed(StringName name)
    {
        if (!Clips.HasAnimation(name))
        {
            return 0f;
        }

        Animation clip = Clips.GetAnimation(name);
        int track = clip.FindTrack(RootTrack, Godot.Animation.TrackType.Position3D);
        if (track < 0 || clip.Length <= 0f)
        {
            return 0f;
        }

        Vector3 travel = clip.PositionTrackInterpolate(track, clip.Length) - clip.PositionTrackInterpolate(track, 0.0);
        return travel.Length() / (float)clip.Length;
    }

    /// <summary>A pale shimmer over the body while the pilgrim can't be hurt (after a hit); false clears it. Per physics tick.</summary>
    public void Blink(bool on)
    {
        _blinkTime = on ? _blinkTime + 1 : 0;
        float shimmer = on ? 0.05f + 0.05f * Mathf.Sin(_blinkTime * 0.45f) : 0f;
        if (on || _blinkTime == 0 && _shimmer != 0f)
        {
            Mesh.SetInstanceShaderParameter(HurtParameter, shimmer);
        }

        _shimmer = shimmer;
    }

    /// <summary>The side the body turns to: +1 right, -1 left.</summary>
    public void SetFacing(int facing) => _facing = facing < 0 ? -1 : 1;

    /// <summary>A strike under way (<see cref="StrikeShow.Strike"/>); each physics tick it lasts.</summary>
    public void Strike(float progress, float windUpEnd, float activeEnd, StrikeKind kind) =>
        Animation.Strike.Strike(progress, windUpEnd, activeEnd, kind);

    /// <summary>The strike is over (<see cref="StrikeShow.End"/>).</summary>
    public void EndStrike() => Animation.Strike.End();

    /// <summary>The held moment the body plays (resting, taking a relic, dying); None lets it go.</summary>
    public PilgrimMoment Moment
    {
        get => Animation.Moment;
        set => Animation.Moment = value;
    }

    /// <summary>A blow landed on the pilgrim: the body recoils.</summary>
    public void Hurt() => Animation.Hurt();

    /// <summary>The ledge assist lifted the body onto a top: it vaults the edge.</summary>
    public void Vault() => Animation.Vault();

    /// <summary>Where the Harpoon's line pulls, in the world: the near arm reaches along it while on the line.</summary>
    public void Reach(Vector3 toward)
    {
        Vector3 local = GetParentNode3D().GlobalBasis.Inverse() * toward;
        Animation.Reach = new System.Numerics.Vector2(local.X * _facing, local.Y);
    }

    /// <summary>The body touched down; <paramref name="impactSpeed"/> is the downward speed at touchdown.</summary>
    public void Land(float impactSpeed) => Animation.Land(impactSpeed);

    /// <summary>
    /// The body (the parent) just snapped to a new pose: the visible body starts from
    /// <paramref name="previousGlobal"/> and eases into the new pose instead of jumping.
    /// </summary>
    public void CarryOver(Transform3D previousGlobal)
    {
        _carry = GetParentNode3D().GlobalTransform.AffineInverse() * previousGlobal * new Transform3D(new Basis(Vector3.Up, _yaw), Vector3.Zero).AffineInverse();
        Transform = _carry * new Transform3D(new Basis(Vector3.Up, _yaw), Vector3.Zero);
        ResetPhysicsInterpolation();
    }

    /// <summary>
    /// One physics tick of animation, after the body moved. <paramref name="velocity"/> is in the body's own
    /// frame; <paramref name="floorY"/> is NaN when the body isn't upright (Mag-grip).
    /// </summary>
    public void Animate(float delta, Vector3 velocity, MotionMode mode, bool onFloor, float floorY)
    {
        _yaw = Motion.Damp(_yaw, TargetYaw, TurnSharpness, delta);
        float k = 1f - Mathf.Exp(-CarrySharpness * delta);
        _carry = new Transform3D(new Basis(_carry.Basis.GetRotationQuaternion().Slerp(Quaternion.Identity, k)), _carry.Origin.Lerp(Vector3.Zero, k));
        PilgrimAnimation animation = Animation;
        animation.Facing = _facing;
        animation.Update(delta, velocity.Flat(), mode);
        float shown = _poseClock.Tick(delta, animation.Pose != _shownPose || animation.Cue != PilgrimCue.None);
        if (shown > 0f)
        {
            // The lean tips the body about its feet in its own side plane (its local x is the way it faces).
            Transform = _carry * new Transform3D(new Basis(Vector3.Up, _yaw) * new Basis(Vector3.Back, -animation.Lean), Vector3.Zero);
        }

        Show(animation);
        if (shown > 0f)
        {
            Tree.Advance(shown);
        }

        Aim.Influence = animation.ReachWeight;
        if (animation.ReachWeight > 0f)
        {
            Vector3 along = new Vector3(animation.Reach.X, animation.Reach.Y, 0f).Normalized();
            ReachTarget.Position = ToLocal(Shoulder.GlobalPosition) + along * 2f;
        }

        ShowBlade(animation.Strike.Blade);
        Slash?.Track(Shoulder, Blade, _bladeLength, animation.Strike.Edge * animation.Strike.Blade, GlobalPosition, delta);

        bool grounded = onFloor && !float.IsNaN(floorY) && animation.Pose == PilgrimPose.Ground && animation.Gait > 0.3f;
        float floor = float.IsNaN(floorY) ? 0f : floorY;
        int planted = _footfalls.Update(SoleHeight(_footR, _toesR, floor), SoleHeight(_footL, _toesL, floor), grounded);
        if (planted != 0)
        {
            Vector3 foot = BoneWorld(planted > 0 ? _footR : _footL);
            Footfall?.Invoke(foot with { Y = floor }, planted);
        }
    }

    /// <summary>Hands the core's decisions to the tree.</summary>
    private void Show(PilgrimAnimation animation)
    {
        Tree.Set(GaitParameter, animation.Gait);
        Tree.Set(RunRateParameter, animation.RunRate);
        if (animation.Pose is not PilgrimPose.Ground and not PilgrimPose.Air)
        {
            Tree.Set(RateParameters[animation.Pose], animation.PoseRate);
        }

        Tree.Set(LeapParameter, animation.Leap);
        Tree.Set(StrokeParameter, animation.Stroke);
        Tree.Set(JumpSeekParameter, (double)animation.Climb);
        Tree.Set(LeapSeekParameter, (double)animation.Climb);

        _strikes.Show(Tree, animation.Strike, animation.StrikeWhole);
        if (animation.Cue != PilgrimCue.None && _cueShown[(int)animation.Cue])
        {
            Tree.Set(CueRequests[animation.Cue], (int)AnimationNodeOneShot.OneShotRequest.Fire);
        }

        if (animation.Pose != _shownPose)
        {
            if (_transition is not null)
            {
                _transition.XfadeTime = PilgrimPoses.Crossfade(_shownPose, animation.Pose);
            }

            _shownPose = animation.Pose;
            using Variant request = PoseNames[animation.Pose];
            Tree.Set(PoseParameter, request);
        }
    }

    /// <summary>Snaps to a still stance facing the current way (spawn, teleport).</summary>
    public void Snap()
    {
        _yaw = TargetYaw;
        _carry = Transform3D.Identity;
        Transform = new Transform3D(new Basis(Vector3.Up, _yaw), Vector3.Zero);
        Animation.Reset();
        _poseClock.Reset();
        Show(Animation);
        Tree.Advance(0.0);
        _hair?.Reset();
        ShowBlade(0f);
        Slash?.Clear();
        ResetPhysicsInterpolation();
    }

    /// <summary>A bone's head in the world.</summary>
    private Vector3 BoneWorld(int bone) => Skeleton.GlobalTransform * Skeleton.GetBoneGlobalPose(bone).Origin;

    /// <summary>How high a sole is over <paramref name="floor"/>: the lower of its heel and its ball.</summary>
    private float SoleHeight(int foot, int toes, float floor) =>
        Mathf.Min(BoneWorld(foot).Y - AnkleHeight, BoneWorld(toes).Y - BallHeight) - floor;

    /// <summary>The blade slid <paramref name="out"/> of the forearm (0 folded away, hidden; 1 all out), narrowing as it folds.</summary>
    private void ShowBlade(float @out)
    {
        Blade.Visible = @out > 0.02f;
        Blade.Scale = new Vector3(Mathf.Lerp(0.4f, 1f, @out), Mathf.Max(@out, 0.01f), 1f);
    }

    /// <summary>Facing +X is yaw 0; facing -X is -π, so a turn either way passes through facing the camera.</summary>
    private float TargetYaw => _facing > 0 ? 0f : -Mathf.Pi;
}
