using System;

namespace TheDeep;

/// <summary>
/// The strike of the behavior vocabulary: a readable wind-up, a fast blow, a slow recovery, then a cooldown.
/// The wind-up is the player's warning, so it is never skipped. <see cref="Channel"/> drives the animation
/// (about 0.6 at the end of the wind-up, 1 at the blow); <see cref="Update"/> reports the tick the blow lands.
/// </summary>
public sealed class StrikeAction
{
    private float _time = -1f;
    private float _cooldown;

    public float WindUp { get; init; } = 0.45f;
    public float Blow { get; init; } = 0.12f;
    public float Recover { get; init; } = 0.6f;
    public float Cooldown { get; init; } = 0.8f;

    /// <summary>Between the start of the wind-up and the end of the recovery.</summary>
    public bool Active => _time >= 0f;

    public bool Ready => !Active && _cooldown <= 0f;

    /// <summary>The strike's animation channel, 0 to 1.</summary>
    public float Channel { get; private set; }

    public MachineAct Act => !Active ? MachineAct.Idle
        : _time < WindUp ? MachineAct.WindUp
        : _time < WindUp + Blow ? MachineAct.Strike
        : MachineAct.Recover;

    /// <summary>Starts the wind-up if ready; returns whether it started.</summary>
    public bool Start()
    {
        if (!Ready)
        {
            return false;
        }

        _time = 0f;
        return true;
    }

    /// <summary>Advances by <paramref name="delta"/> seconds; returns true on the tick the blow lands.</summary>
    public bool Update(float delta)
    {
        if (!Active)
        {
            _cooldown = MathF.Max(0f, _cooldown - delta);
            Channel = 0f;
            return false;
        }

        float before = _time;
        _time += delta;
        float blowEnd = WindUp + Blow;
        bool hit = before < blowEnd && _time >= blowEnd;
        if (_time < WindUp)
        {
            float t = _time / WindUp;
            Channel = 0.6f * t * t * (3f - 2f * t);
        }
        else if (_time < blowEnd)
        {
            Channel = 0.6f + 0.4f * (_time - WindUp) / Blow;
        }
        else if (_time < blowEnd + Recover)
        {
            float t = (_time - blowEnd) / Recover;
            Channel = 1f - t * t * (3f - 2f * t);
        }
        else
        {
            _time = -1f;
            _cooldown = Cooldown;
            Channel = 0f;
        }

        return hit;
    }

    /// <summary>Back to rest, ready (respawn).</summary>
    public void Reset()
    {
        _time = -1f;
        _cooldown = 0f;
        Channel = 0f;
    }
}
