using Godot;

namespace TheDeep;

/// <summary>
/// Input action names defined in project.godot. Use these instead of string literals.
/// </summary>
public static class InputActions
{
    public static readonly StringName MoveLeft = "move_left";
    public static readonly StringName MoveRight = "move_right";
    public static readonly StringName MoveUp = "move_up";
    public static readonly StringName MoveDown = "move_down";
    public static readonly StringName Jump = "jump";
    public static readonly StringName Dash = "dash";
    public static readonly StringName Attack = "attack";
    public static readonly StringName Interact = "interact";

    // Owner capture keys (OwnerCapture), available in every build.
    public static readonly StringName CaptureScreenshot = "capture_screenshot";
    public static readonly StringName CaptureVideo = "capture_video";

    // Debug-only actions (handled by DebugCheats).
    public static readonly StringName DebugRespawn = "debug_respawn";
    public static readonly StringName DebugToggleMagGrip = "debug_toggle_mag_grip";
    public static readonly StringName DebugToggleHarpoon = "debug_toggle_harpoon";
    public static readonly StringName DebugToggleDrill = "debug_toggle_drill";
    public static readonly StringName DebugToggleBallast = "debug_toggle_ballast";
    public static readonly StringName DebugToggleHeatPlating = "debug_toggle_heat_plating";
}
