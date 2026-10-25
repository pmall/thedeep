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

    public HoarderBrain(int seed)
    {
        _random = new Random(seed);
    }

    /// <summary>How far from home it gleans, meters.</summary>
    public float HomeRange { get; init; } = 4f;

    public float WalkSpeed { get; init; } = 1.3f;
    public float FleeSpeed { get; init; } = 6.5f;

    /// <summary>It bolts when the player comes within this distance, meters.</summary>
    public float Wary { get; init; } = 4.5f;

    /// <summary>It stops running this far from the player, meters.</summary>
    public float Safe { get; init; } = 10f;

    /// <summary>Seconds spent pecking at one spot, shortest and longest.</summary>
    public float MinWork { get; init; } = 1.5f;
    public float MaxWork { get; init; } = 3.5f;

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
                    float speed = WalkSpeed * MathF.Min(1f, MathF.Abs(_target - p.Position.X) / 0.4f);
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
                    if (away != _fleeSide && distance < Wary * 0.6f)
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

                if (distance > Wary + 1.5f)
                {
                    _home = p.Position;
                    Begin(MachineAct.Glean, p);
                }

                break;
        }

        bool fleeing = Act is MachineAct.Flee or MachineAct.Cower;
        float peck = Act == MachineAct.Glean ? 0.5f + 0.5f * MathF.Sin(p.Time * 9f) : 0f;
        _work = Motion.Damp(_work, peck, 10f, delta);
        _alert = Motion.Damp(_alert, fleeing ? 1f : 0f, fleeing ? 10f : 2f, delta);
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
            if (MathF.Abs(home.X + offset - p.Position.X) < 0.8f)
            {
                offset = -offset + MathF.Sign(offset + 1e-3f) * 0.8f;
            }

            _target = home.X + offset;
        }
    }
}
