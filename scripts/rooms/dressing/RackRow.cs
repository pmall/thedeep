using Godot;

namespace TheDeep;

/// <summary>
/// A row of pieces standing side by side along +X (server racks, reliquary cabinets, tanks): seeded
/// variants, some slots empty, a few toppled onto their side. The node is the base of the first slot's
/// center. Visual only, built when ready from swappable mesh resources; deterministic for a given <see cref="Seed"/>.
/// </summary>
public partial class RackRow : Node3D
{
    /// <summary>The variants to pick from (meshes standing on their origin).</summary>
    [Export] public Mesh[] Variants { get; set; } = System.Array.Empty<Mesh>();

    [Export] public Material Material { get; set; } = null!;

    [Export] public int Count { get; set; } = 8;

    /// <summary>Distance between slot centers, meters.</summary>
    [Export] public float Pitch { get; set; } = 1f;

    [Export] public float GapChance { get; set; } = 0.15f;
    [Export] public float ToppleChance { get; set; } = 0.05f;

    /// <summary>A toppled piece lies on its side: how high its center sits (half its depth, for a rack).</summary>
    [Export] public float ToppledHeight { get; set; } = 0.5f;

    /// <summary>Keeps the row clear around regularly spaced obstacles (the piers of an arcade): every this many meters along the row, from <see cref="ClearOffset"/>; 0 for none.</summary>
    [Export] public float ClearEvery { get; set; }

    [Export] public float ClearOffset { get; set; }

    /// <summary>Slots whose center lies within this distance of a clear point stay empty.</summary>
    [Export] public float ClearRadius { get; set; } = 1.5f;

    [Export] public int Seed { get; set; } = 1;

    public override void _Ready()
    {
        if (Variants.Length == 0)
        {
            return;
        }

        foreach (DressingSlot slot in DressingLayout.Row(Count, Variants.Length, GapChance, ToppleChance, Seed))
        {
            float x = slot.Index * Pitch;
            if (ClearEvery > 0f && DressingLayout.NearRepeat(x, ClearOffset, ClearEvery, ClearRadius))
            {
                continue;
            }

            var piece = new MeshInstance3D
            {
                Name = $"Piece{slot.Index}",
                Mesh = Variants[slot.Variant],
                MaterialOverride = Material,
                Position = new Vector3(x, 0f, 0f),
            };
            if (slot.Toppled)
            {
                // Laid over toward +X, its base against the slot's left edge.
                piece.Basis = new Basis(Vector3.Back, -Mathf.Pi / 2f);
                piece.Position += new Vector3(0f, ToppledHeight, 0f);
            }

            AddChild(piece);
        }
    }
}
