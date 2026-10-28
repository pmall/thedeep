using Godot;

namespace TheDeep;

/// <summary>All the game's lines in one place (<see cref="Path"/>): text is never hard-coded in scenes or scripts.</summary>
[GlobalClass]
public partial class LineBook : Resource
{
    public const string Path = "res://resources/text/lines.tres";

    private static LineBook? _shared;

    /// <summary>The game's line book, loaded once and kept (props ask it for lines as rooms come and go).</summary>
    public static LineBook Shared => _shared ??= GD.Load<LineBook>(Path);

    [Export] public TextLine[] Lines { get; set; } = System.Array.Empty<TextLine>();

    /// <summary>The line with <paramref name="id"/>, or null.</summary>
    public TextLine? Find(StringName id)
    {
        foreach (TextLine line in Lines)
        {
            if (line.Id == id)
            {
                return line;
            }
        }

        return null;
    }
}
