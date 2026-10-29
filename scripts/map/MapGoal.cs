using Godot;

namespace TheDeep;

/// <summary>
/// Where the journey ends, for the map solver (docs/long-term.md, objective 2): from every state the pilgrim
/// can reach, a room holding a goal must stay reachable. While the game is being built, the goal sits at the
/// end of what is built (the Brink, for now); the full game will end at the core. With a <see cref="Size"/>, it
/// is also an area: reaching it shows the banner lines (the end of the build), once per visit.
/// </summary>
public partial class MapGoal : Marker3D
{
    /// <summary>Area on the play plane (width, height), centered on the marker; zero for a marker only.</summary>
    [Export] public Vector2 Size { get; set; }

    [Export] public StringName TitleLine { get; set; } = "";
    [Export] public StringName HintLine { get; set; } = "";

    public bool Contains(Vector3 point) =>
        Size != Vector2.Zero && Mathf.Abs(point.X - GlobalPosition.X) <= Size.X * 0.5f && Mathf.Abs(point.Y - GlobalPosition.Y) <= Size.Y * 0.5f;
}
