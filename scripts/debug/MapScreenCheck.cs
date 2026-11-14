using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// The map screen in place (`-- --map-screen-check`, run by tools/check.sh in each world), through the real map action: over the start
/// room alone it opens (the game paused, eased in, laid out sound by <see cref="MapSheet.Problems"/>, a tile for the room the pilgrim
/// is in) and closes (the game resumed, the screen gone); then, the pilgrim taken through open exits into up to <see cref="Rooms"/>
/// rooms, it opens over them all (a tile per room visited, the ways taken bridged, sound) and closes. Each step has a time limit.
/// Prints `MAPSCREEN OK` or `MAPSCREEN CHECK` errors.
/// </summary>
public partial class MapScreenCheck : Node
{
    /// <summary>The running game's world: its rooms, the one loaded, and the map.</summary>
    public required World World { get; init; }
    /// <summary>The running game's travel between rooms.</summary>
    public required RoomTravel Travel { get; init; }
    /// <summary>The running game's map screen.</summary>
    public required MapScreen Map { get; init; }

    /// <summary>How many rooms the pilgrim is taken into before the map opens the second time (a smaller world's every room).</summary>
    public int Rooms { get; set; } = 10;

    /// <summary>Exits crossed at most on the way there.</summary>
    private const int MaxHops = 40;

    /// <summary>Physics ticks between two exits crossed.</summary>
    private const int HopTicks = 6;

    private readonly List<Step> _steps = new();
    private readonly List<string> _problems = new();
    private readonly List<string> _seen = new();
    private readonly Dictionary<string, int> _visits = new();
    private readonly InputEventAction _press = new() { Action = InputActions.Map, Pressed = true };
    private readonly InputEventAction _release = new() { Action = InputActions.Map, Pressed = false };
    private int _index = -1;
    private int _ticks;
    private int _hops;

    private sealed record Step(string What, float Seconds, Action Start, Func<bool> Done);

    public override void _Ready()
    {
        // The map pauses the game: the check runs through it.
        ProcessMode = ProcessModeEnum.Always;
        AddMapSteps("over the start room alone");
        _steps.Add(new($"the pilgrim taken through open exits into {Rooms} rooms", MaxHops * HopTicks / 60f + 2f, () => { }, Walked));
        AddMapSteps("over the rooms visited");
        Next();
    }

    /// <summary>Opens the map through the map action and holds it, then closes it the same way.</summary>
    private void AddMapSteps(string over)
    {
        _steps.Add(new($"the map action opens the map {over}: the game paused, the map eased in and laid out sound", TextStyle.Shared.Panel + 1f,
            Press, () => Map.IsOpen && GetTree().Paused && Map.Shown >= 1f && Laid(over)));
        _steps.Add(new("the map action closes it: the game resumed, the map gone", TextStyle.Shared.Panel + 1f,
            Press, () => !Map.IsOpen && !GetTree().Paused && !Map.Visible));
    }

    private void Press()
    {
        Input.ParseInputEvent(_press);
        Input.ParseInputEvent(_release);
    }

    /// <summary>Whether the map is laid out sound for the rooms the pilgrim knows; its faults are the check's problems.</summary>
    private bool Laid(string over)
    {
        if (Map.Sheet is not { } sheet)
        {
            _problems.Add($"{over}: the map shows no layout");
            return true;
        }

        _problems.AddRange(sheet.Problems(Map.LegendWidths).Select(p => $"{over}: {p}"));
        if (sheet.Tiles.Count != World.Map.Rooms.Count)
        {
            _problems.Add($"{over}: {sheet.Tiles.Count} tiles for the {World.Map.Rooms.Count} rooms visited");
        }

        if (sheet.Here?.Name != World.RoomName)
        {
            _problems.Add($"{over}: the pilgrim's tile is the {sheet.Here?.Name ?? "none"}'s, not the {World.RoomName}'s");
        }

        if (World.Map.Rooms.Count > 1 && sheet.Marks.OfType<WayMark.Taken>().Count() < 2)
        {
            _problems.Add($"{over}: {World.Map.Rooms.Count} rooms visited, but no way taken bridges two of them");
        }

        _seen.Add($"{over}: {sheet.Tiles.Count} rooms, {sheet.Marks.Count} marks, {sheet.Legend.Count} legend entries, {sheet.Scale:0.00} px/m");
        return true;
    }

    /// <summary>Crosses an open exit every few ticks, toward the rooms least visited, until enough rooms are known; true once done.</summary>
    private bool Walked()
    {
        if (World.Map.Rooms.Count >= Math.Min(Rooms, World.RoomScenes().Count()) || _hops >= MaxHops)
        {
            return true;
        }

        if (_ticks % HopTicks == 0)
        {
            _visits[World.RoomName] = _visits.GetValueOrDefault(World.RoomName) + 1;
            RoomExit? exit = World.Room.Exits().Where(e => e.Open && ResourceLoader.Exists(e.TargetRoom))
                .OrderBy(e => _visits.GetValueOrDefault(World.NameOf(e.TargetRoom))).FirstOrDefault();
            if (exit is null)
            {
                _problems.Add($"the {World.RoomName} has no open exit to cross");
                return true;
            }

            Travel.Go(exit);
            _hops++;
        }

        return false;
    }

    private void Next()
    {
        _index++;
        _ticks = 0;
        if (_index >= _steps.Count || _problems.Count > 0)
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
            _problems.Add($"step {_index + 1} \"{step.What}\" failed in {step.Seconds} s (in the {World.RoomName}, the map " +
                          $"{(Map.IsOpen ? "open" : "closed")} at {Map.Shown:0.00}, the game {(GetTree().Paused ? "paused" : "running")})");
            Finish();
        }
    }

    private void Finish()
    {
        _index = _steps.Count;
        GetTree().Paused = false;
        foreach (string problem in _problems)
        {
            GD.PushError($"MAPSCREEN CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"MAPSCREEN OK: {World.Definition.ResourcePath.GetBaseDir().GetFile()}, opened and closed through the map action " +
                     $"({string.Join("; ", _seen)}; {_hops} exits crossed)");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
