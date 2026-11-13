using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// Pure: where a conversation's "Talk" prompt stands on the play plane, so it is always by the pilgrim and on screen. A speaker about
/// level with the pilgrim (within <see cref="Body"/> of its feet, up or down) has the prompt over itself, like any prop's; one higher
/// or lower (a machine that flies) has it over the pilgrim, leaning toward the speaker and reaching that way, never metres overhead.
/// Meters, y up.
/// </summary>
/// <param name="Body">How far up or down the speaker may be from the pilgrim's feet and still carry the prompt itself: about a body's height.</param>
/// <param name="Overhead">How high over the feet it stands (the speaker's origin, or the pilgrim's).</param>
/// <param name="Lean">How far from the pilgrim toward the speaker it stands, when it stands by the pilgrim.</param>
public readonly record struct TalkPrompt(float Body, float Overhead, float Lean)
{
    /// <summary>
    /// Where the prompt stands for a speaker whose conversation stands at <paramref name="speaker"/> and the pilgrim's feet at
    /// <paramref name="feet"/>, and which way it reaches from there (0 centered, +1 east, −1 west).
    /// </summary>
    public (Vector2 At, int Reach) Place(Vector2 speaker, Vector2 feet)
    {
        if (MathF.Abs(speaker.Y - feet.Y) <= Body)
        {
            return (speaker + new Vector2(0f, Overhead), 0);
        }

        int toward = speaker.X >= feet.X ? 1 : -1;
        return (new Vector2(feet.X + toward * Lean, feet.Y + Overhead), toward);
    }
}
