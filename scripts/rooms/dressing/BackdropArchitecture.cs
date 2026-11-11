using System.Collections.Generic;
using Godot;
using NVector3 = System.Numerics.Vector3;

namespace TheDeep;

/// <summary>
/// The architecture behind a room (built by <see cref="RoomStage"/>): rows of structures at a few depths behind the play
/// plane, laid out by <see cref="BackdropLayout"/> in the depth layer's <see cref="BackdropStyle"/>, so the haze has naves,
/// stacks, foundries or machinery in it that recede with real parallax. Each row draws each kind of piece as one
/// <see cref="MultiMesh"/>: a handful of draw calls per row. Visual only (no collision, no shadows); the pieces are kit meshes
/// and the materials exported, so both stay swappable. Deterministic for a given <see cref="Seed"/>.
/// </summary>
public partial class BackdropArchitecture : Node3D
{
    /// <summary>The rows' depths behind the play plane, meters, nearest first; pitch and scale grow with them.</summary>
    private static readonly float[] Depths = { 15f, 24f, 36f };
    private static readonly float[] Pitches = { 9f, 12f, 15f };
    private static readonly float[] Scales = { 1f, 1.3f, 1.7f };

    [Export] public BackdropStyle Style { get; set; }

    /// <summary>Masonry (naves).</summary>
    [Export] public Material Stone { get; set; } = null!;

    /// <summary>Iron and plating (stacks, foundries, machinery).</summary>
    [Export] public Material Metal { get; set; } = null!;

    /// <summary>The sparse accent pieces (a nave's iron ribs).</summary>
    [Export] public Material Accent { get; set; } = null!;

    /// <summary>Icicles under the stacks' walkways.</summary>
    [Export] public Material Ice { get; set; } = null!;

    /// <summary>The play area the architecture surrounds (the room's camera bounds).</summary>
    [Export] public Vector2 BoundsMin { get; set; }

    [Export] public Vector2 BoundsMax { get; set; }

    /// <summary>How many rows, from the farthest (1 to 3).</summary>
    [Export] public int Rows { get; set; } = 3;

    [Export] public int Seed { get; set; } = 1;

    public override void _Ready()
    {
        int rows = Mathf.Clamp(Rows, 1, Depths.Length);
        for (int i = Depths.Length - rows; i < Depths.Length; i++)
        {
            float depth = Depths[i];
            float extra = depth * RoomStage.ViewHalfWidthPerMeter + 10f;
            float viewHeight = (RoomStage.CameraDistance + depth) * 0.27f; // tan(15°), half the vertical view
            var row = new BackdropRow(
                BoundsMin.X - extra, BoundsMax.X + extra,
                BoundsMin.Y - 6f - depth * 0.15f, BoundsMax.Y + viewHeight * 0.6f + 4f,
                Pitches[i], Scales[i], Seed * 31 + i * 7);
            BuildRow(row, depth, i);
        }
    }

    private void BuildRow(BackdropRow row, float depth, int index)
    {
        // Grouped by piece, and columns by their height too (built at their height, not stretched: their ends keep their shape).
        var groups = new Dictionary<(BackdropPiece, int), List<BackdropPlacement>>();
        float step = 2f * row.Scale;
        foreach (BackdropPlacement p in BackdropLayout.Build(Style, row))
        {
            int tall = Stretched(p.Piece) ? 0 : Mathf.Max(1, Mathf.RoundToInt(p.Length / step));
            if (!groups.TryGetValue((p.Piece, tall), out List<BackdropPlacement>? list))
            {
                groups[(p.Piece, tall)] = list = new List<BackdropPlacement>();
            }

            list.Add(p);
        }

        var dims = BackdropDims.For(row);
        foreach (((BackdropPiece piece, int tall), List<BackdropPlacement> placements) in groups)
        {
            var multimesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = MeshFor(piece, dims, row.Seed, tall * step),
                InstanceCount = placements.Count,
            };
            for (int i = 0; i < placements.Count; i++)
            {
                multimesh.SetInstanceTransform(i, Place(placements[i]));
            }

            AddChild(new MultiMeshInstance3D
            {
                Name = $"Row{index}{piece}{(tall > 0 ? tall : "")}",
                Multimesh = multimesh,
                MaterialOverride = MaterialFor(piece),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                GIMode = GeometryInstance3D.GIModeEnum.Disabled,
                Position = new Vector3(0f, 0f, -depth),
            });
        }
    }

    private static Transform3D Place(BackdropPlacement p)
    {
        Vector3 scale = p.Piece switch
        {
            BackdropPiece.Chain => new Vector3(1f, p.Length, 1f),
            BackdropPiece.Chord or BackdropPiece.Brace or BackdropPiece.Spoke => new Vector3(p.Length, 1f, 1f),
            BackdropPiece.Ring => new Vector3(p.Length, 1f, p.Length),
            _ => Vector3.One,
        };
        Basis basis = new Basis(Vector3.Back, p.RollZ) * new Basis(Vector3.Right, p.PitchX) * Basis.FromScale(scale);
        NVector3 o = p.Position;
        return new Transform3D(basis, o.ToGodot());
    }

    /// <summary>Pieces whose <see cref="BackdropPlacement.Length"/> stretches them; the others with a length are built at it.</summary>
    private static bool Stretched(BackdropPiece piece) => piece is not (BackdropPiece.Chimney or BackdropPiece.Piston);

    private Material MaterialFor(BackdropPiece piece) => piece switch
    {
        BackdropPiece.Rib => Accent,
        BackdropPiece.Icicles => Ice,
        BackdropPiece.Pier or BackdropPiece.UpperPier or BackdropPiece.PointedArch or BackdropPiece.UpperArch or BackdropPiece.Course => Stone,
        _ => Metal,
    };

    private static Mesh MeshFor(BackdropPiece piece, BackdropDims d, int seed, float length)
    {
        float s = d.Scale;
        return piece switch
        {
            BackdropPiece.Pier => new ColumnMesh { Height = d.PierHeight, Radius = d.PierRadius, Sides = 10, Shafts = 4, Plinth = 0.8f * s, Capital = 0.6f * s },
            BackdropPiece.UpperPier => new ColumnMesh { Height = d.UpperHeight, Radius = 0.6f * s, Sides = 8, Shafts = 1, Plinth = 0.35f * s, Capital = 0.4f * s },
            BackdropPiece.PointedArch => new ArchMesh { Span = d.Pitch, Rise = d.PointedRise, Thickness = d.ArchThickness, Depth = d.ArchDepth, Segments = 24 },
            BackdropPiece.UpperArch => new ArchMesh { Span = d.Pitch, Rise = d.UpperRise, Thickness = d.ArchThickness * 0.8f, Depth = d.ArchDepth * 0.9f, Segments = 20 },
            BackdropPiece.Course => new BlockMesh { Size = new Vector3(d.Pitch, d.CourseHeight, d.ArchDepth * 1.15f), Bevel = 0.1f * s },
            BackdropPiece.Rib => new ArchMesh { Span = d.Pitch * 0.9f, Rise = d.PointedRise * 0.94f, Thickness = 0.22f * s, Depth = 0.3f * s, Flange = 0.4f * s, Segments = 24 },
            BackdropPiece.Tower => new StackMesh { Size = new Vector3(d.TowerWidth, d.TowerHeight, 2.4f * s), Shelf = 1.2f * s },
            BackdropPiece.Walkway => new BlockMesh { Size = new Vector3(d.Pitch - d.TowerWidth + 0.4f * s, d.WalkwayHeight, d.WalkwayDepth), Bevel = 0.06f * s },
            BackdropPiece.Icicles => new IcicleMesh { Width = (d.Pitch - d.TowerWidth) * 0.9f, Length = 1.8f * s, Count = 9, Seed = seed },
            BackdropPiece.Chimney => new ColumnMesh { Height = length, Radius = d.ChimneyRadius, Sides = 12, Shafts = 1, Plinth = 1.2f * s, Capital = 0.7f * s },
            BackdropPiece.Chord => new BlockMesh { Size = new Vector3(1f, d.BeamSection, d.BeamSection), Bevel = 0.02f },
            BackdropPiece.Brace => new BlockMesh { Size = new Vector3(1f, d.BeamSection * 0.7f, d.BeamSection * 0.7f), Bevel = 0.02f },
            BackdropPiece.Chain => new CableMesh { End = new Vector3(0f, -1f, 0f), Sag = 0f, Radius = 0.08f * s, Strands = 1, Seed = seed },
            BackdropPiece.Ring => new RingMesh { RadiusX = 1f, RadiusZ = 1f, Height = 0.7f * s, Thickness = 0.07f, Segments = 48 },
            BackdropPiece.Hub => new ColumnMesh { Height = 1.2f * s, Radius = 1.1f * s, Sides = 12, Shafts = 1, Plinth = 0.3f * s, Capital = 0.3f * s },
            BackdropPiece.Spoke => new BlockMesh { Size = new Vector3(1f, 0.4f * s, 0.4f * s), Bevel = 0.03f },
            BackdropPiece.Piston => new ColumnMesh { Height = length, Radius = d.PistonRadius, Sides = 10, Shafts = 1, Plinth = 0.8f * s, Capital = 0.5f * s },
            _ => new BoxMesh(),
        };
    }
}
