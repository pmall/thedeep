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

    /// <summary>A praying machine of species <paramref name="kind"/> that bows every <paramref name="period"/> seconds,
    /// starting <paramref name="phase"/> (0 to 1) through the cycle, pulled into step with its neighbors by <paramref name="coupling"/>
    /// (cycles per second at most, see <see cref="PrayerCycle.Coupling"/>), and strikes with <paramref name="strike"/>.</summary>
    public PrayerBrain(string kind, float period, float phase, StrikeAction strike, float coupling = 0.12f)
    {
        Kind = kind;
        _prayer = new PrayerCycle(period, phase) { Coupling = coupling };
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

    /// <summary>The nearest an intruder can be ahead of it and still be struck, meters (behind its back is safe).</summary>
    public float MinAhead { get; init; } = 0.1f;

    /// <summary>How fast it bows down into its prayer, per second.</summary>
    public float BowRate { get; init; } = 6f;

    /// <summary>How fast it stands up from its bow when it stops praying, per second.</summary>
    public float RiseRate { get; init; } = 12f;

    /// <summary>How fast its alertness rises as it strikes, per second.</summary>
    public float AlertRise { get; init; } = 14f;

    /// <summary>How fast its alertness fades once it is calm, per second.</summary>
    public float AlertFall { get; init; } = 3f;

    /// <summary>Above this alertness (0 to 1) it watches the player, and otherwise gazes ahead.</summary>
    public float WatchAlert { get; init; } = 0.3f;

    /// <summary>Where it gazes while praying: this far ahead of its chest, meters.</summary>
    public float GazeAhead { get; init; } = 3f;

    /// <summary>and this far below it, meters (negative is down).</summary>
    public float GazeDrop { get; init; } = -1.2f;

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
        for (int i = 0; i < p.Neighbors.Count; i++)
        {
            Neighbor n = p.Neighbors[i];
            if (n.Kind == Kind && Vector2.Distance(n.Position, p.Position) <= Congregation)
            {
                _neighborPhases.Add(n.Signal);
            }
        }

        // The prayer goes on underneath, so it stays in step with the others while it watches.
        _prayer.Step(delta, _neighborPhases);
        _bow = Motion.Damp(_bow, praying ? _prayer.Bow : 0f, praying ? BowRate : RiseRate, delta);
        _alert = Motion.Damp(_alert, _strike.Active || _calm > 0f ? 1f : 0f, _strike.Active ? AlertRise : AlertFall, delta);

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
            LookAt = _alert > WatchAlert && p.Player is { } player ? player.Position : chest + new Vector2(p.Facing * GazeAhead, GazeDrop),
        };
    }

    private bool Intruder(in Perception p)
    {
        if (p.Player is not { InSight: true } player)
        {
            return false;
        }

        float ahead = Senses.Ahead(p, player.Position);
        return ahead > MinAhead && ahead <= Reach && MathF.Abs(player.Position.Y - (p.Position.Y + ChestHeight)) <= Band;
    }
}
