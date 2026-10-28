using Godot;

namespace TheDeep;

/// <summary>
/// A harpoon anchor: an old mooring ring with a glowing core, hung in the air of a room. With the Harpoon the
/// pilgrim fires at it and zips to it (<see cref="States.HarpoonState"/>). The node's origin is the point the
/// pilgrim's center is pulled to; keep a body's height of air below it. The anchor in sight brightens
/// (<see cref="Sighted"/>, eased). Visual and a position only: no collision.
/// </summary>
public partial class HarpoonAnchor : Node3D
{
    [Export] public Node3D Core { get; set; } = null!;

    [Export] public OmniLight3D Light { get; set; } = null!;

    private float _baseEnergy;
    private float _sighted;
    private bool _wanted;
    private float _time;

    /// <summary>Whether this is the anchor the harpoon would fire at now.</summary>
    public bool Sighted
    {
        get => _wanted;
        set => _wanted = value;
    }

    /// <summary>The point the pilgrim's center is pulled to, on the play plane.</summary>
    public System.Numerics.Vector2 Point => new(GlobalPosition.X, GlobalPosition.Y);

    public override void _Ready()
    {
        _baseEnergy = Light.LightEnergy;
        _time = GlobalPosition.X * 0.7f;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        _sighted = Motion.Approach(_sighted, _wanted ? 1f : 0f, dt * 6f);
        float s = _sighted * _sighted * (3f - 2f * _sighted);
        float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 2.2f);
        Core.Scale = Vector3.One * (1f + 0.08f * pulse + 0.45f * s);
        Light.LightEnergy = _baseEnergy * (0.85f + 0.15f * pulse + 2.2f * s);
    }
}
