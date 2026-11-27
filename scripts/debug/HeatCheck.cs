using System;
using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// Scripted burn check in place, in the Basalt (`-- --room Basalt --heat-check`, run by tools/check.sh), through the real input
/// actions: its slag channel (a <see cref="SlagBody"/>, `docs/gameplay.md#slag`) and its vent (a <see cref="HeatColumn"/>,
/// `docs/gameplay.md#heat`). Without Heat plating: walking off the bank into the slag burns, and the pilgrim is soon back on the
/// bank it came from, in control; walking into it again and again hurts once per touch, never faster than the grace period, and
/// it is never across nor stuck in it; dropped onto the slag past its middle it still comes back to its own bank; the vent's heat
/// burns too, and the pilgrim says its plating would not bear it. With Heat plating: it stands in the vent's heat unharmed; the slag
/// burns it all the same and sets it back on its bank; a running jump from the bank's edge lands it on the far bank, unharmed.
/// Throughout, control is never gone longer than <see cref="ControlLoss"/>. Each step has a time limit. Prints `HEAT OK` or
/// `HEAT CHECK` errors.
/// </summary>
public partial class HeatCheck : StepDriver
{
    private static readonly NodePath SlagPath = "Slag";
    private static readonly NodePath VentPath = "Heat/Vent";

    /// <summary>The running game's gate hints.</summary>
    public required GateHints Hints { get; init; }

    /// <summary>The longest the heat may take control away at once (the blow, the veil and the return), seconds.</summary>
    private const float ControlLoss = 0.45f;

    /// <summary>How long the pilgrim keeps walking into the channel (step 3), seconds.</summary>
    private const float Pressing = 4f;

    /// <summary>The physics ticks at which a blow hurt the pilgrim, since the check began.</summary>
    private readonly List<int> _hurts = new();

    private SlagBody? _slag;
    private HeatColumn? _vent;
    private int _clock;
    private int _lostSince = -1;
    private int _heldTicks;

    /// <summary>Ticks in control that end a loss of control.</summary>
    private const int RegainTicks = 6;
    private int _longestLoss;
    private bool _crossed;

    private Vector2 P => new(Player.GlobalPosition.X, Player.GlobalPosition.Y);

    /// <summary>The slag channel of the room under check, found on first use.</summary>
    private Box Channel => (_slag ??= World.Room.GetNode<SlagBody>(SlagPath)).PlayRegion;

    /// <summary>The vent's heat, on the cavern's floor, found on first use.</summary>
    private Box Vent => (_vent ??= World.Room.GetNode<HeatColumn>(VentPath)).Region;

    /// <summary>Standing on the west bank, clear of the channel, in control.</summary>
    private bool OnWestBank => Player.InControl && Player.IsOnFloor() && P.X < Channel.MinX - 0.4f && P.Y > Channel.MaxY - 1f;

    protected override void Build()
    {
        var hinted = new HintLog(Hints);
        Player.Hurt += () => _hurts.Add(_clock);
        Vector3 bank = new(Channel.MinX - 4f, 1f, 0f);
        Vector3 cavern = new(Vent.MaxX + 3.5f, Vent.MinY, 0f);
        int pressingFrom = 0, ventFrom = 0;

        Steps.Add(new("without Heat plating, walking off the bank into the slag burns", 2f,
            () =>
            {
                Player.Abilities.Lock(Ability.HeatPlating);
                Player.Vitality.Restore();
                Player.Respawn(bank, 1);
                Press(InputActions.MoveRight);
            },
            () => _hurts.Count == 1 && Player.Vitality.Current == Player.Vitality.Max - 1));
        Steps.Add(new("and the pilgrim is soon back on the bank it came from, in control (holding toward it and jumping)", 1.5f,
            () =>
            {
                Release(InputActions.MoveRight);
                Press(InputActions.MoveLeft);
            },
            () =>
            {
                Tap(InputActions.Jump);
                return OnWestBank;
            }));
        Steps.Add(new($"walking into it for {Pressing} s: one health per touch, never faster than the grace, never across nor stuck", Pressing + 0.5f,
            () =>
            {
                Release(InputActions.MoveLeft);
                Release(InputActions.Jump);
                Player.Vitality.Restore();
                Press(InputActions.MoveRight);
                pressingFrom = _clock;
            },
            () => _clock - pressingFrom >= Pressing * 60f && Burned(pressingFrom)));
        Steps.Add(new("dropped onto the slag past its middle, it comes back to the bank it came from", 1.5f,
            () =>
            {
                Release(InputActions.MoveRight);
                Player.Vitality.Restore();
                Player.GlobalPosition = new Vector3(0.5f * (Channel.MinX + Channel.MaxX) + 1f, Channel.MaxY + 0.5f, 0f);
                Player.Velocity = Vector3.Zero;
            },
            () => Ticks > 2 && OnWestBank));
        Steps.Add(new("the vent's heat burns too, and the pilgrim says its plating would not bear it (the gate hint)", 2f,
            () =>
            {
                Player.Vitality.Restore();
                Player.Respawn(cavern, -1);
                Press(InputActions.MoveLeft);
                ventFrom = _clock;
            },
            () => _hurts.Count > 0 && _hurts[^1] >= ventFrom && hinted.Said(Ability.HeatPlating)));
        Steps.Add(new("with Heat plating, the pilgrim walks into the vent's heat to the wall behind it and stands there unharmed", 2.5f,
            () =>
            {
                Player.Vitality.Restore();
                Player.Abilities.Unlock(Ability.HeatPlating);
                Player.Respawn(cavern, -1);
            },
            () => Ticks > 60 && P.X < Vent.MinX && Player.Vitality.Current == Player.Vitality.Max));
        Steps.Add(new("with Heat plating, walking off the bank into the slag burns all the same", 2f,
            () =>
            {
                Release(InputActions.MoveLeft);
                Player.Vitality.Restore();
                Player.Respawn(bank, 1);
                Press(InputActions.MoveRight);
            },
            () => Ticks > 2 && Player.Vitality.Current == Player.Vitality.Max - 1));
        Steps.Add(new("and sets the pilgrim back on its bank, in control (holding toward it and jumping)", 1.5f,
            () =>
            {
                Release(InputActions.MoveRight);
                Press(InputActions.MoveLeft);
            },
            () =>
            {
                Tap(InputActions.Jump);
                return OnWestBank;
            }));
        Steps.Add(new("with Heat plating, a running jump from the bank's edge lands on the far bank unharmed", 3f,
            () =>
            {
                Release(InputActions.MoveLeft);
                Release(InputActions.Jump);
                Player.Vitality.Restore();
                Player.Respawn(bank, 1);
                Press(InputActions.MoveRight);
            },
            () =>
            {
                if (P.X > Channel.MinX - 0.35f && Player.IsOnFloor())
                {
                    Press(InputActions.Jump); // held: a full jump over the slag
                }

                return P.X > Channel.MaxX + 0.5f && Player.IsOnFloor() && Player.Vitality.Current == Player.Vitality.Max;
            }));
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
                Problems.Add($"two blows {hurts[i] - hurts[i - 1]} ticks apart, under the grace ({grace})");
            }
        }

        if (hurts.Count < 2)
        {
            Problems.Add($"only {hurts.Count} blows in {Pressing} s of walking into the heat");
        }

        return true;
    }

    private void Tap(StringName action)
    {
        if (_clock % 12 == 0)
        {
            Press(action);
        }
        else if (_clock % 12 == 4)
        {
            Release(action);
        }
    }

    protected override string Tag => "HEAT";

    protected override void Observe(float delta)
    {
        _clock++;
        if (StepIndex < StepCount - 1 && P.X > Channel.MaxX)
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
    }

    protected override void Ending()
    {
        if (_crossed)
        {
            Problems.Add("the pilgrim got across the slag other than by the jump over it");
        }

        if (_longestLoss > ControlLoss * 60f)
        {
            Problems.Add($"control was gone for {_longestLoss / 60f:0.00} s at once (at most {ControlLoss} s)");
        }
    }

    protected override string Detail =>
        $"at {Player.GlobalPosition}, velocity {Player.Velocity}, health {Player.Vitality.Current}/{Player.Vitality.Max}, " +
        $"in control {Player.InControl}, blows {_hurts.Count}";

    protected override string Outcome =>
        $"{StepCount} steps (slag: burned and back on its bank, plated or not, one blow per grace, " +
        $"never across but by the jump; the vent: burned, the gate hint, in it unharmed with plating; control gone at most {_longestLoss / 60f:0.00} s)";
}
