using Godot;

namespace TheDeep;

/// <summary>
/// Scatters placeholder boxes off the play plane (behind it for the hazy backdrop, in front of it for
/// the blurred foreground), so the 2.5D depth reads. Purely visual: no collision, no shadows.
/// Deterministic for a given <see cref="Seed"/>.
/// </summary>
public partial class BackdropScatter : Node3D
{
    [Export] public Material Material { get; set; } = null!;
    [Export] public int Count { get; set; } = 20;
    [Export] public int Seed { get; set; } = 1;

    /// <summary>Range (X, Y) for each box's base: its bottom, or its top when <see cref="Hanging"/>.</summary>
    [Export] public Vector2 AreaMin { get; set; } = new(-10f, -2f);
    [Export] public Vector2 AreaMax { get; set; } = new(10f, 2f);

    /// <summary>Z range (min, max). Negative is behind the play plane, positive in front of it.</summary>
    [Export] public Vector2 DepthRange { get; set; } = new(-30f, -10f);

    [Export] public Vector3 SizeMin { get; set; } = Vector3.One;
    [Export] public Vector3 SizeMax { get; set; } = new(3f, 10f, 3f);

    /// <summary>Boxes hang down from their base (ceiling clutter) instead of standing on it.</summary>
    [Export] public bool Hanging { get; set; }

    public override void _Ready()
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)Seed };
        var unitBox = new BoxMesh();
        for (int i = 0; i < Count; i++)
        {
            var size = new Vector3(
                rng.RandfRange(SizeMin.X, SizeMax.X),
                rng.RandfRange(SizeMin.Y, SizeMax.Y),
                rng.RandfRange(SizeMin.Z, SizeMax.Z));
            var basePosition = new Vector3(
                rng.RandfRange(AreaMin.X, AreaMax.X),
                rng.RandfRange(AreaMin.Y, AreaMax.Y),
                rng.RandfRange(DepthRange.X, DepthRange.Y));

            var box = new MeshInstance3D
            {
                Mesh = unitBox,
                MaterialOverride = Material,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                Position = basePosition + new Vector3(0f, (Hanging ? -0.5f : 0.5f) * size.Y, 0f),
                Scale = size,
            };
            AddChild(box);
        }
    }
}
