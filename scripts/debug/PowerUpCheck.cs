using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// Power-ups in the Sacristy (`-- --room Sacristy --powerup-check`, run by tools/check.sh), through the real input actions: walking up
/// to a strength one the check lays on its floor takes it (a blow's damage rises by its amount), then up to the room's own on the
/// dais, a defense one (the health's maximum rises, refilled); both are in the save; the room loaded again shows its own taken.
/// Each step has a time limit. Prints `POWERUP OK` or `POWERUP CHECK` errors.
/// </summary>
public partial class PowerUpCheck : Node
{
    public required Player Player { get; init; }
    public required World World { get; init; }
    public required PowerUps PowerUps { get; init; }
    public required Session Session { get; init; }

    /// <summary>Where the check lays its strength power-up, and how much it adds.</summary>
    private const float StrengthX = 18f;
    private const int StrengthAmount = 2;

    private readonly List<Step> _steps = new();
    private readonly List<string> _problems = new();
    private readonly List<PowerUp> _taken = new();
    private int _index = -1;
    private int _ticks;
    private int _damage, _health, _defense;

    private sealed record Step(string What, float Seconds, Action Start, Func<bool> Done);

    public PowerUpCheck()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    public override void _Ready()
    {
        PowerUps.Taken += power => _taken.Add(power);
        PowerUp? dais = World.Room.All<PowerUp>().FirstOrDefault();
        if (dais is not { Stat: PowerStat.Defense })
        {
            _problems.Add("the Sacristy has no defense power-up");
            Finish();
            return;
        }

        var strength = GD.Load<PackedScene>("res://scenes/props/PowerUp.tscn").Instantiate<PowerUp>();
        strength.Name = "CheckStrength";
        strength.Stat = PowerStat.Strength;
        strength.Amount = StrengthAmount;
        strength.Position = new Vector3(StrengthX, 0f, 0f);
        World.Room.AddChild(strength);
        _damage = Player.AttackDamage;
        _health = Player.Vitality.Max;
        _defense = dais.Amount;
        Vector3 onDais = dais.GlobalPosition + new Vector3(2.5f, 0f, 0f);

        _steps.Add(new($"walking up to a strength power-up takes it: a blow strikes {StrengthAmount} harder", 4f,
            () => { Player.Respawn(new Vector3(StrengthX + 3f, 0f, 0f), -1); Input.ActionPress(InputActions.MoveLeft); },
            () => _taken.Any(p => p.Stat == PowerStat.Strength) && Player.AttackDamage == _damage + StrengthAmount && Player.InControl));
        _steps.Add(new("walking up to the dais's defense power-up takes it: more hits, refilled", 4f,
            () => Player.Respawn(onDais, -1),
            () => _taken.Any(p => p.Stat == PowerStat.Defense) && Player.Vitality.Max == _health + _defense
                  && Player.Vitality.Current == Player.Vitality.Max && Player.InControl));
        _steps.Add(new("both are in the save", 0.5f,
            () => Input.ActionRelease(InputActions.MoveLeft),
            () => Session.Save.Strength == StrengthAmount && Session.Save.Defense == _defense));
        _steps.Add(new("the room loaded again shows its own taken", 1f,
            () => World.Load(GD.Load<PackedScene>(World.RoomPath(World.RoomName))),
            () => World.Room.All<PowerUp>().Count == 1 && World.Room.All<PowerUp>()[0].Taken));
        Next();
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

        Step step = _steps[_index];
        _ticks++;
        if (step.Done())
        {
            Next();
        }
        else if (_ticks > step.Seconds * 60f)
        {
            _problems.Add($"step {_index + 1} \"{step.What}\" failed in {step.Seconds} s (at {Player.GlobalPosition}, damage " +
                          $"{Player.AttackDamage}, health {Player.Vitality.Current}/{Player.Vitality.Max}, taken {_taken.Count})");
            Finish();
        }
    }

    private void Finish()
    {
        _index = _steps.Count;
        Input.ActionRelease(InputActions.MoveLeft);
        foreach (string problem in _problems)
        {
            GD.PushError($"POWERUP CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"POWERUP OK: {_steps.Count} steps (strength taken, defense taken, saved, taken for good)");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
