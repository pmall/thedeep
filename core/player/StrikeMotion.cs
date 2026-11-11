using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// The pilgrim's sword strikes, Godot-free: a layer over <see cref="PilgrimMotion"/>'s pose, choreographed in the spirit of
/// <i>NieR: Automata</i> (docs/art-direction.md#character). Each kind is keyed at four poses: coiled (the anticipation), at
/// contact (the blade crossing the target, reached fast), followed through, and held; a strike starts from the pose last shown,
/// so a chain flows from each hit's end into the next one's coil, and after its last tick it holds its end pose a moment, then
/// lets go. The hips lead, the chest follows, the arm comes last and the hand whips after it. The chain builds: a forehand cut
/// from over the shoulder down through the front, a backhand rising back up through it, then the finisher, coiled high and
/// arched back on a raised knee, brought over the top down into a deep lunge, held longest. The down strike sweeps the blade
/// from ahead down under the body, the knees drawn up out of its way; the dash strike draws it from the hip up through the front, low along the dash.
/// The blade (<see cref="Blade"/>) springs out of the forearm as a strike begins and folds away as it lets go; <see cref="Edge"/>
/// says when its sweep leaves a trail. The near (R) arm holds the blade, and the arms stay in the side plane while the body turns
/// under them, so every cut draws its arc on the screen. Feel numbers are constants here: they shape the animation only.
/// </summary>
public sealed class StrikeMotion
{
    /// <summary>Contact comes this share of the hit window after the coil: the blade crosses the target early in it.</summary>
    private const float ContactShare = 0.3f;

    // How far ahead (a share of the strike) each part of the body runs: the hips first, the chest after them, the hand last.
    private const float HipsLead = 0.07f;
    private const float ChestLead = 0.035f;
    private const float HandLag = 0.025f;

    /// <summary>How fast the blade springs out and folds away, 1/s.</summary>
    private const float BladeOut = 45f;
    private const float BladeIn = 14f;

    private readonly HumanoidDims _dims;
    private readonly Keys[] _keys;
    private readonly HumanoidPose _from = new();
    private StrikeKind _kind;
    private float _progress = 1f;
    private float _windUpEnd = 0.15f;
    private float _activeEnd = 0.5f;
    private bool _active;
    private bool _started;
    private float _released = float.MaxValue;

    /// <summary>The strikes of a body of sizes <paramref name="dims"/>, each kind's four poses keyed to those sizes.</summary>
    public StrikeMotion(HumanoidDims dims)
    {
        _dims = dims;
        _keys = new Keys[Enum.GetValues<StrikeKind>().Length];
        foreach (StrikeKind kind in Enum.GetValues<StrikeKind>())
        {
            _keys[(int)kind] = Build(kind);
        }
    }

    /// <summary>The strike layer after the last <see cref="Update"/>: the whole body as the strike would have it.</summary>
    public HumanoidPose Pose { get; } = new();

    /// <summary>How much the layer shows: 1 through a strike and its hold, then easing to 0 as it lets go.</summary>
    public float Weight { get; private set; }

    /// <summary>The strike shown (or the last one, while it lets go).</summary>
    public StrikeKind Kind => _kind;

    /// <summary>How far the blade is out of the forearm, 0 (folded away) to 1.</summary>
    public float Blade { get; private set; }

    /// <summary>How strongly the blade's sweep leaves a trail now, 0 to 1: through the cut and its follow-through.</summary>
    public float Edge { get; private set; }

    /// <summary>A strike under way this tick (see <see cref="PilgrimMotion.Strike"/>).</summary>
    public void Strike(float progress, float windUpEnd, float activeEnd, StrikeKind kind)
    {
        progress = Math.Clamp(progress, 0f, 1f);
        _started |= !_active || kind != _kind || progress < _progress;
        _active = true;
        _kind = kind;
        _progress = progress;
        _windUpEnd = Math.Clamp(windUpEnd, 0.01f, 0.9f);
        _activeEnd = Math.Clamp(activeEnd, _windUpEnd + 0.01f, 0.99f);
    }

    /// <summary>The strike is over: the body holds its end pose a moment, then lets go.</summary>
    public void End()
    {
        if (_active)
        {
            _active = false;
            _released = 0f;
        }
    }

    /// <summary>No strike shown at all, at once: the blade folded away and no trail.</summary>
    public void Reset()
    {
        _active = false;
        _started = false;
        _released = float.MaxValue;
        Weight = 0f;
        Blade = 0f;
        Edge = 0f;
    }

    /// <summary>
    /// Advances by <paramref name="delta"/> seconds. <paramref name="shown"/> is the pose the body showed last: a strike that
    /// begins now starts from it.
    /// </summary>
    public void Update(float delta, HumanoidPose shown)
    {
        float dt = MathF.Max(delta, 0f);
        Keys keys = _keys[(int)_kind];
        if (_started)
        {
            _from.CopyFrom(shown);
            _started = false;
            _released = float.MaxValue;
        }

        if (_active)
        {
            Weight = 1f;
            float h = Track(_progress + HipsLead, keys, out HumanoidPose hipsFrom, out HumanoidPose hipsTo);
            Pose.HipsOffset = Vector2.Lerp(hipsFrom.HipsOffset, hipsTo.HipsOffset, h);
            for (int i = 0; i < Humanoid.BoneCount; i++)
            {
                var bone = (HumanoidBone)i;
                float q = _progress + Lead(bone);
                float s = Track(q, keys, out HumanoidPose from, out HumanoidPose to);
                Pose.SetBetween(bone, from, to, s);
            }

            // Laid from the coil to the end of the follow-through, not after: settling into the hold turns the blade back, and a
            // trail laid over itself piles into a bright spot.
            Edge = SmoothStep(_windUpEnd - 0.01f, _windUpEnd + 0.04f, _progress) * (1f - SmoothStep(_activeEnd - 0.1f, _activeEnd, _progress));
        }
        else if (_released < float.MaxValue)
        {
            _released += dt;
            Pose.CopyFrom(keys.Hold);
            Weight = 1f - SmoothStep(keys.HoldTime, keys.HoldTime + keys.LetGo, _released);
            Edge = 0f;
            if (Weight <= 0f)
            {
                _released = float.MaxValue;
            }
        }
        else
        {
            Weight = 0f;
            Edge = 0f;
        }

        bool drawn = _active || _released < keys.HoldTime + keys.LetGo * 0.4f;
        Blade = Motion.Damp(Blade, drawn ? 1f : 0f, drawn ? BladeOut : BladeIn, dt);
        if (_active)
        {
            Blade = MathF.Max(Blade, SmoothStep(0f, _windUpEnd * 0.8f, _progress)); // all out by the coil, however short the wind-up
        }
        if (!drawn && Blade < 0.002f)
        {
            Blade = 0f;
        }
    }

    /// <summary>How far ahead of the strike's progress <paramref name="bone"/> runs.</summary>
    private static float Lead(HumanoidBone bone) => bone switch
    {
        HumanoidBone.Hips or (>= HumanoidBone.ThighR and <= HumanoidBone.FootL) => HipsLead,
        HumanoidBone.Spine => (HipsLead + ChestLead) * 0.5f,
        HumanoidBone.Chest or HumanoidBone.Neck or HumanoidBone.Head => ChestLead,
        HumanoidBone.HandR or HumanoidBone.HandL => -HandLag,
        _ => 0f,
    };

    /// <summary>The two key poses <paramref name="q"/> lies between, and how far (eased) from the first to the second.</summary>
    private float Track(float q, Keys keys, out HumanoidPose a, out HumanoidPose b)
    {
        q = Math.Clamp(q, 0f, 1f);
        float w = _windUpEnd;
        float contact = w + (_activeEnd - w) * ContactShare;
        if (q < w)
        {
            // Into the coil: fast at first, settling into it, so the coiled pose reads before the blow.
            (a, b) = (_from, keys.Coil);
            float x = q / w;
            return 1f - (1f - x) * (1f - x);
        }

        if (q < contact)
        {
            // The blow: accelerating through the target.
            (a, b) = (keys.Coil, keys.Contact);
            return MathF.Pow((q - w) / (contact - w), 1.3f);
        }

        if (q < _activeEnd)
        {
            // The follow-through: still fast past the target, slowing as it ends.
            (a, b) = (keys.Contact, keys.Follow);
            float x = (q - contact) / (_activeEnd - contact);
            return 1f - (1f - x) * (1f - x);
        }

        (a, b) = (keys.Follow, keys.Hold);
        return SmoothStep(_activeEnd, 1f, q);
    }

    private Keys Build(StrikeKind kind) => kind switch
    {
        // The first: from over the shoulder, the chest opened to the camera, down through the front onto a step in, the blade
        // held low ahead.
        StrikeKind.Swing => new Keys(
            KeyPose(new Key(Lean: 0.12f, HipsYaw: -0.25f, ChestYaw: -0.7f, Hips: new(-0.03f, -0.05f), Arm: 3.3f, Elbow: 0.7f, Wrist: 0.2f,
                Off: 1.0f, OffElbow: 0.5f, Front: 0.14f, FrontLift: 0.07f, Back: -0.18f, Head: -0.05f)),
            KeyPose(new Key(Lean: -0.3f, HipsYaw: 0.3f, ChestYaw: 0.5f, Hips: new(0.08f, -0.12f), Arm: 1.45f, Elbow: 0.1f, Wrist: 0.05f,
                Off: -0.5f, OffElbow: 0.6f, Front: 0.4f, Back: -0.3f, Head: -0.12f)),
            KeyPose(new Key(Lean: -0.45f, HipsYaw: 0.4f, ChestYaw: 0.75f, Hips: new(0.1f, -0.15f), Arm: 0.35f, Elbow: 0f, Wrist: 0f,
                Off: -0.9f, OffElbow: 0.7f, Front: 0.42f, Back: -0.32f, Head: -0.15f)),
            KeyPose(new Key(Lean: -0.38f, HipsYaw: 0.35f, ChestYaw: 0.6f, Hips: new(0.09f, -0.13f), Arm: 0.6f, Elbow: 0.2f, Wrist: 0.1f,
                Off: -0.7f, OffElbow: 0.8f, Front: 0.42f, Back: -0.32f, Head: -0.1f)),
            HoldTime: 0.05f, LetGo: 0.2f),

        // The backhand: wound further round, the blade dropped low behind, then swept back up through the front, the back foot
        // drawn in, the body rising with it, ending high: where the finisher begins.
        StrikeKind.Second => new Keys(
            KeyPose(new Key(Lean: -0.5f, HipsYaw: 0.45f, ChestYaw: 0.95f, Hips: new(0.1f, -0.17f), Arm: 0f, Elbow: -0.35f, Wrist: -0.2f,
                Off: -0.3f, OffElbow: 1.0f, Front: 0.42f, Back: -0.32f, Head: -0.15f)),
            KeyPose(new Key(Lean: -0.2f, HipsYaw: -0.05f, ChestYaw: -0.3f, Hips: new(0.14f, -0.1f), Arm: 1.55f, Elbow: 0.2f, Wrist: 0.1f,
                Off: 0.3f, OffElbow: 0.4f, Front: 0.5f, Back: -0.2f, BackLift: 0.05f, Head: -0.05f)),
            KeyPose(new Key(Lean: 0.05f, HipsYaw: -0.25f, ChestYaw: -0.65f, Hips: new(0.15f, -0.04f), Arm: 2.55f, Elbow: 0.25f, Wrist: 0.1f,
                Off: 0.7f, OffElbow: 0.3f, Front: 0.5f, Back: -0.18f, Head: 0.1f)),
            KeyPose(new Key(Lean: 0.02f, HipsYaw: -0.2f, ChestYaw: -0.55f, Hips: new(0.14f, -0.06f), Arm: 2.4f, Elbow: 0.35f, Wrist: 0.15f,
                Off: 0.5f, OffElbow: 0.5f, Front: 0.5f, Back: -0.18f, Head: 0.05f)),
            HoldTime: 0.05f, LetGo: 0.2f),

        // The finisher: arched back on a raised knee, the blade cocked far behind the head, then over the top and down, the
        // whole body dropping into a deep lunge behind it; held longest.
        StrikeKind.Finisher => new Keys(
            KeyPose(new Key(Lean: 0.3f, HipsYaw: -0.3f, ChestYaw: -0.9f, Hips: new(0.06f, 0.03f), Arm: 3.7f, Elbow: 0.9f, Wrist: 0.2f,
                Off: 2.3f, OffElbow: 0.6f, Front: 0.3f, FrontLift: 0.16f, Back: -0.15f, Head: 0.15f)),
            KeyPose(new Key(Lean: -0.6f, HipsYaw: 0.35f, ChestYaw: 0.6f, Hips: new(0.2f, -0.3f), Arm: 1.3f, Elbow: 0f, Wrist: 0f,
                Off: -0.6f, OffElbow: 0.4f, Front: 0.7f, Back: -0.45f, Head: -0.2f)),
            KeyPose(new Key(Lean: -0.85f, HipsYaw: 0.5f, ChestYaw: 0.85f, Hips: new(0.22f, -0.36f), Arm: 0.55f, Elbow: -0.05f, Wrist: 0f,
                Off: -1.2f, OffElbow: 0.3f, Front: 0.72f, Back: -0.47f, Head: -0.2f)),
            KeyPose(new Key(Lean: -0.75f, HipsYaw: 0.45f, ChestYaw: 0.75f, Hips: new(0.21f, -0.34f), Arm: 0.7f, Elbow: 0f, Wrist: 0.05f,
                Off: -1.1f, OffElbow: 0.4f, Front: 0.72f, Back: -0.47f, Head: -0.15f)),
            HoldTime: 0.2f, LetGo: 0.32f),

        // The down strike: the knees drawn up to the chest, the blade swept from ahead down and under them, curled over it.
        StrikeKind.Down => new Keys(
            KeyPose(new Key(Lean: -0.15f, HipsYaw: 0f, ChestYaw: -0.3f, Hips: new(0f, 0.06f), Arm: 1.9f, Elbow: -0.2f, Wrist: 0f,
                Off: 2.2f, OffElbow: 0.4f, Head: -0.2f, Tuck: 0.7f)),
            KeyPose(new Key(Lean: -0.55f, HipsYaw: 0.1f, ChestYaw: 0.3f, Hips: new(0f, 0.08f), Arm: 0.3f, Elbow: -0.3f, Wrist: 0f,
                Off: 1.0f, OffElbow: 0.5f, Head: -0.45f, Tuck: 1f)),
            KeyPose(new Key(Lean: -0.6f, HipsYaw: 0.15f, ChestYaw: 0.45f, Hips: new(0f, 0.08f), Arm: -0.4f, Elbow: -0.4f, Wrist: -0.1f,
                Off: 0.6f, OffElbow: 0.6f, Head: -0.5f, Tuck: 1f)),
            KeyPose(new Key(Lean: -0.5f, HipsYaw: 0.1f, ChestYaw: 0.4f, Hips: new(0f, 0.07f), Arm: -0.3f, Elbow: -0.3f, Wrist: 0f,
                Off: 0.7f, OffElbow: 0.6f, Head: -0.45f, Tuck: 1f)),
            HoldTime: 0.06f, LetGo: 0.22f),

        // The dash strike: the blade drawn from the hip, low along the dash, up through the front, the far hand thrown back.
        _ => new Keys(
            KeyPose(new Key(Lean: -0.8f, HipsYaw: -0.2f, ChestYaw: -0.7f, Hips: new(0.04f, -0.1f), Arm: -0.9f, Elbow: 0.6f, Wrist: 0.1f,
                Off: 0.9f, OffElbow: 0.8f, Front: 0.45f, Back: -0.45f, Head: -0.15f)),
            KeyPose(new Key(Lean: -0.65f, HipsYaw: 0.2f, ChestYaw: 0.4f, Hips: new(0.06f, -0.12f), Arm: 1.4f, Elbow: 0.1f, Wrist: 0.05f,
                Off: -0.8f, OffElbow: 0.5f, Front: 0.5f, Back: -0.5f, Head: -0.2f)),
            KeyPose(new Key(Lean: -0.5f, HipsYaw: 0.3f, ChestYaw: 0.7f, Hips: new(0.06f, -0.1f), Arm: 2.3f, Elbow: 0.2f, Wrist: 0.1f,
                Off: -1.5f, OffElbow: 0.3f, Front: 0.5f, Back: -0.5f, Head: -0.1f)),
            KeyPose(new Key(Lean: -0.45f, HipsYaw: 0.25f, ChestYaw: 0.6f, Hips: new(0.06f, -0.1f), Arm: 2.1f, Elbow: 0.35f, Wrist: 0.15f,
                Off: -1.3f, OffElbow: 0.4f, Front: 0.5f, Back: -0.5f, Head: -0.1f)),
            HoldTime: 0.05f, LetGo: 0.2f),
    };

    /// <summary>A key's whole-body pose: the trunk curved into its lean and turned, the arms in the side plane, the feet planted (or tucked).</summary>
    private HumanoidPose KeyPose(Key k)
    {
        var pose = new HumanoidPose { HipsOffset = k.Hips };
        pose[HumanoidBone.Hips] = k.Lean * 0.45f;
        pose[HumanoidBone.Spine] = k.Lean * 0.8f;
        pose[HumanoidBone.Chest] = k.Lean;
        pose[HumanoidBone.Neck] = k.Lean * 0.5f + k.Head * 0.3f;
        pose[HumanoidBone.Head] = k.Head;
        pose.SetTurn(HumanoidBone.Hips, k.HipsYaw, 0f);
        pose.SetTurn(HumanoidBone.Spine, (k.HipsYaw + k.ChestYaw) * 0.5f, 0f);
        pose.SetTurn(HumanoidBone.Chest, k.ChestYaw, -0.08f * k.ChestYaw);
        pose.SetTurn(HumanoidBone.Neck, k.ChestYaw * 0.4f, 0f);
        pose[HumanoidBone.UpperArmR] = k.Arm;
        pose[HumanoidBone.ForearmR] = k.Arm + k.Elbow;
        pose[HumanoidBone.HandR] = k.Arm + k.Elbow + k.Wrist;
        pose[HumanoidBone.UpperArmL] = k.Off;
        pose[HumanoidBone.ForearmL] = k.Off + k.OffElbow;
        pose[HumanoidBone.HandL] = k.Off + k.OffElbow + 0.1f;

        if (k.Tuck > 0f)
        {
            // In the air: the knees drawn up toward the chest, the near one highest, the shins hanging under them.
            pose[HumanoidBone.ThighR] = Lerp(0.9f, 1.9f, k.Tuck);
            pose[HumanoidBone.ShinR] = Lerp(-0.3f, 0.05f, k.Tuck);
            pose[HumanoidBone.FootR] = Lerp(-0.5f, -0.7f, k.Tuck);
            pose[HumanoidBone.ThighL] = Lerp(0.6f, 1.55f, k.Tuck);
            pose[HumanoidBone.ShinL] = Lerp(-0.6f, -0.25f, k.Tuck);
            pose[HumanoidBone.FootL] = Lerp(-0.8f, -0.85f, k.Tuck);
            return pose;
        }

        Plant(pose, HumanoidBone.ThighR, HumanoidBone.ShinR, HumanoidBone.FootR, 1, new Vector2(k.Front, k.FrontLift), -3f * k.FrontLift);
        Plant(pose, HumanoidBone.ThighL, HumanoidBone.ShinL, HumanoidBone.FootL, -1, new Vector2(k.Back, k.BackLift), -0.45f);
        return pose;
    }

    /// <summary>Reaches the leg on <paramref name="side"/> for a foot at <paramref name="foot"/> (X forward, Y up from the ground).</summary>
    private void Plant(HumanoidPose pose, HumanoidBone thigh, HumanoidBone shin, HumanoidBone foot, int side, Vector2 at, float footAngle)
    {
        (float upper, float lower) = PlanarIk.TwoBone(pose.HipJoint(_dims, side), at + new Vector2(0f, _dims.AnkleHeight), _dims.Thigh, _dims.Shin, 1f);
        pose[thigh] = upper;
        pose[shin] = lower;
        pose[foot] = footAngle;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float SmoothStep(float from, float to, float x)
    {
        float t = Math.Clamp((x - from) / (to - from), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// One key of a strike, in the side plane (radians; see <see cref="HumanoidPose"/>): the trunk's lean, the hips' and the
    /// chest's turn (positive brings the near shoulder forward), the hips' offset, the near (blade) arm's upper arm and its
    /// elbow's and wrist's bend, the far arm and its elbow, where the feet stand (meters ahead of the body's pivot, and lifted),
    /// the head, and for a strike in the air how far the legs are folded (<c>Tuck</c>, 0 standing).
    /// </summary>
    private readonly record struct Key(float Lean, float HipsYaw, float ChestYaw, Vector2 Hips, float Arm, float Elbow, float Wrist,
        float Off, float OffElbow, float Head, float Front = 0.06f, float FrontLift = 0f, float Back = -0.1f, float BackLift = 0f,
        float Tuck = 0f);

    /// <summary>A strike's four key poses, how long its end is held after its last tick and how long it then takes to let go.</summary>
    private sealed record Keys(HumanoidPose Coil, HumanoidPose Contact, HumanoidPose Follow, HumanoidPose Hold, float HoldTime, float LetGo);
}
