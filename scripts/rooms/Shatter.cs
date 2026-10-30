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
    /// <summary>The block's size, its look, the direction the Drill moved in, the dust burst.</summary>
    public Vector3 Size { get; set; } = Vector3.One;
    public Material? Material { get; set; }
    public Vector3 Push { get; set; }
    public PackedScene? Dust { get; set; }

    /// <summary>Seconds a chunk flies, and the share of it spent shrinking away at the end.</summary>
    private const float Life = 1.2f;
    private const float Shrink = 0.4f;
    private const float Gravity = 16f;

    private readonly List<(Node3D Node, Vector3 Velocity, Vector3 Spin, float Scale)> _chunks = new();
    private float _age;

    public override void _Ready()
    {
        var rng = new RandomNumberGenerator { Seed = (ulong)Mathf.Abs(GlobalPosition.X * 73f + GlobalPosition.Y * 131f) + 1 };
        float area = Size.X * Size.Y;
        int count = Mathf.Clamp(Mathf.RoundToInt(area * 2.5f), 7, 26);
        Vector3 push = Push.LengthSquared() > 0f ? Push.Normalized() : Vector3.Zero;
        var meshes = new[] { new BlockMesh { Size = new Vector3(0.5f, 0.42f, 0.46f), Bevel = 0.06f },
                             new BlockMesh { Size = new Vector3(0.34f, 0.3f, 0.36f), Bevel = 0.05f },
                             new BlockMesh { Size = new Vector3(0.62f, 0.32f, 0.4f), Bevel = 0.07f } };
        for (int i = 0; i < count; i++)
        {
            var at = new Vector3(rng.RandfRange(-0.45f, 0.45f) * Size.X, rng.RandfRange(-0.45f, 0.45f) * Size.Y, rng.RandfRange(0f, 0.45f) * Size.Z);
            Vector3 outward = new Vector3(at.X, at.Y, at.Z * 0.6f + 0.6f).Normalized();
            Vector3 velocity = push * rng.RandfRange(3f, 7f) + outward * rng.RandfRange(1f, 3.5f) + Vector3.Up * rng.RandfRange(1f, 4f);
            var chunk = new MeshInstance3D { Mesh = meshes[i % meshes.Length], MaterialOverride = Material, Position = at,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            float scale = rng.RandfRange(0.7f, 1.4f);
            chunk.Scale = Vector3.One * scale;
            AddChild(chunk);
            _chunks.Add((chunk, velocity, new Vector3(rng.RandfRange(-9f, 9f), rng.RandfRange(-9f, 9f), rng.RandfRange(-9f, 9f)), scale));
        }

        if (Dust?.Instantiate() is GpuParticles3D dust)
        {
            var process = (ParticleProcessMaterial)dust.ProcessMaterial.Duplicate();
            process.EmissionBoxExtents = new Vector3(Size.X * 0.5f, Size.Y * 0.5f, 0.6f);
            dust.ProcessMaterial = process;
            dust.Position = new Vector3(0f, 0f, Size.Z * 0.5f);
            dust.Amount = Mathf.Clamp(Mathf.RoundToInt(area * 4f), 12, 60);
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

        if (_age >= Life + 1.2f)
        {
            QueueFree();
        }
    }
}
