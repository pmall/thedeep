using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// A band of light left behind a dash: the body's path over the last moment, narrowing and fading with age.
/// Points are recorded on physics ticks (<see cref="Track"/>) and the band is drawn every frame, its head
/// on the body's interpolated position, so it stays smooth at any refresh rate. Visual only.
/// </summary>
public partial class DashTrail : MeshInstance3D
{
    /// <summary>How long a point of the trail lives, seconds.</summary>
    [Export] public float Lifetime { get; set; } = 0.24f;

    /// <summary>Half the band's height at its head, meters.</summary>
    [Export] public float HalfHeight { get; set; } = 0.3f;

    private readonly List<(Vector3 Position, float Age)> _points = new();
    private Node3D? _head;
    private bool _emitting;
    private readonly ArrayMesh _mesh = new();

    public override void _Ready()
    {
        TopLevel = true;
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        CastShadow = ShadowCastingSetting.Off;
        Mesh = _mesh;
    }

    /// <summary>
    /// One physics tick: ages the trail and, while <paramref name="emitting"/>, adds the current position of
    /// <paramref name="head"/> (the body's center).
    /// </summary>
    public void Track(Node3D head, bool emitting, float delta)
    {
        _head = head;
        _emitting = emitting;
        for (int i = _points.Count - 1; i >= 0; i--)
        {
            (Vector3 position, float age) = _points[i];
            age += delta;
            if (age >= Lifetime)
            {
                _points.RemoveAt(i);
            }
            else
            {
                _points[i] = (position, age);
            }
        }

        if (emitting)
        {
            _points.Add((head.GlobalPosition, 0f));
        }
    }

    /// <summary>Clears the trail (teleport).</summary>
    public void Clear() => _points.Clear();

    public override void _Process(double delta)
    {
        _mesh.ClearSurfaces();
        int count = _points.Count + (_emitting && _head is not null ? 1 : 0);
        if (count < 2)
        {
            return;
        }

        var vertices = new Vector3[count * 2];
        var colors = new Color[count * 2];
        var uvs = new Vector2[count * 2];
        for (int i = 0; i < count; i++)
        {
            (Vector3 position, float age) = i < _points.Count ? _points[i] : (_head!.GetGlobalTransformInterpolated().Origin, 0f);
            float life = 1f - age / Lifetime;
            float half = HalfHeight * (0.35f + 0.65f * life);
            var color = new Color(1f, 1f, 1f, life * life);
            float u = (float)i / (count - 1);
            vertices[i * 2] = position + Vector3.Up * half;
            vertices[i * 2 + 1] = position - Vector3.Up * half;
            colors[i * 2] = color;
            colors[i * 2 + 1] = color;
            uvs[i * 2] = new Vector2(u, 0f);
            uvs[i * 2 + 1] = new Vector2(u, 1f);
        }

        var indices = new int[(count - 1) * 6];
        for (int i = 0; i < count - 1; i++)
        {
            int a = i * 2;
            indices[i * 6] = a;
            indices[i * 6 + 1] = a + 2;
            indices[i * 6 + 2] = a + 1;
            indices[i * 6 + 3] = a + 1;
            indices[i * 6 + 4] = a + 2;
            indices[i * 6 + 5] = a + 3;
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.Color] = colors;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Index] = indices;
        _mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
    }
}
