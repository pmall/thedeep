using Godot;

namespace TheDeep;

/// <summary>
/// Input action names defined in project.godot. Use these instead of string literals.
/// </summary>
public static class InputActions
{
    /// <summary>Held to move left.</summary>
    public static readonly StringName MoveLeft = "move_left";
    /// <summary>Held to move right.</summary>
    public static readonly StringName MoveRight = "move_right";
    /// <summary>Held up: the vertical input's upper half (aiming the harpoon above, moving up a gripped wall, the Ballast with
    /// down).</summary>
    public static readonly StringName MoveUp = "move_up";
    /// <summary>Held down: the vertical input's lower half (aiming the harpoon below, the Drill's dive with dash, the Ballast
    /// with up).</summary>
    public static readonly StringName MoveDown = "move_down";
    /// <summary>Jumps; held longer, higher.</summary>
    public static readonly StringName Jump = "jump";
    /// <summary>Dashes.</summary>
    public static readonly StringName Dash = "dash";
    /// <summary>Strikes.</summary>
    public static readonly StringName Attack = "attack";
    /// <summary>Fires the Harpoon at the anchor in sight.</summary>
    public static readonly StringName Harpoon = "harpoon";
    /// <summary>Takes what is offered: talks, pulls a lever, rests at a shrine, reads a plate.</summary>
    public static readonly StringName Interact = "interact";
    /// <summary>Opens and closes the map screen.</summary>
    public static readonly StringName Map = "map";

    // Owner capture keys (OwnerCapture), available in every build.
    /// <summary>Saves a screenshot of the game (<see cref="OwnerCapture"/>).</summary>
    public static readonly StringName CaptureScreenshot = "capture_screenshot";
    /// <summary>Starts and stops a video recording of the game (<see cref="OwnerCapture"/>).</summary>
    public static readonly StringName CaptureVideo = "capture_video";

    // Debug-only actions (handled by DebugCheats).
    /// <summary>Puts the pilgrim back at the room's spawn point.</summary>
    public static readonly StringName DebugRespawn = "debug_respawn";
    /// <summary>Gives or takes away the Mag-grip.</summary>
    public static readonly StringName DebugToggleMagGrip = "debug_toggle_mag_grip";
    /// <summary>Gives or takes away the Harpoon.</summary>
    public static readonly StringName DebugToggleHarpoon = "debug_toggle_harpoon";
    /// <summary>Gives or takes away the Drill.</summary>
    public static readonly StringName DebugToggleDrill = "debug_toggle_drill";
    /// <summary>Gives or takes away the Ballast.</summary>
    public static readonly StringName DebugToggleBallast = "debug_toggle_ballast";
    /// <summary>Gives or takes away the Heat plating.</summary>
    public static readonly StringName DebugToggleHeatPlating = "debug_toggle_heat_plating";
    /// <summary>Dresses the room in the next layer's biome.</summary>
    public static readonly StringName DebugCycleBiome = "debug_cycle_biome";
    /// <summary>Shows or hides the debug readout (<see cref="DebugHud"/>).</summary>
    public static readonly StringName DebugToggleHud = "debug_toggle_hud";
    /// <summary>Shows or hides the controls manual (<see cref="ControlsManual"/>).</summary>
    public static readonly StringName DebugToggleManual = "debug_toggle_manual";
}
