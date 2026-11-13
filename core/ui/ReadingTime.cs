using System;

namespace TheDeep;

/// <summary>
/// Pure: how long words said on screen are held, from their length: long enough to read them at a steady pace, never shorter than a
/// glance. What the pace and the glance are is the showing brick's data (<c>Banner</c>).
/// </summary>
public static class ReadingTime
{
    /// <summary>
    /// Seconds to hold <paramref name="letters"/> letters read at <paramref name="lettersPerSecond"/>, at least
    /// <paramref name="minimumSeconds"/>.
    /// </summary>
    public static float Seconds(int letters, float lettersPerSecond, float minimumSeconds)
    {
        if (lettersPerSecond <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(lettersPerSecond), lettersPerSecond, "words are read at some pace");
        }

        return MathF.Max(minimumSeconds, Math.Max(0, letters) / lettersPerSecond);
    }
}
