using Godot;

namespace TheDeep;

/// <summary>
/// A named look-dev shot (tools/capture.sh) placed inside a room scene. The marker is the camera:
/// it sits in front of the play plane (its Z is the distance) and looks down -Z like SideCamera.
/// The node name is the shot's name in the capture files.
/// </summary>
public partial class PhotoSpot : Marker3D
{
    /// <summary>Where the player stands for this shot, in room coordinates (Z is ignored).</summary>
    [Export] public Vector3 PlayerPosition { get; set; }

    /// <summary>+1 facing right, -1 facing left.</summary>
    [Export] public int PlayerFacing { get; set; } = 1;

    /// <summary>Vertical field of view in degrees; 0 keeps the gameplay camera's.</summary>
    [Export] public float Fov { get; set; }

    /// <summary>Shows the game's own HUD (health) in this shot, to judge it; the shots are otherwise clean.</summary>
    [Export] public bool ShowHud { get; set; }
}
