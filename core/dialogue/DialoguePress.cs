
namespace TheDeep;

/// <summary>What a press did to a conversation (<see cref="DialogueFlow.Press"/>).</summary>
public enum DialoguePress
{
    /// <summary>The line being revealed is shown whole at once.</summary>
    Completed,

    /// <summary>On to the next line.</summary>
    Next,

    /// <summary>That was the last line: the conversation is over.</summary>
    Ended,
}
