using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// A choir's conductor (the Precentor, a boss placeholder): it beats time where it stands until it sees the
/// player, then never lets them go. It stalks them along the floor to striking distance and brings its baton
/// down after a long, readable wind-up, lunging into the blow; the slow recovery after it is the opening.
/// Every few strikes it gathers itself and strikes twice in a row. It beats time for its choir all along
/// (<see cref="Intent.Signal"/> is the beat).
/// </summary>
public sealed class ConductorBrain : MachineBrain
{
    private readonly StrikeAction _strike;
    private readonly StrikeAction _quick;
    private bool _engaged;
    private int _strikes;
    private bool _again;
    private float _beat;
    private float _alert;

    public ConductorBrain(StrikeAction strike, StrikeAction quick)
    {
        _strike = strike;
        _quick = quick;
    }

    public float SightRange { get; init; } = 14f;

    /// <summary>How far in front its baton reaches, meters.</summary>
    public float Reach { get; init; } = 2.8f;

    /// <summary>Half the height of the band in front where the baton lands, meters (around its chest).</summary>
    public float Band { get; init; } = 2f;

    public float ChestHeight { get; init; } = 1.6f;

    /// <summary>Stalking speed, m/s.</summary>
    public float Speed { get; init; } = 1.6f;

    /// <summary>Speed of the lunge into a blow, m/s.</summary>
    public float Lunge { get; init; } = 7f;

    /// <summary>Every this many strikes, a second, quicker one follows.</summary>
    public int Flurry { get; init; } = 3;

    /// <summary>Seconds per beat while it conducts.</summary>
    public float BeatPeriod { get; init; } = 1.2f;

    /// <summary>Whether it has seen the player (the fight is on).</summary>
    public bool Engaged => _engaged;

    private StrikeAction Current => _quick.Active ? _quick : _strike;

    public override Intent Think(in Perception p, float delta)
    {
        _beat = (_beat + delta / MathF.Max(BeatPeriod, 0.1f)) % 1f;
        if (!_engaged && Senses.SeesPlayer(p, SightRange, behind: SightRange * 0.5f))
        {
            _engaged = true;
        }

        bool hit = _strike.Update(delta) | _quick.Update(delta);
        var intent = new Intent { Signal = _beat, Rouse = 1f };
        if (!_engaged || p.Player is not { } player)
        {
            Act = MachineAct.Idle;
            _alert = Motion.Damp(_alert, 0f, 3f, delta);
            intent.Work = 0.5f + 0.5f * MathF.Sin(_beat * MathF.Tau);
            intent.Alert = _alert;
            intent.Act = Act;
            return intent;
        }

        // Turning only between strikes: a blow, once begun, comes down where it was aimed.
        int toward = player.Position.X >= p.Position.X ? 1 : -1;
        bool striking = _strike.Active || _quick.Active;
        float ahead = (player.Position.X - p.Position.X) * (striking ? p.Facing : toward);
        bool inBand = MathF.Abs(player.Position.Y - (p.Position.Y + ChestHeight)) <= Band;

        if (_again && !_strike.Active && _quick.Start())
        {
            _again = false;
        }
        else if (!striking && ahead <= Reach && inBand && _strike.Start())
        {
            _strikes++;
            _again = Flurry > 0 && _strikes % Flurry == 0;
        }

        StrikeAction current = Current;
        striking = current.Active;
        intent.Facing = striking ? p.Facing : toward;
        intent.Hit = hit;
        intent.Strike = current.Channel;
        if (striking)
        {
            // Still while winding up, a lunge into the blow, then planted through the recovery.
            intent.Velocity = current.Act == MachineAct.Strike && !p.Blocked(p.Facing) ? new Vector2(p.Facing * Lunge, 0f) : Vector2.Zero;
            Act = current.Act;
        }
        else
        {
            float gap = MathF.Abs(player.Position.X - p.Position.X);
            intent.Velocity = gap > Reach * 0.8f && !p.Blocked(toward) ? new Vector2(toward * Speed, 0f) : Vector2.Zero;
            Act = MachineAct.Hunt;
        }

        _alert = Motion.Damp(_alert, 1f, 8f, delta);
        intent.Alert = _alert;
        intent.Work = 0.5f + 0.5f * MathF.Sin(_beat * MathF.Tau) * (striking ? 0f : 0.4f);
        intent.LookAt = player.Position;
        intent.Act = Act;
        return intent;
    }
}
