using System.Numerics;

namespace TheDeep.Tests;

/// <summary>
/// The live tuning's numbers (resources/player_tuning.tres) the tests simulate, kept once here; <c>TuningFixtureTests</c> holds
/// them to that file.
/// </summary>
public static class TestTuning
{
    /// <summary>The share of a jump's take-off speed an arrival up through a hatch keeps (RoomTravel.ClimbRise's default).</summary>
    public const float ClimbRise = 0.85f;

    /// <summary>The base moveset, without ledge assist (a test that wants it says so: <c>Move with { LedgeAssist = ... }</c>).</summary>
    public static readonly MoveLimits Move = new(
        RunSpeed: 7f, AirAcceleration: 45f, AirDeceleration: 30f, AirDrag: 12f, AirCarrySpeed: 11.5f, JumpHeight: 3.2f, JumpTimeToApex: 0.38f, JumpCutFactor: 0.45f,
        FallGravityMultiplier: 1.6f, ApexHangThreshold: 1.5f, ApexGravityMultiplier: 0.55f, MaxFallSpeed: 20f,
        DashDistance: 4.5f, DashDuration: 0.16f, DashExitSpeedFactor: 1f, AirDashes: 1, Width: 0.7f, Height: 1.6f);

    /// <summary>The pilgrim's body box: its width by its height.</summary>
    public static readonly Vector2 Body = new(Move.Width, Move.Height);

    /// <summary>The Harpoon group.</summary>
    public static readonly HarpoonLimits Harpoon = new(Range: 9f, MinDistance: 1.2f, ZipSpeed: 17f, ZipAcceleration: 110f, ExitSpeedFactor: 0.65f, ExitLift: 6f);

    /// <summary>The Mag-grip group.</summary>
    public static readonly GripLimits Grip = new(LeapAwaySpeed: 6.5f, LeapUpFactor: 0.85f, RegrabDelay: 0.25f, DropSpeed: 2f, LetGoSpeed: 1.5f);

    /// <summary>The Water group.</summary>
    public static readonly WaterLimits Water = new(SwimSpeed: 4.5f, SwimAcceleration: 30f, SwimDeceleration: 18f, FloatDepth: 1.05f,
        FloatStiffness: 4f, RiseSpeed: 4f, SinkSpeed: 4.5f, WaterAcceleration: 22f, SplashKeep: 0.3f, EnterDepth: 0.55f, JumpWindow: 0.4f);

    /// <summary>Every group the planner simulates.</summary>
    public static readonly PlannerLimits Planner = new(Move, Harpoon, Grip, Water, Rise: ClimbRise * JumpMath.TakeOffSpeed(3.2f, 0.38f));

    /// <summary>The controls' numbers.</summary>
    public static readonly ControlLimits Control = new(
        RunSpeed: 7f, GroundAcceleration: 70f, GroundDeceleration: 80f, AirAcceleration: 45f, AirDeceleration: 30f, AirDrag: 12f, AirCarrySpeed: 11.5f,
        Gravity: JumpMath.Gravity(3.2f, 0.38f), ApexHangThreshold: 1.5f, ApexGravityMultiplier: 0.55f, FallGravityMultiplier: 1.6f,
        MaxFallSpeed: 20f, CoyoteTime: 0.15f, JumpBufferTime: 0.15f, DashBufferTime: 0.1f, DashCooldown: 0.4f, HarpoonBufferTime: 0.1f,
        GripRegrabDelay: 0.25f, AirDashes: 1, HurtKnockback: 6f, HurtLift: 6f, HurtStun: 0.22f, StickThreshold: 0.3f);
}
