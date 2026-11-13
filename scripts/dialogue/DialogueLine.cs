using Godot;

namespace TheDeep;

/// <summary>
/// One line of a <see cref="Conversation"/>: who speaks it and what they say, both ids of the line book. The speaker is named by a
/// line (its text the name shown, "Lamplighter"), as a <see cref="Proclamation"/>'s is, never a raw name.
/// </summary>
[GlobalClass]
public partial class DialogueLine : Resource
{
    /// <summary>The line naming who speaks (its caption over the words).</summary>
    [Export] public StringName Speaker { get; set; } = "";

    /// <summary>What they say.</summary>
    [Export] public StringName Line { get; set; } = "";
}
