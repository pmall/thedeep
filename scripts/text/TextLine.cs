using Godot;

namespace TheDeep;

/// <summary>
/// One line of text as data (docs/roadmap.md, "Later: audio"): a stable id, who says it, the words, and
/// optional delivery notes, so voices can be generated per id in a batch and regenerated when text changes.
/// </summary>
[GlobalClass]
public partial class TextLine : Resource
{
    [Export] public StringName Id { get; set; } = "";
    [Export] public string Speaker { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string Text { get; set; } = "";
    [Export] public string Notes { get; set; } = "";
}
