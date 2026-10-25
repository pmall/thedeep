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

    public SleeperBrain(StrikeAction strike)
    {
        _strike = strike;
        Act = MachineAct.Sleep;
    }

    public float HearingRange { get; init; } = 10f;

    /// <summary>Impact speed (m/s, after fading with distance) that wakes it.</summary>
    public float WakeThreshold { get; init; } = 7f;

    /// <summary>Range at which it keeps tracking an awake target, meters.</summary>
    public float TrackRange { get; init; } = 9f;

    public float Speed { get; init; } = 3f;
    public float Reach { get; init; } = 1.3f;
    public float WakeTime { get; init; } = 0.7f;

    /// <summary>Seconds with nothing to track before it goes back to sleep.</summary>
    public float Patience { get; init; } = 5f;

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
                    intent.Velocity = Steering.Arrive(p.Position, _target, Speed, 0.8f);
                }

                break;
        }

        bool awake = Act != MachineAct.Sleep;
        _rouse = Motion.Damp(_rouse, awake ? 1f : 0f, awake ? 1f / MathF.Max(WakeTime * 0.4f, 0.05f) : 1.2f, delta);
        intent.Act = Act;
        intent.Hit = hit;
        intent.Rouse = _rouse;
        intent.Alert = Act is MachineAct.Hunt or MachineAct.WindUp or MachineAct.Strike or MachineAct.Recover ? 1f : _rouse * 0.5f;
        intent.Strike = _strike.Channel;
        intent.LookAt = awake ? _target : null;
        return intent;
    }
}
