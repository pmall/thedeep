using System;
using Godot;

namespace TheDeep;

/// <summary>
/// A connection marker inside a room scene: where the room connects to another one, and what it takes.
/// The map is never listed by hand: the extractor (M7) builds the world graph from these markers, and
/// room transitions (M6) use them. The marker sits where the player stands on arrival through it.
/// The requirement is for leaving through this exit; the way back is the target exit's own.
/// </summary>
public partial class RoomExit : Marker3D
{
    /// <summary>Room scene this exit leads to; empty while that room isn't built yet.</summary>
    [Export(PropertyHint.File, "*.tscn")] public string TargetRoom { get; set; } = "";

    /// <summary>Name of the <see cref="RoomExit"/> node in the target room where the player arrives.</summary>
    [Export] public string TargetExit { get; set; } = "";

    /// <summary>
    /// Abilities needed to leave through here, as a <see cref="TheDeep.Requirement"/> expression
    /// (<c>MagGrip | Harpoon</c>, <c>Drill &amp; Ballast</c>); empty for the base moveset.
    /// </summary>
    [Export] public string Requires { get; set; } = "";

    /// <summary>
    /// Only usable from this side (a drop, a collapsing passage). Otherwise the target exit must lead back here.
    /// </summary>
    [Export] public bool OneWay { get; set; }

    /// <summary>
    /// Arriving up through this exit (a hatch in the floor), the player is carried this way (−1 west, +1 east)
    /// until it lands, onto the floor beside the opening; 0 for none. Keep that floor within 1.5 m above the marker.
    /// </summary>
    [Export] public int ArrivalSide { get; set; }

    /// <summary>Trigger area on the play plane (width, height), centered on the marker.</summary>
    [Export] public Vector2 Size { get; set; } = new(1f, 3f);

    public Requirement Requirement => Requirement.Parse(Requires);

    /// <summary>The player walked into the exit's area (only while armed): travel through it.</summary>
    public event Action<RoomExit>? Crossed;

    /// <summary>Whether entering the area travels. False until the player has left it, after arriving through it.</summary>
    public bool Armed { get; private set; } = true;

    /// <summary>An exit with a target room has a trigger area; one leading to a room not built yet stays sealed.</summary>
    public bool Open => TargetRoom != "";

    /// <summary>The marker's position in its room, on the play plane (rooms are loaded at the world origin).</summary>
    public Vector2 RoomPosition => new(GlobalPosition.X, GlobalPosition.Y);

    public override void _Ready()
    {
        if (!Open)
        {
            return;
        }

        var area = new Area3D
        {
            Name = "Trigger",
            CollisionLayer = PhysicsLayers.Trigger,
            CollisionMask = PhysicsLayers.Player,
            Monitorable = false,
        };
        area.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(Size.X, Size.Y, 2f) } });
        area.BodyEntered += OnBodyEntered;
        area.BodyExited += OnBodyExited;
        AddChild(area);
    }

    /// <summary>The player just arrived through here: entering again only counts after leaving the area.</summary>
    public void Disarm() => Armed = false;

    private void OnBodyEntered(Node3D body)
    {
        if (Armed && body is Player)
        {
            Crossed?.Invoke(this);
        }
    }

    private void OnBodyExited(Node3D body)
    {
        if (body is Player)
        {
            Armed = true;
        }
    }
}
