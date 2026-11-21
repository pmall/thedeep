using System;
using System.Numerics;
using static TheDeep.Motion;

namespace TheDeep;

/// <summary>
/// The pilgrim's animation decided from its movement, Godot-free: which <see cref="PilgrimPose"/> the body shows, how the
/// idle gives way to the run (<see cref="Gait"/>) and how fast the run plays so its feet keep pace with the ground
/// (<see cref="RunRate"/>), as the grip's crawl and the swim's stroke do (<see cref="PoseRate"/>); in the air, how far the jump is a leap (<see cref="Leap"/>) and where
/// along its arc the body is (<see cref="Climb"/>: the air's clips are scrubbed by it, not played in time, so the pose
/// follows the arc the player steers); the short moves played once over the pose (<see cref="Cue"/>) and the sword
/// strike's (<see cref="Strike"/>). The clips are played by the body's adapter (PlayerVisual). The feel numbers here shape
/// the animation only, never the movement.
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

    /// <summary>Below this gait, standing, a strike drives the whole body; above it the legs keep running.</summary>
    private const float WholeStrikeGait = 0.5f;

    /// <summary>How fast the strike's hold on the legs follows, 1/s.</summary>
    private const float StrikeWholeSharpness = 20f;

    /// <summary>How fast the arm takes up the Harpoon's line and lets it go, 1/s.</summary>
    private const float ReachSharpness = 18f;

    /// <summary>A landing at this downward speed (m/s) or more is a hard one.</summary>
    public const float HardLanding = 14f;

    /// <summary>A landing slower than this (m/s) plays nothing: a step down.</summary>
    public const float SoftestLanding = 3f;

    /// <summary>A landing at this ground speed (m/s) or more plays nothing: the run carries on through it.</summary>
    public const float RunningLanding = 2.5f;

    /// <summary>Above this share of the run clip's speed, a jump shows the leap alone; below, it is the tuck.</summary>
    private const float FullLeapShare = 0.8f;

    /// <summary>How fast the leap follows the speed across once in the air, 1/s (steering eases it, never snaps it).</summary>
    private const float LeapSharpness = 5f;

    /// <summary>
    /// The climb (m/s, up positive) the air's clips start at, about a full jump's take-off (resources/player_tuning.tres:
    /// a 3.2 m rise in 0.38 s): <see cref="Climb"/> is 0 there, a half at the apex, 1 falling as fast.
    /// </summary>
    private const float FullClimb = 16f;

    /// <summary>How fast <see cref="Climb"/> follows the vertical speed, 1/s: a cut jump eases into its apex.</summary>
    private const float ClimbSharpness = 22f;

    /// <summary>The swim's stroke never slows below this rate (slower, it gives way to treading water).</summary>
    private const float MinStrokeRate = 0.45f;

    /// <summary>Above this share of the stroke clip's speed, the swim shows the stroke alone; below, it fades into treading water.</summary>
    private const float FullStrokeShare = 0.35f;

    /// <summary>How fast the swim's stroke follows the speed, 1/s.</summary>
    private const float StrokeSharpness = 6f;

    private readonly float _runClipSpeed;
    private readonly ClipSpeeds _speeds;

    public PilgrimAnimation(ClipSpeeds speeds)
    {
        _speeds = speeds;
        _runClipSpeed = MathF.Max(speeds.Run, 0.1f);
    }

    /// <summary>The pose the body shows.</summary>
    public PilgrimPose Pose { get; private set; }

    /// <summary>How much the run shows over the idle on the ground, 0 standing to 1 running.</summary>
    public float Gait { get; private set; }

    /// <summary>The rate the run clip plays at, so its feet keep pace with the ground.</summary>
    public float RunRate { get; private set; } = 1f;

    /// <summary>In the water, how much the stroke shows over treading water, 0 still to 1 swimming.</summary>
    public float Stroke { get; private set; }

    /// <summary>The rate the pose's clip plays at: matched to the speed for the grip's crawl and the swim, 1 otherwise.</summary>
    public float PoseRate { get; private set; } = 1f;

    /// <summary>In the air, how much the jump shows the leap over the tuck, 0 to 1 (from the speed across).</summary>
    public float Leap { get; private set; }

    /// <summary>In the air, where along the arc the body is: 0 taking off at full climb, a half at the apex, 1 falling as fast.</summary>
    public float Climb { get; private set; } = 0.5f;

    /// <summary>The short move to start this tick, if any; one tick only.</summary>
    public PilgrimCue Cue { get; private set; }

    /// <summary>The held moment over the movement (resting, taking a relic, dying).</summary>
    public PilgrimMoment Moment { get; set; }

    /// <summary>Where the Harpoon's line pulls, in the body's frame (x forward along the facing, y up).</summary>
    public Vector2 Reach { get; set; } = Vector2.UnitX;

    /// <summary>How much the near arm aims along <see cref="Reach"/>, 0 to 1: all the way on the line, not at all off it.</summary>
    public float ReachWeight { get; private set; }

    /// <summary>The sword strike shown over the pose.</summary>
    public StrikeShow Strike { get; } = new();

    /// <summary>
    /// How much of the body the strike drives, 0 the trunk and arms only (over a run, a jump) to 1 the whole body (standing;
    /// the down strike and the dash strike, which are whole-body moves wherever they are).
    /// </summary>
    public float StrikeWhole { get; private set; }

    private PilgrimCue _pending;

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
        float leap = Math.Clamp(speed / (_runClipSpeed * FullLeapShare), 0f, 1f);
        float climb = Math.Clamp(0.5f - 0.5f * velocity.Y / FullClimb, 0f, 1f);
        bool tookOff = airborne && was != PilgrimPose.Air;
        Leap = tookOff ? leap : Damp(Leap, leap, LeapSharpness, dt); // a jump is taken as it begins
        Climb = tookOff ? climb : Damp(Climb, climb, ClimbSharpness, dt);
        bool walking = Pose is PilgrimPose.Ground;
        float target = walking ? Math.Clamp(speed / (_runClipSpeed * FullGaitShare), 0f, 1f) : 0f;
        Gait = Damp(Gait, target, GaitSharpness, dt);
        RunRate = Math.Clamp(speed / _runClipSpeed, MinRunRate, MaxRunRate);
        float stroke = Pose == PilgrimPose.Swim && _speeds.Swim > 0f ? Math.Clamp(speed / (_speeds.Swim * FullStrokeShare), 0f, 1f) : 0f;
        Stroke = Damp(Stroke, stroke, StrokeSharpness, dt);
        PoseRate = Pose switch
        {
            PilgrimPose.Grip => Matched(speed, _speeds.Grip, 0f),
            PilgrimPose.Swim => Matched(speed, _speeds.Swim, MinStrokeRate),
            _ => 1f,
        };
        // A landing shows only standing on the ground: running, the run carries through it; elsewhere it is no landing.
        bool landing = _pending is PilgrimCue.Land or PilgrimCue.HardLand;
        Cue = landing && (Pose != PilgrimPose.Ground || speed >= RunningLanding) ? PilgrimCue.None : _pending;
        _pending = PilgrimCue.None;
        Strike.Update(dt);
        bool whole = Strike.Kind is StrikeKind.Down or StrikeKind.Dash or StrikeKind.Finisher || Pose == PilgrimPose.Ground && Gait < WholeStrikeGait;
        StrikeWhole = Damp(StrikeWhole, whole ? 1f : 0f, StrikeWholeSharpness, dt);
        ReachWeight = Damp(ReachWeight, Pose == PilgrimPose.Zip ? 1f : 0f, ReachSharpness, dt);
    }

    /// <summary>The rate a clip traveling <paramref name="clipSpeed"/> plays at to keep pace with <paramref name="speed"/>.</summary>
    private static float Matched(float speed, float clipSpeed, float slowest) =>
        clipSpeed > 0f ? Math.Clamp(speed / clipSpeed, slowest, MaxRunRate) : 1f;

    /// <summary>Back to a still stance (spawn, teleport).</summary>
    public void Reset()
    {
        Moment = PilgrimMoment.None;
        Gait = 0f;
        RunRate = 1f;
        Leap = 0f;
        Climb = 0.5f;
        Stroke = 0f;
        _pending = PilgrimCue.None;
        Strike.Reset();
        StrikeWhole = 0f;
        ReachWeight = 0f;
        Update(0f, Vector2.Zero, MotionMode.Ground);
    }
}
