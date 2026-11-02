using System;
using System.Collections.Generic;

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

/// <summary>
/// A conversation in play, pure: its lines in turn, each revealed letter by letter (<see cref="LettersPerSecond"/>). A press shows the
/// line being revealed whole at once, or, once it is whole, moves on to the next; after the last, the conversation is over. The texts
/// come from the line book; who speaks each is the adapter's to show.
/// </summary>
public sealed class DialogueFlow
{
    private readonly IReadOnlyList<string> _texts;
    private float _revealed;

    public DialogueFlow(IReadOnlyList<string> texts, float lettersPerSecond)
    {
        if (texts.Count == 0)
        {
            throw new ArgumentException("a conversation has at least one line", nameof(texts));
        }

        _texts = texts;
        LettersPerSecond = Math.Max(1f, lettersPerSecond);
    }

    public float LettersPerSecond { get; }

    /// <summary>The line being spoken (0-based).</summary>
    public int Index { get; private set; }

    public int Count => _texts.Count;

    public string Text => _texts[Index];

    /// <summary>How many of the line's letters show.</summary>
    public int Shown => Math.Min(Text.Length, (int)_revealed);

    /// <summary>The whole line shows: a press moves on.</summary>
    public bool Whole => Shown >= Text.Length;

    public bool Over { get; private set; }

    /// <summary>Reveals more of the line, <paramref name="seconds"/> later.</summary>
    public void Tick(float seconds)
    {
        if (!Over)
        {
            _revealed = Math.Min(Text.Length, _revealed + seconds * LettersPerSecond);
        }
    }

    public DialoguePress Press()
    {
        if (Over)
        {
            return DialoguePress.Ended;
        }

        if (!Whole)
        {
            _revealed = Text.Length;
            return DialoguePress.Completed;
        }

        if (Index + 1 >= _texts.Count)
        {
            Over = true;
            return DialoguePress.Ended;
        }

        Index++;
        _revealed = 0f;
        return DialoguePress.Next;
    }
}
