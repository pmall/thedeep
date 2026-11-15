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
    private static readonly StringName FrameAboveName = "FrameAbove";
    private static readonly StringName FrameBelowName = "FrameBelow";
    private static readonly StringName ArchitectureName = "Architecture";
    private static readonly StringName UnderglowName = "Underglow";

    private static readonly StageSpec Spec = StageSpec.Default;

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

    /// <summary>Its furnace mouths' light.</summary>
    [Export] public Material ArchitectureGlow { get; set; } = null!;

    /// <summary>Its ice.</summary>
    [Export] public Material ArchitectureIce { get; set; } = null!;

    /// <summary>How much architecture stands behind the room.</summary>
    public enum PillarRows
    {
        /// <summary>None (a closed shaft).</summary>
        None,

        /// <summary>All its rows but the nearest.</summary>
        ExceptNearest,

        /// <summary>All of them.</summary>
        All,
    }

    /// <summary>How much architecture stands behind the room.</summary>
    [Export] public PillarRows Pillars { get; set; } = PillarRows.All;

    /// <summary>This room's rows of architecture, nearest first; empty for its layer's (<see cref="BiomeProfile.ArchitectureRows"/>).</summary>
    [Export] public BackdropRowData[] ArchitectureRows { get; set; } = [];

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

        _farSkyline = AddSkyline("FarSkyline", FarSkylineDepth, min.Y - Spec.FarSkylineBelow, size, centerX, Spec.FarSkylineMinHeight);
        _nearSkyline = AddSkyline("NearSkyline", NearSkylineDepth, min.Y - Spec.NearSkylineBelow, size, centerX, Spec.NearSkylineMinHeight);

        var depthRange = new Vector2(Spec.FrameDepthMin, Spec.FrameDepthMax);
        if (FrameBelow)
        {
            FrameBand band = Spec.Frame(min.ToCore(), max.ToCore(), above: false);
            AddChild(new BackdropScatter
            {
                Name = FrameBelowName,
                Material = FrameMaterial,
                Count = band.Count,
                AreaMin = band.AreaMin.ToGodot(),
                AreaMax = band.AreaMax.ToGodot(),
                DepthRange = depthRange,
                SizeMin = Spec.FrameBelowSizeMin.ToGodot(),
                SizeMax = Spec.FrameBelowSizeMax.ToGodot(),
                Seed = Spec.Seed(Seed, Spec.FrameBelowSalt),
            });
        }

        if (FrameAbove)
        {
            FrameBand band = Spec.Frame(min.ToCore(), max.ToCore(), above: true);
            AddChild(new BackdropScatter
            {
                Name = FrameAboveName,
                Material = FrameMaterial,
                Count = band.Count,
                AreaMin = band.AreaMin.ToGodot(),
                AreaMax = band.AreaMax.ToGodot(),
                DepthRange = depthRange,
                SizeMin = Spec.FrameAboveSizeMin.ToGodot(),
                SizeMax = Spec.FrameAboveSizeMax.ToGodot(),
                Seed = Spec.Seed(Seed, Spec.FrameAboveSalt),
                Hanging = true,
            });
        }

        if (Dust is not null && DustDensity > 0f)
        {
            var dust = Dust.Instantiate<AmbientParticles>();
            dust.Named("Dust");
            dust.Position = new Vector3(centerX, (min.Y + max.Y) * 0.5f - Spec.DustSink.X, -Spec.DustSink.Y);
            dust.Extents = new Vector3(size.X * 0.5f, size.Y * Spec.DustHeightShare, Spec.DustHalfDepth);
            dust.Density = DustDensity;
            AddChild(dust);
            _dust = dust;
        }

        List<SolidBox> solids = room.SolidBoxes();
        foreach (Light3D light in room.Shafts)
        {
            if (light is SpotLight3D { LightVolumetricFogEnergy: > 0f } spot)
            {
                AddChild(new LightShaft { Light = spot, Material = ShaftMaterial, Solids = solids, FloorY = min.Y }.Named($"Beam{spot.NameText()}"));
            }
        }
    }

    private BackdropArchitecture? _architecture;
    private AmbientParticles? _dust;
    private Vector2 _min, _max;

    /// <summary>
    /// Raises the depth layer's architecture behind the room (again, in another layer's style, when the biome changes).
    /// Called by the room after <see cref="Build"/> and on every biome change.
    /// </summary>
    public void Restyle(BiomeProfile biome)
    {
        Underglow(biome);
        _dust?.Vary(biome.AirMoteSize, biome.AirMoteColor, biome.AirMoteLift);

        // The layer's skylines, or none where it names none.
        ShowSkyline(_farSkyline, biome.FarSkyline);
        ShowSkyline(_nearSkyline, biome.NearSkyline);

        BackdropRowData[] rows = ArchitectureRows.Length > 0 ? ArchitectureRows : biome.ArchitectureRows;
        if (_architecture is not null && ((_architecture.Style == biome.Architecture && _architecture.Rows == rows) || Pillars == PillarRows.None))
        {
            return;
        }

        _architecture?.Free();
        _architecture = null;
        if (Pillars == PillarRows.None)
        {
            return;
        }

        _architecture = new BackdropArchitecture
        {
            Name = ArchitectureName,
            Style = biome.Architecture,
            Stone = PillarMaterial,
            Metal = ArchitectureMetal,
            Glow = ArchitectureGlow,
            Ice = ArchitectureIce,
            BoundsMin = _min,
            BoundsMax = _max,
            Rows = rows,
            OmitNearest = Pillars == PillarRows.ExceptNearest,
            Seed = Spec.Seed(Seed, Spec.ArchitectureSalt),
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
                Name = UnderglowName,
                ShadowEnabled = false,
                SkyMode = DirectionalLight3D.SkyModeEnum.LightOnly,
            };
            AddChild(_underglow);
            _underglow.LookAt(_underglow.GlobalPosition + Spec.UnderglowDirection.ToGodot(), Vector3.Forward);
        }

        _underglow.Visible = true;
        _underglow.LightColor = biome.UnderglowColor;
        _underglow.LightEnergy = biome.UnderglowEnergy;
    }

    /// <summary>A skyline's sheet, hidden until the biome gives it a look (<see cref="Restyle"/>).</summary>
    private MeshInstance3D AddSkyline(string name, float depth, float bottom, Vector2 roomSize, float centerX, float minHeight)
    {
        float viewDistance = SideCamera.DefaultDistance + depth;
        float width = roomSize.X + 2f * viewDistance * SideCamera.ViewHalfWidthPerMeter;
        float height = Mathf.Max(minHeight, roomSize.Y + minHeight * Spec.SkylineHeightShare);
        var skyline = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(width, height), CenterOffset = new Vector3(0f, height * 0.5f, 0f) },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(centerX, bottom, -depth),
            Visible = false,
        }.Named(name);
        AddChild(skyline);
        return skyline;
    }
}
