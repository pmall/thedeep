using System;
using Godot;

namespace TheDeep;

/// <summary>
/// The crescent a swing draws (assets/shaders/slash.gdshader), as a swappable mesh resource: an arc in the
/// XY plane around the origin, from <see cref="StartAngle"/> to <see cref="EndAngle"/> (degrees from +X),
/// thick in the middle and tapering to points. UV.x runs along the arc, UV.y from the inner to the outer edge.
/// </summary>
[GlobalClass]
public partial class SlashMesh : PrimitiveMesh
{
    private float _radius = 1.15f;
    private float _thickness = 0.45f;
    private float _startAngle = 105f;
    private float _endAngle = -50f;
    private int _segments = 32;

    [Export] public float Radius { get => _radius; set { _radius = value; RequestUpdate(); } }
    [Export] public float Thickness { get => _thickness; set { _thickness = value; RequestUpdate(); } }
    [Export] public float StartAngle { get => _startAngle; set { _startAngle = value; RequestUpdate(); } }
    [Export] public float EndAngle { get => _endAngle; set { _endAngle = value; RequestUpdate(); } }
    [Export] public int Segments { get => _segments; set { _segments = Math.Max(2, value); RequestUpdate(); } }

    public override Godot.Collections.Array _CreateMeshArray()
    {
        int n = _segments + 1;
        var vertices = new Vector3[n * 2];
        var normals = new Vector3[n * 2];
        var uvs = new Vector2[n * 2];
        var indices = new int[_segments * 6];
        for (int i = 0; i < n; i++)
        {
            float u = i / (float)_segments;
            float a = Mathf.DegToRad(Mathf.Lerp(_startAngle, _endAngle, u));
            var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            float inner = _radius - _thickness * Mathf.Pow(Mathf.Sin(Mathf.Pi * u), 0.7f) - 0.02f;
            vertices[2 * i] = dir * inner;
            vertices[2 * i + 1] = dir * _radius;
            normals[2 * i] = normals[2 * i + 1] = Vector3.Back;
            uvs[2 * i] = new Vector2(u, 0f);
            uvs[2 * i + 1] = new Vector2(u, 1f);
        }

        for (int i = 0; i < _segments; i++)
        {
            int k = i * 6, v = i * 2;
            indices[k] = v;
            indices[k + 1] = v + 1;
            indices[k + 2] = v + 2;
            indices[k + 3] = v + 1;
            indices[k + 4] = v + 3;
            indices[k + 5] = v + 2;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        return arrays;
    }
}
