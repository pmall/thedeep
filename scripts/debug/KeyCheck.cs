using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// The key and its door in place (`-- --room Narthex --key-check`, run by tools/check.sh), through the real input actions: in the
/// Narthex without the Sacristy key, walking into the locked door is stopped by it, and the pilgrim says it is locked; in the
/// Scriptorium, walking up to the key takes it (held, remembered, in the save); back in the Narthex, coming near the door opens it,
/// and walking on crosses into the Sacristy; walking back out, the door stands open for good, and reloading the Narthex finds it
/// open. Each step has a time limit. Prints `KEY OK` or `KEY CHECK` errors.
/// </summary>
public partial class KeyCheck : Node
{
    public required Player Player { get; init; }
    public required World World { get; init; }
    public required Keyring Keyring { get; init; }
    public required Session Session { get; init; }

    private const string Key = "SacristyKey";

    private readonly List<Step> _steps = new();
    private readonly List<string> _problems = new();
    private int _index = -1;
    private int _ticks;
    private bool _refused, _opened;

    private sealed record Step(string What, float Seconds, Action Start, Func<bool> Done);

    public KeyCheck()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    private LockedDoor? Door => World.Room.All<LockedDoor>().FirstOrDefault();

    private void Load(string room) => World.Load(GD.Load<PackedScene>(World.RoomPath(room)));

    public override void _Ready()
    {
        Keyring.Refused += _ => _refused = true;
        Keyring.Opened += _ => _opened = true;
        float doorX = Door?.GlobalPosition.X ?? 0f;

        _steps.Add(new("without the key, walking into the locked door is stopped by it", 2f,
            () => { Player.Respawn(new Vector3(doorX + 3f, 0f, 0f), -1); Input.ActionPress(InputActions.MoveLeft); },
            () => _ticks > 90 && Door is { Opened: false } && Player.GlobalPosition.X > doorX && Mathf.Abs(Player.Velocity.X) < 0.1f));
        _steps.Add(new("and the pilgrim says it is locked", 0.5f, () => { }, () => _refused));
        _steps.Add(new("in the Scriptorium, walking up to the key takes it, for good and in the save", 4f,
            () =>
            {
                Input.ActionRelease(InputActions.MoveLeft);
                Load("Scriptorium");
                ItemPickup? pickup = World.Room.All<ItemPickup>().FirstOrDefault();
                Player.Respawn((pickup?.GlobalPosition ?? Vector3.Zero) + new Vector3(1.5f, 0f, 0f), -1);
                Input.ActionPress(InputActions.MoveLeft);
            },
            () => Player.Items.Holds(Key) && Player.InControl && World.Room.All<ItemPickup>().All(p => p.Taken)
                  && Session.Save.Items.Contains(Key) && Session.Save.Collected.Any(c => c.StartsWith("Scriptorium/"))));
        _steps.Add(new("back in the Narthex holding it, coming near the door opens it", 3f,
            () =>
            {
                Input.ActionRelease(InputActions.MoveLeft);
                Load("Narthex");
                Player.Respawn(new Vector3(doorX + 3f, 0f, 0f), -1);
                Input.ActionPress(InputActions.MoveLeft);
            },
            () => _opened));
        _steps.Add(new("and walking on, under the rising gate, crosses into the Sacristy", 4f, () => { },
            () => World.RoomName == "Sacristy" && Player.InControl));
        _steps.Add(new("walking back out, the door stands open", 5f,
            () => { Input.ActionRelease(InputActions.MoveLeft); Input.ActionPress(InputActions.MoveRight); },
            () => World.RoomName == "Narthex" && Door is { Opened: true } door && door.Gate.Raised));
        _steps.Add(new("and the Narthex loaded again finds it open, remembered", 1f,
            () => { Input.ActionRelease(InputActions.MoveRight); Load("Narthex"); },
            () => Door is { Opened: true } door && door.Gate.Raised && Session.Save.Collected.Contains($"Narthex/{door.Name}")));
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
            _problems.Add($"step {_index + 1} \"{step.What}\" failed in {step.Seconds} s (in {World.RoomName} at {Player.GlobalPosition}, " +
                          $"velocity {Player.Velocity}, holding {Player.Items.Held}, door {(Door is { } d ? d.Opened ? "open" : "shut" : "none")})");
            Finish();
        }
    }

    private void Finish()
    {
        _index = _steps.Count;
        Input.ActionRelease(InputActions.MoveLeft);
        Input.ActionRelease(InputActions.MoveRight);
        foreach (string problem in _problems)
        {
            GD.PushError($"KEY CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"KEY OK: {_steps.Count} steps (stopped by the locked door, its line, the key taken and saved, the door opened, " +
                     "through to the Sacristy, open behind, remembered)");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
