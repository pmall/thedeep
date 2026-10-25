using Godot;

namespace TheDeep;

/// <summary>
/// The atmosphere stack every room repeats, as one shared piece (scenes/stage/RoomStage.tscn): the
/// environment and key light, clear air in front of the play plane, the far backdrop (two skylines and
/// hazy pillars), the dark framing foreground above and below the play area, and dust in the air.
/// Everything is sized from the room's camera bounds when the room is ready (<see cref="Build"/>), so a
/// room only states its layout and the few choices exported here. Deterministic for a given <see cref="Seed"/>.
/// Visual only: nothing here touches gameplay. Room-specific light (shafts, lamps), water and extra
/// particles stay in the room.
/// </summary>
public partial class RoomStage : Node3D
{
    /// <summary>Distance from the camera to the play plane (the gameplay camera); used to size the backdrop.</summary>
    private const float CameraDistance = 22f;

    /// <summary>Half the horizontal field of view's tangent at 16:9 with the 30° vertical FOV, with a margin.</summary>
    private const float ViewHalfWidthPerMeter = 0.55f;

    [Export] public WorldEnvironment Environment { get; set; } = null!;
    [Export] public DirectionalLight3D KeyLight { get; set; } = null!;

    /// <summary>Cancels the volumetric fog in front of the play plane (fog_clear.tres).</summary>
    [Export] public Material ClearFrontMaterial { get; set; } = null!;

    /// <summary>The farthest skyline (a backdrop_silhouette material); null for none.</summary>
    [ExportGroup("Backdrop")]
    [Export] public Material? FarSkyline { get; set; }

    /// <summary>The nearer skyline; null for none.</summary>
    [Export] public Material? NearSkyline { get; set; }

    [Export] public float FarSkylineDepth { get; set; } = 70f;
    [Export] public float NearSkylineDepth { get; set; } = 48f;

    /// <summary>Hazy out-of-focus pillars between the room and the skylines.</summary>
    [Export] public Material PillarMaterial { get; set; } = null!;

    /// <summary>Pillars per 10 m of backdrop width (0 for none).</summary>
    [Export] public float PillarDensity { get; set; } = 3.8f;

    /// <summary>Dark blurred silhouettes framing the screen, in front of the play plane (foreground.tres).</summary>
    [ExportGroup("Frame")]
    [Export] public Material FrameMaterial { get; set; } = null!;

    /// <summary>Silhouettes rising from below the bottom of the play area.</summary>
    [Export] public bool FrameBelow { get; set; } = true;

    /// <summary>Silhouettes hanging from above the top of the play area.</summary>
    [Export] public bool FrameAbove { get; set; } = true;

    /// <summary>Ambient dust behind the play plane (scenes/fx/Dust.tscn); null for none.</summary>
    [ExportGroup("Air")]
    [Export] public PackedScene? Dust { get; set; }

    /// <summary>Dust motes per m² of the room's face.</summary>
    [Export] public float DustDensity { get; set; } = 2.3f;

    [Export] public int Seed { get; set; } = 1;

    /// <summary>Builds the stack for <paramref name="room"/>'s bounds. Called once by the room when it is ready.</summary>
    public void Build(Room room)
    {
        Vector2 min = room.BoundsMin, max = room.BoundsMax;
        Vector2 size = max - min;
        float centerX = (min.X + max.X) * 0.5f;

        AddChild(new FogVolume
        {
            Name = "ClearFront",
            Position = new Vector3(centerX, (min.Y + max.Y) * 0.5f, 14f),
            Size = new Vector3(size.X + 32f, size.Y + 24f, 24f),
            Shape = RenderingServer.FogVolumeShape.Box,
            Material = ClearFrontMaterial,
        });

        AddSkyline("FarSkyline", FarSkyline, FarSkylineDepth, min.Y - 6f, size, centerX, 90f);
        AddSkyline("NearSkyline", NearSkyline, NearSkylineDepth, min.Y - 4f, size, centerX, 60f);

        float backdropWidth = size.X + 22f;
        if (PillarDensity > 0f)
        {
            AddChild(new BackdropScatter
            {
                Name = "FarPillars",
                Material = PillarMaterial,
                Count = Mathf.RoundToInt(backdropWidth * PillarDensity / 10f),
                AreaMin = new Vector2(min.X - 10f, min.Y - 2f),
                AreaMax = new Vector2(max.X + 12f, min.Y + 6f),
                DepthRange = new Vector2(-38f, -14f),
                SizeMin = new Vector3(1f, 14f, 1f),
                SizeMax = new Vector3(3.5f, Mathf.Max(40f, size.Y + 6f), 3.5f),
                Seed = Seed * 7 + 11,
            });
        }

        float frameWidth = size.X + 8f;
        if (FrameBelow)
        {
            AddChild(new BackdropScatter
            {
                Name = "FrameBelow",
                Material = FrameMaterial,
                Count = Mathf.Max(1, Mathf.RoundToInt(frameWidth / 9.5f)),
                AreaMin = new Vector2(min.X - 4f, min.Y - 4f),
                AreaMax = new Vector2(max.X + 4f, min.Y - 3f),
                DepthRange = new Vector2(6f, 9f),
                SizeMin = new Vector3(0.6f, 3f, 0.6f),
                SizeMax = new Vector3(1.8f, 6.5f, 1.5f),
                Seed = Seed * 7 + 37,
            });
        }

        if (FrameAbove)
        {
            AddChild(new BackdropScatter
            {
                Name = "FrameAbove",
                Material = FrameMaterial,
                Count = Mathf.Max(1, Mathf.RoundToInt(frameWidth / 15f)),
                AreaMin = new Vector2(min.X - 4f, max.Y + 2f),
                AreaMax = new Vector2(max.X + 4f, max.Y + 3f),
                DepthRange = new Vector2(6f, 9f),
                SizeMin = new Vector3(0.4f, 3f, 0.4f),
                SizeMax = new Vector3(1f, 6.5f, 1f),
                Seed = Seed * 7 + 41,
                Hanging = true,
            });
        }

        if (Dust is not null && DustDensity > 0f)
        {
            var dust = Dust.Instantiate<AmbientParticles>();
            dust.Name = "Dust";
            dust.Position = new Vector3(centerX, (min.Y + max.Y) * 0.5f - 1f, -5.5f);
            dust.Extents = new Vector3(size.X * 0.5f, size.Y * 0.4f, 6.5f);
            dust.Density = DustDensity;
            AddChild(dust);
        }
    }

    private void AddSkyline(string name, Material? material, float depth, float bottom, Vector2 roomSize, float centerX, float minHeight)
    {
        if (material is null)
        {
            return;
        }

        float viewDistance = CameraDistance + depth;
        float width = roomSize.X + 2f * viewDistance * ViewHalfWidthPerMeter;
        float height = Mathf.Max(minHeight, roomSize.Y + minHeight * 0.6f);
        AddChild(new MeshInstance3D
        {
            Name = name,
            Mesh = new QuadMesh { Size = new Vector2(width, height), CenterOffset = new Vector3(0f, height * 0.5f, 0f) },
            MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(centerX, bottom, -depth),
        });
    }
}
