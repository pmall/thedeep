using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// Power-ups in the Sacristy (`-- --room Sacristy --powerup-check`, run by tools/check.sh), through the real input actions: walking up
/// to a strength one the check lays on its floor takes it (a blow's damage rises by its amount), then up to the room's own on the
/// dais, a defense one (the health's maximum rises, refilled); both are in the save; the room loaded again shows its own taken.
/// The banner names each as it is taken: the strength one shows its words, the defense one, taken while it shows, waits its turn,
/// and each is shown whole for its full time, one after the other; the room loaded again drops the banner.
/// Each step has a time limit. Prints `POWERUP OK` or `POWERUP CHECK` errors.
/// </summary>
public partial class PowerUpCheck : Node
{
    /// <summary>The running game's pilgrim.</summary>
    public required Player Player { get; init; }
    /// <summary>The running game's world: its rooms, and the one loaded.</summary>
    public required World World { get; init; }
    /// <summary>The running game's power-ups in play (<see cref="TheDeep.PowerUps"/>).</summary>
    public required PowerUps PowerUps { get; init; }
    /// <summary>The running game's session.</summary>
    public required Session Session { get; init; }
    /// <summary>The running game's banner.</summary>
    public required Banner Banner { get; init; }

    /// <summary>Where the check lays its strength power-up, and how much it adds.</summary>
    private const float StrengthX = 18f;
    private const int StrengthAmount = 2;

    private readonly List<Step> _steps = new();
    private readonly List<string> _problems = new();
    private readonly List<PowerUp> _taken = new();
    private int _index = -1;
    private int _ticks;
    private int _damage, _health, _defense;

    /// <summary>Each banner shown whole (eased in) so far, in turn, with how long it stood whole, seconds.</summary>
    private readonly List<(Announcement Said, float Seconds)> _read = new();

    private sealed record Step(string What, float Seconds, Action Start, Func<bool> Done);

    /// <summary>Runs before the pilgrim each frame and each physics tick, so what it presses is read the same tick.</summary>
    public PowerUpCheck()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    public override void _Ready()
    {
        PowerUps.Taken += power => _taken.Add(power);
        PowerUp? dais = World.Room.First<PowerUp>();
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

        Announcement strong = PowerStats.Said(PowerStat.Strength), defended = PowerStats.Said(PowerStat.Defense);
        string strongTitle = LineBook.Shared.Find(strong.Title)?.Text ?? "";
        _steps.Add(new($"walking up to a strength power-up takes it: a blow strikes {StrengthAmount} harder, and the banner says \"{strongTitle}\"", 4f,
            () => { Player.Respawn(new Vector3(StrengthX + 3f, 0f, 0f), -1); Input.ActionPress(InputActions.MoveLeft); },
            () => _taken.Any(p => p.Stat == PowerStat.Strength) && Player.AttackDamage == _damage + StrengthAmount && Player.InControl
                  && Banner.Current == strong && Banner.ShownTitle == strongTitle && strongTitle != ""));
        _steps.Add(new("walking up to the dais's defense power-up takes it: more hits, refilled; its banner waits behind the strength one", 4f,
            () => Player.Respawn(onDais, -1),
            () => _taken.Any(p => p.Stat == PowerStat.Defense) && Player.Vitality.Max == _health + _defense
                  && Player.Vitality.Current == Player.Vitality.Max && Player.InControl && Banner.Current == strong && Banner.Waiting == 1));
        _steps.Add(new("both are in the save", 0.5f,
            () => Input.ActionRelease(InputActions.MoveLeft),
            () => Session.Save.Strength == StrengthAmount && Session.Save.Defense == _defense));
        _steps.Add(new("the strength banner is read whole for its full time, then the defense one shows", 8f,
            () => { },
            () => _read.Count == 2 && _read[0].Said == strong && _read[0].Seconds >= Hold(strong) - 0.1f && _read[1].Said == defended
                  && Banner.Opacity >= 1f));
        _steps.Add(new("the room loaded again shows its own taken, and drops the banner", 1f,
            () => World.Load(GD.Load<PackedScene>(World.RoomPath(World.RoomName))),
            () => World.Room.All<PowerUp>().Count == 1 && World.Room.All<PowerUp>()[0].Taken && !Banner.Showing && Banner.Opacity == 0f));
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

        Watch((float)delta);
        Step step = _steps[_index];
        _ticks++;
        if (step.Done())
        {
            Next();
        }
        else if (_ticks > step.Seconds * 60f)
        {
            _problems.Add($"step {_index + 1} \"{step.What}\" failed in {step.Seconds} s (at {Player.GlobalPosition}, damage " +
                          $"{Player.AttackDamage}, health {Player.Vitality.Current}/{Player.Vitality.Max}, taken {_taken.Count}, banner " +
                          $"{Banner.Current?.Title ?? "none"} at {Banner.Opacity:0.00}, {Banner.Waiting} waiting, read " +
                          $"{string.Join(", ", _read.Select(r => $"{r.Said.Title} {r.Seconds:0.00} s"))})");
            Finish();
        }
    }

    /// <summary>Notes each banner as it stands whole, and for how long.</summary>
    private void Watch(float dt)
    {
        if (Banner.Current is not { } said || Banner.Opacity < 1f)
        {
            return;
        }

        if (_read.Count == 0 || _read[^1].Said != said)
        {
            _read.Add((said, 0f));
        }

        _read[^1] = (said, _read[^1].Seconds + dt);
    }

    /// <summary>How long the banner holds <paramref name="said"/> whole, by its own reading pace.</summary>
    private float Hold(Announcement said) =>
        ReadingTime.Seconds((LineBook.Shared.Find(said.Title)?.Text.Length ?? 0) + (LineBook.Shared.Find(said.Line)?.Text.Length ?? 0),
            Banner.LettersPerSecond, Banner.MinimumSeconds);

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
            GD.Print($"POWERUP OK: {_steps.Count} steps (strength taken, defense taken, saved, both banners read in turn, taken for good)");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
