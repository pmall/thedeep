using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// A flyer that, once it has seen the player, follows them through the room at a respectful distance:
/// trailing behind and above, arcing over their head when it changes sides, darting back when the player
/// comes too close. It never attacks. Before it has seen anyone it loiters where it woke.
/// </summary>
public sealed class FollowerBrain : MachineBrain
{
    private Vector2? _home;
    private bool _noticed;
    private int _side = -1;
    private float _alert;

    /// <summary>How far it sees the player, meters, in front or behind; seeing them once, it follows for good.</summary>
    public float SightRange { get; init; } = 12f;

    /// <summary>How far behind the player it keeps, meters.</summary>
    public float Distance { get; init; } = 4f;

    /// <summary>How far above the player's chest it flies, meters.</summary>
    public float Height { get; init; } = 1.6f;

    /// <summary>Closer than this, it darts away, meters.</summary>
    public float Shy { get; init; } = 2.2f;

    /// <summary>Its flying speed when following, m/s; it darts away <see cref="DartFactor"/> times as fast.</summary>
    public float Speed { get; init; } = 6f;

    /// <summary>Player speed (m/s) above which it moves to trail behind them.</summary>
    public float TrailSpeed { get; init; } = 1.5f;

    /// <summary>How much faster than <see cref="Speed"/> it darts away.</summary>
    public float DartFactor { get; init; } = 1.3f;

    /// <summary>How much higher it flies, meters, to pass over the player's head when it changes sides.</summary>
    public float CrossingLift { get; init; } = 1.8f;

    /// <summary>How close to its place by the player, meters, it starts slowing to settle there.</summary>
    public float SlowRadius { get; init; } = 2.5f;

    /// <summary>How wide its lazy loop is while it waits unwoken, meters.</summary>
    public float LoiterSize { get; init; } = 2.5f;

    /// <summary>Its speed drifting around that loop, m/s.</summary>
    public float LoiterSpeed { get; init; } = 1.2f;

    /// <summary>How close to its loop point, meters, it starts slowing.</summary>
    public float LoiterSlowRadius { get; init; } = 1f;

    /// <summary>How alert it looks when startled, 0 to 1.</summary>
    public float StartleAlert { get; init; } = 0.35f;

    /// <summary>How fast its alertness follows, per second.</summary>
    public float AlertRate { get; init; } = 6f;

    public override Intent Think(in Perception p, float delta)
    {
        Vector2 home = _home ??= p.Position;
        if (Senses.SeesPlayer(p, SightRange, behind: SightRange))
        {
            _noticed = true;
        }

        var intent = new Intent { Rouse = 1f };
        if (_noticed && p.Player is { } player)
        {
            if (MathF.Abs(player.Velocity.X) > TrailSpeed)
            {
                _side = -MathF.Sign(player.Velocity.X);
            }

            Vector2 target = player.Position + new Vector2(_side * Distance, Height);
            // Changing sides: rise over the player's head instead of passing through them.
            bool crossing = MathF.Sign(p.Position.X - player.Position.X) != _side && MathF.Abs(p.Position.X - player.Position.X) < Distance;
            if (crossing)
            {
                target.Y += CrossingLift;
            }

            float distance = Vector2.Distance(p.Position, player.Position);
            if (distance < Shy && !crossing)
            {
                Vector2 away = Vector2.Normalize(p.Position - player.Position + new Vector2(0f, 0.3f));
                intent.Velocity = away * Speed * DartFactor;
                Act = MachineAct.Startle;
            }
            else
            {
                intent.Velocity = Steering.Arrive(p.Position, target, Speed, SlowRadius);
                Act = MachineAct.Follow;
            }

            intent.Facing = player.Position.X >= p.Position.X ? 1 : -1;
            intent.LookAt = player.Position;
        }
        else
        {
            intent.Velocity = Steering.Arrive(p.Position, Steering.Loiter(home, p.Time, LoiterSize), LoiterSpeed, LoiterSlowRadius);
            Act = MachineAct.Hover;
        }

        _alert = Motion.Damp(_alert, Act == MachineAct.Startle ? StartleAlert : 0f, AlertRate, delta);
        intent.Act = Act;
        intent.Alert = _alert;
        intent.Work = Act == MachineAct.Follow ? 1f : 0f;
        return intent;
    }
}
