using Godot;

namespace TheDeep;

/// <summary>
/// Someone to talk to (a dialogue brick): standing near shows "Talk"; talking (interact) plays <see cref="Lines"/> in turn in the
/// <see cref="DialogueBox"/>, each line's speaker its own (the line book's), advanced by the player (<see cref="Conversations"/>).
/// Make it a child of whoever speaks (a machine), and it goes where they go. With <see cref="Once"/>, it is offered until it has been
/// heard to its end, then never again (remembered in the save).
/// </summary>
public partial class Conversation : Node3D
{
    /// <summary>The lines, in the order they are spoken (ids in the line book).</summary>
    [Export] public StringName[] Lines { get; set; } = System.Array.Empty<StringName>();

    /// <summary>How near the pilgrim must stand, meters on the play plane.</summary>
    [Export] public float Range { get; set; } = 2.5f;

    [Export] public bool Once { get; set; }

    /// <summary>"Talk", shown over the speaker while the pilgrim may talk.</summary>
    [Export] public Label3D Prompt { get; set; } = null!;

    private float _shown;
    private bool _wanted;

    /// <summary>Heard to its end, and <see cref="Once"/>: no longer offered.</summary>
    public bool Spent { get; set; }

    public override void _Ready()
    {
        Prompt.Text = LineBook.Shared.Find("ui.talk")?.Text ?? "";
        Prompt.Modulate = Prompt.Modulate with { A = 0f };
    }

    public void ShowPrompt(bool shown) => _wanted = shown;

    /// <summary>Whether feet at <paramref name="feet"/> are near enough to talk.</summary>
    public bool Near(Vector3 feet) => new Vector2(feet.X - GlobalPosition.X, feet.Y - GlobalPosition.Y).Length() <= Range;

    public override void _Process(double delta)
    {
        _shown = Motion.Approach(_shown, _wanted ? 1f : 0f, (float)delta * 4f);
        Prompt.Modulate = Prompt.Modulate with { A = _shown * _shown * (3f - 2f * _shown) * 0.85f };
    }
}
