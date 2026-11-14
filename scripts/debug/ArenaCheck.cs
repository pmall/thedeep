using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// The sealed arena of the room it starts in, in place (`-- --room Pyre --arena-check`, run by tools/check.sh on every world's ambush and
/// boss seal), through the real input actions where they matter: an <see cref="Ambush"/> or a <see cref="BossSeal"/>, entered through its
/// first gate from the side away from the arena. Before the fight, its foes stand only if it is a boss's (an ambush's wave is not there)
/// and the gates are up; walking in past the gate begins it (every gate falls, the foes stand); walking back is barred by the gate; a boss's name
/// shows only with the boss on screen; when the foes are broken (struck down directly: the fight itself is the combat check's and the playthrough's) the gates rise and the arena
/// is remembered; the room loaded again holds no foe, its gates up. The pilgrim holds the abilities a route to the room needs (the
/// world's network and its solver, as `--room` alone gives: the Pyre's heat stands inside its doorway). Each step has a time limit.
/// Prints `ARENA OK` or `ARENA CHECK` errors.
/// </summary>
public partial class ArenaCheck : Node
{
    /// <summary>The running game's pilgrim.</summary>
    public required Player Player { get; init; }
    /// <summary>The running game's world: its rooms, and the one loaded.</summary>
    public required World World { get; init; }
    /// <summary>The running game's session.</summary>
    public required Session Session { get; init; }

    /// <summary>The running game's banner.</summary>
    public required Banner Banner { get; init; }
    /// <summary>The running game's camera.</summary>
    public required SideCamera Camera { get; init; }

    private readonly List<Step> _steps = new();
    private readonly List<string> _problems = new();
    private int _index = -1;
    private int _ticks;
    private bool _began;
    private string _room = "";

    private sealed record Step(string What, float Seconds, Action Start, Func<bool> Done);

    /// <summary>Runs before the pilgrim each frame and each physics tick, so what it presses is read the same tick.</summary>
    public ArenaCheck()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    private Arena? Fight => World.Room.First<Arena>();

    public override void _Ready()
    {
        _room = World.RoomName;
        if (Fight is not { } arena || arena.Gates.Length == 0)
        {
            _problems.Add($"{_room} has no sealed arena with a gate");
            Finish();
            return;
        }

        if (World.Definition.LoadNetwork() is WorldData<MapDraft>.Loaded(var draft) && MapSolver.Arrival(draft.Graph, _room) is { } arrival)
        {
            Player.Abilities.Set(arrival.Owned, true);
        }

        arena.Began += _ => _began = true;
        Portcullis gate = arena.Gates[0];
        float gateX = gate.GlobalPosition.X;
        float gateY = gate.GlobalPosition.Y;
        // Entered from the gate's side away from the arena: walking in is walking toward its middle.
        int inward = gateX < (arena.ArenaMin.X + arena.ArenaMax.X) / 2f ? 1 : -1;
        StringName walkIn = inward > 0 ? InputActions.MoveRight : InputActions.MoveLeft;
        StringName walkOut = inward > 0 ? InputActions.MoveLeft : InputActions.MoveRight;
        bool boss = arena is BossSeal;
        string kind = boss ? "the boss" : "the wave";
        _steps.Add(new($"before the fight, {(boss ? "the boss stands" : "the wave is not there")} and the gates are up", 0.5f,
            () => { },
            () => arena.Foes.All(m => boss ? !m.Broken && m.Visible : m.Broken && !m.Visible) && arena.Gates.All(g => g.Raised)
                  && !arena.Fighting));
        _steps.Add(new($"walking in past the gate begins it: every gate falls, {kind} stands", 3f,
            () =>
            {
                Player.Respawn(new Vector3(gateX - inward * 1.2f, gateY, 0f), inward);
                Input.ActionPress(walkIn);
            },
            () => _began && arena.Fighting && arena.Gates.All(g => g.Shut) && arena.Foes.All(m => !m.Broken && m.Visible)));
        if (arena is BossSeal seal)
        {
            _steps.Add(new("the boss's name shows only with the boss on screen", 8f,
                () => { },
                () =>
                {
                    if (Banner.Current != seal.Named)
                    {
                        return false;
                    }

                    if (!Camera.Frames(seal.Boss.PlanePosition, 0f))
                    {
                        _problems.Add($"{_room}: the boss's name shows with the boss off screen (boss {seal.Boss.PlanePosition}, camera {Camera.GlobalPosition})");
                    }

                    return true;
                }));
        }

        _steps.Add(new("walking back, the gate bars the way out", 1.5f,
            () => { Input.ActionRelease(walkIn); Input.ActionPress(walkOut); },
            () => _ticks > 60 && (Player.GlobalPosition.X - gateX) * inward > 0f && World.RoomName == _room));
        _steps.Add(new($"{kind} broken, the gates rise and the arena is remembered", 4f,
            () =>
            {
                Input.ActionRelease(walkOut);
                foreach (Machine machine in arena.Foes)
                {
                    machine.TakeHit(999, machine.PlanePosition.X - 1f, 0f);
                }
            },
            () => arena.Won && arena.Gates.All(g => g.Raised) && Session.Save.Collected.Contains($"{_room}/{arena.Name}")));
        _steps.Add(new("the room loaded again holds no foe, its gates up", 1f,
            () => World.Load(GD.Load<PackedScene>(World.RoomPath(_room))),
            () => Fight is { Won: true } again && again.Gates.All(g => g.Raised) && again.Foes.All(m => !m.Visible)));
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

        Player.Vitality.Restore(); // the arena's rule is checked, not the fight
        Step step = _steps[_index];
        _ticks++;
        if (step.Done())
        {
            Next();
        }
        else if (_ticks > step.Seconds * 60f)
        {
            _problems.Add($"{_room}: step {_index + 1} \"{step.What}\" failed in {step.Seconds} s (at {Player.GlobalPosition}, began {_began}, " +
                          $"fighting {Fight?.Fighting}, won {Fight?.Won})");
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
            GD.PushError($"ARENA CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"ARENA OK: {_room}, {_steps.Count} steps (its foes before, sealed behind a falling gate, the way out barred, cleared " +
                     "and remembered, gone for good)");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
