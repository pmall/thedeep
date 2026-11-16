using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// The fight moveset in the mechanics lab (`-- --room MechanicsLab --fight-check`, run by tools/check.sh), through the real input
/// actions, on the lab's Votary (renewed between steps, the pilgrim kept whole): presses in rhythm chain a swing, a second and the
/// finisher; a landed blow holds the pilgrim still a moment (the hit stop); a strike in the air holding down is a down strike, and
/// landing it bounces the pilgrim up; a strike during a dash is a dash strike, and it lands. Each step has a time limit. Prints
/// `FIGHT OK` or `FIGHT CHECK` errors.
/// </summary>
public partial class FightCheck : StepDriver
{
    private readonly List<StrikeKind> _kinds = new();
    private bool _stopped, _bounced, _landed;

    private Machine Votary => World.Room.First<Machine>() ?? throw new InvalidOperationException("no Machine in the room");

    /// <summary>Renews the Votary and clears what the last step saw.</summary>
    private void Renew()
    {
        Votary.Reset();
        _kinds.Clear();
        _stopped = _bounced = _landed = false;
    }

    protected override void Build()
    {
        if (World.Room.All<Machine>().Count == 0)
        {
            Fail("the lab has no machine to strike");
            return;
        }

        Player.Attack.Swung += strike => _kinds.Add(strike.Kind);
        float x = Votary.PlanePosition.X;
        Steps.Add(new("presses in rhythm chain a swing, a second and the finisher, and a landed blow holds still a moment", 3f,
            () => { Renew(); Player.Respawn(new Vector3(x - 1.4f, 0f, 0f), 1); },
            () => _kinds.Take(3).SequenceEqual(new[] { StrikeKind.Swing, StrikeKind.Second, StrikeKind.Finisher }) && _stopped,
            () => Hold(InputActions.Attack, Ticks % 21 is 1 or 2)));
        Steps.Add(new("in the air holding down, a down strike; landed, it bounces the pilgrim up", 3f,
            () =>
            {
                Release(InputActions.Attack);
                Renew();
                Player.Respawn(new Vector3(x, 2.6f, 0f), 1);
                Press(InputActions.MoveDown);
            },
            () => _kinds.Contains(StrikeKind.Down) && _bounced,
            () => Hold(InputActions.Attack, Ticks % 21 is 4 or 5)));
        Steps.Add(new("a strike during a dash is a dash strike, and it lands", 3f,
            () =>
            {
                Release(InputActions.Attack);
                Release(InputActions.MoveDown);
                Renew();
                Player.Respawn(new Vector3(x + 4.5f, 0f, 0f), -1); // from behind it: a praying Votary strikes whoever is in front
            },
            () => _kinds.Contains(StrikeKind.Dash) && _landed,
            () =>
            {
                Hold(InputActions.Dash, Ticks is 10 or 11);
                Hold(InputActions.Attack, Ticks is 13 or 14);
            }));
    }

    protected override string Tag => "FIGHT";

    protected override void Observe(float delta)
    {
        Player.Vitality.Restore();
        _stopped |= Player.HitStopped;
        _landed |= Player.HitStopped && Player.Attack.Strike.Kind == StrikeKind.Dash;
        _bounced |= _kinds.Contains(StrikeKind.Down) && Player.Velocity.Y > 5f && !Player.IsOnFloor();
    }

    protected override string Detail =>
        $"at {Player.GlobalPosition}, velocity {Player.Velocity}, strikes {string.Join(",", _kinds)}, hit stop seen {_stopped}, Votary at {Votary.PlanePosition}";

    protected override string Outcome => $"{StepCount} steps (a chain to the finisher with a hit stop, a down strike's bounce, a dash strike)";
}
