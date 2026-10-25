using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// Scripted combat and session check in the mechanics lab (`-- --room MechanicsLab --combat-check`, run by
/// tools/check.sh), through the real input actions: lingering in front of the Votary gets the pilgrim struck;
/// swings break the Votary; a deadly blow brings the pilgrim back whole at the room's spawn, the room renewed;
/// resting at the shrine heals and makes it the place to come back to; walking into the relic takes its
/// ability. Prints `COMBAT OK` or `COMBAT CHECK` errors.
/// </summary>
public partial class CombatCheck : Node
{
    public Player Player { get; set; } = null!;
    public World World { get; set; } = null!;
    public Session Session { get; set; } = null!;

    private readonly List<Step> _steps = new();
    private readonly List<string> _problems = new();
    private int _index = -1;
    private int _ticks;

    private sealed record Step(string What, float Seconds, Action Start, Func<bool> Done, Action? Tick = null);

    public CombatCheck()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    private bool _dealt;

    /// <summary>A blow too heavy to survive, once the grace period of an earlier hit is over.</summary>
    private void DeadlyBlow()
    {
        if (!_dealt && Player.Vitality.Vulnerable)
        {
            _dealt = Player.TakeHit(100, new System.Numerics.Vector2(18f, 0f));
        }
    }

    private Machine? Votary => World.Room.All<Machine>().FirstOrDefault();

    public override void _Ready()
    {
        _steps.Add(new("get struck lingering in front of the Votary", 5f,
            () => Player.Respawn(new Vector3(27.5f, 0f, 0f), 1),
            () => Player.Vitality.Current < Player.Vitality.Max));
        _steps.Add(new("break the Votary with swings", 6f,
            () => Player.Respawn(new Vector3(27.6f, 0f, 0f), 1),
            () => Votary is { Broken: true },
            () =>
            {
                // Keep in reach (a hit pushes it back), and press every 21 ticks (a swing and its cooldown is about 20).
                bool far = Votary is { } v && v.PlanePosition.X - Player.GlobalPosition.X > 1.5f;
                if (far)
                {
                    Input.ActionPress(InputActions.MoveRight);
                }
                else
                {
                    Input.ActionRelease(InputActions.MoveRight);
                }

                if (_ticks % 21 == 1)
                {
                    Input.ActionPress(InputActions.Attack);
                }
                else if (_ticks % 21 == 3)
                {
                    Input.ActionRelease(InputActions.Attack);
                }
            }));
        _steps.Add(new("die and come back whole at the spawn point, the Votary renewed", 6f,
            () =>
            {
                Input.ActionRelease(InputActions.Attack);
                Input.ActionRelease(InputActions.MoveRight);
                Player.Respawn(new Vector3(20f, 0f, 0f), 1);
                _dealt = false;
            },
            () => _dealt && Player.Vitality.Current == Player.Vitality.Max && Player.InControl
                  && Player.GlobalPosition.DistanceTo(World.Room.SpawnPoint.GlobalPosition) < 0.6f && Votary is { Broken: false },
            DeadlyBlow));
        _steps.Add(new("rest at the shrine", 2f,
            () =>
            {
                Player.Respawn(new Vector3(4.5f, 0f, 0f), 1);
                Player.TakeHit(1, new System.Numerics.Vector2(10f, 0f));
            },
            () => Session.Save.Shrine == "Shrine" && Player.Vitality.Current == Player.Vitality.Max && _ticks > 57,
            () =>
            {
                // Pressed once the pilgrim has landed from the blow's knockback, back by the shrine.
                if (_ticks == 50)
                {
                    Player.Respawn(new Vector3(4.5f, 0f, 0f), 1);
                }
                else if (_ticks == 55)
                {
                    Input.ActionPress(InputActions.Interact);
                }
                else if (_ticks == 57)
                {
                    Input.ActionRelease(InputActions.Interact);
                }
            }));
        _steps.Add(new("die and come back at the shrine", 6f,
            () =>
            {
                Player.Respawn(new Vector3(20f, 0f, 0f), 1);
                _dealt = false;
            },
            () => _dealt && Player.InControl && Player.Vitality.Current == Player.Vitality.Max
                  && Player.GlobalPosition.DistanceTo(new Vector3(4f, 0f, 0f)) < 0.6f,
            DeadlyBlow));
        _steps.Add(new("walk into the relic and take its ability", 4f,
            () => { Player.Respawn(new Vector3(13f, 0f, 0f), 1); Input.ActionPress(InputActions.MoveRight); },
            () => Player.Abilities.Has(Ability.Drill),
            () =>
            {
                if (_ticks == 30)
                {
                    Input.ActionRelease(InputActions.MoveRight);
                }
            }));
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
        step.Tick?.Invoke();
        if (step.Done())
        {
            Next();
            return;
        }

        if (_ticks > step.Seconds * 60f)
        {
            _problems.Add($"step {_index + 1} \"{step.What}\" did not happen in {step.Seconds} s (at {Player.GlobalPosition}, " +
                          $"health {Player.Vitality.Current}/{Player.Vitality.Max}, state {Player.CurrentState.Name})");
            Finish();
        }
    }

    private void Finish()
    {
        _index = _steps.Count;
        foreach (StringName action in new[] { InputActions.Attack, InputActions.MoveRight, InputActions.Interact })
        {
            Input.ActionRelease(action);
        }

        foreach (string problem in _problems)
        {
            GD.PushError($"COMBAT CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"COMBAT OK: {_steps.Count} steps (struck, swings break a machine, death and return, rest, relic)");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
