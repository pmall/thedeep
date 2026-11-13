using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// A column of heat on the play plane (`docs/gameplay.md#heat`): <see cref="Size"/> wide and tall, rising from the node's origin
/// (its foot, on rock) to the roof, hood or exit that ends it, a gate the room check holds to having no way round
/// (<see cref="HeatGate"/>). Without Heat plating, touching it burns (<see cref="Player.Scorch"/>: health taken unless in the
/// grace period, and the pilgrim set back on the last ground it stood on clear of heat, its <see cref="Player.Footing"/>);
/// ground within <see cref="FootingMargin"/> of it is no footing. With Heat plating it is harmless.
/// <para>
/// It is seen at its true edges (`docs/art-direction.md#volcanic-look`), everything built from <see cref="Region"/> so look and
/// burn never disagree: a plume of one sheet exactly its size (<see cref="Plume"/>), just behind the play plane so its edges show at their true x from any
/// camera angle, dimmed to
/// <see cref="PlatedStrength"/> once the pilgrim is plated; embers carried up inside it; at its foot the vent it rises from, a
/// grate as wide as the column between two posts at its edges (<see cref="Source"/>); a row of shadowless lights in the biome's
/// molten colour (<see cref="Glow"/>) up its height (<see cref="GlowSpacing"/>), from the vent to the hood, lighting the floor, the hood and the pilgrim beside it,
/// dimmed with the plume once the pilgrim is plated.
/// </para>
/// <para>
/// Not a <see cref="Block"/>: the map's geometry check reads its <see cref="Region"/> as space no planner may enter without Heat
/// plating.
/// </para>
/// </summary>
public partial class HeatColumn : Node3D
{
    /// <summary>Width (X) and height (Y) of the column, rising from the node's origin, meters.</summary>
    [Export] public Vector2 Size { get; set; } = new(2f, 6f);

    /// <summary>
    /// The hatch exit the column rises through, if any: the seam's frame then gives the column its width and line
    /// (<see cref="SeamFrame.Heat"/>), so the two rooms' columns are one column through the seam by construction. Under an up exit the
    /// column keeps its foot (the node's height) and reaches the slab; over a down exit it rises from the slab to its own top (the node's
    /// height plus <see cref="Size"/>.Y). Unset, the column's place and <see cref="Size"/> are its own.
    /// </summary>
    [Export] public RoomExit? Through { get; set; }

    [Export] public int Damage { get; set; } = 1;

    /// <summary>Ground closer than this to the column (meters, around it) is no footing to be set back on.</summary>
    [Export] public float FootingMargin { get; set; } = 0.75f;

    [Export] public PackedScene? Embers { get; set; }

    /// <summary>Embers per square meter of the column's face.</summary>
    [Export] public float EmberDensity { get; set; } = 1.2f;

    /// <summary>Each light up the column: its energy (its colour is the biome's: <see cref="Glow"/>).</summary>
    [Export] public float GlowEnergy { get; set; } = 2.5f;

    /// <summary>The most room between two of its lights up the column, meters: one just over the vent, one under its end.</summary>
    [Export] public float GlowSpacing { get; set; } = 3f;

    /// <summary>How far ahead of the plume (toward the camera) its lights hang, meters: in front of the play plane's faces (at 2), so they light them.</summary>
    [Export] public float GlowAhead { get; set; } = 2.6f;

    /// <summary>The plume's material (heat_plume.gdshader); none draws no plume.</summary>
    [Export] public Material? Plume { get; set; }

    /// <summary>The vent's metal (its grate and posts); none builds no vent.</summary>
    [Export] public Material? Source { get; set; }

    /// <summary>How strong the plume shows once the pilgrim wears Heat plating: still there, read as passable.</summary>
    [Export] public float PlatedStrength { get; set; } = 0.4f;

    /// <summary>The pilgrim, set by the session when the room loads.</summary>
    public Player? Player { get; set; }

    /// <summary>Raised when any column burns a pilgrim without Heat plating (hurt or, in its grace, only set back): the gate hints listen.</summary>
    public static event System.Action<HeatColumn>? Scorched;


    /// <summary>The sheet's depth: just behind the pilgrim, close enough to the play plane that no parallax moves its edges, off every face.</summary>
    private const float SheetZ = -0.1f;

    /// <summary>The vent: fixed proportions of one look, no world sets them (the grate takes the column's width); its grate's thickness and depth, its posts' width, height and depth (meters).</summary>
    private const float GrateThickness = 0.1f, VentDepth = 3f, PostWidth = 0.08f, PostHeight = 0.3f, PostDepth = 0.5f;

    private readonly System.Collections.Generic.List<OmniLight3D> _glows = [];
    private MeshInstance3D? _sheet;
    private float _strength = -1f;

    /// <summary>The column on the play plane, for the map's geometry check.</summary>
    public Box Region => new(GlobalPosition.X - 0.5f * Size.X, GlobalPosition.Y, GlobalPosition.X + 0.5f * Size.X, GlobalPosition.Y + Size.Y);

    /// <summary>Takes the column's width, line and the end at the seam from the hatch it rises through (<see cref="Through"/>).</summary>
    private void RiseThrough()
    {
        if (Through is null)
        {
            return;
        }

        SeamEnd frame = Through.GetChildren().OfType<SeamEnd>().FirstOrDefault()
            ?? throw new System.InvalidOperationException($"heat {Name} rises through {Through.Name}, an exit no seam brick frames");
        float end = Through.Side == ExitSide.Up ? GlobalPosition.Y : GlobalPosition.Y + Size.Y;
        Box column = frame.Profile.Frame.Heat(Through.Side, Through.RoomPosition.ToCore(), end);
        GlobalPosition = new Vector3(0.5f * (column.MinX + column.MaxX), column.MinY, GlobalPosition.Z);
        Size = new Vector2(column.MaxX - column.MinX, column.MaxY - column.MinY);
    }

    public override void _Ready()
    {
        RiseThrough();
        if (Embers?.Instantiate() is AmbientParticles embers)
        {
            // The embers start low and inside its width, so they rise within it.
            embers.Extents = new Vector3(0.4f * Size.X, 0.25f * Size.Y, 0.5f);
            embers.Density = EmberDensity;
            embers.Position = new Vector3(0f, 0.25f * Size.Y, 0f);
            AddChild(embers);
        }

        BuildPlume();
        BuildVent();

        BuildGlow();
    }

    /// <summary>Its lights, spread from just over the vent to just under its end.</summary>
    private void BuildGlow()
    {
        float low = Mathf.Min(0.6f, 0.5f * Size.Y), high = Mathf.Max(low, Size.Y - 0.6f);
        int gaps = Mathf.Max(1, Mathf.CeilToInt((high - low) / GlowSpacing));
        float range = Mathf.Max(Size.X, 2f) + 4f;
        for (int i = 0; i <= gaps; i++)
        {
            var glow = new OmniLight3D
            {
                Name = $"Glow{i}",
                Position = new Vector3(0f, low + (high - low) * i / gaps, GlowAhead),
                LightEnergy = GlowEnergy,
                OmniAttenuation = 1.3f,
                OmniRange = range,
                ShadowEnabled = false,
            };
            _glows.Add(glow);
            AddChild(glow);
        }
    }

    /// <summary>Gives its lights the biome's molten colour, the plume's own glow (<see cref="BiomeProfile.Molten"/>); the room calls it with its biome.</summary>
    public void Glow(BiomeProfile biome)
    {
        foreach (OmniLight3D glow in _glows)
        {
            glow.LightColor = biome.Molten;
        }
    }

    private void BuildPlume()
    {
        if (Plume is null)
        {
            return;
        }

        _sheet = new MeshInstance3D
        {
            Name = "Plume",
            Mesh = new QuadMesh { Size = Size, Material = Plume },
            Position = new Vector3(0f, 0.5f * Size.Y, SheetZ),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _sheet.SetInstanceShaderParameter("size", Size);
        AddChild(_sheet);

        ShowStrength(1f);
    }

    /// <summary>The vent at its foot: a grate as wide as the column, raised off the floor's face, between two slim posts just outside its edges, at the play plane.</summary>
    private void BuildVent()
    {
        if (Source is null)
        {
            return;
        }

        var grate = new MeshInstance3D
        {
            Name = "Grate",
            Mesh = new GrateMesh { Size = new Vector3(Size.X, GrateThickness, VentDepth), Spacing = 0.28f, Bar = 0.07f },
            MaterialOverride = Source,
            Position = new Vector3(0f, 0.02f, 0f),
        };
        AddChild(grate);
        foreach (float side in new[] { -1f, 1f })
        {
            AddChild(new MeshInstance3D
            {
                Name = side < 0f ? "PostWest" : "PostEast",
                Mesh = new BoxMesh { Size = new Vector3(PostWidth, PostHeight, PostDepth) },
                MaterialOverride = Source,
                Position = new Vector3(side * (0.5f * Size.X + 0.5f * PostWidth), 0.5f * PostHeight + 0.01f, 0f),
            });
        }
    }

    private void ShowStrength(float strength)
    {
        if (Mathf.IsEqualApprox(strength, _strength))
        {
            return;
        }

        _strength = strength;
        _sheet?.SetInstanceShaderParameter("strength", strength);
        foreach (OmniLight3D glow in _glows)
        {
            glow.LightEnergy = GlowEnergy * (0.5f + 0.5f * strength);
        }
    }

    public override void _Process(double delta)
    {
        if (_sheet is not null)
        {
            ShowStrength(Player is { } player && player.Abilities.Has(Ability.HeatPlating) ? PlatedStrength : 1f);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Player is not { } player || player.Abilities.Has(Ability.HeatPlating) || !player.InControl)
        {
            return;
        }

        BurnReach reach = Burn.Reach(Region, Region.Grown(FootingMargin), player.BodyBox);
        if (reach == BurnReach.Clear)
        {
            return;
        }

        player.NearBurn();
        if (reach == BurnReach.Near)
        {
            return;
        }

        player.Scorch(Damage);
        Scorched?.Invoke(this);
    }
}
