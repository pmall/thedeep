using Godot;

namespace TheDeep;

/// <summary>
/// A connection marker inside a room scene: where the room connects to another one, which way, and what it takes. Room travel
/// crosses it (<see cref="RoomTravel"/>), and the room check builds the world graph from these markers (the map is never listed by hand).
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

    /// <summary>
    /// Arriving up through this exit (a hatch in the floor), the player is carried this way (−1 west, +1 east)
    /// until it lands, onto the floor beside the opening; 0 for none. Keep that floor within 1.5 m above the trigger.
    /// </summary>
    [Export] public int ArrivalSide { get; set; }

    /// <summary>Which way the exit leads out of the room: a doorway in the west or east wall, a hatch in the floor or the roof.</summary>
    [Export] public ExitSide Side { get; set; }

    /// <summary>
    /// The exit's area on the play plane (width, height): a doorway's is the stretch of floor before the opening, its marker on that floor,
    /// as tall as the opening; a hatch's is the hole, centered on the marker, as wide as it. The trigger starts there (<see cref="Threshold"/>).
    /// </summary>
    [Export] public Vector2 Size { get; set; } = new(1f, 4f);

    public Requirement Requirement => Requirement.Parse(Requires);

    /// <summary>An exit with a target room; one leading to a room not built yet stays sealed.</summary>
    public bool Open => TargetRoom != "";

    /// <summary>A way out leading to a room not built yet (an arrival-only marker is not one).</summary>
    public bool Sealed => TargetRoom == "" && !ArrivalOnly;

    /// <summary>The marker's position in its room, on the play plane (rooms are loaded at the world origin).</summary>
    public Vector2 RoomPosition => new(GlobalPosition.X, GlobalPosition.Y);

    /// <summary>The exit's trigger and arrival (<see cref="TheDeep.Threshold"/>).</summary>
    public Threshold Threshold => new(Side, RoomPosition.ToCore(), Size.ToCore());
}
