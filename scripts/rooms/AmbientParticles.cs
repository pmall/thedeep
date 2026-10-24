using Godot;

namespace TheDeep;

/// <summary>
/// Ambient particles filling a box centered on the node (dust in light, silt, drips, bubbles). The effect
/// itself (process material, draw pass, lifetime) comes from its scene in scenes/fx/; a room places one
/// and sets only <see cref="Extents"/> and <see cref="Density"/>, so the same effect fills any area.
/// Deterministic: the seed comes from the node's position, so captures stay pixel-identical.
/// </summary>
public partial class AmbientParticles : GpuParticles3D
{
    /// <summary>Half size of the emission box, meters.</summary>
    [Export] public Vector3 Extents { get; set; } = new(5f, 5f, 1f);

    /// <summary>
    /// Particles alive per square meter of the box's front face (X by Y, as the camera sees it).
    /// 0 keeps the scene's <see cref="GpuParticles3D.Amount"/>.
    /// </summary>
    [Export] public float Density { get; set; }

    /// <summary>How far particles may drift out of the box, for the visibility bounds.</summary>
    [Export] public Vector3 Drift { get; set; } = Vector3.One;

    private const int MaxAmount = 4096;

    public override void _Ready()
    {
        if (ProcessMaterial is ParticleProcessMaterial process)
        {
            var own = (ParticleProcessMaterial)process.Duplicate();
            own.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
            own.EmissionBoxExtents = Extents;
            ProcessMaterial = own;
        }

        if (Density > 0f)
        {
            Amount = Mathf.Clamp(Mathf.RoundToInt(Density * 4f * Extents.X * Extents.Y), 1, MaxAmount);
        }

        Vector3 reach = Extents + Drift;
        VisibilityAabb = new Aabb(-reach, reach * 2f);
        UseFixedSeed = true;
        Seed = (uint)GD.Hash(GlobalPosition);
        Preprocess = Lifetime;
        Restart();
    }
}
