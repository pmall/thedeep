using Godot;

namespace TheDeep;

/// <summary>
/// Dresses a room's back wall (behind the play plane) in a <see cref="WallStyle"/>, laid out by <see cref="WallLayout"/>:
/// the node stands at the middle of the wall's foot, on its face; <see cref="Size"/> is the area dressed (X along the
/// wall, Y up). Visual only, built when ready from kit meshes and swappable materials, deterministic for a <see cref="Seed"/>.
/// Pieces stay within <see cref="Size"/> and about a meter out from the wall, so keep the node a meter behind the blocks'
/// back faces (Z −3 against blocks 4 deep). A machinery wall casts no shadows: its works stand in the haze as midground, and
/// their hard shadows on the wall just behind them flattened them into cut-outs (`docs/art-direction.md#midground`).
/// </summary>
public partial class WallDressing : Node3D
{
    [Export] public WallStyle Style { get; set; }

    [Export] public Vector2 Size { get; set; } = new(20f, 10f);

    [Export] public int Seed { get; set; } = 1;

    /// <summary>The materials the wall is built with (a <see cref="WallPalette"/>); the room must set it.</summary>
    [Export] public WallPalette Palette { get; set; } = null!;

    /// <summary>A nave's share of bays with icicles under their courses (0 to 1): a chapel of the frozen layer.</summary>
    [Export(PropertyHint.Range, "0,1,0.05")] public float Frost { get; set; }

    /// <summary>A nave's pieces repeat at a handful of sizes: each built once per wall.</summary>
    private readonly System.Collections.Generic.Dictionary<(string, System.Numerics.Vector2), Mesh> _shared = new();

    private static readonly FoundryWallSpec Foundry = FoundryWallSpec.Default;

    private static readonly MachineryWallSpec Machinery = MachineryWallSpec.Default;

    private static readonly ArchiveWallSpec Archive = ArchiveWallSpec.Default;

    private static readonly NaveWallSpec Nave = NaveWallSpec.Default;

    /// <summary>The racks an archive's shelves carry, one mesh per look (<see cref="ArchiveWallSpec.RackVariants"/>), built once per wall.</summary>
    private readonly Mesh[] _racks = new Mesh[Archive.RackVariants];

    public override void _Ready()
    {
        WallPalette looks = Palette;
        int i = 0;
        foreach (WallPlacement p in WallLayout.Build(Style, Size.X, Size.Y, Seed, Frost))
        {
            Build(p, i++, looks);
        }
    }

    private void Build(WallPlacement p, int index, WallPalette looks)
    {
        Vector3 at = p.Position.ToGodot();
        float w = p.Size.X, h = p.Size.Y;
        switch (p.Piece)
        {
            case WallPiece.Pilaster:
                Add(new BlockMesh { Size = new Vector3(w, h, Foundry.PilasterMeshDepth), Bevel = Foundry.PilasterBevel }, looks.Iron, at + new Vector3(0f, h * 0.5f, 0f));
                Add(new BlockMesh { Size = new Vector3(w * Foundry.PilasterRibShare, h, Foundry.PilasterRibDepth), Bevel = Foundry.PilasterRibBevel }, looks.Iron,
                    at + new Vector3(0f, h * 0.5f, Foundry.PilasterRibOffset));
                break;
            case WallPiece.Girder:
                Add(new BlockMesh { Size = new Vector3(w, h, Foundry.GirderMeshDepth), Bevel = Foundry.GirderBevel }, looks.Iron, at);
                Add(new BlockMesh { Size = new Vector3(w, h * Foundry.GirderLipShare, Foundry.GirderLipDepth), Bevel = Foundry.GirderLipBevel }, looks.Iron,
                    at + new Vector3(0f, h * 0.5f, 0f));
                break;
            case WallPiece.MouthGlow:
                Add(new QuadMesh { Size = new Vector2(w, h) }, looks.Molten, at + new Vector3(0f, h * 0.5f, 0f), shadow: false);
                break;
            case WallPiece.Mouth:
                // A grate stood up over the glow, in a frame: a lintel and two jambs.
                Add(new GrateMesh { Size = new Vector3(w, Foundry.GrateThickness, h), Spacing = Foundry.GrateSpacing, Bar = Foundry.GrateBar }, looks.Iron,
                    at + new Vector3(0f, h * 0.5f, 0f), new Basis(Vector3.Right, -Mathf.Pi * 0.5f));
                Add(new BlockMesh { Size = new Vector3(w + Foundry.LintelOverhang, Foundry.LintelHeight, Foundry.FrameDepth), Bevel = Foundry.LintelBevel }, looks.Iron,
                    at + new Vector3(0f, h + Foundry.LintelHeight * 0.5f, Foundry.FrameOffset));
                Add(new BlockMesh { Size = new Vector3(Foundry.JambWidth, h, Foundry.FrameDepth), Bevel = Foundry.JambBevel }, looks.Iron,
                    at + new Vector3(-w * 0.5f - Foundry.JambWidth * 0.5f, h * 0.5f, Foundry.FrameOffset));
                Add(new BlockMesh { Size = new Vector3(Foundry.JambWidth, h, Foundry.FrameDepth), Bevel = Foundry.JambBevel }, looks.Iron,
                    at + new Vector3(w * 0.5f + Foundry.JambWidth * 0.5f, h * 0.5f, Foundry.FrameOffset));
                break;
            case WallPiece.Pipe:
                Add(new PipeMesh { Points = new[] { Vector3.Zero, new Vector3(0f, h, 0f) }, Radius = w, FlangeSpacing = Foundry.PipeFlangeSpacing }, looks.Iron, at);
                break;
            case WallPiece.Chain:
                Add(new CableMesh { End = new Vector3(0f, -h, 0f), Sag = 0f, Radius = w, Strands = 1, Seed = Seed + index }, looks.Iron, at, shadow: false);
                Add(new BlockMesh { Size = new Vector3(Foundry.ChainWeightWidth, Foundry.ChainWeightHeight, Foundry.ChainWeightWidth), Bevel = Foundry.ChainWeightBevel }, looks.Iron,
                    at + new Vector3(0f, -h - Foundry.ChainWeightHeight * 0.5f, 0f));
                break;
            case WallPiece.Bank:
                Add(new BankMesh { Size = new Vector3(w, h, Machinery.BankDepth), Chest = MachineryWall.BankChest(h), Seed = Seed + index }, looks.Plate,
                    at + new Vector3(0f, 0f, Machinery.BankDepth * 0.5f));
                break;
            case WallPiece.Slit:
                Add(new QuadMesh { Size = new Vector2(w, h) }, looks.Glow, at);
                break;
            case WallPiece.Piston:
                Add(new ColumnMesh { Height = h, Radius = w, Sides = Machinery.PistonSides, Shafts = 1, Plinth = Machinery.PistonPlinth, Capital = Machinery.PistonCapital }, looks.Plate, at);
                break;
            case WallPiece.Gear:
                Add(new CogMesh { Radius = w, Teeth = MachineryWall.GearTeeth(w), Depth = Machinery.GearThickness }, looks.Iron, at, new Basis(Vector3.Back, p.Roll));
                break;
            case WallPiece.Pier:
                Add(new ColumnMesh { Height = h, Radius = w * Archive.PierRadiusShare, Sides = Archive.PierSides, Shafts = 1, Plinth = Archive.PierPlinth, Capital = Archive.PierCapital }, looks.Stone, at);
                break;
            case WallPiece.Shelf:
                Add(new BlockMesh { Size = new Vector3(w, h, Archive.ShelfMeshDepth), Bevel = Archive.ShelfBevel }, looks.Iron, at, shadow: false);
                break;
            case WallPiece.Icicles:
                Add(new IcicleMesh { Width = w, Length = h, Count = Mathf.Max(IcicleSpec.Default.MinCount, (int)(w * IcicleSpec.Default.PerMeter)), Seed = Seed + index }, looks.Ice, at, shadow: false);
                break;
            case WallPiece.Rack:
                int variant = Mathf.Clamp(p.Variant, 0, _racks.Length - 1);
                // Stacks, not full racks: a wall holds hundreds, far back in the fog (racks cost a millisecond on the Sounding's wall).
                _racks[variant] ??= new StackMesh { Size = new Vector3(w, h, Archive.RackMeshDepth), Shelf = Archive.RackShelf + Archive.RackShelfStep * variant };
                Add(_racks[variant], looks.Plate, at, shadow: false); // hundreds against the wall: their shadows cost more than they show
                break;
            case WallPiece.Plinth:
                Add(Shared(p.Piece.ToString(), p.Size, () => new BlockMesh { Size = new Vector3(w, h, Nave.PlinthDepth * 2f), Bevel = Nave.PlinthBevel }), looks.Stone, at + new Vector3(0f, h * 0.5f, 0f));
                break;
            case WallPiece.Shaft:
                // A pilaster with a fillet down its face.
                Add(Shared(p.Piece.ToString(), p.Size, () => new BlockMesh { Size = new Vector3(w, h, Nave.ShaftDepth * 2f), Bevel = Mathf.Min(Nave.ShaftBevel, w * Nave.ShaftBevelShare) }), looks.Stone,
                    at + new Vector3(0f, h * 0.5f, 0f));
                if (w > Nave.FilletMinWidth)
                {
                    Add(Shared("fillet", p.Size, () => new BlockMesh { Size = new Vector3(w * Nave.FilletShare, h, Nave.FilletDepth), Bevel = Nave.FilletBevel }), looks.Stone,
                        at + new Vector3(0f, h * 0.5f, Nave.FilletOffset));
                }

                break;
            case WallPiece.Capital:
                Add(Shared(p.Piece.ToString(), p.Size, () => new BlockMesh { Size = new Vector3(w, h, Nave.CapitalDepth * 2f), Bevel = Nave.CapitalBevel }), looks.Stone, at);
                break;
            case WallPiece.Arch:
                Add(Shared(p.Piece.ToString(), p.Size, () => new ArchMesh { Span = w, Rise = h, Thickness = Mathf.Min(Nave.ArchRing, w * Nave.ArchRingShare), Depth = Nave.ArchMeshDepth, Segments = Nave.ArchSegments }), looks.Stone, at);
                break;
            case WallPiece.Recess:
                Add(Shared(p.Piece.ToString(), p.Size, () => new BlockMesh { Size = new Vector3(w, h, Nave.RecessMeshDepth), Bevel = Nave.RecessBevel }), looks.Recess, at, shadow: false);
                break;
            case WallPiece.Course:
                Add(Shared(p.Piece.ToString(), p.Size, () => new BlockMesh { Size = new Vector3(w, h, Nave.CourseDepth * 2f), Bevel = Nave.CourseBevel }), looks.Stone, at);
                break;
            case WallPiece.Rose:
                // The rose's shadowed back: a ring as wide as its radius is a disc.
                Add(Shared("rose back", p.Size, () => new RingMesh { RadiusX = w * 0.5f, RadiusZ = w * 0.5f, Height = Nave.RoseBackHeight, Thickness = w, Segments = Nave.RoseSegments }), looks.Recess, at,
                    new Basis(Vector3.Right, Mathf.Pi * 0.5f), shadow: false);
                Add(Shared(p.Piece.ToString(), p.Size, () => new RingMesh { RadiusX = w, RadiusZ = w, Height = Nave.RoseRingHeight, Thickness = w * Nave.RoseRingShare, Segments = Nave.RoseSegments }), looks.Stone, at,
                    new Basis(Vector3.Right, Mathf.Pi * 0.5f));
                for (int s = 0; s < Nave.TraceryCount; s++)
                {
                    Add(Shared("tracery", p.Size, () => new BlockMesh { Size = new Vector3(w * 2f * Nave.TraceryWidthShare, Nave.TraceryThickness, Nave.TraceryDepth), Bevel = Nave.TraceryBevel }),
                        looks.Stone, at, new Basis(Vector3.Back, p.Roll + Mathf.Pi * s / Nave.TraceryCount));
                }

                break;
            case WallPiece.Hub:
                Add(new ColumnMesh { Height = Machinery.HubLength, Radius = w, Sides = Machinery.HubSides, Shafts = 1, Plinth = Machinery.HubPlinth, Capital = Machinery.HubCapital }, looks.Iron, at,
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
            CastShadow = shadow && Style != WallStyle.Machinery ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }
}
