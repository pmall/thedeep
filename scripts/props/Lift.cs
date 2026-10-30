using System;
using Godot;

namespace TheDeep;

/// <summary>
/// A counterweighted lift that only carries its rider up (the Counterweight): an iron cage on a rope over a sheave, an iron
/// weight on the rope's other end. Standing on the cage at its lower stop lets go of the brake; it rises <see cref="Rise"/>
/// and eases into its upper stop while the weight sinks past it (<see cref="LiftMotion"/>, pure). The node's origin is the
/// top of the cage floor, centered, at the lower stop. The cage's collision is rock (never grippable) and it is not a
/// <see cref="Block"/>: the map's geometry check rides it as a <see cref="LiftPath"/> (<see cref="Path"/>). Visual pieces
/// (the weight, the sheave, the ropes) only follow the travel.
/// </summary>
public partial class Lift : Node3D
{
    [Export] public AnimatableBody3D Cage { get; set; } = null!;

    /// <summary>The counterweight (its X and Z); it sinks <see cref="WeightRatio"/> of the cage's travel as the cage rises.</summary>
    [Export] public Node3D Weight { get; set; } = null!;

    /// <summary>
    /// The wheel at the rope's top: its mount (set in the plane of the two ropes, raised over the upper stop on ready) and
    /// the wheel itself (its pivot at the center, turning about its Z with the travel).
    /// </summary>
    [Export] public Node3D SheaveMount { get; set; } = null!;
    [Export] public Node3D Sheave { get; set; } = null!;

    /// <summary>Vertical ropes of unit length (scaled): from the cage's head, and from the weight's top, up to the sheave.</summary>
    [Export] public Node3D CageRope { get; set; } = null!;
    [Export] public Node3D WeightRope { get; set; } = null!;

    /// <summary>Above the cage floor, where its rope is tied; above the weight's origin, where its rope is tied, meters.</summary>
    [Export] public float HeadHeight { get; set; } = 3.6f;
    [Export] public float WeightTop { get; set; } = 2.2f;

    /// <summary>The weight's origin above the lower stop once the cage is at the top (its lowest), meters.</summary>
    [Export] public float WeightLow { get; set; } = 2.5f;

    /// <summary>How far the weight sinks per meter the cage rises: 0.5 on a doubled rope (a heavier weight, half the travel).</summary>
    [Export] public float WeightRatio { get; set; } = 0.5f;

    /// <summary>The sheave's center over the cage's head at the upper stop, meters.</summary>
    [Export] public float SheaveClearance { get; set; } = 1.2f;

    [Export] public float SheaveRadius { get; set; } = 3f;

    /// <summary>The cage floor's width (riders stand on it), meters.</summary>
    [Export] public float Width { get; set; } = 3f;

    /// <summary>From the lower stop to the upper, meters.</summary>
    [Export] public float Rise { get; set; } = 20f;

    /// <summary>Cruising speed and how quickly it gathers and sheds it (m/s, m/s²): slow and heavy, a breath to get going.</summary>
    [Export] public float Speed { get; set; } = 4.5f;
    [Export] public float Acceleration { get; set; } = 1.6f;

    /// <summary>Seconds a rider stands on it before the brake lets go.</summary>
    [Export] public float BoardDelay { get; set; } = 0.45f;

    /// <summary>Left empty at the top this long (seconds), it sinks back at this speed (m/s).</summary>
    [Export] public float ReturnDelay { get; set; } = 3f;
    [Export] public float ReturnSpeed { get; set; } = 1.2f;

    private LiftMotion _motion = null!;
    private Vector3 _weightRest;

    /// <summary>The pilgrim, set by the session when the room loads.</summary>
    public Player? Player { get; set; }

    public LiftMotion Travel => _motion;

    /// <summary>Raised when the brake lets go under a rider (for a shake).</summary>
    public event Action<Lift>? Released;

    /// <summary>The lift as the geometry check sees it (room positions: rooms sit at the world origin).</summary>
    public LiftPath Path
    {
        get
        {
            Vector3 p = GlobalPosition;
            return new LiftPath(new Box(p.X - Width * 0.5f, p.Y - 0.5f, p.X + Width * 0.5f, p.Y), Rise);
        }
    }

    public override void _Ready()
    {
        Cage.CollisionLayer = PhysicsLayers.WorldRock;
        Cage.CollisionMask = 0;
        _weightRest = new Vector3(Weight.Position.X, WeightLow + Rise * WeightRatio, Weight.Position.Z);
        SheaveMount.Position = new Vector3(SheaveMount.Position.X, Rise + HeadHeight + SheaveClearance, SheaveMount.Position.Z);
        _motion = new LiftMotion(Rise, Speed, Acceleration, BoardDelay, ReturnSpeed, ReturnDelay);
        Place();
    }

    public override void _PhysicsProcess(double delta)
    {
        // Held still while the pilgrim is (travelling between rooms under the veil).
        if (Player?.Frozen == true)
        {
            return;
        }

        if (_motion.Step((float)delta, Ridden()))
        {
            Released?.Invoke(this);
        }

        Place();
    }

    /// <summary>The pilgrim stands upright on the cage floor.</summary>
    private bool Ridden()
    {
        if (Player is null || !Player.IsOnFloor() || !Player.IsUpright)
        {
            return false;
        }

        Vector3 feet = Player.GlobalPosition - Cage.GlobalPosition;
        return MathF.Abs(feet.Y) < 0.2f && MathF.Abs(feet.X) < Width * 0.5f + 0.3f;
    }

    private void Place()
    {
        float offset = _motion.Offset;
        Cage.Position = new Vector3(0f, offset, 0f);
        Weight.Position = _weightRest - new Vector3(0f, offset * WeightRatio, 0f);
        Sheave.Rotation = new Vector3(0f, 0f, -offset / SheaveRadius);
        float top = SheaveMount.Position.Y;
        Rope(CageRope, offset + HeadHeight, top);
        Rope(WeightRope, Weight.Position.Y + WeightTop, top);
    }

    private static void Rope(Node3D rope, float from, float to)
    {
        float length = Mathf.Max(0.01f, to - from);
        rope.Position = new Vector3(rope.Position.X, from + length * 0.5f, rope.Position.Z);
        rope.Scale = new Vector3(1f, length, 1f);
    }
}
