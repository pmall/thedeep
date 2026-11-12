using Godot;

namespace TheDeep;

/// <summary>
/// Every movement number for the player. The live values are in resources/player_tuning.tres;
/// the defaults here only matter for a freshly created resource. Distances in meters, times in seconds.
/// </summary>
[GlobalClass]
public partial class PlayerTuning : Resource
{
    [ExportGroup("Run")]
    [Export] public float RunSpeed { get; set; } = 7f;
    [Export] public float GroundAcceleration { get; set; } = 70f;
    [Export] public float GroundDeceleration { get; set; } = 80f;
    [Export] public float AirAcceleration { get; set; } = 45f;
    [Export] public float AirDeceleration { get; set; } = 30f;
    /// <summary>In the air, speed above a run (a harpoon's release) bleeds off at this rate (m/s²) unless the stick pulls against it.</summary>
    [Export] public float AirDrag { get; set; } = 12f;

    /// <summary>Apex height of a full (held) jump.</summary>
    [ExportGroup("Jump")]
    [Export] public float JumpHeight { get; set; } = 3.2f;
    /// <summary>Time from take-off to apex of a full jump. Together with JumpHeight this sets gravity.</summary>
    [Export] public float JumpTimeToApex { get; set; } = 0.38f;
    /// <summary>Upward speed is multiplied by this when jump is released early (short hop).</summary>
    [Export] public float JumpCutFactor { get; set; } = 0.45f;
    /// <summary>Gravity multiplier while falling, for a snappier descent.</summary>
    [Export] public float FallGravityMultiplier { get; set; } = 1.6f;
    /// <summary>Below this vertical speed (m/s) with jump held, the apex gravity multiplier applies.</summary>
    [Export] public float ApexHangThreshold { get; set; } = 1.5f;
    [Export] public float ApexGravityMultiplier { get; set; } = 0.55f;
    [Export] public float MaxFallSpeed { get; set; } = 20f;
    /// <summary>Grace period after walking off a ledge during which a jump still works.</summary>
    [Export] public float CoyoteTime { get; set; } = 0.1f;
    /// <summary>A jump pressed this long before landing still fires on landing.</summary>
    [Export] public float JumpBufferTime { get; set; } = 0.12f;
    /// <summary>
    /// Ledge assist: airborne and pressing into a wall whose top is at most this far above the feet, the pilgrim is lifted onto
    /// it, keeping its way and speed (a jump that ends just short of a ledge still makes it).
    /// </summary>
    [Export] public float LedgeAssistHeight { get; set; } = 0.6f;

    [ExportGroup("Dash")]
    [Export] public float DashDistance { get; set; } = 4.5f;
    [Export] public float DashDuration { get; set; } = 0.16f;
    /// <summary>Time after a dash ends before the next one can start.</summary>
    [Export] public float DashCooldown { get; set; } = 0.4f;
    /// <summary>Horizontal speed kept when a dash ends, as a fraction of RunSpeed.</summary>
    [Export] public float DashExitSpeedFactor { get; set; } = 1f;
    /// <summary>A dash pressed this long before it becomes available still fires.</summary>
    [Export] public float DashBufferTime { get; set; } = 0.1f;
    /// <summary>Dashes allowed per airtime; refilled on landing.</summary>
    [Export] public int AirDashes { get; set; } = 1;

    /// <summary>Walking speed on metal walls and ceilings (Mag-grip).</summary>
    [ExportGroup("Mag-grip")]
    [Export] public float GripSpeed { get; set; } = 5.5f;
    [Export] public float GripAcceleration { get; set; } = 45f;
    [Export] public float GripDeceleration { get; set; } = 60f;
    /// <summary>Leaping off a wall: speed away from it, and upward speed as a fraction of the jump's take-off speed.</summary>
    [Export] public float GripLeapAwaySpeed { get; set; } = 6.5f;
    [Export] public float GripLeapUpFactor { get; set; } = 0.85f;
    /// <summary>After letting go or leaping off, the same surface can't be gripped again for this long.</summary>
    [Export] public float GripRegrabDelay { get; set; } = 0.25f;

    /// <summary>How far from the pilgrim's center an anchor can be fired at, and how near it must not be.</summary>
    [ExportGroup("Harpoon")]
    [Export] public float HarpoonRange { get; set; } = 9f;
    [Export] public float HarpoonMinDistance { get; set; } = 1.2f;
    /// <summary>Speed of the line flying out to the anchor; the pilgrim hangs, slowing, until it bites.</summary>
    [Export] public float HarpoonThrowSpeed { get; set; } = 70f;
    /// <summary>Top speed of the pull toward the anchor, and how fast the pull reaches it.</summary>
    [Export] public float HarpoonZipSpeed { get; set; } = 17f;
    [Export] public float HarpoonZipAcceleration { get; set; } = 110f;
    /// <summary>Letting go at the anchor: the pull's speed kept, as a fraction, and at least this much upward speed.</summary>
    [Export] public float HarpoonExitSpeedFactor { get; set; } = 0.65f;
    [Export] public float HarpoonExitLift { get; set; } = 6f;
    /// <summary>A harpoon pressed this long before an anchor comes into sight still fires.</summary>
    [Export] public float HarpoonBufferTime { get; set; } = 0.1f;

    /// <summary>The Drill's dive (dash while holding down): straight down at this speed until the pilgrim lands, at most this long.</summary>
    [ExportGroup("Drill")]
    [Export] public float DiveSpeed { get; set; } = 17f;
    [Export] public float DiveMaxTime { get; set; } = 1.2f;

    /// <summary>Swimming: top speed across the water, how fast it is reached and lost.</summary>
    [ExportGroup("Water")]
    [Export] public float SwimSpeed { get; set; } = 4.5f;
    [Export] public float SwimAcceleration { get; set; } = 30f;
    [Export] public float SwimDeceleration { get; set; } = 18f;
    /// <summary>Floating, the feet ride this far under the surface (the head and shoulders above it).</summary>
    [Export] public float FloatDepth { get; set; } = 1.05f;
    /// <summary>Floating, the body moves toward its float line at this many m/s per meter away, rising at most RiseSpeed.</summary>
    [Export] public float FloatStiffness { get; set; } = 4f;
    [Export] public float RiseSpeed { get; set; } = 4f;
    /// <summary>With Ballast's tanks full, the body sinks at this speed (also the fastest a float line pulls down).</summary>
    [Export] public float SinkSpeed { get; set; } = 4.5f;
    /// <summary>How quickly water changes the body's vertical speed (m/s per second): a plunge dips, then bobs back.</summary>
    [Export] public float WaterAcceleration { get; set; } = 22f;
    /// <summary>Breaking the surface, a plunge keeps this fraction of its downward speed.</summary>
    [Export] public float SplashKeep { get; set; } = 0.3f;
    /// <summary>The feet this deep under the surface swim (shallower is wading).</summary>
    [Export] public float WaterEnterDepth { get; set; } = 0.55f;
    /// <summary>A jump leaves the water from within this distance of the float line.</summary>
    [Export] public float WaterJumpWindow { get; set; } = 0.4f;

    /// <summary>The swing: wind-up, the window in which it hits, recovery, then a short cooldown (seconds).</summary>
    [ExportGroup("Attack")]
    [Export] public float AttackWindUp { get; set; } = 0.04f;
    [Export] public float AttackActive { get; set; } = 0.1f;
    [Export] public float AttackRecover { get; set; } = 0.14f;
    [Export] public float AttackCooldown { get; set; } = 0.05f;
    /// <summary>A blow's damage, before a strike adds its own (<see cref="StrikeTuning.ExtraDamage"/>) and the power-ups theirs.</summary>
    [Export] public int AttackDamage { get; set; } = 1;
    /// <summary>Seconds after a swing ends in which a press chains to the next (<see cref="Combo"/>).</summary>
    [Export] public float AttackChainWindow { get; set; } = 0.28f;
    /// <summary>Each strike's reach, damage, knockback, hit stop and bounce: one per <see cref="StrikeKind"/>.</summary>
    [Export] public StrikeTuning[] Strikes { get; set; } = System.Array.Empty<StrikeTuning>();

    /// <summary>Hits the pilgrim can take.</summary>
    [ExportGroup("Hurt")]
    [Export] public int MaxHealth { get; set; } = 5;
    /// <summary>After a hit, nothing hurts for this long.</summary>
    [Export] public float HurtGrace { get; set; } = 1.2f;
    /// <summary>A hit throws the pilgrim back (away from the blow) and up, and takes control away briefly.</summary>
    [Export] public float HurtKnockback { get; set; } = 6f;
    [Export] public float HurtLift { get; set; } = 6f;
    [Export] public float HurtStun { get; set; } = 0.22f;

    /// <summary>Gravity (m/s², positive) that makes the jump peak as tuned, derived from <see cref="JumpHeight"/> and
    /// <see cref="JumpTimeToApex"/>.</summary>
    public float Gravity => JumpMath.Gravity(JumpHeight, JumpTimeToApex);

    /// <summary>Upward speed at take-off (m/s), derived from <see cref="JumpHeight"/> and
    /// <see cref="JumpTimeToApex"/>.</summary>
    public float JumpTakeOffSpeed => JumpMath.TakeOffSpeed(JumpHeight, JumpTimeToApex);

    // The core reads these numbers as plain records: each one is built here, the one place they cross the seam.

    /// <summary>The Water group as the pure <see cref="WaterMotion"/> reads it.</summary>
    public WaterLimits Water => new(SwimSpeed, SwimAcceleration, SwimDeceleration, FloatDepth, FloatStiffness, RiseSpeed, SinkSpeed,
        WaterAcceleration, SplashKeep, WaterEnterDepth, WaterJumpWindow);

    /// <summary>Running, falling, the grace windows, the air dashes and the blows, as <see cref="PilgrimControl"/> and <see cref="MoveRules"/> read them.</summary>
    public ControlLimits Control => new(RunSpeed, GroundAcceleration, GroundDeceleration, AirAcceleration, AirDeceleration, AirDrag,
        Gravity, ApexHangThreshold, ApexGravityMultiplier, FallGravityMultiplier, MaxFallSpeed,
        CoyoteTime, JumpBufferTime, DashBufferTime, DashCooldown, HarpoonBufferTime, GripRegrabDelay,
        AirDashes, HurtKnockback, HurtLift, HurtStun);

    /// <summary>The strikes as the pure <see cref="Combo"/> reads them.</summary>
    public Combo NewCombo()
    {
        var shapes = new System.Collections.Generic.Dictionary<StrikeKind, StrikeShape>();
        foreach (StrikeTuning strike in Strikes)
        {
            shapes[strike.Kind] = strike.Shape;
        }

        return new Combo(shapes, AttackChainWindow);
    }

    /// <summary>The base moveset as the map's planner simulates it (<see cref="ReachPlanner"/>).</summary>
    public MoveLimits Move => new(RunSpeed, AirAcceleration, AirDeceleration, AirDrag, JumpHeight, JumpTimeToApex, JumpCutFactor,
        FallGravityMultiplier, ApexHangThreshold, ApexGravityMultiplier, MaxFallSpeed, DashDistance, DashDuration, DashExitSpeedFactor,
        AirDashes, LedgeAssist: LedgeAssistHeight);

    /// <summary>The Harpoon group, as the planner simulates it.</summary>
    public HarpoonLimits Harpoon => new(HarpoonRange, HarpoonMinDistance, HarpoonZipSpeed, HarpoonZipAcceleration, HarpoonExitSpeedFactor,
        HarpoonExitLift);

    /// <summary>The Mag-grip group, as the planner simulates it.</summary>
    public GripLimits Grip => new(GripLeapAwaySpeed, GripLeapUpFactor, GripRegrabDelay);
}
