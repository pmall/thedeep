using Godot;

namespace TheDeep;

/// <summary>
/// A plate of scripture on a lectern, copied by the machines: standing at it shows "Read", and reading (interact)
/// shows its words in the banner (<see cref="Session"/>). Lore, nothing more. The node is where the pilgrim stands
/// to read, on the floor in front of the lectern. Its words are lines in the line book.
/// </summary>
public partial class ScripturePlate : Node3D
{
    [Export] public OmniLight3D Light { get; set; } = null!;
    [Export] public Label3D Prompt { get; set; } = null!;

    /// <summary>Who wrote it (the banner's title) and what it says.</summary>
    [Export] public StringName TitleLine { get; set; } = "";
    [Export] public StringName TextLine { get; set; } = "";

    private float _promptShown;
    private bool _promptWanted;
    private float _baseEnergy;
    private float _read;

    public override void _Ready()
    {
        _baseEnergy = Light.LightEnergy;
        Prompt.Text = LineBook.Shared.Find("ui.read")?.Text ?? "";
        Prompt.Modulate = Prompt.Modulate with { A = 0f };
    }

    public void ShowPrompt(bool shown) => _promptWanted = shown;

    /// <summary>The glyphs brighten while their words are shown.</summary>
    public void Read() => _read = 1f;

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _promptShown = Motion.Approach(_promptShown, _promptWanted ? 1f : 0f, dt * 4f);
        Prompt.Modulate = Prompt.Modulate with { A = _promptShown * _promptShown * (3f - 2f * _promptShown) * 0.85f };
        _read = Mathf.Max(0f, _read - dt / 5f);
        Light.LightEnergy = _baseEnergy * (1f + 2f * _read);
    }
}
