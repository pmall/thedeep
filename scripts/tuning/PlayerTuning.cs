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

    public float Gravity => JumpMath.Gravity(JumpHeight, JumpTimeToApex);

    public float JumpTakeOffSpeed => JumpMath.TakeOffSpeed(JumpHeight, JumpTimeToApex);
}
