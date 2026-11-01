namespace TheDeep;

public enum SwingPhase
{
    Ready,
    WindUp,
    Active,
    Recover,
}

/// <summary>
/// The timing of one melee swing, pure: a short wind-up, the window in which it hits, a recovery, then a
/// cooldown before the next. A press during recovery is buffered so swings chain without dropped inputs.
/// </summary>
public sealed class Swing
{
    private float _time = float.MaxValue;
    private bool _queued;

    public Swing(float windUp, float active, float recover, float cooldown)
    {
        WindUp = windUp;
        ActiveTime = active;
        Recover = recover;
        Cooldown = cooldown;
    }

    public float WindUp { get; }
    public float ActiveTime { get; }
    public float Recover { get; }
    public float Cooldown { get; }

    /// <summary>Counts swings, so a hit list can tell one swing from the next.</summary>
    public int Count { get; private set; }

    /// <summary>Seconds since the swing started.</summary>
    public float Time => _time;

    public SwingPhase Phase =>
        _time < WindUp ? SwingPhase.WindUp
        : _time < WindUp + ActiveTime ? SwingPhase.Active
        : _time < WindUp + ActiveTime + Recover ? SwingPhase.Recover
        : SwingPhase.Ready;

    /// <summary>0 to 1 through the whole swing (for the visual sweep).</summary>
    public float Progress => Phase == SwingPhase.Ready ? 1f : _time / (WindUp + ActiveTime + Recover);

    public bool CanStart => _time >= WindUp + ActiveTime + Recover + Cooldown;

    /// <summary>Asks for a swing: starts now if possible, else is queued for when it is (during the recovery only).</summary>
    public bool Press()
    {
        if (CanStart)
        {
            Begin();
            return true;
        }

        if (Phase == SwingPhase.Recover || Phase == SwingPhase.Ready)
        {
            _queued = true;
        }

        return false;
    }

    /// <summary>Advances time; returns true when a queued swing started.</summary>
    public bool Tick(float delta)
    {
        if (_time < float.MaxValue)
        {
            _time += delta;
        }

        if (_queued && CanStart)
        {
            Begin();
            return true;
        }

        return false;
    }

    public void Cancel()
    {
        _time = float.MaxValue;
        _queued = false;
    }

    private void Begin()
    {
        _time = 0f;
        _queued = false;
        Count++;
    }
}
