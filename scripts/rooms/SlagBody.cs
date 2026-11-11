using Godot;

namespace TheDeep;

/// <summary>
/// A body of molten slag (`docs/gameplay.md#slag`), placed and sized like a <see cref="WaterBody"/>: its surface at the node's
/// height, <see cref="Size"/> wide (X) and deep into the scene (Z), <see cref="Depth"/> meters down to its bed. The room gives it
/// its basin with blocks: a bed under its whole width and a bank on each side from the bed up to the surface, which the room
/// check holds (<see cref="SlagBasin"/>); rock standing in it is a pillar from the bed. Built here: the surface, carrying a
/// drifting crust (<see cref="SurfaceMaterial"/>); its front on the play plane from surface to bed (<see cref="SectionMaterial"/>,
/// brightest at the surface, darkening with depth, the bed and the banks' feet dark through it); a row of small shadowless lights
/// just over it and ahead of the front, in the biome's accent, from bank to bank (<see cref="GlowSpacing"/>), so every bank and
/// pillar standing in it is lit from below, the light falling off up the rock.
/// Touching it burns, plated or not (<see cref="Burn"/>, <see cref="Player.Scorch"/>: one health unless in the grace, the
/// pilgrim set back on its <see cref="Player.Footing"/>); ground within <see cref="FootingMargin"/> beside it and
/// <see cref="FootingHeight"/> over it is no footing. It never
/// kills outright. The map's geometry check reads its <see cref="PlayRegion"/> as space no planner enters (<see cref="RoomShape.Slag"/>).
/// </summary>
public partial class SlagBody : Node3D
{
    /// <summary>The section shader's per-instance surface and bed heights.</summary>
    private static readonly StringName SurfaceParameter = "surface_y";

    private static readonly StringName BedParameter = "bed_y";

    private static readonly StringName HalfWidthParameter = "half_width";

    /// <summary>Surface size: X across the room, Y into the scene (world Z).</summary>
    [Export] public Vector2 Size { get; set; } = new(4f, 4f);

    /// <summary>How far below the surface its bed lies, meters.</summary>
    [Export] public float Depth { get; set; } = 2f;

    /// <summary>Health a touch takes (none in the grace).</summary>
    [Export] public int Damage { get; set; } = 1;

    /// <summary>Ground closer than this to the slag (meters, beside it) is no footing to be set back on.</summary>
    [Export] public float FootingMargin { get; set; } = 0.75f;

    /// <summary>
    /// How far over the surface the space whose ground is no footing reaches, meters: a bank's lip standing over the slag, or
    /// rock just over it, is never where a burn sets the pilgrim back.
    /// </summary>
    [Export] public float FootingHeight { get; set; } = 2.5f;

    /// <summary>The front's world Z on the play plane (the blocks' front faces are at 2: kept off them, so no face shares its plane).</summary>
    [Export] public float SectionFront { get; set; } = 2.05f;

    /// <summary>Each light over the surface: its energy (its color is the biome's accent).</summary>
    [Export] public float GlowEnergy { get; set; } = 10f;

    /// <summary>How far each light reaches, meters: small, so the light stays on the rock near the slag and costs little.</summary>
    [Export] public float GlowRange { get; set; } = 5f;

    /// <summary>
    /// The most room between two lights along the surface, meters. One stands at each bank (just inside it) and the rest are spread
    /// evenly between, so the faces at the slag's edges, where banks and pillars stand, are lit as well as its middle.
    /// </summary>
    [Export] public float GlowSpacing { get; set; } = 2.5f;

    /// <summary>How high the lights hang over the surface, meters: low, so they light the rock over the slag from below.</summary>
    [Export] public float GlowHeight { get; set; } = 0.25f;

    /// <summary>How far ahead of the front (toward the camera) the lights hang, meters, so they reach the faces on the play plane.</summary>
    [Export] public float GlowAhead { get; set; } = 1.3f;

    /// <summary>The surface's look: crust drifting on the melt (<c>magma.tres</c>).</summary>
    [Export] public Material SurfaceMaterial { get; set; } = null!;

    /// <summary>The front's look on the play plane (<c>slag_section.tres</c>).</summary>
    [Export] public Material SectionMaterial { get; set; } = null!;

    /// <summary>The pilgrim, set by the session when the room loads.</summary>
    public Player? Player { get; set; }

    /// <summary>The surface's height.</summary>
    public float SurfaceY => GlobalPosition.Y;

    /// <summary>The slag on the play plane (its top is the surface, its bottom the bed), for the burn and the map's geometry check.</summary>
    public Box PlayRegion => new(GlobalPosition.X - 0.5f * Size.X, SurfaceY - Depth, GlobalPosition.X + 0.5f * Size.X, SurfaceY);

    public override void _Ready()
    {
        AddChild(new MeshInstance3D
        {
            Mesh = new PlaneMesh { Size = Size },
            MaterialOverride = SurfaceMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        var front = new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(Size.X, Depth) },
            MaterialOverride = SectionMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Position = new Vector3(0f, -0.5f * Depth, SectionFront - GlobalPosition.Z),
        };
        front.SetInstanceShaderParameter(SurfaceParameter, SurfaceY);
        front.SetInstanceShaderParameter(BedParameter, SurfaceY - Depth);
        front.SetInstanceShaderParameter(HalfWidthParameter, 0.5f * Size.X);
        AddChild(front);

        Color color = Colors.OrangeRed;
        for (Node? n = GetParent(); n is not null; n = n.GetParent())
        {
            if (n is Room { Biome: { } biome })
            {
                color = biome.Accent;
                break;
            }
        }

        float half = 0.5f * Size.X - 0.15f;
        int gaps = Mathf.Max(1, Mathf.CeilToInt(2f * half / GlowSpacing));
        for (int i = 0; i <= gaps; i++)
        {
            AddChild(new OmniLight3D
            {
                Name = $"Glow{i}",
                Position = new Vector3(-half + 2f * half * i / gaps, GlowHeight, SectionFront + GlowAhead - GlobalPosition.Z),
                LightColor = color,
                LightEnergy = GlowEnergy,
                OmniAttenuation = 1.3f,
                OmniRange = GlowRange,
                ShadowEnabled = false,
            });
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Player is not { } player || !player.InControl)
        {
            return;
        }

        Box slag = PlayRegion;
        var keepOff = new Box(slag.MinX - FootingMargin, slag.MinY - FootingMargin, slag.MaxX + FootingMargin, slag.MaxY + FootingHeight);
        BurnReach reach = Burn.Reach(slag, keepOff, player.BodyBox);
        if (reach == BurnReach.Clear)
        {
            return;
        }

        player.NearBurn();
        if (reach == BurnReach.Touching)
        {
            player.Scorch(Damage);
        }
    }
}
