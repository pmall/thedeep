namespace TheDeep;

/// <summary>
/// Pure: a button held for a while to confirm an act (an <see cref="Seconds"/>-long hold on a prompt). Progress fills only while
/// the button is held and the act is offered, and only for a press that began while it was offered: a button already held when the
/// offer comes counts nothing until it is let go. Letting go, or the offer going, drops the progress to zero at once, so taps never
/// add up. A full hold completes once; the next needs a new press.
/// </summary>
public sealed class HoldProgress
{
    private bool _armed;

    /// <summary>A hold of <paramref name="seconds"/> (above zero).</summary>
    public HoldProgress(float seconds)
    {
        if (!(seconds > 0f))
        {
            throw new System.ArgumentOutOfRangeException(nameof(seconds), seconds, "a hold lasts some time");
        }

        Seconds = seconds;
    }

    /// <summary>How long the button must be held, seconds.</summary>
    public float Seconds { get; }

    /// <summary>How full the hold is, 0–1.</summary>
    public float Fraction { get; private set; }

    /// <summary>Advances by <paramref name="dtSeconds"/>; true on the step the hold completes.</summary>
    public bool Step(bool held, bool offered, float dtSeconds)
    {
        if (!offered)
        {
            Fraction = 0f;
            _armed = false;
            return false;
        }

        if (!held)
        {
            Fraction = 0f;
            _armed = true;
            return false;
        }

        if (!_armed)
        {
            return false;
        }

        Fraction = System.MathF.Min(1f, Fraction + dtSeconds / Seconds);
        if (Fraction < 1f)
        {
            return false;
        }

        _armed = false;
        return true;
    }
}
