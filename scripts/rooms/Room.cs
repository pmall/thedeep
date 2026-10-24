using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>Root of a room scene: where the player spawns, the area the camera may show, and its look.</summary>
public partial class Room : Node3D
{
    [Export] public Marker3D SpawnPoint { get; set; } = null!;

    /// <summary>The depth layer's look, applied on ready (see <see cref="ApplyBiome"/>).</summary>
    [Export] public BiomeProfile Biome { get; set; } = null!;

    [Export] public WorldEnvironment Environment { get; set; } = null!;
    [Export] public DirectionalLight3D KeyLight { get; set; } = null!;

    /// <summary>Lamps that take the biome's lamp color.</summary>
    [Export] public Light3D[] Lamps { get; set; } = System.Array.Empty<Light3D>();

    /// <summary>Light shafts (spot lights seen in the volumetric fog) that take the biome's shaft color.</summary>
    [Export] public Light3D[] Shafts { get; set; } = System.Array.Empty<Light3D>();

    /// <summary>Lower-left corner (X, Y) of the area the camera is allowed to show, on the play plane.</summary>
    [Export] public Vector2 BoundsMin { get; set; } = new(-10f, -10f);

    /// <summary>Upper-right corner (X, Y) of the area the camera is allowed to show, on the play plane.</summary>
    [Export] public Vector2 BoundsMax { get; set; } = new(10f, 10f);

    public override void _Ready()
    {
        ApplyBiome(Biome);
    }

    /// <summary>Re-themes the room with <paramref name="biome"/>; can be called at any time.</summary>
    public void ApplyBiome(BiomeProfile biome)
    {
        Biome = biome;
        biome.Apply(Environment.Environment, KeyLight, Lamps, Shafts);
    }

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
