using System;
using Godot;

namespace TheDeep;

/// <summary>
/// A connection marker inside a room scene: where the room connects to another one, and what it takes.
/// The map is never listed by hand: the room check builds the world graph from these markers, and
/// room travel uses them. The marker sits where the player stands on arrival through it.
/// The requirement is for leaving through this exit; the way back is the target exit's own.
/// </summary>
public partial class RoomExit : Marker3D
{
    /// <summary>Room scene this exit leads to; empty while that room isn't built yet.</summary>
    [Export(PropertyHint.File, "*.tscn")] public string TargetRoom { get; set; } = "";

    /// <summary>Name of the <see cref="RoomExit"/> node in the target room where the player arrives.</summary>
    [Export] public string TargetExit { get; set; } = "";

    /// <summary>
    /// Abilities and items needed to leave through here, as a <see cref="TheDeep.Requirement"/> expression
    /// (<c>MagGrip | Harpoon</c>, <c>Drill &amp; Ballast</c>, <c>CryptKey</c>); empty for the base moveset.
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

    /// <summary>
    /// Only arrived through, never left by: the far end of a one-way drop from another room (the Brink's jump lands in
    /// Rimefall's sky). It has no target and no trigger, and it is not a sealed way.
    /// </summary>
    [Export] public bool ArrivalOnly { get; set; }

    /// <summary>
    /// The locked door that bars this way, or none. Its item must be in every alternative of <see cref="Requires"/>, and in the way
    /// back's (the room check holds both to it).
    /// </summary>
    [Export] public LockedDoor? Door { get; set; }

    /// <summary>
    /// A secret's way in: a <see cref="FalseWall"/> covers it (the room check holds it to), and the map shows the way only once taken.
    /// </summary>
    [Export] public bool Hidden { get; set; }

    /// <summary>Trigger area on the play plane (width, height), centered on the marker.</summary>
    [Export] public Vector2 Size { get; set; } = new(1f, 3f);

    public Requirement Requirement => Requirement.Parse(Requires);

    /// <summary>The player is leaving through the exit (in its area while armed, and moving out through a doorway): travel through it.</summary>
    public event Action<RoomExit>? Crossed;

    /// <summary>Whether entering the area travels. False until the player has left it, after arriving through it.</summary>
    public bool Armed { get; private set; } = true;

    private Player? _inside;
    private int _outward;

    /// <summary>An exit with a target room has a trigger area; one leading to a room not built yet stays sealed.</summary>
    public bool Open => TargetRoom != "";

    /// <summary>A way out leading to a room not built yet (an arrival-only marker is not one).</summary>
    public bool Sealed => TargetRoom == "" && !ArrivalOnly;

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
        for (Node? n = GetParent(); n is not null; n = n.GetParent())
        {
            if (n is Room room)
            {
                _outward = ExitMath.Outward(RoomPosition.ToCore(), Size.ToCore(), room.BoundsMin.ToCore(), room.BoundsMax.ToCore());
                break;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // Inside a doorway's area, the player leaves once it moves or presses out of the room (ExitMath.Leaves). Its body must
        // really be there: the area's report can be stale after a teleport (a respawn), and a pilgrim far off once crossed.
        if (_inside is not null && Armed && Holds(_inside) && ExitMath.Leaves(_outward, _inside.Velocity.X, _inside.CurrentInput.MoveX))
        {
            _inside = null;
            Crossed?.Invoke(this);
        }
    }

    /// <summary>Whether the player's body (its middle) is within the trigger area, with a body's width of slack.</summary>
    private bool Holds(Player player)
    {
        Vector3 p = player.GlobalPosition;
        Vector2 at = RoomPosition;
        return Mathf.Abs(p.X - at.X) <= Size.X * 0.5f + 0.6f && p.Y + 0.9f >= at.Y - Size.Y * 0.5f - 1f && p.Y <= at.Y + Size.Y * 0.5f + 1f;
    }

    /// <summary>The player just arrived through here: entering again only counts after leaving the area.</summary>
    public void Disarm() => Armed = false;

    private void OnBodyEntered(Node3D body)
    {
        if (body is Player player)
        {
            _inside = player;
        }
    }

    private void OnBodyExited(Node3D body)
    {
        if (body is Player)
        {
            _inside = null;
            Armed = true;
        }
    }
}
