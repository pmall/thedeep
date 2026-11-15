using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// Gleans scrap around its home: walks to a spot, pecks at it, moves on. It never fights. When the player
/// comes near it bolts away with its hoard, and settles somewhere new once safe; cornered against a wall or
/// a drop, it cowers until the player leaves (or dashes past the other way, if the player steps aside).
/// </summary>
public sealed class HoarderBrain : MachineBrain
{
    private readonly Random _random;
    private Vector2? _home;
    private float _target;
    private float _timer;
    private int _fleeSide = 1;
    private float _work;
    private float _alert;
    private bool _started;

    /// <summary>A hoarder whose choice of spots and pecking times is drawn from <paramref name="seed"/>, so each one gleans
    /// differently and the same one always the same.</summary>
    public HoarderBrain(int seed)
    {
        _random = new Random(seed);
    }

    /// <summary>How far from home it gleans, meters.</summary>
    public float HomeRange { get; init; } = 4f;

    /// <summary>Its speed walking between gleaning spots, m/s.</summary>
    public float WalkSpeed { get; init; } = 1.3f;
    /// <summary>Its speed bolting from the player, m/s.</summary>
    public float FleeSpeed { get; init; } = 6.5f;

    /// <summary>It bolts when the player comes within this distance, meters.</summary>
    public float Wary { get; init; } = 4.5f;

    /// <summary>It stops running this far from the player, meters.</summary>
    public float Safe { get; init; } = 10f;

    /// <summary>Seconds spent pecking at one spot, shortest and longest.</summary>
    public float MinWork { get; init; } = 1.5f;
    /// <summary>The longest it pecks at one spot, seconds.</summary>
    public float MaxWork { get; init; } = 3.5f;

    /// <summary>How close to its spot, meters, it starts slowing to stop on it.</summary>
    public float WalkSlowRadius { get; init; } = 0.4f;

    /// <summary>The shortest walk to a new spot, meters: at least a stride, so it visibly walks.</summary>
    public float MinStride { get; init; } = 0.8f;

    /// <summary>While running, it turns back when the player slips past within this fraction of <see cref="Wary"/>.</summary>
    public float TurnFraction { get; init; } = 0.6f;

    /// <summary>A cowering machine gets up once the player is this much farther than <see cref="Wary"/>, meters.</summary>
    public float CowerMargin { get; init; } = 1.5f;

    /// <summary>How fast it pecks at its spot, radians per second.</summary>
    public float PeckRate { get; init; } = 9f;

    /// <summary>How fast its pecking follows the beat, per second.</summary>
    public float PeckSharpness { get; init; } = 10f;

    /// <summary>How fast its alertness rises when it bolts, per second.</summary>
    public float AlertRise { get; init; } = 10f;

    /// <summary>How fast its alertness fades once it is safe, per second.</summary>
    public float AlertFall { get; init; } = 2f;

    public override Intent Think(in Perception p, float delta)
    {
        Vector2 home = _home ??= p.Position;
        if (!_started)
        {
            _started = true;
            Begin(MachineAct.Glean, p);
        }

        float distance = Senses.PlayerDistance(p);
        bool near = distance <= Wary;
        var intent = new Intent();
        switch (Act)
        {
            case MachineAct.Glean:
            case MachineAct.Wander:
                if (near && p.Player is { } threat)
                {
                    _fleeSide = Steering.AwaySide(p.Position, threat.Position, -p.Facing);
                    Begin(MachineAct.Flee, p);
                    goto case MachineAct.Flee;
                }

                if (Act == MachineAct.Glean)
                {
                    _timer -= delta;
                    if (_timer <= 0f)
                    {
                        Begin(MachineAct.Wander, p);
                    }
                }
                else
                {
                    int side = MathF.Sign(_target - p.Position.X);
                    float speed = WalkSpeed * MathF.Min(1f, MathF.Abs(_target - p.Position.X) / WalkSlowRadius);
                    intent.Velocity = new Vector2(side * speed, 0f);
                    if (MathF.Abs(_target - p.Position.X) < 0.1f || p.Blocked(side))
                    {
                        Begin(MachineAct.Glean, p);
                    }
                }

                break;

            case MachineAct.Flee:
                if (p.Player is { } chaser)
                {
                    // The player got past it: run the other way.
                    int away = Steering.AwaySide(p.Position, chaser.Position, _fleeSide);
                    if (away != _fleeSide && distance < Wary * TurnFraction)
                    {
                        _fleeSide = away;
                    }
                }

                if (p.Blocked(_fleeSide))
                {
                    Begin(MachineAct.Cower, p);
                    intent.Facing = -_fleeSide;
                    break;
                }

                intent.Velocity = new Vector2(_fleeSide * FleeSpeed, 0f);
                if (distance >= Safe)
                {
                    _home = p.Position;
                    Begin(MachineAct.Glean, p);
                }

                break;

            case MachineAct.Cower:
                if (p.Player is { } cornering)
                {
                    int away = Steering.AwaySide(p.Position, cornering.Position, _fleeSide);
                    if (!p.Blocked(away) && away != _fleeSide)
                    {
                        _fleeSide = away;
                        Begin(MachineAct.Flee, p);
                        break;
                    }

                    intent.Facing = -away;
                }

                if (distance > Wary + CowerMargin)
                {
                    _home = p.Position;
                    Begin(MachineAct.Glean, p);
                }

                break;
        }

        bool fleeing = Act is MachineAct.Flee or MachineAct.Cower;
        float peck = Act == MachineAct.Glean ? 0.5f + 0.5f * MathF.Sin(p.Time * PeckRate) : 0f;
        _work = Motion.Damp(_work, peck, PeckSharpness, delta);
        _alert = Motion.Damp(_alert, fleeing ? 1f : 0f, fleeing ? AlertRise : AlertFall, delta);
        intent.Act = Act;
        intent.Work = _work;
        intent.Alert = _alert;
        intent.Rouse = Act == MachineAct.Cower ? 0f : 1f;
        intent.LookAt = fleeing && p.Player is { } seen ? seen.Position : null;
        return intent;
    }

    private void Begin(MachineAct act, in Perception p)
    {
        Act = act;
        if (act == MachineAct.Glean)
        {
            _timer = MinWork + (MaxWork - MinWork) * (float)_random.NextDouble();
        }
        else if (act == MachineAct.Wander)
        {
            Vector2 home = _home ?? p.Position;
            float offset = HomeRange * (2f * (float)_random.NextDouble() - 1f);
            // At least a stride away, so it visibly walks.
            if (MathF.Abs(home.X + offset - p.Position.X) < MinStride)
            {
                offset = -offset + MathF.Sign(offset + 1e-3f) * MinStride;
            }

            _target = home.X + offset;
        }
    }
}
