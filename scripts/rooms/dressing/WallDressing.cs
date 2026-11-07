using Godot;

namespace TheDeep;

/// <summary>
/// Dresses a room's back wall (behind the play plane) in a <see cref="WallStyle"/>, laid out by <see cref="WallLayout"/>:
/// the node stands at the middle of the wall's foot, on its face; <see cref="Size"/> is the area dressed (X along the
/// wall, Y up). Visual only, built when ready from kit meshes and swappable materials, deterministic for a <see cref="Seed"/>.
/// Pieces stay within <see cref="Size"/> and about a meter out from the wall, so keep the node a meter behind the blocks'
/// back faces (Z −3 against blocks 4 deep).
/// </summary>
public partial class WallDressing : Node3D
{
    [Export] public WallStyle Style { get; set; }

    [Export] public Vector2 Size { get; set; } = new(20f, 10f);

    [Export] public int Seed { get; set; } = 1;

    /// <summary>Iron (pilasters, girders, pipes, chains); rust_mid.tres when unset.</summary>
    [Export] public Material? Iron { get; set; }

    /// <summary>
    /// Plating (panels, wheels, pistons, racks); when unset, backdrop_metal.tres for machinery (dark, hazed: in the core's pale
    /// bronze the midground plating outshone the play plane and its grippable metal), metal_mid.tres otherwise.
    /// </summary>
    [Export] public Material? Plate { get; set; }

    /// <summary>What glows in a furnace mouth; furnace_mouth.tres when unset.</summary>
    [Export] public Material? Molten { get; set; }

    /// <summary>A slit's light; glow_strip.tres when unset.</summary>
    [Export] public Material? Glow { get; set; }

    /// <summary>Masonry (an archive's piers, a nave's arcades); stone_mid.tres when unset.</summary>
    [Export] public Material? Stone { get; set; }

    /// <summary>Ice (icicles under shelves); ice_mid.tres when unset.</summary>
    [Export] public Material? Ice { get; set; }

    /// <summary>The shadowed backs of a nave's niches and blind arches; stone_recess.tres when unset.</summary>
    [Export] public Material? Recess { get; set; }

    /// <summary>A nave's share of bays with icicles under their courses (0 to 1): a chapel of the frozen layer.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float Frost { get; set; }

    /// <summary>A nave's pieces repeat at a handful of sizes: each built once per wall.</summary>
    private readonly System.Collections.Generic.Dictionary<(string, System.Numerics.Vector2), Mesh> _shared = new();

    /// <summary>The racks an archive's shelves carry: three variants, built once per wall.</summary>
    private readonly Mesh[] _racks = new Mesh[3];

    public override void _Ready()
    {
        var looks = new Looks(
            Iron ?? GD.Load<Material>("res://assets/materials/rust_mid.tres"),
            Plate ?? GD.Load<Material>(Style == WallStyle.Machinery ? "res://assets/materials/backdrop_metal.tres" : "res://assets/materials/metal_mid.tres"),
            Molten ?? GD.Load<Material>("res://assets/materials/furnace_mouth.tres"),
            Glow ?? GD.Load<Material>("res://assets/materials/glow_strip.tres"),
            Stone ?? GD.Load<Material>("res://assets/materials/stone_mid.tres"),
            Ice ?? GD.Load<Material>("res://assets/materials/ice_mid.tres"),
            Recess ?? GD.Load<Material>("res://assets/materials/stone_recess.tres"));
        int i = 0;
        foreach (WallPlacement p in WallLayout.Build(Style, Size.X, Size.Y, Seed, Frost))
        {
            Build(p, i++, looks);
        }
    }

    /// <summary>The materials the wall is built with: each export, or its default when unset.</summary>
    private readonly record struct Looks(Material Iron, Material Plate, Material Molten, Material Glow, Material Stone, Material Ice, Material Recess);

    private void Build(WallPlacement p, int index, in Looks looks)
    {
        Vector3 at = p.Position.ToGodot();
        float w = p.Size.X, h = p.Size.Y;
        switch (p.Piece)
        {
            case WallPiece.Pilaster:
                Add(new BlockMesh { Size = new Vector3(w, h, 0.7f), Bevel = 0.06f }, looks.Iron, at + new Vector3(0f, h * 0.5f, 0f));
                Add(new BlockMesh { Size = new Vector3(w * 0.45f, h, 0.25f), Bevel = 0.04f }, looks.Iron, at + new Vector3(0f, h * 0.5f, 0.45f));
                break;
            case WallPiece.Girder:
                Add(new BlockMesh { Size = new Vector3(w, h, 0.5f), Bevel = 0.05f }, looks.Iron, at);
                Add(new BlockMesh { Size = new Vector3(w, h * 0.3f, 0.7f), Bevel = 0.03f }, looks.Iron, at + new Vector3(0f, h * 0.5f, 0f));
                break;
            case WallPiece.MouthGlow:
                Add(new QuadMesh { Size = new Vector2(w, h) }, looks.Molten, at + new Vector3(0f, h * 0.5f, 0f), shadow: false);
                break;
            case WallPiece.Mouth:
                // A grate stood up over the glow, in a frame: a lintel and two jambs.
                Add(new GrateMesh { Size = new Vector3(w, 0.12f, h), Spacing = 0.35f, Bar = 0.07f }, looks.Iron,
                    at + new Vector3(0f, h * 0.5f, 0f), new Basis(Vector3.Right, -Mathf.Pi * 0.5f));
                Add(new BlockMesh { Size = new Vector3(w + 1f, 0.6f, 0.7f), Bevel = 0.06f }, looks.Iron, at + new Vector3(0f, h + 0.3f, 0.1f));
                Add(new BlockMesh { Size = new Vector3(0.5f, h, 0.7f), Bevel = 0.05f }, looks.Iron, at + new Vector3(-w * 0.5f - 0.25f, h * 0.5f, 0.1f));
                Add(new BlockMesh { Size = new Vector3(0.5f, h, 0.7f), Bevel = 0.05f }, looks.Iron, at + new Vector3(w * 0.5f + 0.25f, h * 0.5f, 0.1f));
                break;
            case WallPiece.Pipe:
                Add(new PipeMesh { Points = new[] { Vector3.Zero, new Vector3(0f, h, 0f) }, Radius = w, FlangeSpacing = 2.5f }, looks.Iron, at);
                break;
            case WallPiece.Chain:
                Add(new CableMesh { End = new Vector3(0f, -h, 0f), Sag = 0f, Radius = w, Strands = 1, Seed = Seed + index }, looks.Iron, at, shadow: false);
                Add(new BlockMesh { Size = new Vector3(0.45f, 0.6f, 0.45f), Bevel = 0.08f }, looks.Iron, at + new Vector3(0f, -h - 0.3f, 0f));
                break;
            case WallPiece.Panel:
                Add(new BlockMesh { Size = new Vector3(w, h, 0.25f), Bevel = 0.08f }, looks.Plate, at);
                break;
            case WallPiece.Slit:
                Add(new QuadMesh { Size = new Vector2(w, h) }, looks.Glow, at, shadow: false);
                break;
            case WallPiece.Piston:
                Add(new ColumnMesh { Height = h, Radius = w, Sides = 10, Shafts = 1, Plinth = 0.6f, Capital = 0.5f }, looks.Plate, at);
                break;
            case WallPiece.Wheel:
                Add(new RingMesh { RadiusX = w, RadiusZ = w, Height = 0.45f, Thickness = w * 0.12f, Segments = 48 }, looks.Plate, at,
                    new Basis(Vector3.Right, Mathf.Pi * 0.5f));
                for (int s = 0; s < 6; s++)
                {
                    float a = p.Roll + Mathf.Pi * s / 6f;
                    Add(new BlockMesh { Size = new Vector3(w * 2f * 0.94f, 0.22f, 0.22f), Bevel = 0.03f }, looks.Plate, at, new Basis(Vector3.Back, a));
                }

                break;
            case WallPiece.Pier:
                Add(new ColumnMesh { Height = h, Radius = w * 0.4f, Sides = 8, Shafts = 1, Plinth = 0.5f, Capital = 0.4f }, looks.Stone, at);
                break;
            case WallPiece.Shelf:
                Add(new BlockMesh { Size = new Vector3(w, h, 1f), Bevel = 0.03f }, looks.Iron, at, shadow: false);
                break;
            case WallPiece.Icicles:
                Add(new IcicleMesh { Width = w, Length = h, Count = Mathf.Max(3, (int)(w * 1.6f)), Seed = Seed + index }, looks.Ice, at, shadow: false);
                break;
            case WallPiece.Rack:
                int variant = Mathf.Clamp((int)p.Roll, 0, _racks.Length - 1);
                // Stacks, not full racks: a wall holds hundreds, far back in the fog (racks cost a millisecond on the Sounding's wall).
                _racks[variant] ??= new StackMesh { Size = new Vector3(w, h, 0.9f), Shelf = 0.45f + 0.12f * variant };
                Add(_racks[variant], looks.Plate, at, shadow: false); // hundreds against the wall: their shadows cost more than they show
                break;
            case WallPiece.Plinth:
                Add(Shared(p.Piece.ToString(), p.Size, () => new BlockMesh { Size = new Vector3(w, h, 0.8f), Bevel = 0.08f }), looks.Stone, at + new Vector3(0f, h * 0.5f, 0f));
                break;
            case WallPiece.Shaft:
                // A pilaster with a fillet down its face.
                Add(Shared(p.Piece.ToString(), p.Size, () => new BlockMesh { Size = new Vector3(w, h, 0.5f), Bevel = Mathf.Min(0.06f, w * 0.2f) }), looks.Stone, at + new Vector3(0f, h * 0.5f, 0f));
                if (w > 0.4f)
                {
                    Add(Shared("fillet", p.Size, () => new BlockMesh { Size = new Vector3(w * 0.4f, h, 0.2f), Bevel = 0.04f }), looks.Stone,
                        at + new Vector3(0f, h * 0.5f, 0.3f));
                }

                break;
            case WallPiece.Capital:
                Add(Shared(p.Piece.ToString(), p.Size, () => new BlockMesh { Size = new Vector3(w, h, 0.8f), Bevel = 0.06f }), looks.Stone, at);
                break;
            case WallPiece.Arch:
                Add(Shared(p.Piece.ToString(), p.Size, () => new ArchMesh { Span = w, Rise = h, Thickness = Mathf.Min(0.45f, w * 0.12f), Depth = 0.45f, Segments = 24 }), looks.Stone, at);
                break;
            case WallPiece.Recess:
                Add(Shared(p.Piece.ToString(), p.Size, () => new BlockMesh { Size = new Vector3(w, h, 0.06f), Bevel = 0.01f }), looks.Recess, at, shadow: false);
                break;
            case WallPiece.Course:
                Add(Shared(p.Piece.ToString(), p.Size, () => new BlockMesh { Size = new Vector3(w, h, 0.7f), Bevel = 0.08f }), looks.Stone, at);
                break;
            case WallPiece.Rose:
                // The rose's shadowed back: a ring as wide as its radius is a disc.
                Add(Shared("rose back", p.Size, () => new RingMesh { RadiusX = w * 0.5f, RadiusZ = w * 0.5f, Height = 0.06f, Thickness = w, Segments = 32 }), looks.Recess, at,
                    new Basis(Vector3.Right, Mathf.Pi * 0.5f), shadow: false);
                Add(Shared(p.Piece.ToString(), p.Size, () => new RingMesh { RadiusX = w, RadiusZ = w, Height = 0.35f, Thickness = w * 0.16f, Segments = 32 }), looks.Stone, at,
                    new Basis(Vector3.Right, Mathf.Pi * 0.5f));
                for (int s = 0; s < 4; s++)
                {
                    Add(Shared("tracery", p.Size, () => new BlockMesh { Size = new Vector3(w * 2f * 0.9f, 0.1f, 0.2f), Bevel = 0.02f }),
                        looks.Stone, at, new Basis(Vector3.Back, p.Roll + Mathf.Pi * s / 4f));
                }

                break;
            case WallPiece.Hub:
                Add(new ColumnMesh { Height = 0.7f, Radius = w, Sides = 12, Shafts = 1, Plinth = 0.15f, Capital = 0.15f }, looks.Plate, at,
                    new Basis(Vector3.Right, Mathf.Pi * 0.5f));
                break;
        }
    }

    /// <summary>The mesh of this kind and size, built the first time it is asked for.</summary>
    private Mesh Shared(string kind, System.Numerics.Vector2 size, System.Func<Mesh> build)
    {
        if (!_shared.TryGetValue((kind, size), out Mesh? mesh))
        {
            mesh = build();
            _shared[(kind, size)] = mesh;
        }

        return mesh;
    }

    private void Add(Mesh mesh, Material material, Vector3 position, Basis? basis = null, bool shadow = true)
    {
        AddChild(new MeshInstance3D
        {
            Mesh = mesh,
            MaterialOverride = material,
            Transform = new Transform3D(basis ?? Basis.Identity, position),
            CastShadow = shadow ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }
}
