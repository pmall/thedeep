using Godot;

namespace TheDeep;

/// <summary>
/// A zone of heat on the play plane (a vent's column of hot air, the air over a pool of slag): <see cref="Size"/> wide and
/// tall, rising from the node's origin. Without Heat plating, touching it burns: a blow (it takes health, throws the pilgrim
/// back out and up, then the grace period); during the grace it still throws the pilgrim out, unhurt. With Heat plating
/// it is harmless. It looks like heat: a shimmering haze (<see cref="HazeMaterial"/>), embers rising (<see cref="Embers"/>),
/// a glow at its foot. Not a <see cref="Block"/>: the map's geometry check reads its <see cref="Region"/> as space no planner
/// may enter without Heat plating.
/// </summary>
public partial class HeatZone : Node3D
{
    /// <summary>Width (X) and height (Y) of the zone, rising from the node's origin, meters.</summary>
    [Export] public Vector2 Size { get; set; } = new(2f, 6f);

    [Export] public int Damage { get; set; } = 1;

    [Export] public Material? HazeMaterial { get; set; }
    [Export] public PackedScene? Embers { get; set; }

    /// <summary>Embers per square meter of the zone's face.</summary>
    [Export] public float EmberDensity { get; set; } = 1.2f;

    /// <summary>The light at the zone's foot (its color is the room's lamp color).</summary>
    [Export] public float GlowEnergy { get; set; } = 2.5f;

    /// <summary>The haze shader's per-instance column height.</summary>
    private static readonly StringName HeightParameter = "height";

    /// <summary>The pilgrim, set by the session when the room loads.</summary>
    public Player? Player { get; set; }

    /// <summary>Raised when any zone throws back a pilgrim without Heat plating (burned or repelled): the gate hints listen.</summary>
    public static event System.Action<HeatZone>? Scorched;

    /// <summary>The light at its foot, in the room's lamp color.</summary>
    public OmniLight3D Glow => _glow ??= new OmniLight3D { Position = new Vector3(0f, 0.6f, 1.2f) };

    private OmniLight3D? _glow;

    /// <summary>The zone on the play plane, for the map's geometry check.</summary>
    public Box Region => new(GlobalPosition.X - 0.5f * Size.X, GlobalPosition.Y, GlobalPosition.X + 0.5f * Size.X, GlobalPosition.Y + Size.Y);

    public override void _Ready()
    {
        if (HazeMaterial is not null)
        {
            var haze = new MeshInstance3D
            {
                Mesh = new QuadMesh { Size = Size + new Vector2(0.6f, 0f) },
                MaterialOverride = HazeMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = new Vector3(0f, 0.5f * Size.Y, 0.4f),
            };
            haze.SetInstanceShaderParameter(HeightParameter, Size.Y);
            AddChild(haze);
        }

        if (Embers?.Instantiate() is AmbientParticles embers)
        {
            embers.Extents = new Vector3(0.5f * Size.X, 0.25f * Size.Y, 0.6f);
            embers.Density = EmberDensity;
            embers.Position = new Vector3(0f, 0.25f * Size.Y, 0.2f);
            AddChild(embers);
        }

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

    public override void _PhysicsProcess(double delta)
    {
        if (Player is not { } player || player.Abilities.Has(Ability.HeatPlating) || !player.InControl)
        {
            return;
        }

        Rect2 body = player.BodyRect;
        Box r = Region;
        if (body.Position.X < r.MaxX && r.MinX < body.End.X && body.Position.Y < r.MaxY && r.MinY < body.End.Y)
        {
            // In its grace period the pilgrim is not hurt, but the heat still throws it out: no way through without the plating.
            var from = new System.Numerics.Vector2(GlobalPosition.X, player.GlobalPosition.Y);
            if (!player.TakeHit(Damage, from))
            {
                player.Repel(from);
            }

            Scorched?.Invoke(this);
        }
    }
}
