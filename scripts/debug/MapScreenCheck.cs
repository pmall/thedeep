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
public partial class MapScreenCheck : StepDriver
{
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

    /// <summary>The map pauses the game: the check runs through it, so after the pilgrim rather than before.</summary>
    public MapScreenCheck() : base(false)
    {
    }

    private readonly List<string> _seen = new();
    private readonly Dictionary<string, int> _visits = new();
    private readonly InputEventAction _press = new() { Action = InputActions.Map, Pressed = true };
    private readonly InputEventAction _release = new() { Action = InputActions.Map, Pressed = false };
    private int _hops;

    protected override void Build()
    {
        // The map pauses the game: the check runs through it.
        ProcessMode = ProcessModeEnum.Always;
        AddMapSteps("over the start room alone");
        Steps.Add(new($"the pilgrim taken through open exits into {Rooms} rooms", MaxHops * HopTicks / 60f + 2f, () => { }, Walked));
        AddMapSteps("over the rooms visited");
    }

    /// <summary>Opens the map through the map action and holds it, then closes it the same way.</summary>
    private void AddMapSteps(string over)
    {
        Steps.Add(new($"the map action opens the map {over}: the game paused, the map eased in and laid out sound", TextStyle.Shared.Panel + 1f,
            Tap, () => Map.IsOpen && GetTree().Paused && Map.Shown >= 1f && Laid(over)));
        Steps.Add(new("the map action closes it: the game resumed, the map gone", TextStyle.Shared.Panel + 1f,
            Tap, () => !Map.IsOpen && !GetTree().Paused && !Map.Visible));
    }

    private void Tap()
    {
        Input.ParseInputEvent(_press);
        Input.ParseInputEvent(_release);
    }

    /// <summary>Whether the map is laid out sound for the rooms the pilgrim knows; its faults are the check's problems.</summary>
    private bool Laid(string over)
    {
        if (Map.Sheet is not { } sheet)
        {
            Problems.Add($"{over}: the map shows no layout");
            return true;
        }

        Problems.AddRange(sheet.Problems(Map.LegendWidths).Select(p => $"{over}: {p}"));
        if (sheet.Tiles.Count != World.Map.Rooms.Count)
        {
            Problems.Add($"{over}: {sheet.Tiles.Count} tiles for the {World.Map.Rooms.Count} rooms visited");
        }

        if (sheet.Here?.Name != World.RoomName)
        {
            Problems.Add($"{over}: the pilgrim's tile is the {sheet.Here?.Name ?? "none"}'s, not the {World.RoomName}'s");
        }

        if (World.Map.Rooms.Count > 1 && sheet.Marks.OfType<WayMark.Taken>().Count() < 2)
        {
            Problems.Add($"{over}: {World.Map.Rooms.Count} rooms visited, but no way taken bridges two of them");
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

        // A crossing the pilgrim started itself (standing in an exit it arrived on) is finished before the next is forced.
        if (Ticks % HopTicks == 0 && !Travel.Travelling)
        {
            _visits[World.RoomName] = _visits.GetValueOrDefault(World.RoomName) + 1;
            RoomExit? exit = World.Room.Exits().Where(e => e.Open && ResourceLoader.Exists(e.TargetRoom))
                .OrderBy(e => _visits.GetValueOrDefault(World.NameOf(e.TargetRoom))).FirstOrDefault();
            if (exit is null)
            {
                Problems.Add($"the {World.RoomName} has no open exit to cross");
                return true;
            }

            Travel.Go(exit);
            _hops++;
        }

        return false;
    }

    protected override string Tag => "MAPSCREEN";

    protected override bool StopAtFirstProblem => true;

    protected override void Ending() => GetTree().Paused = false;

    protected override string Detail =>
        $"in the {World.RoomName}, the map {(Map.IsOpen ? "open" : "closed")} at {Map.Shown:0.00}, the game {(GetTree().Paused ? "paused" : "running")}";

    protected override string Outcome =>
        $"{World.Definition.ResourcePath.GetBaseDir().GetFile()}, opened and closed through the map action " +
        $"({string.Join("; ", _seen)}; {_hops} exits crossed)";
}
