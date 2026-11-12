using Godot;

namespace TheDeep;

/// <summary>
/// A plate of scripture on a lectern, copied by the machines: standing at it offers its prompt (an <see cref="ActionPrompt"/>, "Read"), and reading (interact)
/// shows its words in the banner (<see cref="Session"/>). Lore, nothing more. The node is where the pilgrim stands
/// to read, on the floor in front of the lectern. Its words are lines in the line book.
/// </summary>
public partial class ScripturePlate : Node3D, INamesLines
{
    [Export] public OmniLight3D Light { get; set; } = null!;
    [Export] public ActionPrompt Prompt { get; set; } = null!;

    /// <summary>Who wrote it (the banner's title) and what it says.</summary>
    [Export] public StringName TitleLine { get; set; } = "";
    [Export] public StringName TextLine { get; set; } = "";

    /// <inheritdoc/>
    public System.Collections.Generic.IEnumerable<StringName> LineIds => new[] { TitleLine, TextLine };

    private float _baseEnergy;
    private float _read;

    public override void _Ready()
    {
        _baseEnergy = Light.LightEnergy;
    }

    /// <summary>The glyphs brighten while their words are shown.</summary>
    public void Read() => _read = 1f;

    public override void _Process(double delta)
    {
        _read = Mathf.Max(0f, _read - (float)delta / 5f);
        Light.LightEnergy = _baseEnergy * (1f + 2f * _read);
    }
}
