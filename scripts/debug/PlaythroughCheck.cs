using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// The whole run, from a new game to the end (`-- --playthrough-check`, run by tools/check.sh). It reads the built map from
/// the room scenes' markers, asks the solver for a shortest route from the start to the goal (<see cref="MapSolver.Route"/>),
/// and plays it in the real game: every way is crossed through its real exit (the pilgrim put in its trigger moving out, its crossing the
/// <see cref="TravelValidator"/>'s to prove) only once the pilgrim holds what it needs and a door on it stands open (a locked one opened by
/// coming near it); in each room on the way it takes the relics and items (walking into them), pulls the levers (interact), breaks the laws (pressing on at the lip through the real inputs) and fights the bosses (through the
/// real inputs, kept alive: the rules, not the balance) before leaving. At the goal, reaching the heart plays the end card.
/// Moving inside a room is the geometry check's to prove (<see cref="ReachPlanner"/>); this proves the game's flow: abilities
/// in route order, gates, bosses, laws, the end. Prints `PLAYTHROUGH OK` or `PLAYTHROUGH CHECK` errors.
/// </summary>
public partial class PlaythroughCheck : Node
{
    public required World World { get; init; }
    public required Player Player { get; init; }
    public required Session Session { get; init; }

    private enum Phase { Done = -1, Relic, Item, Law, Boss, Door, Cross, Arrive, End }

    private readonly List<string> _problems = new();
    private readonly List<string> _log = new();
    private IReadOnlyList<MapEdge> _route = Array.Empty<MapEdge>();
    private int _hop;
    private Phase _task;
    private int _ticks;
    private bool _ended;
    private AbilityRelic? _relic;
    private ItemPickup? _item;
    private Lever? _lever;
    private LawBreak? _law;
    private BossSeal? _seal;
    private int _bosses, _laws, _relics, _items, _levers, _doors;

    public PlaythroughCheck()
    {
        ProcessPriority = -100;
        ProcessPhysicsPriority = -100;
    }

    public override void _Ready()
    {
        var map = new MapGraph { Start = World.RoomName };
        foreach (string file in World.RoomScenes())
        {
            var room = GD.Load<PackedScene>(file).Instantiate<Room>();
            string name = World.NameOf(file);
            if (room.InGame)
            {
                foreach (RoomExit exit in room.Exits().Where(e => e.Open && ResourceLoader.Exists(e.TargetRoom)))
                {
                    map.AddEdge(new MapEdge(name, exit.Name, World.NameOf(exit.TargetRoom), exit.TargetExit, exit.Requirement));
                }

                foreach (AbilityRelic relic in room.All<AbilityRelic>())
                {
                    map.AddRelic(new MapRelic(name, relic.Name, relic.Ability));
                }

                foreach (ItemPickup pickup in room.All<ItemPickup>())
                {
                    map.AddItem(new MapItem(name, pickup.Name, pickup.Item.Id));
                }

                foreach (Lever lever in room.All<Lever>())
                {
                    map.AddItem(new MapItem(name, lever.Name, lever.Item.Id));
                }

                if (room.All<MapGoal>().Any())
                {
                    map.AddGoal(name);
                }
            }

            room.Free();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        if (MapSolver.Route(map) is not { } route)
        {
            Fail("the solver finds no route from the start to a goal");
            return;
        }

        _route = route;
        Session.Ended += () => _ended = true;
        _hop = 0;
        Begin(Phase.Relic);
    }

    private MapEdge Hop => _route[_hop];

    /// <summary>After the boss (if any) of the room: leave by the next way (through its door first), or, at the route's end, reach the heart.</summary>
    private Phase AfterBoss => _hop < _route.Count ? Phase.Door : Phase.End;

    private void Begin(Phase task)
    {
        _task = task;
        _ticks = 0;
        Release();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_route.Count == 0 || _task == Phase.Done)
        {
            return;
        }

        _ticks++;
        Room room = World.Room;
        switch (_task)
        {
            case Phase.Relic:
                if (_relic is null)
                {
                    _relic = room.All<AbilityRelic>().FirstOrDefault(r => !r.Taken && !Player.Abilities.Has(r.Ability));
                    if (_relic is null)
                    {
                        Begin(Phase.Item);
                        return;
                    }

                    Player.Respawn(_relic.GlobalPosition with { Z = 0f });
                }
                else if (Player.Abilities.Has(_relic.Ability) && Player.InControl)
                {
                    _log.Add($"{World.RoomName}: took {_relic.Ability}");
                    _relics++;
                    _relic = null;
                    _ticks = 0;
                }
                else if (_ticks > 600)
                {
                    Fail($"in {World.RoomName}, walking into the relic did not give {_relic.Ability}");
                }

                break;
            case Phase.Item:
                if (_item is null)
                {
                    _item = room.All<ItemPickup>().FirstOrDefault(i => !i.Taken && !Player.Items.Holds(i.Item.Id));
                    if (_item is null && (_lever ?? room.All<Lever>().FirstOrDefault(l => !l.Pulled)) is { } lever)
                    {
                        Pull(lever);
                        return;
                    }

                    if (_item is null)
                    {
                        Begin(Phase.Law);
                        return;
                    }

                    Player.Respawn(_item.GlobalPosition with { Z = 0f });
                }
                else if (Player.Items.Holds(_item.Item.Id) && Player.InControl)
                {
                    _log.Add($"{World.RoomName}: took {_item.Item.Id}");
                    _items++;
                    _item = null;
                    _ticks = 0;
                }
                else if (_ticks > 600)
                {
                    Fail($"in {World.RoomName}, walking into the item did not give {_item.Item.Id}");
                }

                break;
            case Phase.Door:
                if (room.FindExit(Hop.FromExit)?.Door is not { } door)
                {
                    Begin(Phase.Cross);
                    return;
                }

                if (_ticks == 1 && !door.Opened)
                {
                    float inward = (room.BoundsMin.X + room.BoundsMax.X) * 0.5f > door.GlobalPosition.X ? 1f : -1f;
                    Player.Respawn(door.GlobalPosition with { Z = 0f } + new Vector3(inward * 1.2f, 0.1f, 0f));
                }

                if (door.Opened && door.Gate.Raised)
                {
                    _log.Add($"{World.RoomName}: {door.Name} open with {door.Item.Id}");
                    _doors++;
                    Begin(Phase.Cross);
                }
                else if (_ticks > 600)
                {
                    Fail($"in {World.RoomName}, coming near {door.Name} holding {door.Item.Id} did not open it");
                }

                break;
            case Phase.Law:
                _law ??= room.All<LawBreak>().FirstOrDefault(l => !l.Broken);
                if (_law is null)
                {
                    Begin(Phase.Boss);
                    return;
                }

                if (_ticks == 1)
                {
                    Player.Respawn(new Vector3(_law.GlobalPosition.X - _law.Toward * 5f, _law.GlobalPosition.Y + 0.5f, 0f), _law.Toward);
                }

                Hold(_law.Toward > 0 ? InputActions.MoveRight : InputActions.MoveLeft, !_law.Broken);
                if (_law.Broken && Player.InControl)
                {
                    _log.Add($"{World.RoomName}: {_law.LawLine} broken");
                    _laws++;
                    _law = null;
                    Begin(Phase.Boss);
                }
                else if (_ticks > 900)
                {
                    Fail($"in {World.RoomName}, pressing on at the lip did not break the law");
                }

                break;
            case Phase.Boss:
                _seal ??= room.All<BossSeal>().FirstOrDefault(s => !s.Won);
                if (_seal is null)
                {
                    Begin(AfterBoss);
                    return;
                }

                Fight(_seal);
                break;
            case Phase.Cross:
                if (World.RoomName != Hop.From)
                {
                    Fail($"the route leaves {Hop.From} by {Hop.FromExit}, but the pilgrim is in {World.RoomName}");
                    return;
                }

                if (!Hop.Requires.IsMetBy(Player.Abilities.Unlocked, Player.Items.Held))
                {
                    Fail($"the route leaves {Hop.From} by {Hop.FromExit} needing {Hop.Requires}, but the pilgrim holds only " +
                         $"{MapSolver.Holding(Player.Abilities.Unlocked, Player.Items.Held)}");
                    return;
                }

                if (room.FindExit(Hop.FromExit) is not { } exit)
                {
                    Fail($"{Hop.From} has no exit {Hop.FromExit}");
                    return;
                }

                // Into the trigger, moving out: crossing an opening is the travel check's to prove; this one only goes through,
                // holding what a player holds (the move out of a doorway, and down: Ballast sinks on through water).
                Threshold threshold = exit.Threshold;
                Player.Respawn((threshold.Arrival(Player.BodySize) + threshold.Outward * 0.6f).OnPlane());
                Player.Velocity = (threshold.Outward * (threshold.IsHatch ? 10f : 7f)).OnPlane();
                Begin(Phase.Arrive);
                if (!threshold.IsHatch)
                {
                    Hold(threshold.Side == ExitSide.East ? InputActions.MoveRight : InputActions.MoveLeft, true);
                }

                Hold(InputActions.MoveDown, threshold.Side != ExitSide.Up);
                break;
            case Phase.Arrive:
                if (World.RoomName == Hop.To && Player.InControl && _ticks > 30)
                {
                    _log.Add($"{Hop.From} → {Hop.To}");
                    _hop++;
                    if (_hop >= _route.Count)
                    {
                        Begin(Phase.Boss);
                        return;
                    }

                    Begin(Phase.Relic);
                }
                else if (_ticks > 300)
                {
                    Fail($"crossing {Hop.From}:{Hop.FromExit} never arrived in {Hop.To} (in {World.RoomName})");
                }

                break;
            case Phase.End:
                if (_ticks == 1)
                {
                    MapGoal? goal = room.All<MapGoal>().FirstOrDefault(g => g.Ending);
                    if (goal is null)
                    {
                        Fail($"the route ends in {World.RoomName}, which has no ending goal");
                        return;
                    }

                    Player.Respawn(goal.GlobalPosition with { Z = 0f } + new Vector3(0f, -1.5f, 0f));
                }

                if (_ended)
                {
                    Finish();
                }
                else if (_ticks > 600)
                {
                    Fail($"reaching the heart in {World.RoomName} did not end the game");
                }

                break;
        }
    }

    /// <summary>At a lever: interact pulls it, through the real input (pressed one tick, released the next).</summary>
    private void Pull(Lever lever)
    {
        if (_lever != lever)
        {
            _lever = lever;
            _ticks = 0;
            Player.Respawn(lever.GlobalPosition with { Z = 0f });
        }

        Hold(InputActions.Interact, _ticks % 20 == 10);
        if (_ticks > 600)
        {
            Fail($"in {World.RoomName}, interact at the lever {lever.Name} did not pull it");
        }
        else if (lever.Pulled)
        {
            _log.Add($"{World.RoomName}: pulled {lever.Name} ({lever.Item.Id})");
            _levers++;
            _lever = null;
            Hold(InputActions.Interact, false);
        }
    }

    /// <summary>A boss fight through the real inputs, the pilgrim kept alive, until the seal's gates rise.</summary>
    private void Fight(BossSeal seal)
    {
        Machine boss = seal.Boss;
        if (_ticks == 1)
        {
            Player.Respawn(new Vector3(Mathf.Max(seal.ArenaMin.X + 1.5f, boss.PlanePosition.X - 4f), seal.ArenaMin.Y + 1.5f, 0f), 1);
        }

        if (seal.Won)
        {
            if (seal.Gates.All(g => g.Raised))
            {
                _log.Add($"{World.RoomName}: {boss.Species.DisplayName} broken");
                _bosses++;
                _seal = null;
                Begin(AfterBoss);
            }

            return;
        }

        Player.Vitality.Restore();
        Vector2 at = boss.PlanePosition;
        float gap = at.X - Player.GlobalPosition.X, rise = at.Y - Player.GlobalPosition.Y;
        bool hovers = boss.Species.Locomotion == Locomotion.Hover;
        bool reachable = !hovers || boss.Intent.Act is MachineAct.Recover or MachineAct.Strike;
        float near = hovers ? 1.2f : 1.4f;
        Hold(InputActions.MoveRight, reachable && gap > near);
        Hold(InputActions.MoveLeft, reachable && gap < -near);
        if (Mathf.Abs(gap) <= near && Math.Sign(gap) != Player.WorldFacing && gap != 0f)
        {
            Hold(gap > 0f ? InputActions.MoveRight : InputActions.MoveLeft, true);
        }

        Hold(InputActions.Jump, hovers && reachable && rise > 2.2f && Mathf.Abs(gap) < 2.5f && Player.IsOnFloor() && _ticks % 30 < 14);
        Hold(InputActions.Attack, Mathf.Abs(gap) < 2.6f && _ticks % 21 is 1 or 2);
        if (_ticks > 120 * 60)
        {
            Fail($"in {World.RoomName}, the {boss.Species.DisplayName} did not break in 120 s");
        }
    }

    private static void Hold(StringName action, bool held)
    {
        if (held)
        {
            Input.ActionPress(action);
        }
        else
        {
            Input.ActionRelease(action);
        }
    }

    private static void Release()
    {
        foreach (StringName action in new[] { InputActions.Interact, InputActions.Attack, InputActions.MoveRight, InputActions.MoveLeft, InputActions.MoveUp,
                     InputActions.MoveDown, InputActions.Dash, InputActions.Jump })
        {
            Input.ActionRelease(action);
        }
    }

    private void Fail(string problem)
    {
        _problems.Add(problem);
        Finish();
    }

    private void Finish()
    {
        _task = Phase.Done;
        Release();
        foreach (string line in _log)
        {
            GD.Print($"  {line}");
        }

        foreach (string problem in _problems)
        {
            GD.PushError($"PLAYTHROUGH CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"PLAYTHROUGH OK: {_route.Count} ways from {_route[0].From} to {_route[^1].To}, {_relics} relics, {_items} items, {_levers} levers " +
                     $"pulled, {_doors} doors passed, {_laws} laws broken, {_bosses} bosses broken, the end card");
        }

        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }
}
