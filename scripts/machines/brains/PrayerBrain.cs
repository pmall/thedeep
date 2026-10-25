using System;
using System.Collections.Generic;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// Prays in place, bowing toward what it faces, in step with praying neighbors of its kind. It ignores the
/// player behind it; only someone passing in front of it, between it and its devotion, is struck. After
/// the blow it watches a moment, then goes back to prayer.
/// </summary>
public sealed class PrayerBrain : MachineBrain
{
    private readonly PrayerCycle _prayer;
    private readonly StrikeAction _strike;
    private readonly List<float> _neighborPhases = new();
    private float _alert;
    private float _bow;
    private float _calm;

    public PrayerBrain(string kind, float period, float phase, StrikeAction strike)
    {
        Kind = kind;
        _prayer = new PrayerCycle(period, phase) { Coupling = 0.12f };
        _strike = strike;
    }

    /// <summary>The species id: only neighbors of the same kind pray in step.</summary>
    public string Kind { get; }

    /// <summary>How far in front it strikes, meters.</summary>
    public float Reach { get; init; } = 2f;

    /// <summary>Half the height of the band in front where an intruder is struck, meters (around its own chest).</summary>
    public float Band { get; init; } = 1.4f;

    /// <summary>Height of its chest above its feet, meters.</summary>
    public float ChestHeight { get; init; } = 1f;

    /// <summary>Neighbors of its kind within this radius pray in step with it.</summary>
    public float Congregation { get; init; } = 8f;

    /// <summary>Seconds without an intruder before it prays again.</summary>
    public float Calm { get; init; } = 1.6f;

    public override Intent Think(in Perception p, float delta)
    {
        bool intruder = Intruder(p);
        bool hit = _strike.Update(delta);
        if (intruder)
        {
            _strike.Start();
            _calm = Calm;
        }

        _calm = MathF.Max(0f, _calm - delta);
        bool praying = !_strike.Active && _calm <= 0f;

        _neighborPhases.Clear();
        foreach (Neighbor n in p.Neighbors)
        {
            if (n.Kind == Kind && Vector2.Distance(n.Position, p.Position) <= Congregation)
            {
                _neighborPhases.Add(n.Signal);
            }
        }

        // The prayer goes on underneath, so it stays in step with the others while it watches.
        _prayer.Step(delta, _neighborPhases);
        _bow = Motion.Damp(_bow, praying ? _prayer.Bow : 0f, praying ? 6f : 12f, delta);
        _alert = Motion.Damp(_alert, _strike.Active || _calm > 0f ? 1f : 0f, _strike.Active ? 14f : 3f, delta);

        Act = _strike.Active ? _strike.Act : praying ? MachineAct.Pray : MachineAct.Watch;
        Vector2 chest = p.Position + new Vector2(0f, ChestHeight);
        return new Intent
        {
            Act = Act,
            Hit = hit,
            Signal = _prayer.Phase,
            Pray = _bow,
            Strike = _strike.Channel,
            Alert = _alert,
            LookAt = _alert > 0.3f && p.Player is { } player ? player.Position : chest + new Vector2(p.Facing * 3f, -1.2f),
        };
    }

    private bool Intruder(in Perception p)
    {
        if (p.Player is not { InSight: true } player)
        {
            return false;
        }

        float ahead = Senses.Ahead(p, player.Position);
        return ahead > 0.1f && ahead <= Reach && MathF.Abs(player.Position.Y - (p.Position.Y + ChestHeight)) <= Band;
    }
}
