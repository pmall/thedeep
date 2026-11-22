namespace TheDeep;

/// <summary>
/// The pace a figure's pose is shown at: every tick (smooth), or held for <see cref="HoldSeconds"/> and then jumped to
/// where the motion has got to, as drawn animation is shown on twos or threes. A change of pose shows at once, so a
/// strike or a jump never waits out a hold. The time held is never lost: the next shown pose catches up with all of it.
/// Godot-free.
/// </summary>
public sealed class PoseClock
{
    /// <summary>Within this of the hold, in seconds, a pose is due: ticks summed in floats fall a hair short of it.</summary>
    private const float Slack = 1e-4f;

    private float _pending;

    /// <summary>How long each shown pose holds, in seconds; 0 shows every tick.</summary>
    public float HoldSeconds { get; set; }

    /// <summary>
    /// One tick of <paramref name="deltaSeconds"/>: the seconds to advance the shown pose by now, or 0 while it holds.
    /// <paramref name="changed"/> says the figure went to another pose this tick.
    /// </summary>
    public float Tick(float deltaSeconds, bool changed)
    {
        _pending += deltaSeconds;
        if (HoldSeconds > 0f && !changed && _pending + Slack < HoldSeconds)
        {
            return 0f;
        }

        float advance = _pending;
        _pending = 0f;
        return advance;
    }

    /// <summary>Forgets the time held (a snap to a still stance).</summary>
    public void Reset() => _pending = 0f;
}
