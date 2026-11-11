using System;
using System.Collections.Generic;

namespace TheDeep;

/// <summary>
/// The fight's rule, pure (docs/gameplay.md#fight): which strike a press gives. During a dash, a dash strike; in the air holding
/// down, a down strike; otherwise the next swing of the chain: a press within <see cref="ChainWindow"/> seconds of the last swing's
/// end goes on to the second, then the finisher, after which the chain starts over (so does a pause, a dash strike or a down strike).
/// Each kind's numbers are given (the tuning's, <see cref="StrikeShape"/>).
/// </summary>
public sealed class Combo
{
    private readonly IReadOnlyDictionary<StrikeKind, StrikeShape> _shapes;
    private int _step;
    private float _sinceEnd = float.MaxValue;

    /// <summary>A chain with the given shape for each kind of strike (every kind is needed) and a chain window of
    /// <paramref name="chainWindow"/> seconds.</summary>
    public Combo(IReadOnlyDictionary<StrikeKind, StrikeShape> shapes, float chainWindow)
    {
        foreach (StrikeKind kind in Enum.GetValues<StrikeKind>())
        {
            if (!shapes.ContainsKey(kind))
            {
                throw new ArgumentException($"no shape for the {kind} strike", nameof(shapes));
            }
        }

        _shapes = shapes;
        ChainWindow = chainWindow;
    }

    /// <summary>Seconds after a swing ends during which a press chains on.</summary>
    public float ChainWindow { get; }

    /// <summary>The strike a press gives now (and the chain moves on).</summary>
    public StrikeShape Next(bool dashing, bool airborne, bool holdingDown)
    {
        if (dashing)
        {
            _step = 0;
            return _shapes[StrikeKind.Dash];
        }

        if (airborne && holdingDown)
        {
            _step = 0;
            return _shapes[StrikeKind.Down];
        }

        _step = _sinceEnd <= ChainWindow ? _step % 3 + 1 : 1;
        return _shapes[_step switch { 1 => StrikeKind.Swing, 2 => StrikeKind.Second, _ => StrikeKind.Finisher }];
    }

    /// <summary>A strike's recovery ended: the chain window opens.</summary>
    public void Ended() => _sinceEnd = 0f;

    /// <summary>Runs the chain window down by <paramref name="seconds"/>; once it has passed, the next press starts the chain
    /// over.</summary>
    public void Tick(float seconds)
    {
        if (_sinceEnd < float.MaxValue)
        {
            _sinceEnd += seconds;
        }
    }

    /// <summary>The chain starts over (a respawn, a hurt).</summary>
    public void Reset()
    {
        _step = 0;
        _sinceEnd = float.MaxValue;
    }
}
