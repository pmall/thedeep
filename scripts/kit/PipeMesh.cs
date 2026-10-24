using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>Kit piece: a pipe through <see cref="Points"/> (local, the first is usually the origin) with rounded bends and flanges.</summary>
[GlobalClass]
public partial class PipeMesh : KitMesh
{
    private Vector3[] _points = { Vector3.Zero, new(4f, 0f, 0f) };
    private float _radius = 0.18f;
    private float _bendRadius = 0.6f;
    private float _flangeSpacing = 2f;

    [Export] public Vector3[] Points { get => _points; set { _points = value; RequestUpdate(); } }
    [Export] public float Radius { get => _radius; set { _radius = value; RequestUpdate(); } }
    [Export] public float BendRadius { get => _bendRadius; set { _bendRadius = value; RequestUpdate(); } }

    /// <summary>Meters between flange collars; 0 for none.</summary>
    [Export] public float FlangeSpacing { get => _flangeSpacing; set { _flangeSpacing = value; RequestUpdate(); } }

    protected override void Build(MeshBuilder builder)
    {
        if (Points.Length >= 2)
        {
            KitShapes.Pipe(builder, Points.Select(ToNumerics).ToArray(), Radius, BendRadius, FlangeSpacing);
        }
    }
}
