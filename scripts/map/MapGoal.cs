using Godot;

namespace TheDeep;

/// <summary>
/// Where the journey ends, for the map solver (docs/long-term.md#no-softlock): from every state the pilgrim
/// can reach, a room holding a goal must stay reachable. It sits in the room that ends its world's journey. With a <see cref="Size"/>, it
/// is also an area: reaching it shows the banner lines (the end of the build), once per visit; an <see cref="Ending"/>
/// goal instead closes the run (<see cref="EndCard"/>).
/// </summary>
public partial class MapGoal : Marker3D, IAnnounces
{
    /// <summary>Area on the play plane (width, height), centered on the marker; zero for a marker only.</summary>
    [Export] public Vector2 Size { get; set; }

    /// <summary>The end of the game (the Heart): reaching it, once the room's boss is broken, closes the run with the end card.</summary>
    [Export] public bool Ending { get; set; }

    [Export] public StringName TitleLine { get; set; } = "";
    [Export] public StringName HintLine { get; set; } = "";

    /// <inheritdoc/>
    public System.Collections.Generic.IEnumerable<StringName> LineIds => new[] { TitleLine, HintLine };

    /// <summary>What the banner says as it is reached.</summary>
    public Announcement Said => new(TitleLine, HintLine);

    /// <inheritdoc/>
    public System.Collections.Generic.IEnumerable<Announcement> Announcements => new[] { Said };

    /// <summary>Whether <paramref name="point"/> is inside the goal's area on the play plane; never for a marker
    /// only.</summary>
    public bool Contains(Vector3 point) =>
        Size != Vector2.Zero && Mathf.Abs(point.X - GlobalPosition.X) <= Size.X * 0.5f && Mathf.Abs(point.Y - GlobalPosition.Y) <= Size.Y * 0.5f;
}
