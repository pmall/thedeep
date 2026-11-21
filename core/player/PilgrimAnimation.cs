using System;
using System.Numerics;
using static TheDeep.Motion;

namespace TheDeep;

/// <summary>
/// The pilgrim's animation decided from its movement, Godot-free: which <see cref="PilgrimPose"/> the body shows, how the
/// idle gives way to the run (<see cref="Gait"/>) and how fast the run plays so its feet keep pace with the ground
/// (<see cref="RunRate"/>); in the air, how far the jump is a leap (<see cref="Leap"/>) and how far it has turned into a
/// fall (<see cref="Fall"/>, restarted at the apex: <see cref="FallBegan"/>); the short moves played once over the pose
/// (<see cref="Cue"/>) and the sword strike's (<see cref="Strike"/>). The clips themselves are motion capture, played by the body's adapter (PlayerVisual). The feel
/// numbers here shape the animation only, never the movement.
/// </summary>
public sealed class PilgrimAnimation
{
    /// <summary>Above this share of the run clip's speed, the body shows the run alone; below, it fades into the idle.</summary>
    private const float FullGaitShare = 0.4f;

    /// <summary>How fast the gait follows the speed, 1/s.</summary>
    private const float GaitSharpness = 12f;

    /// <summary>The run never plays slower than this rate (slower reads as slow motion: the idle covers that), nor faster than
    /// <see cref="MaxRunRate"/>.</summary>
    private const float MinRunRate = 0.7f;
    private const float MaxRunRate = 1.8f;

    /// <summary>A landing at this downward speed (m/s) or more is a hard one.</summary>
    public const float HardLanding = 14f;

    /// <summary>A landing slower than this (m/s) plays nothing: a step down.</summary>
    public const float SoftestLanding = 3f;

    /// <summary>A landing at this ground speed (m/s) or more plays nothing: the run carries on through it.</summary>
    public const float RunningLanding = 2.5f;

    /// <summary>Above this share of the run clip's speed, a jump shows the leap alone; below, it is the tuck.</summary>
    private const float FullLeapShare = 0.8f;

    /// <summary>The fall shows from this rise (m/s, up positive), and alone from <see cref="FallFull"/> down.</summary>
    private const float FallFrom = 1.5f;
    private const float FallFull = -3f;

    private readonly float _runClipSpeed;

    /// <param name="runClipSpeed">How fast the run clip travels at its own rate, m/s (its root's travel over its length).</param>
    public PilgrimAnimation(float runClipSpeed)
    {
        _runClipSpeed = MathF.Max(runClipSpeed, 0.1f);
    }

    /// <summary>The pose the body shows.</summary>
    public PilgrimPose Pose { get; private set; }

    /// <summary>How much the run shows over the idle on the ground, 0 standing to 1 running.</summary>
    public float Gait { get; private set; }

    /// <summary>The rate the run clip plays at, so its feet keep pace with the ground.</summary>
    public float RunRate { get; private set; } = 1f;

    /// <summary>In the air, how much the jump shows the leap over the tuck, 0 to 1 (from the speed across).</summary>
    public float Leap { get; private set; }

    /// <summary>In the air, how much the fall shows over the jump, 0 rising to 1 falling.</summary>
    public float Fall { get; private set; }

    /// <summary>The body began to come down this tick: the fall's clip starts over.</summary>
    public bool FallBegan { get; private set; }

    /// <summary>The short move to start this tick, if any; one tick only.</summary>
    public PilgrimCue Cue { get; private set; }

    /// <summary>The held moment over the movement (resting, taking a relic, dying).</summary>
    public PilgrimMoment Moment { get; set; }

    /// <summary>Where the Harpoon's line pulls, in the body's frame (x forward along the facing, y up).</summary>
    public Vector2 Reach { get; set; } = Vector2.UnitX;

    /// <summary>The sword strike shown over the pose.</summary>
    public StrikeShow Strike { get; } = new();

    private PilgrimCue _pending;
    private float _lastVelocityY;

    /// <summary>The body touched down at <paramref name="impactSpeed"/> m/s downward.</summary>
    public void Land(float impactSpeed)
    {
        if (impactSpeed >= SoftestLanding)
        {
            Ask(impactSpeed >= HardLanding ? PilgrimCue.HardLand : PilgrimCue.Land);
        }
    }

    /// <summary>A blow landed on the pilgrim.</summary>
    public void Hurt() => Ask(PilgrimCue.Hurt);

    /// <summary>The ledge assist lifted the body onto a top.</summary>
    public void Vault() => Ask(PilgrimCue.Vault);

    /// <summary>Within a tick, a cue later in <see cref="PilgrimCue"/> outranks an earlier one (a blow, a landing).</summary>
    private void Ask(PilgrimCue cue)
    {
        if (_pending == PilgrimCue.None || cue >= _pending)
        {
            _pending = cue;
        }
    }

    /// <summary>
    /// Advances by <paramref name="delta"/> seconds. <paramref name="velocity"/> is the body's in its own frame (m/s; on a wall,
    /// walking up it is walking forward).
    /// </summary>
    public void Update(float delta, Vector2 velocity, MotionMode mode)
    {
        float dt = MathF.Max(delta, 0f);
        PilgrimPose was = Pose;
        Pose = PilgrimPoses.Of(mode, Moment);
        float speed = MathF.Abs(velocity.X);
        bool airborne = Pose == PilgrimPose.Air;
        if (airborne && was != PilgrimPose.Air)
        {
            Leap = Math.Clamp(speed / (_runClipSpeed * FullLeapShare), 0f, 1f); // a jump is taken as it begins
        }

        float fall = SmoothStep(FallFrom, FallFull, velocity.Y);
        FallBegan = airborne && was == PilgrimPose.Air && _lastVelocityY > 0f && velocity.Y <= 0f;
        Fall = airborne ? fall : 0f;
        _lastVelocityY = velocity.Y;
        bool walking = Pose is PilgrimPose.Ground;
        float target = walking ? Math.Clamp(speed / (_runClipSpeed * FullGaitShare), 0f, 1f) : 0f;
        Gait = Damp(Gait, target, GaitSharpness, dt);
        RunRate = Math.Clamp(speed / _runClipSpeed, MinRunRate, MaxRunRate);
        // A landing shows only standing on the ground: running, the run carries through it; elsewhere it is no landing.
        bool landing = _pending is PilgrimCue.Land or PilgrimCue.HardLand;
        Cue = landing && (Pose != PilgrimPose.Ground || speed >= RunningLanding) ? PilgrimCue.None : _pending;
        _pending = PilgrimCue.None;
        Strike.Update(dt);
    }

    /// <summary>Back to a still stance (spawn, teleport).</summary>
    public void Reset()
    {
        Moment = PilgrimMoment.None;
        Gait = 0f;
        RunRate = 1f;
        Leap = 0f;
        Fall = 0f;
        _lastVelocityY = 0f;
        _pending = PilgrimCue.None;
        Strike.Reset();
        Update(0f, Vector2.Zero, MotionMode.Ground);
    }
}
