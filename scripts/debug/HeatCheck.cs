using System;
using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// Scripted heat check in place, at the Basalt's slag channel (`-- --room Basalt --heat-check`, run by tools/check.sh), through
/// the real input actions (`docs/gameplay.md#heat`). Without Heat plating: walking off the bank into the channel burns, and the
/// pilgrim is soon back on the bank it came from, in control; walking into it again and again hurts once per touch, never
/// faster than the grace period, and it is never across nor stuck in it; dropped into the heat past its middle it still comes
/// back to its own bank; the pilgrim says its plating would not bear it. With Heat plating: it walks into the channel
/// unharmed and jumps out on the far bank. Throughout, control is never gone longer than <see cref="ControlLoss"/>. Each step
/// has a time limit. Prints `HEAT OK` or `HEAT CHECK` errors.
/// </summary>
public partial class HeatCheck : Node
{
    public required Player Player { get; init; }
    public required World World { get; init; }
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

    private HeatZone? _zone;
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

    public HeatCheck()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    private Vector2 P => new(Player.GlobalPosition.X, Player.GlobalPosition.Y);

    /// <summary>The slag channel of the room under check, found on first use.</summary>
    private HeatZone Zone => _zone ??= World.Room.GetNode<HeatZone>("Heat/Slag");

    private Box Channel => Zone.Region;

    /// <summary>Standing on the west bank, clear of the channel, in control.</summary>
    private bool OnWestBank => Player.InControl && Player.IsOnFloor() && P.X < Channel.MinX - 0.4f && P.Y > Channel.MaxY - 1f;

    public override void _Ready()
    {
        Hints.Said += kind => _hinted.Add(kind);
        Player.Hurt += () => _hurts.Add(_clock);
        Vector3 bank = new(Channel.MinX - 4f, Channel.MaxY, 0f);
        int pressingFrom = 0;

        _steps.Add(new("without Heat plating, walking off the bank into the slag channel burns", 2f,
            () =>
            {
                Player.Abilities.Lock(Ability.HeatPlating);
                Player.Vitality.Restore();
                Player.Respawn(bank with { Y = 1f }, 1);
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
        _steps.Add(new("fallen to the channel's bed by its bank: holding toward the bank and jumping, the pilgrim is soon on it", 1.5f,
            () =>
            {
                Player.Vitality.Restore();
                Player.GlobalPosition = new Vector3(Channel.MinX + 1.2f, Channel.MinY + 0.3f, 0f);
                Player.Velocity = Vector3.Zero;
            },
            () =>
            {
                Tap(InputActions.Jump);
                return _ticks > 2 && OnWestBank;
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
        _steps.Add(new("dropped into the heat past its middle, it comes back to the bank it came from", 1.5f,
            () =>
            {
                Input.ActionRelease(InputActions.MoveRight);
                Player.Vitality.Restore();
                Player.GlobalPosition = new Vector3(0.5f * (Channel.MinX + Channel.MaxX) + 1f, Channel.MaxY + 0.5f, 0f);
                Player.Velocity = Vector3.Zero;
            },
            () => _ticks > 2 && OnWestBank));
        _steps.Add(new("and the pilgrim says its plating would not bear it (the gate hint)", 1f,
            () => { },
            () => _hinted.Contains(Ability.HeatPlating)));
        _steps.Add(new("with Heat plating, the pilgrim walks into the channel unharmed and jumps out on the far bank", 3f,
            () =>
            {
                Player.Vitality.Restore();
                Player.Abilities.Unlock(Ability.HeatPlating);
                Player.Respawn(bank with { Y = 1f }, 1);
                _crossed = false;
                Input.ActionPress(InputActions.MoveRight);
            },
            () =>
            {
                if (P.X > Channel.MinX + 1f && Player.IsOnFloor())
                {
                    Input.ActionPress(InputActions.Jump); // held: a full jump out of the bed
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
        if (!Player.Abilities.Has(Ability.HeatPlating) && P.X > Channel.MaxX)
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
            _problems.Add("without Heat plating, the pilgrim got across the channel");
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
            GD.Print($"HEAT OK: {_steps.Count} steps (burned and back on its bank, one blow per grace, never across, the gate hint, " +
                     $"through unharmed with it; control gone at most {_longestLoss / 60f:0.00} s)");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
