using System;

namespace TheDeep;

/// <summary>
/// Pure: how fast a spoken line is revealed, letter by letter, and where it breathes: a letter takes <see cref="LettersPerSecond"/>'s
/// share of a second, and the first letter after a sentence's end (. ! ? …) or a comma's (, ; :) waits <see cref="SentencePause"/> or
/// <see cref="CommaPause"/> more, so the words come as they would be said. Only a mark followed by a space pauses (a decimal point,
/// an ellipsis's inner dots and a line's last mark do not).
/// </summary>
/// <param name="LettersPerSecond">Letters revealed a second, at least 1.</param>
/// <param name="SentencePause">Seconds held after a sentence's end, at least zero.</param>
/// <param name="CommaPause">Seconds held after a comma, a semicolon or a colon, at least zero.</param>
public readonly record struct RevealPace(float LettersPerSecond, float SentencePause, float CommaPause)
{
    /// <summary>Seconds letter <paramref name="index"/> of <paramref name="text"/> takes to come, its pause before it included.</summary>
    public float SecondsFor(string text, int index)
    {
        float letter = 1f / Math.Max(1f, LettersPerSecond);
        if (index <= 0 || index >= text.Length || !char.IsWhiteSpace(text[index]))
        {
            return letter;
        }

        return letter + text[index - 1] switch
        {
            '.' or '!' or '?' or '…' => Math.Max(0f, SentencePause),
            ',' or ';' or ':' => Math.Max(0f, CommaPause),
            _ => 0f,
        };
    }
}
