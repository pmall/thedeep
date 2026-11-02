using Godot;

namespace TheDeep;

/// <summary>
/// Lines of text as data, found by id: text is never hard-coded in scenes or scripts. The game's own book holds the lines every
/// world shares (its screens' words, the abilities, the power-ups, the species' creeds); a world's book (<see cref="WorldDefinition.Lines"/>)
/// holds its content's lines and extends the game's through <see cref="Base"/>, so a line it gives itself wins over the game's.
/// </summary>
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

    /// <summary>The book this one extends (a world's: the game's), asked for the lines this one doesn't have; none for the game's own.</summary>
    [Export] public LineBook? Base { get; set; }

    /// <summary>This book's own lines.</summary>
    [Export] public TextLine[] Lines { get; set; } = System.Array.Empty<TextLine>();

    /// <summary>The line with <paramref name="id"/>, from this book or else its <see cref="Base"/>, or null.</summary>
    public TextLine? Find(StringName id)
    {
        foreach (TextLine line in Lines)
        {
            if (line.Id == id)
            {
                return line;
            }
        }

        return Base?.Find(id);
    }
}
