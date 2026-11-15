using System;
using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// A band of light left behind a dash: the body's path over the last moment, narrowing and fading with age.
/// Points are recorded on physics ticks (<see cref="Track"/>) and the band is drawn every frame, its head
/// on the body's interpolated position, so it stays smooth at any refresh rate. The mesh is rebuilt every frame from kept buffers,
/// so drawing it leaves nothing for the garbage collector (instructions/principles.md#performance). Visual only.
/// </summary>
public partial class DashTrail : MeshInstance3D
{
    /// <summary>How long a point of the trail lives, seconds.</summary>
    [Export] public float Lifetime { get; set; } = 0.24f;

    /// <summary>Half the band's height at its head, meters.</summary>
    [Export] public float HalfHeight { get; set; } = 0.3f;

    private static readonly Godot.Collections.Array<Godot.Collections.Array> NoBlendShapes = new();
    private static readonly Godot.Collections.Dictionary NoLods = new();

    private readonly List<(Vector3 Position, float Age)> _points = new();
    private Node3D? _head;
    private bool _emitting;
    private readonly ArrayMesh _mesh = new();
    private NativeMeshArrays? _arrays;
    private Vector3[] _vertices = Array.Empty<Vector3>();
    private Color[] _colors = Array.Empty<Color>();
    private Vector2[] _uvs = Array.Empty<Vector2>();
    private int[] _indices = Array.Empty<int>();

    public override void _Ready()
    {
        TopLevel = true;
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        CastShadow = ShadowCastingSetting.Off;
        Mesh = _mesh;

        // A point lives Lifetime seconds and one is added per physics tick; one more for the head and one of slack.
        int capacity = (int)MathF.Ceiling(Lifetime * Engine.PhysicsTicksPerSecond) + 3;
        _arrays = new NativeMeshArrays();
        _vertices = new Vector3[capacity * 2];
        _colors = new Color[capacity * 2];
        _uvs = new Vector2[capacity * 2];
        _indices = new int[(capacity - 1) * 6];
        for (int i = 0; i < capacity - 1; i++)
        {
            int a = i * 2, k = i * 6;
            _indices[k] = a;
            _indices[k + 1] = a + 2;
            _indices[k + 2] = a + 1;
            _indices[k + 3] = a + 1;
            _indices[k + 4] = a + 2;
            _indices[k + 5] = a + 3;
        }
    }

    public override void _ExitTree()
    {
        _arrays?.Dispose();
        _arrays = null;
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
        if (_arrays is null)
        {
            return;
        }

        // While emitting, the trail runs on to the body's interpolated position.
        Vector3? head = _emitting && _head is not null ? _head.GetGlobalTransformInterpolated().Origin : null;
        int count = Math.Min(_points.Count + (head is null ? 0 : 1), _vertices.Length / 2);
        if (count < 2)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            (Vector3 position, float age) = i < _points.Count ? _points[i] : (head.GetValueOrDefault(), 0f);
            float life = 1f - age / Lifetime;
            float half = HalfHeight * (0.35f + 0.65f * life);
            var color = new Color(1f, 1f, 1f, life * life);
            float u = (float)i / (count - 1);
            _vertices[i * 2] = position + Vector3.Up * half;
            _vertices[i * 2 + 1] = position - Vector3.Up * half;
            _colors[i * 2] = color;
            _colors[i * 2 + 1] = color;
            _uvs[i * 2] = new Vector2(u, 0f);
            _uvs[i * 2 + 1] = new Vector2(u, 1f);
        }

        _arrays.Set(Mesh.ArrayType.Vertex, _vertices.AsSpan(0, count * 2));
        _arrays.Set(Mesh.ArrayType.Color, _colors.AsSpan(0, count * 2));
        _arrays.Set(Mesh.ArrayType.TexUV, _uvs.AsSpan(0, count * 2));
        _arrays.Set(Mesh.ArrayType.Index, _indices.AsSpan(0, (count - 1) * 6));
        _mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, _arrays.Managed, NoBlendShapes, NoLods);
    }
}
