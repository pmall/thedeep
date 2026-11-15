using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// Cracked rock breaking under the Drill, visual only: chunks of the block's own material thrown along the pilgrim's way
/// and outward, tumbling, falling and shrinking away, and a dust cloud over where it stood. Added as a child of the
/// broken <see cref="Block"/> (at its center); frees itself when the last chunk is gone.
/// </summary>
public partial class Shatter : Node3D
{
    /// <summary>The broken block's size.</summary>
    public Vector3 Size { get; set; } = Vector3.One;

    /// <summary>The broken block's own material, worn by the chunks.</summary>
    public Material? Material { get; set; }
    /// <summary>The direction the Drill moved in: the chunks are thrown along it.</summary>
    public Vector3 Push { get; set; }
    /// <summary>The dust burst left where the block stood; null for none.</summary>
    public PackedScene? Dust { get; set; }

    /// <summary>Seconds a chunk flies.</summary>
    [Export] public float Life { get; set; } = 1.2f;

    /// <summary>The share of <see cref="Life"/> a chunk spends shrinking away at the end.</summary>
    [Export] public float Shrink { get; set; } = 0.4f;

    /// <summary>How long the node lives past <see cref="Life"/>, seconds: the dust's time to settle.</summary>
    [Export] public float Linger { get; set; } = 1.2f;

    /// <summary>The chunks' fall, m/s².</summary>
    [Export] public float Gravity { get; set; } = 16f;

    /// <summary>How many chunks per m² of the block's face; at least <see cref="MinChunks"/>, at most <see cref="MaxChunks"/>.</summary>
    [Export] public float ChunksPerArea { get; set; } = 2.5f;

    /// <summary>The fewest chunks a block throws.</summary>
    [Export] public int MinChunks { get; set; } = 7;

    /// <summary>The most chunks a block throws.</summary>
    [Export] public int MaxChunks { get; set; } = 26;

    /// <summary>The sizes of the chunk shapes, each paired with its bevel in <see cref="ChunkBevels"/>; chunks take them in turn.</summary>
    [Export] public Vector3[] ChunkSizes { get; set; } = [new(0.5f, 0.42f, 0.46f), new(0.34f, 0.3f, 0.36f), new(0.62f, 0.32f, 0.4f)];

    /// <summary>The bevel of each chunk shape of <see cref="ChunkSizes"/>, meters.</summary>
    [Export] public float[] ChunkBevels { get; set; } = [0.06f, 0.05f, 0.07f];

    /// <summary>A chunk's speed along the Drill's push: least and greatest, m/s.</summary>
    [Export] public Vector2 PushSpeed { get; set; } = new(3f, 7f);

    /// <summary>A chunk's speed outward from the block's middle: least and greatest, m/s.</summary>
    [Export] public Vector2 OutwardSpeed { get; set; } = new(1f, 3.5f);

    /// <summary>A chunk's upward kick: least and greatest, m/s.</summary>
    [Export] public Vector2 LiftSpeed { get; set; } = new(1f, 4f);

    /// <summary>A chunk's tumble about each axis is at most this, radians per second either way.</summary>
    [Export] public float SpinSpeed { get; set; } = 9f;

    /// <summary>A chunk's scale: least and greatest.</summary>
    [Export] public Vector2 ChunkScale { get; set; } = new(0.7f, 1.4f);

    /// <summary>Dust particles per m² of the block's face; at least <see cref="MinDust"/>, at most <see cref="MaxDust"/>.</summary>
    [Export] public float DustPerArea { get; set; } = 4f;

    /// <summary>The fewest dust particles.</summary>
    [Export] public int MinDust { get; set; } = 12;

    /// <summary>The most dust particles.</summary>
    [Export] public int MaxDust { get; set; } = 60;

    private readonly List<(Node3D Node, Vector3 Velocity, Vector3 Spin, float Scale)> _chunks = new();
    private float _age;

    public override void _Ready()
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)Mathf.Abs(GlobalPosition.X * 73f + GlobalPosition.Y * 131f) + 1 };
        float area = Size.X * Size.Y;
        int count = Mathf.Clamp(Mathf.RoundToInt(area * ChunksPerArea), MinChunks, MaxChunks);
        Vector3 push = Push.LengthSquared() > 0f ? Push.Normalized() : Vector3.Zero;
        var meshes = new BlockMesh[ChunkSizes.Length];
        for (int m = 0; m < meshes.Length; m++)
        {
            meshes[m] = new BlockMesh { Size = ChunkSizes[m], Bevel = ChunkBevels[m] };
        }

        for (int i = 0; i < count; i++)
        {
            var at = new Vector3(rng.RandfRange(-0.45f, 0.45f) * Size.X, rng.RandfRange(-0.45f, 0.45f) * Size.Y, rng.RandfRange(0f, 0.45f) * Size.Z);
            Vector3 outward = new Vector3(at.X, at.Y, at.Z * 0.6f + 0.6f).Normalized();
            Vector3 velocity = push * rng.RandfRange(PushSpeed.X, PushSpeed.Y) + outward * rng.RandfRange(OutwardSpeed.X, OutwardSpeed.Y) + Vector3.Up * rng.RandfRange(LiftSpeed.X, LiftSpeed.Y);
            var chunk = new MeshInstance3D
            {
                Mesh = meshes[i % meshes.Length],
                MaterialOverride = Material,
                Position = at,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
            };
            float scale = rng.RandfRange(ChunkScale.X, ChunkScale.Y);
            chunk.Scale = Vector3.One * scale;
            AddChild(chunk);
            _chunks.Add((chunk, velocity, new Vector3(rng.RandfRange(-SpinSpeed, SpinSpeed), rng.RandfRange(-SpinSpeed, SpinSpeed), rng.RandfRange(-SpinSpeed, SpinSpeed)), scale));
        }

        if (Dust?.Instantiate() is GpuParticles3D dust)
        {
            var process = (ParticleProcessMaterial)dust.ProcessMaterial.Duplicate();
            process.EmissionBoxExtents = new Vector3(Size.X * 0.5f, Size.Y * 0.5f, 0.6f);
            dust.ProcessMaterial = process;
            dust.Position = new Vector3(0f, 0f, Size.Z * 0.5f);
            dust.Amount = Mathf.Clamp(Mathf.RoundToInt(area * DustPerArea), MinDust, MaxDust);
            AddChild(dust);
            dust.Restart();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        _age += dt;
        float fade = Mathf.Clamp((Life - _age) / (Life * Shrink), 0f, 1f);
        for (int i = 0; i < _chunks.Count; i++)
        {
            (Node3D node, Vector3 velocity, Vector3 spin, float scale) = _chunks[i];
            velocity += Vector3.Down * Gravity * dt;
            node.Position += velocity * dt;
            node.Rotation += spin * dt;
            node.Scale = Vector3.One * scale * fade;
            _chunks[i] = (node, velocity, spin, scale);
        }

        if (_age >= Life + Linger)
        {
            QueueFree();
        }
    }
}
