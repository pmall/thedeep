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

    /// <summary>Trigger area on the play plane (width, height), centered on the marker.</summary>
    [Export] public Vector2 Size { get; set; } = new(1f, 3f);

    public Requirement Requirement => Requirement.Parse(Requires);
}
