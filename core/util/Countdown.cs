using System;

namespace TheDeep;

/// <summary>
/// A timer that counts down to zero. Used for coyote time, input buffers and cooldowns.
/// Pure logic, unit tested.
/// </summary>
public sealed class Countdown
{
    public float Remaining { get; private set; }

    public bool Active => Remaining > 0f;

    public void Start(float duration) => Remaining = MathF.Max(0f, duration);

    public void Tick(float delta) => Remaining = MathF.Max(0f, Remaining - delta);

    public void Clear() => Remaining = 0f;
}
