namespace TheDeep;

/// <summary>Where a melee swing is in its timing: ready for the next, winding up, hitting, or recovering.</summary>
public enum SwingPhase
{
    Ready,
    WindUp,
    Active,
    Recover,
}

/// <summary>
/// The timing of one melee swing, pure: a short wind-up, the window in which it hits, a recovery, then a
/// cooldown before the next. A press once the swing hits (its window, its recovery, the cooldown) is buffered so swings chain
/// without dropped inputs; one during the wind-up is a double tap and is dropped.
/// </summary>
public sealed class Swing
{
    private float _time = float.MaxValue;
    private bool _queued;

    /// <summary>A swing with the given phase lengths, all in seconds; it starts ready.</summary>
    public Swing(float windUp, float active, float recover, float cooldown)
    {
        WindUp = windUp;
        ActiveTime = active;
        Recover = recover;
        Cooldown = cooldown;
    }

    /// <summary>Seconds from the press until the swing can hit.</summary>
    public float WindUp { get; }
    /// <summary>Seconds during which the swing hits.</summary>
    public float ActiveTime { get; }
    /// <summary>Seconds after the hitting window before the swing is over; a press in it is buffered.</summary>
    public float Recover { get; }
    /// <summary>Seconds after the recovery before the next swing can start.</summary>
    public float Cooldown { get; }

    /// <summary>Counts swings, so a hit list can tell one swing from the next.</summary>
    public int Count { get; private set; }

    /// <summary>Seconds since the swing started.</summary>
    public float Time => _time;

    /// <summary>Where the swing is now, from the time since it started.</summary>
    public SwingPhase Phase =>
        _time < WindUp ? SwingPhase.WindUp
        : _time < WindUp + ActiveTime ? SwingPhase.Active
        : _time < WindUp + ActiveTime + Recover ? SwingPhase.Recover
        : SwingPhase.Ready;

    /// <summary>0 to 1 through the whole swing (for the visual sweep).</summary>
    public float Progress => Phase == SwingPhase.Ready ? 1f : _time / (WindUp + ActiveTime + Recover);

    /// <summary>Whether a new swing can start now: none is under way and the cooldown has passed.</summary>
    public bool CanStart => _time >= WindUp + ActiveTime + Recover + Cooldown;

    /// <summary>Asks for a swing: starts now if possible, else is queued for when it is (from the hit window on).</summary>
    public bool Press()
    {
        if (CanStart)
        {
            Begin();
            return true;
        }

        if (Phase != SwingPhase.WindUp)
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

    /// <summary>Ends the swing at once and drops a buffered press (the next can start right away).</summary>
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
