using System;

namespace TheDeep;

/// <summary>
/// A timer that counts down to zero. Used for coyote time, input buffers and cooldowns.
/// Pure logic, unit tested.
/// </summary>
public sealed class Countdown
{
    /// <summary>Time left before it reaches zero, in seconds; never negative.</summary>
    public float Remaining { get; private set; }

    /// <summary>Whether it is still running: time is left.</summary>
    public bool Active => Remaining > 0f;

    /// <summary>Restarts it with <paramref name="duration"/> seconds left; a negative duration leaves it at zero.</summary>
    public void Start(float duration) => Remaining = MathF.Max(0f, duration);

    /// <summary>Runs it down by <paramref name="delta"/> seconds, stopping at zero.</summary>
    public void Tick(float delta) => Remaining = MathF.Max(0f, Remaining - delta);

    /// <summary>Ends it now.</summary>
    public void Clear() => Remaining = 0f;
}
