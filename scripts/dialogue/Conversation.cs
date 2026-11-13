using Godot;

namespace TheDeep;

/// <summary>
/// Someone to talk to (a dialogue brick): standing near offers its prompt (an <see cref="ActionPrompt"/>, "Talk"); talking (interact)
/// plays <see cref="Lines"/> in turn in the <see cref="DialogueBox"/>, each line naming its own speaker (<see cref="DialogueLine"/>),
/// advanced by the player (<see cref="Conversations"/>). Make it a child of whoever speaks (a machine), and it goes where they go; the
/// prompt stands over them, or by the pilgrim when they hover high or low (<see cref="TalkPrompt"/>). With <see cref="Once"/>, it is
/// offered until it has been heard to its end, then never again (remembered in the save).
/// </summary>
public partial class Conversation : Node3D, INamesLines
{
    /// <summary>The lines, in the order they are spoken, each with who speaks it.</summary>
    [Export] public DialogueLine[] Lines { get; set; } = System.Array.Empty<DialogueLine>();

    /// <inheritdoc/>
    public System.Collections.Generic.IEnumerable<StringName> LineIds
    {
        get
        {
            foreach (DialogueLine line in Lines)
            {
                yield return line.Speaker;
                yield return line.Line;
            }
        }
    }

    /// <summary>How near the pilgrim must stand, meters on the play plane.</summary>
    [Export] public float Range { get; set; } = 2.5f;

    [Export] public bool Once { get; set; }

    /// <summary>"Talk", offered while the pilgrim may talk; where it stands is <see cref="Place"/>'s.</summary>
    [Export] public ActionPrompt Prompt { get; set; } = null!;

    /// <summary>Where the prompt stands against the speaker and the pilgrim, meters.</summary>
    [ExportGroup("Prompt")]
    [Export] public float Body { get; set; } = 2f;

    [Export] public float Overhead { get; set; } = 2.3f;

    [Export] public float Lean { get; set; } = 0.6f;

    /// <summary>Heard to its end, and <see cref="Once"/>: no longer offered.</summary>
    public bool Spent { get; set; }

    /// <summary>Whether feet at <paramref name="feet"/> are near enough to talk.</summary>
    public bool Near(Vector3 feet) => new Vector2(feet.X - GlobalPosition.X, feet.Y - GlobalPosition.Y).Length() <= Range;

    /// <summary>Stands the prompt for the pilgrim's feet at <paramref name="feet"/>: over the speaker, or by the pilgrim.</summary>
    public void Place(Vector3 feet)
    {
        (System.Numerics.Vector2 at, int reach) = new TalkPrompt(Body, Overhead, Lean)
            .Place(new System.Numerics.Vector2(GlobalPosition.X, GlobalPosition.Y), new System.Numerics.Vector2(feet.X, feet.Y));
        Prompt.GlobalPosition = new Vector3(at.X, at.Y, GlobalPosition.Z + PromptDepth);
        Prompt.Reach = reach;
    }

    /// <summary>How far before the play plane the prompt stands, meters, so it is never inside the speaker.</summary>
    private const float PromptDepth = 0.6f;
}
