using System;
using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// Scripted Heat plating check in the mechanics lab (`-- --room MechanicsLab --heat-check`, run by tools/check.sh), through
/// the real input actions, at a column of heat it places itself: without Heat plating, running into it burns (health taken,
/// thrown back out on the side it came from); with Heat plating, the pilgrim runs through it unharmed. Each step has a time
/// limit. Prints `HEAT OK` or `HEAT CHECK` errors.
/// </summary>
public partial class HeatCheck : Node
{
    public Player Player { get; set; } = null!;
    public World World { get; set; } = null!;
    public GateHints Hints { get; set; } = null!;

    /// <summary>The gates the pilgrim has said it can't pass (GateHints).</summary>
    private readonly System.Collections.Generic.HashSet<Ability> _hinted = new();

    /// <summary>The column: its middle and half width, on the lab floor.</summary>
    private const float Middle = 12f, Half = 1f;

    private readonly List<Step> _steps = new();
    private readonly List<string> _problems = new();
    private int _index = -1;
    private int _ticks;
    private bool _crossed;

    private sealed record Step(string What, float Seconds, Action Start, Func<bool> Done);

    public HeatCheck()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    private float X => Player.GlobalPosition.X;

    public override void _Ready()
    {
        var zone = new HeatZone
        {
            Name = "Vent", Size = new Vector2(2f * Half, 6f), Position = new Vector3(Middle, 0f, 0f),
            HazeMaterial = GD.Load<Material>("res://assets/materials/heat_haze.tres"), Embers = GD.Load<PackedScene>("res://scenes/fx/Embers.tscn"),
        };
        World.Room.AddChild(zone);
        zone.Player = Player;
        Hints.Said += kind => _hinted.Add(kind);

        _steps.Add(new("without Heat plating, running into the heat burns and throws the pilgrim back out", 2f,
            () => { Player.Abilities.Lock(Ability.HeatPlating); Player.Respawn(new Vector3(6f, 0f, 0f), 1); Input.ActionPress(InputActions.MoveRight); },
            () => Player.Vitality.Current < Player.Vitality.Max && X < Middle - Half && Player.Velocity.X < 0f && !_crossed));
        _steps.Add(new("and the pilgrim says its plating would not bear it (the gate hint)", 1f,
            () => { },
            () => _hinted.Contains(Ability.HeatPlating)));
        _steps.Add(new("with Heat plating, the pilgrim runs through it unharmed", 2.5f,
            () =>
            {
                Input.ActionPress(InputActions.MoveRight);
                Player.Vitality.Restore();
                Player.Abilities.Unlock(Ability.HeatPlating);
                Player.Respawn(new Vector3(6f, 0f, 0f), 1);
            },
            () => X > Middle + Half + 1f && Player.Vitality.Current == Player.Vitality.Max));
        Next();
    }

    private void Next()
    {
        _index++;
        _ticks = 0;
        _crossed = false;
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

        _crossed |= X > Middle + Half;
        Step step = _steps[_index];
        if (step.Done())
        {
            Next();
        }
        else if (++_ticks > step.Seconds * 60f)
        {
            _problems.Add($"step {_index + 1} \"{step.What}\" failed in {step.Seconds} s (at {Player.GlobalPosition}, velocity {Player.Velocity}, " +
                          $"health {Player.Vitality.Current}/{Player.Vitality.Max}, crossed {_crossed})");
            Finish();
        }
    }

    private void Finish()
    {
        _index = _steps.Count;
        Input.ActionRelease(InputActions.MoveRight);
        foreach (string problem in _problems)
        {
            GD.PushError($"HEAT CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"HEAT OK: {_steps.Count} steps (burned and thrown back without it, the gate hint, through unharmed with it)");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
