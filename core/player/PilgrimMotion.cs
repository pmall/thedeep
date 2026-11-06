using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// The pilgrim's procedural animation, Godot-free. Each physics tick, <see cref="Update"/> turns the body's
/// motion into a <see cref="HumanoidPose"/>:
/// - on the ground, a run cycle with planted feet (each stride matches the speed, so feet never slide),
///   solved by IK and blended with a calm, breathing stance as the body slows;
/// - in the air, a leap while rising that opens into a reach for the ground while falling;
/// - in a dash, a low lunge with the arms swept back, held a moment after the dash ends;
/// - swimming, a paddle with a flutter kick leaning into the stroke, treading water when still;
/// - in the Drill's dive, a spike: legs together and toes pointed down, arms straight overhead, the body spinning (<see cref="Spin"/>);
/// - on the Harpoon's line, the near arm straight along it (<see cref="Reach"/>), the body trailing behind;
/// - over any of these, a strike (<see cref="Strike"/>: the near arm raised in the wind-up, swept through the blow, the body
///   behind it; a down strike curls the legs up out of its way) and a recoil when hurt (<see cref="Hurt"/>);
/// - always, a lean into acceleration and a landing compression, both on soft springs, so moves flow into
///   each other. Feel numbers are constants here: they shape the animation only, never the movement.
/// </summary>
public sealed class PilgrimMotion
{
    // Stride: meters per full cycle (two steps) and the share of it a foot stays planted, crawl → run.
    private const float WalkCycle = 1.1f;
    private const float RunCycle = 2.7f;
    private const float WalkStance = 0.6f;
    private const float RunStance = 0.36f;
    private const float MaxStanceTravel = 0.95f;
    private const float WalkLift = 0.07f;
    private const float RunLift = 0.3f;

    /// <summary>Feet land this far ahead of the hips, so the body rides over them.</summary>
    private const float RunReach = 0.06f;

    // Body: how low the run carries the hips, how much they bob, how far the body leans.
    private const float StandBend = 0.025f;
    private const float RunCrouch = 0.07f;
    private const float RunBob = 0.035f;
    private const float RunLean = -0.2f;
    private const float AccelLean = -0.018f;
    private const float MaxAccelLean = 0.32f;

    // Arms.
    private const float IdleSwing = 0.05f;
    private const float RunSwing = 0.8f;
    private const float IdleElbow = 0.2f;
    private const float RunElbow = 1.15f;

    /// <summary>Downward speed (m/s) at which a landing compresses fully.</summary>
    private const float HardLanding = 20f;
    private const float MaxCompressionKick = 4.6f;

    // Swimming: strokes per second treading water and swimming across, and the share of run speed that is a full stroke.
    private const float TreadRate = 0.9f;
    private const float StrokeRate = 1.5f;
    private const float SwimShare = 0.45f;

    /// <summary>The dive's spin, radians per second (two and a half turns).</summary>
    private const float DiveSpin = MathF.Tau * 2.5f;

    /// <summary>How long a hurt recoil lasts, seconds.</summary>
    private const float HurtLength = 0.55f;

    private readonly HumanoidDims _dims;
    private readonly float _runSpeed;
    private readonly HumanoidPose _air = new();
    private readonly HumanoidPose _dash = new();
    private readonly HumanoidPose _swim = new();
    private readonly HumanoidPose _dive = new();
    private readonly HumanoidPose _zip = new();
    private readonly HumanoidPose _strikePose = new();
    private readonly HumanoidPose _hurtPose = new();

    private float _time;
    private float _phase;
    private float _gait;
    private float _airWeight;
    private float _fallWeight;
    private float _dashWeight;
    private float _swimWeight;
    private float _diveWeight;
    private float _zipWeight;
    private float _swimPhase;
    private float _swimMove;
    private float _spin;
    private float _hurtTime = float.MaxValue;
    private StrikeMoment? _strike;
    private float _lastVelocityX;
    private bool _inAir;
    private bool _leadRight = true;
    private Spring _lean = new(3.2f, 0.5f);
    private Spring _compression = new(2.2f, 0.55f);

    public PilgrimMotion(HumanoidDims dims, float runSpeed)
    {
        _dims = dims;
        _runSpeed = MathF.Max(runSpeed, 0.1f);
    }

    /// <summary>The pose after the last <see cref="Update"/>.</summary>
    public HumanoidPose Pose { get; } = new();

    /// <summary>A foot planted during the last update: +1 the near (R) foot, -1 the far (L) one, 0 none.</summary>
    public int Footfall { get; private set; }

    /// <summary>How much the run cycle shows, 0 standing to 1 at run speed.</summary>
    public float Gait => _gait;

    /// <summary>How far the landing spring has lowered the hips, meters.</summary>
    public float Compression => MathF.Max(0f, _compression.Value);

    /// <summary>Where the Harpoon's line pulls, in the body's frame (X forward, Y up); read in <see cref="MotionMode.Zip"/>.</summary>
    public Vector2 Reach { get; set; } = Vector2.UnitX;

    /// <summary>The dive's spin around the body's upright axis, radians (0 out of a dive, once it has settled).</summary>
    public float Spin => _spin;

    /// <summary>
    /// A strike under way: <paramref name="progress"/> 0 to 1 through the whole swing, the wind-up over by
    /// <paramref name="windUpEnd"/> and the blow by <paramref name="activeEnd"/> (shares of it). Called each tick it lasts;
    /// <see cref="EndStrike"/> when it is over.
    /// </summary>
    public void Strike(float progress, float windUpEnd, float activeEnd, StrikeKind kind) =>
        _strike = new StrikeMoment(Math.Clamp(progress, 0f, 1f), windUpEnd, MathF.Max(activeEnd, windUpEnd + 0.01f), kind);

    public void EndStrike() => _strike = null;

    /// <summary>A blow landed on the pilgrim: the body recoils, then recovers.</summary>
    public void Hurt() => _hurtTime = 0f;

    /// <summary>Kicks the landing spring; <paramref name="impactSpeed"/> is the downward speed at touchdown.</summary>
    public void Land(float impactSpeed)
    {
        float strength = Math.Clamp(impactSpeed / HardLanding, 0f, 1f);
        _compression.Velocity += MaxCompressionKick * MathF.Sqrt(strength);
    }

    /// <summary>Back to a still stance (spawn, teleport).</summary>
    public void Reset()
    {
        _phase = 0f;
        _gait = 0f;
        _airWeight = 0f;
        _fallWeight = 0f;
        _dashWeight = 0f;
        _swimWeight = 0f;
        _diveWeight = 0f;
        _zipWeight = 0f;
        _swimMove = 0f;
        _spin = 0f;
        _hurtTime = float.MaxValue;
        _strike = null;
        _lastVelocityX = 0f;
        _inAir = false;
        _lean = new Spring(_lean.Frequency, _lean.Damping);
        _compression = new Spring(_compression.Frequency, _compression.Damping);
        Update(0f, Vector2.Zero, 1, MotionMode.Ground);
    }

    /// <summary>
    /// Advances the animation by <paramref name="delta"/> seconds. <paramref name="velocity"/> is the body's
    /// velocity in the world (m/s), <paramref name="facing"/> +1 right or -1 left.
    /// </summary>
    public void Update(float delta, Vector2 velocity, int facing, MotionMode mode)
    {
        float dt = MathF.Max(delta, 0f);
        _time += dt;
        float forward = velocity.X * (facing < 0 ? -1f : 1f);
        float speed = MathF.Abs(velocity.X);

        // Lean into acceleration (measured in the world, so turning around isn't a jolt), on a spring that overshoots a little.
        float accel = dt > 0f && mode == MotionMode.Ground ? (velocity.X - _lastVelocityX) / dt * (facing < 0 ? -1f : 1f) : 0f;
        _lastVelocityX = velocity.X;
        _lean.Step(Math.Clamp(accel * AccelLean, -MaxAccelLean, MaxAccelLean), dt);
        _compression.Step(0f, dt);

        _gait = Motion.Damp(_gait, mode == MotionMode.Ground ? MathF.Min(speed / _runSpeed, 1f) : _gait, 8f, dt);
        float g = _gait;
        float cycle = Lerp(WalkCycle, RunCycle, g);
        float stance = Lerp(WalkStance, RunStance, g);
        float previous = _phase;
        if (mode == MotionMode.Ground)
        {
            _phase = Fraction(_phase + speed * dt / cycle);
        }

        Footfall = 0;
        if (mode == MotionMode.Ground && g > 0.2f && _phase != previous)
        {
            if (_phase < previous)
            {
                Footfall = 1;
            }
            else if (previous < 0.5f && _phase >= 0.5f)
            {
                Footfall = -1;
            }
        }

        // The leg in the air when leaving the ground leads the leap.
        bool airborne = mode == MotionMode.Air;
        if (airborne && !_inAir)
        {
            _leadRight = g < 0.2f || _phase >= stance;
        }

        _inAir = airborne;
        _airWeight = Motion.Damp(_airWeight, airborne ? 1f : mode == MotionMode.Dash ? _airWeight : 0f, airborne ? 14f : 22f, dt);
        _fallWeight = Motion.Damp(_fallWeight, SmoothStep(1.5f, -4.5f, velocity.Y), 9f, dt);
        bool dashing = mode == MotionMode.Dash;
        _dashWeight = Motion.Damp(_dashWeight, dashing ? 1f : 0f, dashing ? 32f : 8f, dt);

        _swimWeight = Motion.Damp(_swimWeight, mode == MotionMode.Swim ? 1f : 0f, 10f, dt);
        _diveWeight = Motion.Damp(_diveWeight, mode == MotionMode.Dive ? 1f : 0f, mode == MotionMode.Dive ? 30f : 10f, dt);
        _zipWeight = Motion.Damp(_zipWeight, mode == MotionMode.Zip ? 1f : 0f, mode == MotionMode.Zip ? 25f : 9f, dt);
        _swimMove = Motion.Damp(_swimMove, Math.Clamp(MathF.Abs(forward) / (_runSpeed * SwimShare), 0f, 1f), 5f, dt);
        _swimPhase = Fraction(_swimPhase + dt * Lerp(TreadRate, StrokeRate, _swimMove));
        if (mode == MotionMode.Dive)
        {
            _spin += DiveSpin * dt;
        }
        else
        {
            float settled = MathF.Round(_spin / MathF.Tau) * MathF.Tau;
            _spin = Motion.Damp(_spin, settled, 10f, dt);
            if (MathF.Abs(_spin - settled) < 0.001f)
            {
                _spin = 0f;
            }
        }

        _hurtTime = _hurtTime < float.MaxValue ? _hurtTime + dt : _hurtTime;

        GroundPose(Pose, g, cycle, stance);
        AirPose(_air, forward);
        Pose.BlendToward(_air, _airWeight);
        DashPose(_dash, grounded: _airWeight < 0.5f);
        Pose.BlendToward(_dash, _dashWeight);
        SwimPose(_swim);
        Pose.BlendToward(_swim, _swimWeight);
        DivePose(_dive);
        Pose.BlendToward(_dive, _diveWeight);
        ZipPose(_zip);
        Pose.BlendToward(_zip, _zipWeight);
        if (_strike is { } strike)
        {
            bool down = strike.Kind == StrikeKind.Down;
            StrikePose(_strikePose, strike);
            float weight = SmoothStep(0f, 0.06f, strike.Progress) * (1f - SmoothStep(strike.ActiveEnd + (1f - strike.ActiveEnd) * 0.35f, 1f, strike.Progress));
            if (down)
            {
                Pose.BlendToward(_strikePose, weight);
            }
            else
            {
                Pose.BlendUpperToward(_strikePose, weight);
            }
        }

        if (_hurtTime < HurtLength)
        {
            HurtPose(_hurtPose);
            Pose.BlendToward(_hurtPose, 0.9f * SmoothStep(0f, 0.05f, _hurtTime) * (1f - SmoothStep(0.12f, HurtLength, _hurtTime)));
        }
    }

    private void GroundPose(HumanoidPose pose, float g, float cycle, float stance)
    {
        float breathe = MathF.Sin(MathF.Tau * 0.23f * _time);
        float still = 1f - g;
        float compression = Compression;

        float bob = RunBob * g * (0.5f + 0.5f * MathF.Cos(MathF.Tau * 2f * (_phase - stance * 0.5f)));
        float hipsY = -StandBend - RunCrouch * g - bob - compression + still * 0.004f * breathe;
        pose.HipsOffset = new Vector2(0.02f * g, hipsY);

        float torso = RunLean * g + _lean.Value - compression * 1.4f;
        float stepRock = MathF.Cos(MathF.Tau * 2f * (_phase - stance * 0.5f));
        pose[HumanoidBone.Hips] = torso * 0.35f;
        pose[HumanoidBone.Spine] = torso * 0.7f;
        pose[HumanoidBone.Chest] = torso + still * 0.014f * breathe - 0.025f * g * stepRock;
        pose[HumanoidBone.Neck] = torso * 0.55f;
        pose[HumanoidBone.Head] = torso * 0.2f + still * 0.012f * MathF.Sin(MathF.Tau * 0.09f * _time);

        float travel = MathF.Min(stance * cycle, MaxStanceTravel);
        float lift = Lerp(WalkLift, RunLift, g);
        float run = SmoothStep(0f, 0.25f, g);
        Vector2 hips = new Vector2(pose.HipsOffset.X, _dims.HipsHeight + hipsY);
        Leg(pose, HumanoidBone.ThighR, HumanoidBone.ShinR, HumanoidBone.FootR, hips, _phase, new Vector2(0.06f, 0f), travel, lift, stance, g, run);
        Leg(pose, HumanoidBone.ThighL, HumanoidBone.ShinL, HumanoidBone.FootL, hips, Fraction(_phase + 0.5f), new Vector2(-0.1f, 0f), travel, lift, stance, g, run);

        // Each arm swings against the leg on its side, the elbow folding more as it comes forward.
        float swing = Lerp(IdleSwing, RunSwing, g);
        Arm(pose, HumanoidBone.UpperArmR, HumanoidBone.ForearmR, HumanoidBone.HandR, _phase, swing, g, still * breathe, compression);
        Arm(pose, HumanoidBone.UpperArmL, HumanoidBone.ForearmL, HumanoidBone.HandL, Fraction(_phase + 0.5f), swing, g, still * breathe, compression);
    }

    private void Leg(HumanoidPose pose, HumanoidBone thigh, HumanoidBone shin, HumanoidBone foot, Vector2 hips, float phase,
        Vector2 idleFoot, float travel, float lift, float stance, float g, float run)
    {
        Vector2 target;
        float footAngle;
        if (phase < stance)
        {
            // Planted: the foot slides back under the body exactly as fast as the body moves over it.
            float u = phase / stance;
            target = new Vector2(travel * (0.5f - u) + RunReach * g, 0f);
            footAngle = -0.45f * g * SmoothStep(0.65f, 1f, u);
        }
        else
        {
            // Swinging: the heel kicks up behind, then the foot reaches forward and settles flat.
            float q = (phase - stance) / (1f - stance);
            target = new Vector2(-travel * 0.5f + travel * SmootherStep(q) + RunReach * g - 0.1f * g * MathF.Sin(MathF.PI * MathF.Min(q * 1.4f, 1f)),
                lift * MathF.Sin(MathF.PI * MathF.Pow(q, 0.7f)));
            footAngle = -0.9f * g * MathF.Sin(MathF.PI * MathF.Pow(q, 0.6f)) * (1f - q) + 0.12f * g * SmoothStep(0.8f, 1f, q);
        }

        target = Vector2.Lerp(idleFoot, target, run);
        footAngle *= run;
        Vector2 hipJoint = hips + PlanarIk.Rotate(_dims.HipJoint, pose[HumanoidBone.Hips]);
        (float upper, float lower) = PlanarIk.TwoBone(hipJoint, target + new Vector2(0f, _dims.AnkleHeight), _dims.Thigh, _dims.Shin, 1f);
        pose[thigh] = upper;
        pose[shin] = lower;
        pose[foot] = footAngle;
    }

    private static void Arm(HumanoidPose pose, HumanoidBone upper, HumanoidBone forearm, HumanoidBone hand, float legPhase,
        float swing, float g, float breathe, float compression)
    {
        float cos = MathF.Cos(MathF.Tau * legPhase);
        float shoulder = -swing * cos - 0.1f * g + (1f - g) * 0.03f + 0.01f * breathe + compression * 1.6f;
        float elbow = Lerp(IdleElbow, RunElbow, g) + 0.4f * g * (0.5f - 0.5f * cos) + compression * 1.2f;
        pose[upper] = shoulder;
        pose[forearm] = shoulder + elbow;
        pose[hand] = shoulder + elbow + 0.12f;
    }

    private void AirPose(HumanoidPose pose, float forward)
    {
        float lean = Math.Clamp(-forward / _runSpeed * 0.12f, -0.15f, 0.15f);
        float f = _fallWeight;
        (HumanoidBone leadThigh, HumanoidBone leadShin, HumanoidBone leadFoot) = _leadRight
            ? (HumanoidBone.ThighR, HumanoidBone.ShinR, HumanoidBone.FootR)
            : (HumanoidBone.ThighL, HumanoidBone.ShinL, HumanoidBone.FootL);
        (HumanoidBone trailThigh, HumanoidBone trailShin, HumanoidBone trailFoot) = _leadRight
            ? (HumanoidBone.ThighL, HumanoidBone.ShinL, HumanoidBone.FootL)
            : (HumanoidBone.ThighR, HumanoidBone.ShinR, HumanoidBone.FootR);
        // The arm on the trailing leg's side swings forward, as in a stride.
        (HumanoidBone frontArm, HumanoidBone frontForearm, HumanoidBone frontHand) = _leadRight
            ? (HumanoidBone.UpperArmL, HumanoidBone.ForearmL, HumanoidBone.HandL)
            : (HumanoidBone.UpperArmR, HumanoidBone.ForearmR, HumanoidBone.HandR);
        (HumanoidBone backArm, HumanoidBone backForearm, HumanoidBone backHand) = _leadRight
            ? (HumanoidBone.UpperArmR, HumanoidBone.ForearmR, HumanoidBone.HandR)
            : (HumanoidBone.UpperArmL, HumanoidBone.ForearmL, HumanoidBone.HandL);

        pose.HipsOffset = new Vector2(0f, Lerp(-0.04f, -0.02f, f));
        float torso = Lerp(-0.12f, 0.05f, f) + lean;
        pose[HumanoidBone.Hips] = torso * 0.4f;
        pose[HumanoidBone.Spine] = torso * 0.75f;
        pose[HumanoidBone.Chest] = torso;
        pose[HumanoidBone.Neck] = torso * 0.5f;
        pose[HumanoidBone.Head] = torso * 0.25f + Lerp(0f, 0.08f, f);

        // Rising: lead knee drawn up, trailing leg folded behind. Falling: both legs reach down to land.
        pose[leadThigh] = Lerp(1.15f, 0.5f, f);
        pose[leadShin] = Lerp(-0.6f, -0.2f, f);
        pose[leadFoot] = Lerp(-0.5f, -0.2f, f);
        pose[trailThigh] = Lerp(-0.3f, 0.12f, f);
        pose[trailShin] = Lerp(-1.3f, -0.45f, f);
        pose[trailFoot] = Lerp(-1.5f, -0.45f, f);

        // Arms open for balance as the fall begins.
        pose[frontArm] = Lerp(0.95f, 1.25f, f);
        pose[frontForearm] = pose[frontArm] + Lerp(0.65f, 0.35f, f);
        pose[frontHand] = pose[frontForearm] + 0.1f;
        pose[backArm] = Lerp(-0.65f, -0.95f, f);
        pose[backForearm] = pose[backArm] + Lerp(0.3f, 0.2f, f);
        pose[backHand] = pose[backForearm] + 0.1f;
    }

    private void DashPose(HumanoidPose pose, bool grounded)
    {
        (HumanoidBone frontThigh, HumanoidBone frontShin, HumanoidBone frontFoot) = _leadRight
            ? (HumanoidBone.ThighR, HumanoidBone.ShinR, HumanoidBone.FootR)
            : (HumanoidBone.ThighL, HumanoidBone.ShinL, HumanoidBone.FootL);
        (HumanoidBone backThigh, HumanoidBone backShin, HumanoidBone backFoot) = _leadRight
            ? (HumanoidBone.ThighL, HumanoidBone.ShinL, HumanoidBone.FootL)
            : (HumanoidBone.ThighR, HumanoidBone.ShinR, HumanoidBone.FootR);

        pose.HipsOffset = new Vector2(0.04f, grounded ? -0.1f : -0.03f);
        const float Torso = -0.75f;
        pose[HumanoidBone.Hips] = Torso * 0.45f;
        pose[HumanoidBone.Spine] = Torso * 0.8f;
        pose[HumanoidBone.Chest] = Torso;
        pose[HumanoidBone.Neck] = Torso * 0.55f;
        pose[HumanoidBone.Head] = -0.18f;

        pose[frontThigh] = 0.75f;
        pose[frontShin] = -0.2f;
        pose[frontFoot] = -0.15f;
        pose[backThigh] = -0.55f;
        pose[backShin] = -1.45f;
        pose[backFoot] = -1.6f;

        // Both arms swept back, nearly straight, like wings folded.
        pose[HumanoidBone.UpperArmR] = -1.75f;
        pose[HumanoidBone.ForearmR] = -1.6f;
        pose[HumanoidBone.HandR] = -1.5f;
        pose[HumanoidBone.UpperArmL] = -1.95f;
        pose[HumanoidBone.ForearmL] = -1.8f;
        pose[HumanoidBone.HandL] = -1.7f;
    }

    private void SwimPose(HumanoidPose pose)
    {
        float m = _swimMove;
        float a = MathF.Tau * _swimPhase;
        pose.HipsOffset = new Vector2(0f, -0.04f);
        float torso = Lerp(-0.08f, -0.6f, m) + 0.03f * MathF.Sin(a * 2f);
        pose[HumanoidBone.Hips] = torso * 0.6f;
        pose[HumanoidBone.Spine] = torso * 0.85f;
        pose[HumanoidBone.Chest] = torso;
        pose[HumanoidBone.Neck] = torso * 0.4f;
        pose[HumanoidBone.Head] = torso * 0.1f + 0.15f * m; // the head held up, out of the water

        // Legs: a slow bicycle treading water, a quick flutter behind when swimming across.
        for (int side = 0; side < 2; side++)
        {
            float s = side == 0 ? 0f : MathF.PI;
            float tread = 0.3f + 0.35f * MathF.Sin(a + s);
            float flutter = -0.35f + 0.28f * MathF.Sin(a * 2f + s);
            float thigh = Lerp(tread, flutter, m);
            float shin = thigh - Lerp(0.55f + 0.35f * MathF.Cos(a + s), 0.35f + 0.2f * MathF.Sin(a * 2f + s + 0.8f), m);
            (HumanoidBone t, HumanoidBone k, HumanoidBone f) = side == 0
                ? (HumanoidBone.ThighR, HumanoidBone.ShinR, HumanoidBone.FootR)
                : (HumanoidBone.ThighL, HumanoidBone.ShinL, HumanoidBone.FootL);
            pose[t] = thigh;
            pose[k] = shin;
            pose[f] = shin - Lerp(0.3f, 0.9f, m);
        }

        // Arms: sculling in front of the chest treading water, a long paddle reaching forward and pulling back when swimming.
        for (int side = 0; side < 2; side++)
        {
            float s = side == 0 ? 0f : MathF.PI;
            float stroke = MathF.Sin(a + s);
            float scull = 0.75f + 0.25f * MathF.Sin(a * 2f + s);
            float paddle = 1.25f + 0.8f * stroke;
            float shoulder = Lerp(scull, paddle, m);
            float elbow = Lerp(0.55f, 0.15f + 0.9f * MathF.Max(0f, MathF.Cos(a + s)), m); // bent coming forward, straight pulling
            (HumanoidBone u, HumanoidBone fo, HumanoidBone h) = side == 0
                ? (HumanoidBone.UpperArmR, HumanoidBone.ForearmR, HumanoidBone.HandR)
                : (HumanoidBone.UpperArmL, HumanoidBone.ForearmL, HumanoidBone.HandL);
            pose[u] = shoulder;
            pose[fo] = shoulder + elbow;
            pose[h] = shoulder + elbow + 0.1f;
        }
    }

    private static void DivePose(HumanoidPose pose)
    {
        pose.HipsOffset = new Vector2(0f, 0.02f);
        pose[HumanoidBone.Hips] = 0f;
        pose[HumanoidBone.Spine] = 0f;
        pose[HumanoidBone.Chest] = 0.02f;
        pose[HumanoidBone.Neck] = -0.05f;
        pose[HumanoidBone.Head] = -0.25f; // looking down where it bores
        pose[HumanoidBone.ThighR] = 0.06f;
        pose[HumanoidBone.ShinR] = -0.02f;
        pose[HumanoidBone.FootR] = -1.25f;
        pose[HumanoidBone.ThighL] = -0.04f;
        pose[HumanoidBone.ShinL] = -0.08f;
        pose[HumanoidBone.FootL] = -1.3f;
        pose[HumanoidBone.UpperArmR] = 2.95f;
        pose[HumanoidBone.ForearmR] = 3.05f;
        pose[HumanoidBone.HandR] = 3.1f;
        pose[HumanoidBone.UpperArmL] = 2.85f;
        pose[HumanoidBone.ForearmL] = 2.95f;
        pose[HumanoidBone.HandL] = 3f;
    }

    private void ZipPose(HumanoidPose pose)
    {
        // The angle of the line from straight down, positive forward (the pose's own measure).
        float along = Reach.LengthSquared() > 1e-6f ? MathF.Atan2(Reach.X, -Reach.Y) : MathF.PI * 0.5f;
        along = Math.Clamp(along, -0.4f, 3.1f);
        float trail = Math.Clamp(Wrap(along - MathF.PI), -1.1f, 0.3f);
        pose.HipsOffset = new Vector2(0f, -0.02f);
        float torso = Math.Clamp(-0.25f * along, -0.55f, 0.1f);
        pose[HumanoidBone.Hips] = torso * 0.5f;
        pose[HumanoidBone.Spine] = torso * 0.8f;
        pose[HumanoidBone.Chest] = torso;
        pose[HumanoidBone.Neck] = torso * 0.4f;
        pose[HumanoidBone.Head] = Math.Clamp(along - MathF.PI * 0.5f, -0.4f, 0.5f) * 0.4f; // eyes on the anchor

        pose[HumanoidBone.UpperArmR] = along;
        pose[HumanoidBone.ForearmR] = along;
        pose[HumanoidBone.HandR] = along + 0.05f;
        pose[HumanoidBone.UpperArmL] = -0.85f;
        pose[HumanoidBone.ForearmL] = -0.45f;
        pose[HumanoidBone.HandL] = -0.35f;

        pose[HumanoidBone.ThighR] = trail + 0.4f;
        pose[HumanoidBone.ShinR] = trail - 0.2f;
        pose[HumanoidBone.FootR] = trail - 0.75f;
        pose[HumanoidBone.ThighL] = trail - 0.1f;
        pose[HumanoidBone.ShinL] = trail - 0.75f;
        pose[HumanoidBone.FootL] = trail - 1.2f;
    }

    private static void StrikePose(HumanoidPose pose, StrikeMoment strike)
    {
        // Each kind keyed at three moments: ready (where the arm starts), wound up, struck. Angles: the near (sword) arm, its
        // elbow, the far arm, the torso.
        (float arm, float elbow, float off, float torso) ready = (0.4f, 0.4f, 0.2f, -0.1f);
        (float arm, float elbow, float off, float torso) wound;
        (float arm, float elbow, float off, float torso) struck;
        switch (strike.Kind)
        {
            case StrikeKind.Second: // a backhand from low behind up through the front
                wound = (-0.9f, -0.5f, 0.9f, 0.05f);
                struck = (2.5f, 0.1f, -0.7f, -0.3f);
                break;
            case StrikeKind.Finisher: // the heaviest: high and cocked, all the body brought down behind it
                wound = (3.0f, 1.0f, 1.0f, 0.3f);
                struck = (0.1f, 0.05f, -1.1f, -0.6f);
                break;
            case StrikeKind.Down: // stabbed straight down between the feet
                wound = (2.4f, 0.6f, 1.2f, 0.1f);
                struck = (-0.05f, 0f, 1.5f, -0.35f);
                break;
            case StrikeKind.Dash: // thrust straight ahead, the body behind it
                wound = (0.6f, 1.2f, -0.6f, -0.4f);
                struck = (1.6f, 0f, -1.4f, -0.7f);
                break;
            default: // the first swing: from over the shoulder down through the front
                wound = (2.8f, 0.8f, 0.7f, 0.15f);
                struck = (0.25f, 0.05f, -0.9f, -0.4f);
                break;
        }

        float p = strike.Progress;
        (float arm, float elbow, float off, float torso) k;
        if (p < strike.WindUpEnd)
        {
            k = Mix(ready, wound, SmoothStep(0f, strike.WindUpEnd, p));
        }
        else
        {
            float t = Math.Clamp((p - strike.WindUpEnd) / (strike.ActiveEnd - strike.WindUpEnd), 0f, 1f);
            k = Mix(wound, struck, 1f - (1f - t) * (1f - t) * (1f - t)); // the blow fast at first, settling into the struck pose
        }

        pose.HipsOffset = new Vector2(0f, strike.Kind == StrikeKind.Down ? 0.05f : -0.05f);
        pose[HumanoidBone.Hips] = k.torso * 0.4f;
        pose[HumanoidBone.Spine] = k.torso * 0.75f;
        pose[HumanoidBone.Chest] = k.torso;
        pose[HumanoidBone.Neck] = k.torso * 0.5f;
        pose[HumanoidBone.Head] = strike.Kind == StrikeKind.Down ? -0.45f : k.torso * 0.2f;
        pose[HumanoidBone.UpperArmR] = k.arm;
        pose[HumanoidBone.ForearmR] = k.arm + k.elbow;
        pose[HumanoidBone.HandR] = k.arm + k.elbow + 0.05f;
        pose[HumanoidBone.UpperArmL] = k.off;
        pose[HumanoidBone.ForearmL] = k.off + 0.45f;
        pose[HumanoidBone.HandL] = k.off + 0.55f;

        // The legs: only a down strike's are used (tucked up out of the blade's way).
        pose[HumanoidBone.ThighR] = 1.2f;
        pose[HumanoidBone.ShinR] = -0.15f;
        pose[HumanoidBone.FootR] = -0.5f;
        pose[HumanoidBone.ThighL] = 0.85f;
        pose[HumanoidBone.ShinL] = -0.45f;
        pose[HumanoidBone.FootL] = -0.8f;
    }

    private static void HurtPose(HumanoidPose pose)
    {
        pose.HipsOffset = new Vector2(-0.04f, -0.04f);
        pose[HumanoidBone.Hips] = 0.2f;
        pose[HumanoidBone.Spine] = 0.35f;
        pose[HumanoidBone.Chest] = 0.5f;
        pose[HumanoidBone.Neck] = 0.55f;
        pose[HumanoidBone.Head] = 0.75f; // thrown back
        pose[HumanoidBone.UpperArmR] = 1.5f;
        pose[HumanoidBone.ForearmR] = 1.95f;
        pose[HumanoidBone.HandR] = 2.1f;
        pose[HumanoidBone.UpperArmL] = 1.1f;
        pose[HumanoidBone.ForearmL] = 1.6f;
        pose[HumanoidBone.HandL] = 1.7f;
        pose[HumanoidBone.ThighR] = 0.65f;
        pose[HumanoidBone.ShinR] = -0.15f;
        pose[HumanoidBone.FootR] = -0.3f;
        pose[HumanoidBone.ThighL] = 0.2f;
        pose[HumanoidBone.ShinL] = -0.55f;
        pose[HumanoidBone.FootL] = -0.6f;
    }

    private static (float, float, float, float) Mix((float, float, float, float) a, (float, float, float, float) b, float t) =>
        (Lerp(a.Item1, b.Item1, t), Lerp(a.Item2, b.Item2, t), Lerp(a.Item3, b.Item3, t), Lerp(a.Item4, b.Item4, t));

    private static float Wrap(float angle) => angle - MathF.Tau * MathF.Floor((angle + MathF.PI) / MathF.Tau);

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private static float Fraction(float x) => x - MathF.Floor(x);

    private static float SmoothStep(float from, float to, float x)
    {
        float t = Math.Clamp((x - from) / (to - from), 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    private static float SmootherStep(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    private readonly record struct StrikeMoment(float Progress, float WindUpEnd, float ActiveEnd, StrikeKind Kind);

    /// <summary>A damped spring toward a moving target (semi-implicit Euler).</summary>
    private struct Spring
    {
        public Spring(float frequency, float damping)
        {
            Frequency = frequency;
            Damping = damping;
            Value = 0f;
            Velocity = 0f;
        }

        /// <summary>Natural frequency in Hz.</summary>
        public float Frequency { get; }

        /// <summary>Damping ratio: 1 settles without overshoot, lower sways.</summary>
        public float Damping { get; }

        public float Value { get; private set; }
        public float Velocity { get; set; }

        public void Step(float target, float delta)
        {
            float omega = MathF.Tau * Frequency;
            float accel = omega * omega * (target - Value) - 2f * Damping * omega * Velocity;
            Velocity += accel * delta;
            Value += Velocity * delta;
        }
    }
}
