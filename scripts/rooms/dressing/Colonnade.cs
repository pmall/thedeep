using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// A row of piers carrying arches (an arcade, a gallery), with an optional beam laid over the arches and
/// cables slung between some capitals. The node is the base of the first pier; piers stand every
/// <see cref="Span"/> meters along +X. Visual only, built when ready from swappable mesh resources
/// (a column, an arch), deterministic for a given <see cref="Seed"/>. Stack two for a two-tier arcade.
/// </summary>
public partial class Colonnade : Node3D
{
    [Export] public Mesh Pier { get; set; } = null!;

    /// <summary>The arch spanning one bay, its origin at the springing line's center; null for bare piers.</summary>
    [Export] public Mesh? Arch { get; set; }

    [Export] public Material Material { get; set; } = null!;

    [Export] public int Bays { get; set; } = 4;
    [Export] public float Span { get; set; } = 8f;

    /// <summary>Height of the arches' springing line above the node: the top of the piers.</summary>
    [Export] public float ArchHeight { get; set; } = 8.5f;

    /// <summary>Bays left without their arch (a fallen arch, an opening).</summary>
    [Export] public int[] OpenBays { get; set; } = System.Array.Empty<int>();

    /// <summary>Height of the beam's center above the node; 0 for no beam.</summary>
    [ExportGroup("Beam")]
    [Export] public float BeamHeight { get; set; }

    /// <summary>Beam thickness (Y) and depth (Z), meters.</summary>
    [Export] public Vector2 BeamSection { get; set; } = new(1f, 1.25f);

    /// <summary>How far the beam runs past the first and last pier.</summary>
    [Export] public float BeamOverhang { get; set; } = 2f;

    /// <summary>A cable bundle spanning one bay, from its left capital; null for no cables.</summary>
    [ExportGroup("Cables")]
    [Export] public Mesh? Cables { get; set; }

    [Export] public Material? CableMaterial { get; set; }

    /// <summary>Share of bays with cables.</summary>
    [Export] public float CableChance { get; set; } = 0.4f;

    /// <summary>Where cables hang from, relative to a pier's base (in front of the capital).</summary>
    [Export] public Vector3 CableOffset { get; set; } = new(0f, 8.25f, 0.75f);

    [Export] public int Seed { get; set; } = 1;

    public override void _Ready()
    {
        for (int i = 0; i <= Bays; i++)
        {
            Add($"Pier{i}", Pier, Material, new Vector3(i * Span, 0f, 0f), cast: true);
        }

        if (Arch is not null)
        {
            foreach (int bay in Enumerable.Range(0, Bays).Where(b => !OpenBays.Contains(b)))
            {
                Add($"Arch{bay}", Arch, Material, new Vector3((bay + 0.5f) * Span, ArchHeight, 0f), cast: true);
            }
        }

        if (BeamHeight > 0f)
        {
            float length = Bays * Span + 2f * BeamOverhang;
            var beam = new BlockMesh { Size = new Vector3(length, BeamSection.X, BeamSection.Y), Bevel = 0.08f };
            Add("Beam", beam, Material, new Vector3(Bays * Span * 0.5f, BeamHeight, 0f), cast: true);
        }

        if (Cables is not null && CableMaterial is not null)
        {
            foreach (int bay in DressingLayout.Chosen(Bays, CableChance, Seed))
            {
                Add($"Cables{bay}", Cables, CableMaterial, new Vector3(bay * Span, 0f, 0f) + CableOffset, cast: false);
            }
        }
    }

    private void Add(string name, Mesh mesh, Material material, Vector3 position, bool cast)
    {
        AddChild(new MeshInstance3D
        {
            Name = name,
            Mesh = mesh,
            MaterialOverride = material,
            Position = position,
            CastShadow = cast ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }
}
