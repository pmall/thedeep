using Godot;

namespace TheDeep;

/// <summary>
/// One line of text as data (docs/roadmap.md#audio): a stable id, who voices it, the words, and
/// optional delivery notes, so voices can be generated per id in a batch and regenerated when text changes.
/// <see cref="Speaker"/> is for those voices only, never shown: a name on screen is a line of its own (a <c>speaker.</c> id, named
/// by a <see cref="DialogueLine"/> or a <see cref="Proclamation"/>).
/// </summary>
[GlobalClass]
public partial class TextLine : Resource
{
    [Export] public StringName Id { get; set; } = "";
    [Export] public string Speaker { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string Text { get; set; } = "";
    [Export] public string Notes { get; set; } = "";
}
