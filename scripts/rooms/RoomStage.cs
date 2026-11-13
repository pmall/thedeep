using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// The atmosphere stack every room repeats, as one shared piece (scenes/stage/RoomStage.tscn): the
/// environment and key light, clear air in front of the play plane, the far backdrop (two skylines, the depth layer's
/// own as its profile names them, and its architecture standing in the haze: <see cref="BackdropArchitecture"/>), the dark framing foreground above and below the play area, dust in the air,
/// and the visible beam of each of the room's light shafts (<see cref="LightShaft"/>).
/// Everything is sized from the room's camera bounds when the room is ready (<see cref="Build"/>), so a
/// room only states its layout and the few choices exported here. Deterministic for a given <see cref="Seed"/>.
/// Visual only: nothing here touches gameplay. Room-specific lights (the shafts' spot lights, lamps), water and extra
/// particles stay in the room.
/// </summary>
public partial class RoomStage : Node3D
{
    /// <summary>Distance from the camera to the play plane (the gameplay camera); used to size the backdrop.</summary>
    public const float CameraDistance = 22f;

    /// <summary>
    /// Half the view's width per meter of distance from the camera: half the horizontal field of view's tangent
    /// at 16:9 with the 30° vertical FOV, with a margin.
    /// </summary>
    public const float ViewHalfWidthPerMeter = 0.55f;

    [Export] public WorldEnvironment Environment { get; set; } = null!;
    [Export] public DirectionalLight3D KeyLight { get; set; } = null!;

    /// <summary>How far behind the play plane the far skyline and the nearer one stand (their looks are the biome's: <see cref="BiomeProfile.FarSkyline"/>).</summary>
    [ExportGroup("Backdrop")]
    [Export] public float FarSkylineDepth { get; set; } = 70f;
    [Export] public float NearSkylineDepth { get; set; } = 48f;

    /// <summary>Masonry of the architecture between the room and the skylines (<see cref="BackdropArchitecture"/>).</summary>
    [Export] public Material PillarMaterial { get; set; } = null!;

    /// <summary>Iron and plating of that architecture.</summary>
    [Export] public Material ArchitectureMetal { get; set; } = null!;

    /// <summary>Its sparse accent pieces.</summary>
    [Export] public Material ArchitectureAccent { get; set; } = null!;

    /// <summary>Its ice.</summary>
    [Export] public Material ArchitectureIce { get; set; } = null!;

    /// <summary>
    /// How much architecture stands behind the room: 0 for none (a closed shaft), under 2 for the two far rows, else all
    /// three (the name is older than the architecture: it once counted boxes per 10 m).
    /// </summary>
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

    /// <summary>The look of the light shafts' beams (light_shaft.tres).</summary>
    [ExportGroup("Light")]
    [Export] public Material ShaftMaterial { get; set; } = null!;

    /// <summary>Builds the stack for <paramref name="room"/>'s bounds. Called once by the room when it is ready.</summary>
    public void Build(Room room)
    {
        Vector2 min = room.BoundsMin, max = room.BoundsMax;
        (_min, _max) = (min, max);
        Vector2 size = max - min;
        float centerX = (min.X + max.X) * 0.5f;

        _farSkyline = AddSkyline("FarSkyline", FarSkylineDepth, min.Y - 6f, size, centerX, 90f);
        _nearSkyline = AddSkyline("NearSkyline", NearSkylineDepth, min.Y - 4f, size, centerX, 60f);

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

        List<SolidBox> solids = room.SolidBoxes();
        foreach (Light3D light in room.Shafts)
        {
            if (light is SpotLight3D { LightVolumetricFogEnergy: > 0f } spot)
            {
                AddChild(new LightShaft { Name = $"Beam{spot.Name}", Light = spot, Material = ShaftMaterial, Solids = solids, FloorY = min.Y });
            }
        }
    }

    private BackdropArchitecture? _architecture;
    private Vector2 _min, _max;

    /// <summary>
    /// Raises the depth layer's architecture behind the room (again, in another layer's style, when the biome changes).
    /// Called by the room after <see cref="Build"/> and on every biome change.
    /// </summary>
    public void Restyle(BiomeProfile biome)
    {
        Underglow(biome);

        // The layer's skylines, or none where it names none.
        ShowSkyline(_farSkyline, biome.FarSkyline);
        ShowSkyline(_nearSkyline, biome.NearSkyline);

        if (_architecture is not null && (_architecture.Style == biome.Architecture || PillarDensity <= 0f))
        {
            return;
        }

        _architecture?.Free();
        _architecture = null;
        if (PillarDensity <= 0f)
        {
            return;
        }

        _architecture = new BackdropArchitecture
        {
            Name = "Architecture",
            Style = biome.Architecture,
            Stone = PillarMaterial,
            Metal = ArchitectureMetal,
            Accent = ArchitectureAccent,
            Ice = ArchitectureIce,
            BoundsMin = _min,
            BoundsMax = _max,
            Rows = PillarDensity < 2f ? 2 : 3,
            Seed = Seed * 7 + 11,
        };
        AddChild(_architecture);
    }

    private MeshInstance3D? _farSkyline, _nearSkyline;

    private static void ShowSkyline(MeshInstance3D? skyline, Material? look)
    {
        if (skyline is null)
        {
            return;
        }

        skyline.MaterialOverride = look;
        skyline.Visible = look is not null;
    }
    private DirectionalLight3D? _underglow;

    /// <summary>The layer's light from below (<see cref="BiomeProfile.UnderglowEnergy"/>), made when first needed.</summary>
    private void Underglow(BiomeProfile biome)
    {
        if (biome.UnderglowEnergy <= 0f)
        {
            if (_underglow is not null)
            {
                _underglow.Visible = false;
            }

            return;
        }

        if (_underglow is null)
        {
            _underglow = new DirectionalLight3D
            {
                Name = "Underglow",
                ShadowEnabled = false,
                SkyMode = DirectionalLight3D.SkyModeEnum.LightOnly,
            };
            AddChild(_underglow);
            // Shining up, a little sideways, and into the scene: onto undersides and the faces of the walls behind.
            _underglow.LookAt(_underglow.GlobalPosition + new Vector3(0.25f, 0.8f, -0.55f), Vector3.Forward);
        }

        _underglow.Visible = true;
        _underglow.LightColor = biome.UnderglowColor;
        _underglow.LightEnergy = biome.UnderglowEnergy;
    }

    /// <summary>A skyline's sheet, hidden until the biome gives it a look (<see cref="Restyle"/>).</summary>
    private MeshInstance3D AddSkyline(string name, float depth, float bottom, Vector2 roomSize, float centerX, float minHeight)
    {
        float viewDistance = CameraDistance + depth;
        float width = roomSize.X + 2f * viewDistance * ViewHalfWidthPerMeter;
        float height = Mathf.Max(minHeight, roomSize.Y + minHeight * 0.6f);
        var skyline = new MeshInstance3D
        {
            Name = name,
            Mesh = new QuadMesh { Size = new Vector2(width, height), CenterOffset = new Vector3(0f, height * 0.5f, 0f) },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(centerX, bottom, -depth),
            Visible = false,
        };
        AddChild(skyline);
        return skyline;
    }
}
