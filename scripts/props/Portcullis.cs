using Godot;

namespace TheDeep;

/// <summary>
/// A gate of iron bars that drops across a way and rises again (a boss's arena, <see cref="BossSeal"/>; a <see cref="LockedDoor"/>). The node's
/// origin is the bottom of the gate where it closes; open, it hangs <see cref="Lift"/> higher, so keep a lintel or
/// a ceiling there to take it. Its collision is rock (never grippable) and it is not a <see cref="Block"/>: the map's
/// geometry check counts the way as open, which it is whenever no fight is on.
/// </summary>
public partial class Portcullis : Node3D
{
    /// <summary>The moving gate: its collision and its bars.</summary>
    [Export] public AnimatableBody3D Gate { get; set; } = null!;

    [Export] public CollisionShape3D Shape { get; set; } = null!;

    /// <summary>The bars' grate (given a mesh of the gate's size, so instances never share one).</summary>
    [Export] public MeshInstance3D Bars { get; set; } = null!;

    /// <summary>The beam across the gate's top, the one along its foot and the one between them.</summary>
    [Export] public MeshInstance3D Header { get; set; } = null!;
    [Export] public MeshInstance3D Foot { get; set; } = null!;
    [Export] public MeshInstance3D Middle { get; set; } = null!;

    /// <summary>How wide the bars stand along the way, meters; the collision is half of it (<see cref="CollisionShare"/>).</summary>
    [Export] public float Width { get; set; } = 1.2f;

    /// <summary>How tall the gate is, meters; <see cref="Lift"/> should clear it.</summary>
    [Export] public float Height { get; set; } = 5f;

    /// <summary>How deep the collision reaches across the play plane, meters.</summary>
    [Export] public float Depth { get; set; } = 4f;

    /// <summary>The gate stops barring the way once it is this far open (0 shut, 1 raised): a hand's breadth off the floor is enough to lift its collision.</summary>
    [Export] public float OpenFraction { get; set; } = 0.6f;

    /// <summary>How far above its closed place the gate hangs open, meters.</summary>
    [Export] public float Lift { get; set; } = 5.5f;

    /// <summary>Seconds to fall shut (a heavy drop) and to rise open (a slow winch).</summary>
    [Export] public float FallTime { get; set; } = 0.35f;
    [Export] public float RiseTime { get; set; } = 1.8f;

    /// <summary>The collision's thickness along the way, as a share of <see cref="Width"/>: the bars' ends reach past what is solid.</summary>
    private const float CollisionShare = 0.5f;

    private const float BarsThickness = 0.3f;
    private const float BarSpacing = 0.3f;
    private const float BarWidth = 0.09f;
    private const float RailHeight = 0.4f;
    private const float RailDepth = 0.34f;
    private const float MiddleShare = 0.62f;
    private const float RailOverhang = 0.12f;

    /// <summary>The bars and beams stand this far toward the camera from the collision's plane.</summary>
    private const float FaceZ = 0.15f;

    private float _open = 1f;
    private float _target = 1f;

    /// <summary>Closing or closed.</summary>
    public bool Closed => _target <= 0f;

    /// <summary>Down on the floor, barring the way.</summary>
    public bool Shut => _open <= 0f;

    /// <summary>All the way up.</summary>
    public bool Raised => _open >= 1f;

    /// <summary>The box its collision fills on the play plane when shut (global positions: room positions in a room at the origin).</summary>
    public Box ShutBox => Box.Around(GlobalPosition.Flat() + new System.Numerics.Vector2(0f, Height / 2f),
        new System.Numerics.Vector2(Width * CollisionShare, Height));

    /// <summary>Raised when the gate hits the floor (for a shake, dust).</summary>
    public event System.Action<Portcullis>? Slammed;

    public override void _Ready()
    {
        Gate.CollisionLayer = PhysicsLayers.WorldRock;
        Gate.CollisionMask = 0;
        Build();
        Place();
    }

    /// <summary>The gate is to shut: it moves there over the next frames.</summary>
    public void Close() => _target = 0f;

    /// <summary>The gate is to open: it moves there over the next frames.</summary>
    public void Open() => _target = 1f;

    /// <summary>Open or shut at once, without the drop or the winch (a room loaded with the gate already so).</summary>
    public void Settle(bool open)
    {
        _open = _target = open ? 1f : 0f;
        Place();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_open == _target)
        {
            return;
        }

        float dt = (float)delta;
        bool falling = _target < _open;
        _open = Motion.Approach(_open, _target, dt / (falling ? FallTime : RiseTime));
        Place();
        if (falling && _open <= 0f)
        {
            Slammed?.Invoke(this);
        }
    }

    /// <summary>Sizes the collision, the bars and the three beams to <see cref="Width"/> and <see cref="Height"/>.</summary>
    private void Build()
    {
        Shape.Shape = new BoxShape3D { Size = new Vector3(Width * CollisionShare, Height, Depth) };
        Shape.Position = new Vector3(0f, Height / 2f, 0f);

        // The grate's depth axis is turned to the vertical by the node's rotation in the scene.
        Bars.Mesh = new GrateMesh { Size = new Vector3(Width, BarsThickness, Height), Spacing = BarSpacing, Bar = BarWidth };
        Bars.Position = new Vector3(0f, Height / 2f, FaceZ);

        var beam = new BlockMesh { Size = new Vector3(Width + RailOverhang, RailHeight, RailDepth), Bevel = 0.04f };
        Header.Mesh = beam;
        Header.Position = new Vector3(0f, Height - RailHeight / 2f, FaceZ);
        Foot.Mesh = beam;
        Foot.Position = new Vector3(0f, RailHeight / 2f, FaceZ);
        Middle.Mesh = beam;
        Middle.Position = new Vector3(0f, Height * MiddleShare, FaceZ);
    }

    private void Place()
    {
        // Falling: accelerating like a dropped weight; rising: eased in and out like a winch.
        float t = _target < _open || _open <= 0f ? _open * _open : _open * _open * (3f - 2f * _open);
        Gate.Position = new Vector3(0f, Lift * t, 0f);
        Shape.Disabled = _open > OpenFraction;
    }
}
