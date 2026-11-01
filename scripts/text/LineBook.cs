using Godot;

namespace TheDeep;

/// <summary>All of a world's lines in one place (<see cref="WorldDefinition.Lines"/>): text is never hard-coded in scenes or scripts.</summary>
[GlobalClass]
public partial class LineBook : Resource
{
    private static LineBook? _shared;

    /// <summary>
    /// The line book of the world being played (props, banners and manuals ask it for lines as rooms come and go). Set by the
    /// <see cref="World"/> as it enters the tree, before any of them is ready.
    /// </summary>
    public static LineBook Shared => _shared ?? throw new System.InvalidOperationException("no world has set its line book yet");

    /// <summary>Makes <paramref name="book"/> the line book every line is read from.</summary>
    public static void Use(LineBook book) => _shared = book;

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
