using System;
using Godot;

namespace TheDeep;

/// <summary>
/// The slash a strike's blade leaves (assets/shaders/slash.gdshader): the band its edge swept over the last moment, from
/// the core's <see cref="BladeTrail"/>, drawn just in front of the body so it reads over it. Recorded on physics ticks
/// (<see cref="Track"/>), so it holds still through a hit stop; rebuilt only when it changes, from kept buffers, so drawing
/// it leaves nothing for the garbage collector (instructions/principles.md#performance). Visual only.
/// </summary>
public partial class SlashTrail : MeshInstance3D
{
    private static readonly Godot.Collections.Array<Godot.Collections.Array> NoBlendShapes = new();
    private static readonly Godot.Collections.Dictionary NoLods = new();

    /// <summary>How long the slash lasts behind the blade, seconds: it fades fast.</summary>
    [Export] public float Lifetime { get; set; } = 0.11f;

    /// <summary>Samples laid along the blade's arc per physics tick.</summary>
    [Export] public int Steps { get; set; } = 10;

    /// <summary>How far in front of the body (toward the camera) the slash is drawn, meters.</summary>
    [Export] public float Front { get; set; } = 0.3f;

    /// <summary>The blade tip's speed (m/s) from which the slash is laid at its full strength: slower, it fades.</summary>
    [Export] public float FullSpeed { get; set; } = 12f;

    /// <summary>Where along the blade the slash starts, a share of its length from its base (the tip is 1).</summary>
    [Export] public float Inner { get; set; } = 0.62f;

    private BladeTrail? _trail;
    private readonly ArrayMesh _mesh = new();
    private NativeMeshArrays? _arrays;
    private Vector3[] _vertices = Array.Empty<Vector3>();
    private Color[] _colors = Array.Empty<Color>();
    private Vector2[] _uvs = Array.Empty<Vector2>();
    private int[] _indices = Array.Empty<int>();
    private float _plane;
    private bool _dirty;
    private bool _drawn;

    public override void _Ready()
    {
        TopLevel = true;
        PhysicsInterpolationMode = PhysicsInterpolationModeEnum.Off;
        CastShadow = ShadowCastingSetting.Off;
        Mesh = _mesh;
        int capacity = (int)MathF.Ceiling(Lifetime * Engine.PhysicsTicksPerSecond + 2f) * Steps;
        _trail = new BladeTrail(Lifetime, Steps, capacity, FullSpeed);
        _arrays = new NativeMeshArrays();
        _vertices = new Vector3[capacity * 2];
        _colors = new Color[capacity * 2];
        _uvs = new Vector2[capacity * 2];
        _indices = new int[Math.Max(capacity - 1, 0) * 6];
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
    /// One physics tick: ages the slash and lays the blade's sweep since the last tick with <paramref name="strength"/> (0 lays
    /// none). <paramref name="pivot"/> is what the blade swings around (the shoulder); <paramref name="blade"/> is the blade's
    /// node, hanging <paramref name="length"/> meters down its -Y axis (its scale included); <paramref name="body"/> is the
    /// body's position, which the slash is drawn in front of.
    /// </summary>
    public void Track(Node3D pivot, Node3D blade, float length, float strength, Vector3 body, float delta)
    {
        if (_trail is null)
        {
            return;
        }

        int before = _trail.Count;
        Transform3D t = blade.GlobalTransform;
        _trail.Step(delta, pivot.GlobalPosition.ToCore(), (t * new Vector3(0f, -length * Inner, 0f)).ToCore(),
            (t * new Vector3(0f, -length, 0f)).ToCore(), strength);
        _plane = body.Z + Front;
        _dirty |= before > 0 || _trail.Count > 0;
    }

    /// <summary>Drops the slash (a teleport).</summary>
    public void Clear()
    {
        _trail?.Clear();
        _dirty = true;
    }

    public override void _Process(double delta)
    {
        if (!_dirty || _trail is null || _arrays is null)
        {
            return;
        }

        _dirty = false;
        int count = _trail.Count;
        if (count < 2)
        {
            if (_drawn)
            {
                _mesh.ClearSurfaces();
                _drawn = false;
            }

            return;
        }

        for (int i = 0; i < count; i++)
        {
            (System.Numerics.Vector3 bladeBase, System.Numerics.Vector3 tip, float life) = _trail[i];
            var color = new Color(1f, 1f, 1f, Mathf.Clamp(life, 0f, 1f));
            float u = i / (float)(count - 1);
            _vertices[i * 2] = bladeBase.ToGodot() with { Z = _plane };
            _vertices[i * 2 + 1] = tip.ToGodot() with { Z = _plane };
            _colors[i * 2] = color;
            _colors[i * 2 + 1] = color;
            _uvs[i * 2] = new Vector2(u, 0f);
            _uvs[i * 2 + 1] = new Vector2(u, 1f);
        }

        _arrays.Set(Mesh.ArrayType.Vertex, _vertices.AsSpan(0, count * 2));
        _arrays.Set(Mesh.ArrayType.Color, _colors.AsSpan(0, count * 2));
        _arrays.Set(Mesh.ArrayType.TexUV, _uvs.AsSpan(0, count * 2));
        _arrays.Set(Mesh.ArrayType.Index, _indices.AsSpan(0, (count - 1) * 6));
        _mesh.ClearSurfaces();
        _mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, _arrays.Managed, NoBlendShapes, NoLods);
        _drawn = true;
    }
}
