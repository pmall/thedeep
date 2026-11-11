using System;
using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// Scripted burn check in place, in the Basalt (`-- --room Basalt --heat-check`, run by tools/check.sh), through the real input
/// actions: its slag channel (a <see cref="SlagBody"/>, `docs/gameplay.md#slag`) and its vent (a <see cref="HeatZone"/>,
/// `docs/gameplay.md#heat`). Without Heat plating: walking off the bank into the slag burns, and the pilgrim is soon back on the
/// bank it came from, in control; walking into it again and again hurts once per touch, never faster than the grace period, and
/// it is never across nor stuck in it; dropped onto the slag past its middle it still comes back to its own bank; the vent's heat
/// burns too, and the pilgrim says its plating would not bear it. With Heat plating: it stands in the vent's heat unharmed; the slag
/// burns it all the same and sets it back on its bank; a running jump from the bank's edge lands it on the far bank, unharmed.
/// Throughout, control is never gone longer than <see cref="ControlLoss"/>. Each step has a time limit. Prints `HEAT OK` or
/// `HEAT CHECK` errors.
/// </summary>
public partial class HeatCheck : Node
{
    /// <summary>The running game's pilgrim.</summary>
    public required Player Player { get; init; }
    /// <summary>The running game's world: its rooms, and the one loaded.</summary>
    public required World World { get; init; }
    /// <summary>The running game's gate hints.</summary>
    public required GateHints Hints { get; init; }

    /// <summary>The longest the heat may take control away at once (the blow, the veil and the return), seconds.</summary>
    private const float ControlLoss = 0.45f;

    /// <summary>How long the pilgrim keeps walking into the channel (step 3), seconds.</summary>
    private const float Pressing = 4f;

    /// <summary>The gates the pilgrim has said it can't pass (GateHints).</summary>
    private readonly HashSet<Ability> _hinted = new();

    private readonly List<Step> _steps = new();
    private readonly List<string> _problems = new();

    /// <summary>The physics ticks at which a blow hurt the pilgrim, since the check began.</summary>
    private readonly List<int> _hurts = new();

    private SlagBody? _slag;
    private HeatZone? _vent;
    private int _index = -1;
    private int _ticks;
    private int _clock;
    private int _lostSince = -1;
    private int _heldTicks;

    /// <summary>Ticks in control that end a loss of control.</summary>
    private const int RegainTicks = 6;
    private int _longestLoss;
    private bool _crossed;

    private sealed record Step(string What, float Seconds, Action Start, Func<bool> Done);

    /// <summary>Runs before the pilgrim each frame and each physics tick, so what it presses is read the same tick.</summary>
    public HeatCheck()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    private Vector2 P => new(Player.GlobalPosition.X, Player.GlobalPosition.Y);

    /// <summary>The slag channel of the room under check, found on first use.</summary>
    private Box Channel => (_slag ??= World.Room.GetNode<SlagBody>("Slag")).PlayRegion;

    /// <summary>The vent's heat, on the cavern's floor, found on first use.</summary>
    private Box Vent => (_vent ??= World.Room.GetNode<HeatZone>("Heat/Vent")).Region;

    /// <summary>Standing on the west bank, clear of the channel, in control.</summary>
    private bool OnWestBank => Player.InControl && Player.IsOnFloor() && P.X < Channel.MinX - 0.4f && P.Y > Channel.MaxY - 1f;

    public override void _Ready()
    {
        Hints.Said += kind => _hinted.Add(kind);
        Player.Hurt += () => _hurts.Add(_clock);
        Vector3 bank = new(Channel.MinX - 4f, 1f, 0f);
        Vector3 cavern = new(Vent.MaxX + 3.5f, Vent.MinY, 0f);
        int pressingFrom = 0;

        _steps.Add(new("without Heat plating, walking off the bank into the slag burns", 2f,
            () =>
            {
                Player.Abilities.Lock(Ability.HeatPlating);
                Player.Vitality.Restore();
                Player.Respawn(bank, 1);
                Input.ActionPress(InputActions.MoveRight);
            },
            () => _hurts.Count == 1 && Player.Vitality.Current == Player.Vitality.Max - 1));
        _steps.Add(new("and the pilgrim is soon back on the bank it came from, in control (holding toward it and jumping)", 1.5f,
            () =>
            {
                Input.ActionRelease(InputActions.MoveRight);
                Input.ActionPress(InputActions.MoveLeft);
            },
            () =>
            {
                Tap(InputActions.Jump);
                return OnWestBank;
            }));
        _steps.Add(new($"walking into it for {Pressing} s: one health per touch, never faster than the grace, never across nor stuck", Pressing + 0.5f,
            () =>
            {
                Input.ActionRelease(InputActions.MoveLeft);
                Input.ActionRelease(InputActions.Jump);
                Player.Vitality.Restore();
                Input.ActionPress(InputActions.MoveRight);
                pressingFrom = _clock;
            },
            () => _clock - pressingFrom >= Pressing * 60f && Burned(pressingFrom)));
        _steps.Add(new("dropped onto the slag past its middle, it comes back to the bank it came from", 1.5f,
            () =>
            {
                Input.ActionRelease(InputActions.MoveRight);
                Player.Vitality.Restore();
                Player.GlobalPosition = new Vector3(0.5f * (Channel.MinX + Channel.MaxX) + 1f, Channel.MaxY + 0.5f, 0f);
                Player.Velocity = Vector3.Zero;
            },
            () => _ticks > 2 && OnWestBank));
        _steps.Add(new("the vent's heat burns too, and the pilgrim says its plating would not bear it (the gate hint)", 2f,
            () =>
            {
                Player.Vitality.Restore();
                Player.Respawn(cavern, -1);
                Input.ActionPress(InputActions.MoveLeft);
            },
            () => _hurts.Count > 0 && _hurts[^1] >= _clock - _ticks && _hinted.Contains(Ability.HeatPlating)));
        _steps.Add(new("with Heat plating, the pilgrim walks into the vent's heat to the wall behind it and stands there unharmed", 2.5f,
            () =>
            {
                Player.Vitality.Restore();
                Player.Abilities.Unlock(Ability.HeatPlating);
                Player.Respawn(cavern, -1);
            },
            () => _ticks > 60 && P.X < Vent.MinX && Player.Vitality.Current == Player.Vitality.Max));
        _steps.Add(new("with Heat plating, walking off the bank into the slag burns all the same", 2f,
            () =>
            {
                Input.ActionRelease(InputActions.MoveLeft);
                Player.Vitality.Restore();
                Player.Respawn(bank, 1);
                Input.ActionPress(InputActions.MoveRight);
            },
            () => _ticks > 2 && Player.Vitality.Current == Player.Vitality.Max - 1));
        _steps.Add(new("and sets the pilgrim back on its bank, in control (holding toward it and jumping)", 1.5f,
            () =>
            {
                Input.ActionRelease(InputActions.MoveRight);
                Input.ActionPress(InputActions.MoveLeft);
            },
            () =>
            {
                Tap(InputActions.Jump);
                return OnWestBank;
            }));
        _steps.Add(new("with Heat plating, a running jump from the bank's edge lands on the far bank unharmed", 3f,
            () =>
            {
                Input.ActionRelease(InputActions.MoveLeft);
                Input.ActionRelease(InputActions.Jump);
                Player.Vitality.Restore();
                Player.Respawn(bank, 1);
                Input.ActionPress(InputActions.MoveRight);
            },
            () =>
            {
                if (P.X > Channel.MinX - 0.35f && Player.IsOnFloor())
                {
                    Input.ActionPress(InputActions.Jump); // held: a full jump over the slag
                }

                return P.X > Channel.MaxX + 0.5f && Player.IsOnFloor() && Player.Vitality.Current == Player.Vitality.Max;
            }));
        Next();
    }

    /// <summary>
    /// Step 3's verdict: hurt at least twice (the heat keeps burning), each blow at least a grace period after the last, the
    /// pilgrim never across the channel.
    /// </summary>
    private bool Burned(int from)
    {
        List<int> hurts = _hurts.FindAll(t => t >= from);
        int grace = Mathf.FloorToInt(Player.Vitality.GraceTime * 60f);
        for (int i = 1; i < hurts.Count; i++)
        {
            if (hurts[i] - hurts[i - 1] < grace)
            {
                _problems.Add($"two blows {hurts[i] - hurts[i - 1]} ticks apart, under the grace ({grace})");
            }
        }

        if (hurts.Count < 2)
        {
            _problems.Add($"only {hurts.Count} blows in {Pressing} s of walking into the heat");
        }

        return true;
    }

    private void Tap(StringName action)
    {
        if (_clock % 12 == 0)
        {
            Input.ActionPress(action);
        }
        else if (_clock % 12 == 4)
        {
            Input.ActionRelease(action);
        }
    }

    private void Next()
    {
        _index++;
        _ticks = 0;
        if (_index >= _steps.Count)
        {
            Finish();
            return;
        }

        _steps[_index].Start();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_index < 0 || _index >= _steps.Count)
        {
            return;
        }

        _clock++;
        if (_index < _steps.Count - 1 && P.X > Channel.MaxX)
        {
            _crossed = true;
        }

        if (Player.InControl)
        {
            // Control back for a mere tick or two between blows is no control: only a real stretch of it ends the loss.
            if (++_heldTicks >= RegainTicks)
            {
                _lostSince = -1;
            }
        }
        else
        {
            _heldTicks = 0;
            _lostSince = _lostSince < 0 ? _clock : _lostSince;
            _longestLoss = Math.Max(_longestLoss, _clock - _lostSince + 1);
        }

        Step step = _steps[_index];
        if (step.Done())
        {
            Next();
        }
        else if (++_ticks > step.Seconds * 60f)
        {
            _problems.Add($"step {_index + 1} \"{step.What}\" failed in {step.Seconds} s (at {Player.GlobalPosition}, velocity {Player.Velocity}, " +
                          $"health {Player.Vitality.Current}/{Player.Vitality.Max}, in control {Player.InControl}, blows {_hurts.Count})");
            Finish();
        }
    }

    private void Finish()
    {
        _index = _steps.Count;
        Input.ActionRelease(InputActions.MoveRight);
        Input.ActionRelease(InputActions.MoveLeft);
        Input.ActionRelease(InputActions.Jump);
        if (_crossed)
        {
            _problems.Add("the pilgrim got across the slag other than by the jump over it");
        }

        if (_longestLoss > ControlLoss * 60f)
        {
            _problems.Add($"control was gone for {_longestLoss / 60f:0.00} s at once (at most {ControlLoss} s)");
        }

        foreach (string problem in _problems)
        {
            GD.PushError($"HEAT CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"HEAT OK: {_steps.Count} steps (slag: burned and back on its bank, plated or not, one blow per grace, " +
                     $"never across but by the jump; the vent: burned, the gate hint, in it unharmed with plating; control gone at most {_longestLoss / 60f:0.00} s)");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
