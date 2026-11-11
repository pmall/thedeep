using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// A keeper that hovers out of reach and dives (the Archivist, a boss placeholder): it drifts high over the player,
/// shadowing them. Every <see cref="Interval"/> it stops dead and shudders (the wind-up, readable), takes aim at where the
/// player is at the end of it, and dives along a straight line through that point and on past it, striking whatever it
/// touches. At the bottom of the dive it hangs low and still (the opening), then climbs back to drift. Every
/// <see cref="Flurry"/> dives it turns and dives again at once, after a shorter wind-up. It wakes when the player comes within
/// <see cref="SightRange"/>, seen or not.
/// </summary>
public sealed class SwoopBrain : MachineBrain
{
    private enum Phase { Waiting, Drift, Wind, Dive, Hang }

    private Phase _phase;
    private float _timer;
    private Vector2 _aim;
    private Vector2 _from;
    private int _dives;
    private bool _again;
    private float _alert;

    /// <summary>It wakes when the player comes within this distance, meters, whether it can see them or not.</summary>
    public float SightRange { get; init; } = 16f;

    /// <summary>How high over the player's chest it drifts, meters, and how far to the side.</summary>
    public float Altitude { get; init; } = 6f;
    /// <summary>How far to the side of the player it drifts, meters.</summary>
    public float Offset { get; init; } = 3f;

    /// <summary>Its speed while drifting over the player, m/s.</summary>
    public float DriftSpeed { get; init; } = 4f;

    /// <summary>Seconds of drifting between dives.</summary>
    public float Interval { get; init; } = 2.4f;

    /// <summary>Seconds it shudders in place before a dive: the player's warning.</summary>
    public float WindUp { get; init; } = 0.7f;
    /// <summary>Seconds of wind-up before the second dive of a double.</summary>
    public float QuickWindUp { get; init; } = 0.4f;
    /// <summary>Its speed along the dive's line, m/s.</summary>
    public float DiveSpeed { get; init; } = 15f;

    /// <summary>How far past its aim a dive carries on, meters.</summary>
    public float Overshoot { get; init; } = 2.5f;

    /// <summary>The longest a dive lasts, seconds (a wall stops it sooner).</summary>
    public float DiveTime { get; init; } = 1.1f;

    /// <summary>Seconds it hangs still after a dive: the opening.</summary>
    public float Hang { get; init; } = 1.4f;

    /// <summary>Every this many dives, a second one follows at once; 0 for never.</summary>
    public int Flurry { get; init; } = 3;

    /// <summary>Whether it has seen the player (the fight is on).</summary>
    public bool Engaged => _phase != Phase.Waiting;

    public override Intent Think(in Perception p, float delta)
    {
        var intent = new Intent { Rouse = 1f };
        if (_phase == Phase.Waiting)
        {
            // A keeper in its own hall: it knows the pilgrim is there without seeing it (stacks and shelves block sight).
            if (Senses.PlayerDistance(p) <= SightRange)
            {
                _phase = Phase.Drift;
                _timer = Interval;
            }
            else
            {
                Act = MachineAct.Hover;
                intent.Act = Act;
                intent.Work = 0.5f + 0.5f * MathF.Sin(p.Time * 1.3f);
                return intent;
            }
        }

        if (p.Player is not { } player)
        {
            Act = MachineAct.Hover;
            intent.Act = Act;
            return intent;
        }

        _timer -= delta;
        int side = p.Position.X >= player.Position.X ? 1 : -1;
        switch (_phase)
        {
            case Phase.Drift:
                Act = MachineAct.Hunt;
                intent.Velocity = Steering.Arrive(p.Position, player.Position + new Vector2(side * Offset, Altitude), DriftSpeed, 2f);
                intent.Facing = -side;
                if (_timer <= 0f)
                {
                    Begin(Phase.Wind, WindUp);
                }

                break;

            case Phase.Wind:
                Act = MachineAct.WindUp;
                intent.Facing = -side;
                intent.Strike = 0.6f * (1f - _timer / MathF.Max(_again ? QuickWindUp : WindUp, 0.01f));
                if (_timer <= 0f)
                {
                    // The aim is where the player is now: past it by the overshoot.
                    _from = p.Position;
                    Vector2 toward = player.Position - p.Position;
                    float length = MathF.Max(toward.Length(), 0.01f);
                    _aim = player.Position + toward / length * Overshoot;
                    Begin(Phase.Dive, MathF.Min(DiveTime, (length + Overshoot) / DiveSpeed));
                }

                break;

            case Phase.Dive:
                Act = MachineAct.Strike;
                Vector2 line = _aim - _from;
                intent.Velocity = line.LengthSquared() > 1e-4f ? Vector2.Normalize(line) * DiveSpeed : Vector2.Zero;
                intent.Facing = line.X >= 0f ? 1 : -1;
                intent.Strike = 1f;
                intent.Hit = true;
                if (_timer <= 0f)
                {
                    _dives++;
                    if (!_again && Flurry > 0 && _dives % Flurry == 0)
                    {
                        _again = true;
                        Begin(Phase.Wind, QuickWindUp);
                    }
                    else
                    {
                        _again = false;
                        Begin(Phase.Hang, Hang);
                    }
                }

                break;

            case Phase.Hang:
                Act = MachineAct.Recover;
                intent.Strike = 0.3f * MathF.Max(_timer / MathF.Max(Hang, 0.01f), 0f);
                if (_timer <= 0f)
                {
                    Begin(Phase.Drift, Interval);
                }

                break;
        }

        _alert = Motion.Damp(_alert, _phase == Phase.Hang ? 0.4f : 1f, 6f, delta);
        intent.Alert = _alert;
        intent.LookAt = player.Position;
        intent.Act = Act;
        return intent;
    }

    private void Begin(Phase phase, float seconds)
    {
        _phase = phase;
        _timer = seconds;
    }
}
