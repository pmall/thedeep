using System;
using System.Numerics;

namespace TheDeep;

/// <summary>
/// Sleeps folded on its perch until something shakes the room nearby (a hard landing, not footsteps). Then
/// it unfolds, hunts toward the noise and the player along its surface, and strikes when they are in reach.
/// With nothing to hear or see for a while, it settles back to sleep.
/// </summary>
public sealed class SleeperBrain : MachineBrain
{
    private readonly StrikeAction _strike;
    private float _rouse;
    private float _timer;
    private Vector2 _target;

    /// <summary>A sleeper, asleep, that strikes with <paramref name="strike"/> once awake.</summary>
    public SleeperBrain(StrikeAction strike)
    {
        _strike = strike;
        Act = MachineAct.Sleep;
    }

    /// <summary>The distance at which an impact has faded to nothing for it, meters.</summary>
    public float HearingRange { get; init; } = 10f;

    /// <summary>Impact speed (m/s, after fading with distance) that wakes it.</summary>
    public float WakeThreshold { get; init; } = 7f;

    /// <summary>Range at which it keeps tracking an awake target, meters.</summary>
    public float TrackRange { get; init; } = 9f;

    /// <summary>Its hunting speed along its surface, m/s.</summary>
    public float Speed { get; init; } = 3f;
    /// <summary>It strikes when the player is within this distance, meters.</summary>
    public float Reach { get; init; } = 1.3f;
    /// <summary>Seconds it takes to unfold once woken, before it hunts.</summary>
    public float WakeTime { get; init; } = 0.7f;

    /// <summary>Seconds with nothing to track before it goes back to sleep.</summary>
    public float Patience { get; init; } = 5f;

    /// <summary>How close to its target, meters, it starts slowing to stop on it.</summary>
    public float ArriveRadius { get; init; } = 0.8f;

    /// <summary>It unfolds in this fraction of <see cref="WakeTime"/>: its limbs rouse ahead of its hunt.</summary>
    public float RouseFraction { get; init; } = 0.4f;

    /// <summary>How fast it folds back down when it dozes off, per second.</summary>
    public float DozeRate { get; init; } = 1.2f;

    /// <summary>How alert it looks while unfolding, as a fraction of how far it has risen.</summary>
    public float DrowseAlert { get; init; } = 0.5f;

    public override Intent Think(in Perception p, float delta)
    {
        bool hit = _strike.Update(delta);
        Impact? heard = Senses.Hears(p, HearingRange, WakeThreshold);
        var intent = new Intent();
        switch (Act)
        {
            case MachineAct.Sleep:
                if (heard is { } noise)
                {
                    _target = noise.Position;
                    _timer = WakeTime;
                    Act = MachineAct.Wake;
                }

                break;

            case MachineAct.Wake:
                _timer -= delta;
                if (_timer <= 0f)
                {
                    _timer = Patience;
                    Act = MachineAct.Hunt;
                }

                break;

            default:
                if (heard is { } again)
                {
                    _target = again.Position;
                    _timer = Patience;
                }

                if (p.Player is { } player && (player.InSight || Act == MachineAct.Strike) && Senses.PlayerDistance(p) <= TrackRange)
                {
                    _target = player.Position;
                    _timer = Patience;
                }

                if (Senses.PlayerDistance(p) <= Reach)
                {
                    _strike.Start();
                }

                _timer -= delta;
                if (_timer <= 0f && !_strike.Active)
                {
                    Act = MachineAct.Sleep;
                    break;
                }

                Act = _strike.Active ? _strike.Act : MachineAct.Hunt;
                if (!_strike.Active)
                {
                    intent.Velocity = Steering.Arrive(p.Position, _target, Speed, ArriveRadius);
                }

                break;
        }

        bool awake = Act != MachineAct.Sleep;
        _rouse = Motion.Damp(_rouse, awake ? 1f : 0f, awake ? 1f / MathF.Max(WakeTime * RouseFraction, 0.05f) : DozeRate, delta);
        intent.Act = Act;
        intent.Hit = hit;
        intent.Rouse = _rouse;
        intent.Alert = Act is MachineAct.Hunt or MachineAct.WindUp or MachineAct.Strike or MachineAct.Recover ? 1f : _rouse * DrowseAlert;
        intent.Strike = _strike.Channel;
        intent.LookAt = awake ? _target : null;
        return intent;
    }
}
