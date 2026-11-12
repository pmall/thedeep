using Godot;

namespace TheDeep;

/// <summary>
/// Someone to talk to (a dialogue brick): standing near offers its prompt (an <see cref="ActionPrompt"/>, "Talk"); talking (interact) plays <see cref="Lines"/> in turn in the
/// <see cref="DialogueBox"/>, each line's speaker its own (the line book's), advanced by the player (<see cref="Conversations"/>).
/// Make it a child of whoever speaks (a machine), and it goes where they go. With <see cref="Once"/>, it is offered until it has been
/// heard to its end, then never again (remembered in the save).
/// </summary>
public partial class Conversation : Node3D, INamesLines
{
    /// <summary>The lines, in the order they are spoken (ids in the line book).</summary>
    [Export] public StringName[] Lines { get; set; } = System.Array.Empty<StringName>();

    /// <inheritdoc/>
    public System.Collections.Generic.IEnumerable<StringName> LineIds => Lines;

    /// <summary>How near the pilgrim must stand, meters on the play plane.</summary>
    [Export] public float Range { get; set; } = 2.5f;

    [Export] public bool Once { get; set; }

    /// <summary>"Talk", offered over the speaker while the pilgrim may talk.</summary>
    [Export] public ActionPrompt Prompt { get; set; } = null!;

    /// <summary>Heard to its end, and <see cref="Once"/>: no longer offered.</summary>
    public bool Spent { get; set; }

    /// <summary>Whether feet at <paramref name="feet"/> are near enough to talk.</summary>
    public bool Near(Vector3 feet) => new Vector2(feet.X - GlobalPosition.X, feet.Y - GlobalPosition.Y).Length() <= Range;
}
