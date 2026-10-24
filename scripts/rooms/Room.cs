using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>Root of a room scene: where the player spawns and the area the camera may show.</summary>
public partial class Room : Node3D
{
    [Export] public Marker3D SpawnPoint { get; set; } = null!;

    /// <summary>Lower-left corner (X, Y) of the area the camera is allowed to show, on the play plane.</summary>
    [Export] public Vector2 BoundsMin { get; set; } = new(-10f, -10f);

    /// <summary>Upper-right corner (X, Y) of the area the camera is allowed to show, on the play plane.</summary>
    [Export] public Vector2 BoundsMax { get; set; } = new(10f, 10f);

    /// <summary>The room's look-dev camera spots (tools/capture.sh), in scene order.</summary>
    public IEnumerable<PhotoSpot> PhotoSpots() => FindPhotoSpots(this);

    private static IEnumerable<PhotoSpot> FindPhotoSpots(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            if (child is PhotoSpot spot)
            {
                yield return spot;
            }

            foreach (PhotoSpot nested in FindPhotoSpots(child))
            {
                yield return nested;
            }
        }
    }
}
