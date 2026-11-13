using System;
using System.Collections.Generic;
using Godot;
using NVector3 = System.Numerics.Vector3;

namespace TheDeep;

/// <summary>
/// The architecture behind a room (built by <see cref="RoomStage"/>): rows of structures at a few depths behind the play
/// plane (<see cref="Rows"/>, <see cref="BackdropRowSpec.Defaults"/> unless set), laid out by <see cref="BackdropLayout"/>
/// in the depth layer's <see cref="BackdropStyle"/>, so the haze has naves, stacks, foundries or machinery in it that recede
/// with real parallax. Each row sinks into the layer's murk by its depth (<see cref="BackdropVeil"/>, as the instance
/// uniform <c>veil</c> of architecture.gdshader), darker and hazier than the play plane, and fills the view at its depth
/// (<see cref="ViewDistance"/>) so its ends never show. Each row draws each kind of piece as one <see cref="MultiMesh"/>: a
/// handful of draw calls per row. Visual only (no collision, no shadows); the pieces are kit meshes and the materials
/// exported, so both stay swappable. Deterministic for a given <see cref="Seed"/>.
/// </summary>
public partial class BackdropArchitecture : Node3D
{
    private static readonly StringName VeilParameter = "veil";

    [Export] public BackdropStyle Style { get; set; }

    /// <summary>Masonry (naves).</summary>
    [Export] public Material Stone { get; set; } = null!;

    /// <summary>Iron and plating (stacks, foundries, machinery).</summary>
    [Export] public Material Metal { get; set; } = null!;

    /// <summary>The furnace mouths' light, in the layer's molten color.</summary>
    [Export] public Material Glow { get; set; } = null!;

    /// <summary>Icicles under the stacks' walkways.</summary>
    [Export] public Material Ice { get; set; } = null!;

    /// <summary>The play area the architecture surrounds (the room's camera bounds).</summary>
    [Export] public Vector2 BoundsMin { get; set; }

    [Export] public Vector2 BoundsMax { get; set; }

    /// <summary>The rows, nearest first; empty for <see cref="BackdropRowSpec.Defaults"/>.</summary>
    [Export] public BackdropRowData[] Rows { get; set; } = [];

    /// <summary>Leaves the nearest row out (a room with little space behind it).</summary>
    [Export] public bool OmitNearest { get; set; }

    /// <summary>How far in front of the play plane a camera may stand and still see the rows fill its view, meters: the
    /// widest look-dev shot, past the gameplay camera's <see cref="RoomStage.CameraDistance"/>.</summary>
    [Export] public float ViewDistance { get; set; } = 90f;

    [Export] public int Seed { get; set; } = 1;

    /// <summary>Half the vertical view's height per meter from the camera: tan(15°), for the 30° vertical FOV.</summary>
    private const float ViewHalfHeightPerMeter = 0.27f;

    public override void _Ready()
    {
        List<BackdropRowSpec> specs = Specs();
        float farthest = 0f;
        foreach (BackdropRowSpec spec in specs)
        {
            farthest = Mathf.Max(farthest, spec.Depth);
        }

        for (int i = OmitNearest && specs.Count > 1 ? 1 : 0; i < specs.Count; i++)
        {
            BackdropRowSpec spec = specs[i];
            BackdropRow row = spec.Behind(BoundsMin.ToCore(), BoundsMax.ToCore(), ViewDistance, RoomStage.ViewHalfWidthPerMeter, ViewHalfHeightPerMeter, Seed * 31 + i * 7);
            BuildRow(row with { Glows = spec.Depth >= farthest }, spec.Depth, i);
        }
    }

    private List<BackdropRowSpec> Specs()
    {
        var specs = new List<BackdropRowSpec>();
        foreach (BackdropRowData data in Rows)
        {
            specs.Add(data.ToCore());
        }

        return specs.Count > 0 ? specs : new List<BackdropRowSpec>(BackdropRowSpec.Defaults);
    }

    private void BuildRow(BackdropRow row, float depth, int index)
    {
        // Grouped by piece, and the pieces built at their length by that length too (their ends keep their shape).
        var groups = new Dictionary<(BackdropPiece, int), List<BackdropPlacement>>();
        var dims = BackdropDims.For(row);
        float step = dims.LengthStep;
        foreach (BackdropPlacement p in BackdropLayout.Build(Style, row))
        {
            int length = Stretched(p.Piece) ? 0 : Mathf.Max(1, Mathf.RoundToInt(p.Length / step));
            if (!groups.TryGetValue((p.Piece, length), out List<BackdropPlacement>? list))
            {
                groups[(p.Piece, length)] = list = new List<BackdropPlacement>();
            }

            list.Add(p);
        }

        float veil = BackdropVeil.Amount(depth);
        foreach (((BackdropPiece piece, int length), List<BackdropPlacement> placements) in groups)
        {
            var multimesh = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = MeshFor(piece, dims, row.Seed, length * step),
                InstanceCount = placements.Count,
            };
            for (int i = 0; i < placements.Count; i++)
            {
                multimesh.SetInstanceTransform(i, Place(placements[i]));
            }

            var instance = new MultiMeshInstance3D
            {
                Name = $"Row{index}{piece}{(length > 0 ? length : "")}",
                Multimesh = multimesh,
                MaterialOverride = MaterialFor(piece),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                GIMode = GeometryInstance3D.GIModeEnum.Disabled,
                Position = new Vector3(0f, 0f, -depth),
            };
            instance.SetInstanceShaderParameter(VeilParameter, veil);
            AddChild(instance);
        }
    }

    private static Transform3D Place(BackdropPlacement p)
    {
        Vector3 scale = p.Piece switch
        {
            BackdropPiece.Chain or BackdropPiece.Shaft => new Vector3(1f, p.Length, 1f),
            BackdropPiece.Chord or BackdropPiece.Brace or BackdropPiece.Spoke => new Vector3(p.Length, 1f, 1f),
            BackdropPiece.Ring => new Vector3(p.Length, 1f, p.Length),
            _ => Vector3.One,
        };
        Basis basis = new Basis(Vector3.Back, p.RollZ) * new Basis(Vector3.Right, p.PitchX) * Basis.FromScale(scale);
        NVector3 o = p.Position;
        return new Transform3D(basis, o.ToGodot());
    }

    /// <summary>Pieces whose <see cref="BackdropPlacement.Length"/> stretches them; the others with a length are built at it.</summary>
    private static bool Stretched(BackdropPiece piece) => piece is not (BackdropPiece.Stack or BackdropPiece.Furnace or BackdropPiece.Duct or BackdropPiece.Piston);

    private Material MaterialFor(BackdropPiece piece) => piece switch
    {
        BackdropPiece.Pier or BackdropPiece.UpperPier or BackdropPiece.PointedArch or BackdropPiece.UpperArch or BackdropPiece.Course
            or BackdropPiece.Shaft => Stone,
        BackdropPiece.Icicles => Ice,
        BackdropPiece.FurnaceMouth => Glow,
        BackdropPiece.Tower or BackdropPiece.Walkway or BackdropPiece.Furnace or BackdropPiece.Hood or BackdropPiece.Stack
            or BackdropPiece.Duct or BackdropPiece.Tank or BackdropPiece.Chord or BackdropPiece.Brace or BackdropPiece.Chain
            or BackdropPiece.Ring or BackdropPiece.Hub or BackdropPiece.Spoke or BackdropPiece.Piston => Metal,
        _ => throw new ArgumentOutOfRangeException(nameof(piece), piece, "no material for this backdrop piece"),
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
            BackdropPiece.Shaft => new BlockMesh { Size = new Vector3(d.ShaftSection, 1f, d.ShaftSection), Bevel = 0f },
            BackdropPiece.Tower => new StackMesh { Size = new Vector3(d.TowerWidth, d.TowerHeight, 2.4f * s), Shelf = 1.2f * s },
            BackdropPiece.Walkway => new BlockMesh { Size = new Vector3(d.Pitch - d.TowerWidth + 0.4f * s, d.WalkwayHeight, d.WalkwayDepth), Bevel = 0.06f * s },
            BackdropPiece.Icicles => new IcicleMesh { Width = (d.Pitch - d.TowerWidth) * 0.9f, Length = 1.8f * s, Count = 9, Seed = seed },
            BackdropPiece.Furnace => new BlockMesh { Size = new Vector3(d.FurnaceWidth, length, d.FurnaceDepth), Bevel = 0.2f * s },
            BackdropPiece.Hood => new BlockMesh { Size = new Vector3(d.HoodWidth, d.HoodHeight, d.FurnaceDepth * 0.7f), Bevel = 0.15f * s },
            BackdropPiece.FurnaceMouth => new BlockMesh { Size = new Vector3(d.MouthWidth, d.MouthHeight, 0.3f * s), Bevel = 0.05f * s },
            BackdropPiece.Stack => new PipeMesh { Points = [Vector3.Zero, new Vector3(0f, length, 0f)], Radius = d.StackRadius, FlangeSpacing = 3.5f * s },
            BackdropPiece.Duct => new PipeMesh { Points = [new Vector3(-0.5f * length, 0f, 0f), new Vector3(0.5f * length, 0f, 0f)], Radius = d.DuctRadius, FlangeSpacing = 2.5f * s },
            BackdropPiece.Tank => new PipeMesh { Points = [new Vector3(-0.5f * d.TankLength, 0f, 0f), new Vector3(0.5f * d.TankLength, 0f, 0f)], Radius = d.TankRadius, FlangeSpacing = 1.6f * s },
            BackdropPiece.Chord => new BlockMesh { Size = new Vector3(1f, d.BeamSection, d.BeamSection), Bevel = 0.02f },
            BackdropPiece.Brace => new BlockMesh { Size = new Vector3(1f, d.BeamSection * 0.7f, d.BeamSection * 0.7f), Bevel = 0.02f },
            BackdropPiece.Chain => new CableMesh { End = new Vector3(0f, -1f, 0f), Sag = 0f, Radius = 0.08f * s, Strands = 1, Seed = seed },
            BackdropPiece.Ring => new RingMesh { RadiusX = 1f, RadiusZ = 1f, Height = 0.7f * s, Thickness = 0.07f, Segments = 48 },
            BackdropPiece.Hub => new ColumnMesh { Height = 1.2f * s, Radius = 1.1f * s, Sides = 12, Shafts = 1, Plinth = 0.3f * s, Capital = 0.3f * s },
            BackdropPiece.Spoke => new BlockMesh { Size = new Vector3(1f, 0.4f * s, 0.4f * s), Bevel = 0.03f },
            BackdropPiece.Piston => new ColumnMesh { Height = length, Radius = d.PistonRadius, Sides = 10, Shafts = 1, Plinth = 0.8f * s, Capital = 0.5f * s },
            _ => throw new ArgumentOutOfRangeException(nameof(piece), piece, "no mesh for this backdrop piece"),
        };
    }
}
