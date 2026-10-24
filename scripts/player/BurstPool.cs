using Godot;

namespace TheDeep;

/// <summary>
/// A few copies of a one-shot particle burst (scenes/fx/Grit, Puff, Sparks), fired in turn so bursts can
/// overlap (footsteps come faster than a burst lives). Top level: a burst stays where it was fired.
/// </summary>
public partial class BurstPool : Node3D
{
    [Export] public PackedScene Effect { get; set; } = null!;
    [Export] public int Count { get; set; } = 3;

    private GpuParticles3D[] _bursts = System.Array.Empty<GpuParticles3D>();
    private int _next;

    public override void _Ready()
    {
        TopLevel = true;
        _bursts = new GpuParticles3D[Mathf.Max(1, Count)];
        for (int i = 0; i < _bursts.Length; i++)
        {
            _bursts[i] = Effect.Instantiate<GpuParticles3D>();
            AddChild(_bursts[i]);
        }
    }

    /// <summary>Fires a burst at <paramref name="position"/>, with <paramref name="strength"/> (0 to 1) of its particles.</summary>
    public void Emit(Vector3 position, float strength)
    {
        if (strength <= 0.01f || _bursts.Length == 0)
        {
            return;
        }

        GpuParticles3D burst = _bursts[_next];
        _next = (_next + 1) % _bursts.Length;
        burst.GlobalPosition = position;
        burst.AmountRatio = Mathf.Clamp(strength, 0f, 1f);
        burst.Restart();
    }
}
