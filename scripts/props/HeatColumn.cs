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
/// burn never disagree: a plume of two sheets exactly its size (<see cref="Plume"/>, before and behind the pilgrim), dimmed to
/// <see cref="PlatedStrength"/> once the pilgrim is plated; embers carried up inside it; at its foot the vent it rises from, a
/// grate as wide as the column between two posts at its edges (<see cref="Source"/>); a light in the room's lamp color.
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

    [Export] public int Damage { get; set; } = 1;

    /// <summary>Ground closer than this to the column (meters, around it) is no footing to be set back on.</summary>
    [Export] public float FootingMargin { get; set; } = 0.75f;

    [Export] public PackedScene? Embers { get; set; }

    /// <summary>Embers per square meter of the column's face.</summary>
    [Export] public float EmberDensity { get; set; } = 1.2f;

    /// <summary>The light at the column's foot (its color is the room's lamp color).</summary>
    [Export] public float GlowEnergy { get; set; } = 2.5f;

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

    /// <summary>The light at its foot, in the room's lamp color.</summary>
    public OmniLight3D Glow => _glow ??= new OmniLight3D { Position = new Vector3(0f, 0.6f, 1.2f) };

    /// <summary>The sheets' depths: one behind the pilgrim, one before it (fainter), off every face of the play plane.</summary>
    private static readonly (float Z, float Strength)[] Sheets = { (-0.6f, 1f), (0.35f, 0.4f) };

    /// <summary>The vent: its grate's thickness and depth, its posts' width and height (meters).</summary>
    private const float GrateThickness = 0.12f, VentDepth = 3f, PostWidth = 0.16f, PostHeight = 0.45f;

    private OmniLight3D? _glow;
    private readonly System.Collections.Generic.List<MeshInstance3D> _sheets = new();
    private float _strength = -1f;

    /// <summary>The column on the play plane, for the map's geometry check.</summary>
    public Box Region => new(GlobalPosition.X - 0.5f * Size.X, GlobalPosition.Y, GlobalPosition.X + 0.5f * Size.X, GlobalPosition.Y + Size.Y);

    public override void _Ready()
    {
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

        Glow.LightEnergy = GlowEnergy;
        Glow.OmniRange = Mathf.Max(Size.X, 3f) * 1.6f;
        for (Node? n = GetParent(); n is not null; n = n.GetParent())
        {
            if (n is Room { Biome: { } biome })
            {
                Glow.LightColor = biome.LampColor;
                break;
            }
        }

        AddChild(Glow);
    }

    private void BuildPlume()
    {
        if (Plume is null)
        {
            return;
        }

        for (int i = 0; i < Sheets.Length; i++)
        {
            var sheet = new MeshInstance3D
            {
                Name = $"Plume{i}",
                Mesh = new QuadMesh { Size = Size, Material = Plume },
                Position = new Vector3(0f, 0.5f * Size.Y, Sheets[i].Z),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            };
            sheet.SetInstanceShaderParameter("size", Size);
            sheet.SetInstanceShaderParameter("phase", 0.43f * i);
            AddChild(sheet);
            _sheets.Add(sheet);
        }

        ShowStrength(1f);
    }

    /// <summary>The vent at its foot: a grate as wide as the column, raised off the floor's face, between two posts just outside its edges.</summary>
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
                Mesh = new BoxMesh { Size = new Vector3(PostWidth, PostHeight, VentDepth + 0.2f) },
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
        for (int i = 0; i < _sheets.Count; i++)
        {
            _sheets[i].SetInstanceShaderParameter("strength", strength * Sheets[i].Strength);
        }
    }

    public override void _Process(double delta)
    {
        if (_sheets.Count > 0)
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
