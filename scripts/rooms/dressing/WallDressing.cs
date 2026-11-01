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

    /// <summary>Masonry (an archive's piers); stone_mid.tres when unset.</summary>
    [Export] public Material? Stone { get; set; }

    /// <summary>Ice (icicles under shelves); ice_mid.tres when unset.</summary>
    [Export] public Material? Ice { get; set; }

    /// <summary>The racks an archive's shelves carry: three variants, built once per wall.</summary>
    private readonly Mesh[] _racks = new Mesh[3];

    public override void _Ready()
    {
        Iron ??= GD.Load<Material>("res://assets/materials/rust_mid.tres");
        Plate ??= GD.Load<Material>(Style == WallStyle.Machinery ? "res://assets/materials/backdrop_metal.tres" : "res://assets/materials/metal_mid.tres");
        Molten ??= GD.Load<Material>("res://assets/materials/furnace_mouth.tres");
        Glow ??= GD.Load<Material>("res://assets/materials/glow_strip.tres");
        Stone ??= GD.Load<Material>("res://assets/materials/stone_mid.tres");
        Ice ??= GD.Load<Material>("res://assets/materials/ice_mid.tres");
        int i = 0;
        foreach (WallPlacement p in WallLayout.Build(Style, Size.X, Size.Y, Seed))
        {
            Build(p, i++);
        }
    }

    private void Build(WallPlacement p, int index)
    {
        Vector3 at = new(p.Position.X, p.Position.Y, p.Position.Z);
        float w = p.Size.X, h = p.Size.Y;
        switch (p.Piece)
        {
            case WallPiece.Pilaster:
                Add(new BlockMesh { Size = new Vector3(w, h, 0.7f), Bevel = 0.06f }, Iron!, at + new Vector3(0f, h * 0.5f, 0f));
                Add(new BlockMesh { Size = new Vector3(w * 0.45f, h, 0.25f), Bevel = 0.04f }, Iron!, at + new Vector3(0f, h * 0.5f, 0.45f));
                break;
            case WallPiece.Girder:
                Add(new BlockMesh { Size = new Vector3(w, h, 0.5f), Bevel = 0.05f }, Iron!, at);
                Add(new BlockMesh { Size = new Vector3(w, h * 0.3f, 0.7f), Bevel = 0.03f }, Iron!, at + new Vector3(0f, h * 0.5f, 0f));
                break;
            case WallPiece.MouthGlow:
                Add(new QuadMesh { Size = new Vector2(w, h) }, Molten!, at + new Vector3(0f, h * 0.5f, 0f), shadow: false);
                break;
            case WallPiece.Mouth:
                // A grate stood up over the glow, in a frame: a lintel and two jambs.
                Add(new GrateMesh { Size = new Vector3(w, 0.12f, h), Spacing = 0.35f, Bar = 0.07f }, Iron!,
                    at + new Vector3(0f, h * 0.5f, 0f), new Basis(Vector3.Right, -Mathf.Pi * 0.5f));
                Add(new BlockMesh { Size = new Vector3(w + 1f, 0.6f, 0.7f), Bevel = 0.06f }, Iron!, at + new Vector3(0f, h + 0.3f, 0.1f));
                Add(new BlockMesh { Size = new Vector3(0.5f, h, 0.7f), Bevel = 0.05f }, Iron!, at + new Vector3(-w * 0.5f - 0.25f, h * 0.5f, 0.1f));
                Add(new BlockMesh { Size = new Vector3(0.5f, h, 0.7f), Bevel = 0.05f }, Iron!, at + new Vector3(w * 0.5f + 0.25f, h * 0.5f, 0.1f));
                break;
            case WallPiece.Pipe:
                Add(new PipeMesh { Points = new[] { Vector3.Zero, new Vector3(0f, h, 0f) }, Radius = w, FlangeSpacing = 2.5f }, Iron!, at);
                break;
            case WallPiece.Chain:
                Add(new CableMesh { End = new Vector3(0f, -h, 0f), Sag = 0f, Radius = w, Strands = 1, Seed = Seed + index }, Iron!, at, shadow: false);
                Add(new BlockMesh { Size = new Vector3(0.45f, 0.6f, 0.45f), Bevel = 0.08f }, Iron!, at + new Vector3(0f, -h - 0.3f, 0f));
                break;
            case WallPiece.Panel:
                Add(new BlockMesh { Size = new Vector3(w, h, 0.25f), Bevel = 0.08f }, Plate!, at);
                break;
            case WallPiece.Slit:
                Add(new QuadMesh { Size = new Vector2(w, h) }, Glow!, at, shadow: false);
                break;
            case WallPiece.Piston:
                Add(new ColumnMesh { Height = h, Radius = w, Sides = 10, Shafts = 1, Plinth = 0.6f, Capital = 0.5f }, Plate!, at);
                break;
            case WallPiece.Wheel:
                Add(new RingMesh { RadiusX = w, RadiusZ = w, Height = 0.45f, Thickness = w * 0.12f, Segments = 48 }, Plate!, at,
                    new Basis(Vector3.Right, Mathf.Pi * 0.5f));
                for (int s = 0; s < 6; s++)
                {
                    float a = p.Roll + Mathf.Pi * s / 6f;
                    Add(new BlockMesh { Size = new Vector3(w * 2f * 0.94f, 0.22f, 0.22f), Bevel = 0.03f }, Plate!, at, new Basis(Vector3.Back, a));
                }

                break;
            case WallPiece.Pier:
                Add(new ColumnMesh { Height = h, Radius = w * 0.4f, Sides = 8, Shafts = 1, Plinth = 0.5f, Capital = 0.4f }, Stone!, at);
                break;
            case WallPiece.Shelf:
                Add(new BlockMesh { Size = new Vector3(w, h, 1f), Bevel = 0.03f }, Iron!, at, shadow: false);
                break;
            case WallPiece.Icicles:
                Add(new IcicleMesh { Width = w, Length = h, Count = Mathf.Max(3, (int)(w * 1.6f)), Seed = Seed + index }, Ice!, at, shadow: false);
                break;
            case WallPiece.Rack:
                int variant = Mathf.Clamp((int)p.Roll, 0, _racks.Length - 1);
                // Stacks, not full racks: a wall holds hundreds, far back in the fog (racks cost a millisecond on the Sounding's wall).
                _racks[variant] ??= new StackMesh { Size = new Vector3(w, h, 0.9f), Shelf = 0.45f + 0.12f * variant };
                Add(_racks[variant], Plate!, at, shadow: false); // hundreds against the wall: their shadows cost more than they show
                break;
            case WallPiece.Hub:
                Add(new ColumnMesh { Height = 0.7f, Radius = w, Sides = 12, Shafts = 1, Plinth = 0.15f, Capital = 0.15f }, Plate!, at,
                    new Basis(Vector3.Right, Mathf.Pi * 0.5f));
                break;
        }
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
