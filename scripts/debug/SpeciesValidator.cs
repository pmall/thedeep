using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// Checks every machine species and quits (`-- --room MachineArena --validate-species`, run by tools/check.sh;
/// docs/long-term.md, objective 1). Prints `SPECIES OK` and a report, or one `SPECIES CHECK` error per problem:
/// - every resource in <see cref="MachineSpecies.Folder"/> loads, its id is its file name, it has a behavior and
///   a body whose parts, chains, drivers and legs all name existing joints, and its creed line exists;
/// - each species then lives alone in the room for a while, first without the player, then with the player
///   running, jumping and dashing around it: any error fails the check, and so does a machine leaving the room.
/// The report counts species per layer and hostile ones, and flags likely duplicates (same behavior, same body).
/// </summary>
public partial class SpeciesValidator : Node
{
    private const int AloneTicks = 240;
    private const int WithPlayerTicks = 360;

    private readonly List<string> _problems = new();
    private IEnumerator<int>? _script;

    public Room Room { get; set; } = null!;
    public Player Player { get; set; } = null!;
    public MachineDirector Machines { get; set; } = null!;

    public SpeciesValidator()
    {
        ProcessPhysicsPriority = -100;
    }

    public override void _PhysicsProcess(double delta)
    {
        _script ??= Run().GetEnumerator();
        if (!_script.MoveNext())
        {
            SetPhysicsProcess(false);
        }
    }

    private IEnumerable<int> Run()
    {
        List<MachineSpecies> species = Load();
        LineBook? lines = GD.Load<LineBook>(LineBook.Path);
        if (lines is null)
        {
            _problems.Add($"no line book at {LineBook.Path}");
        }

        foreach (MachineSpecies s in species)
        {
            CheckData(s, lines);
        }

        // Each species alone in the room: the room's own machines step aside.
        foreach (Machine resident in Machines.Machines.ToList())
        {
            Machines.Remove(resident);
            resident.QueueFree();
        }

        var observed = new Dictionary<StringName, SortedSet<MachineAct>>();
        foreach (MachineSpecies s in species.Where(s => s.Behavior is not null && s.Assembly is not null))
        {
            var machine = new Machine { Name = $"Check_{s.Id}", Species = s, Position = SpawnFor(s) };
            Room.AddChild(machine);
            Machines.Add(machine);
            var acts = observed[s.Id] = new SortedSet<MachineAct>();

            Machines.PlayerPresent = false;
            Player.Respawn(Room.SpawnPoint.GlobalPosition, 1);
            for (int i = 0; i < AloneTicks; i++)
            {
                yield return 0;
                acts.Add(machine.Intent.Act);
            }

            Machines.PlayerPresent = true;
            Player.Respawn(new Vector3(machine.PlanePosition.X - 3f, Room.SpawnPoint.GlobalPosition.Y, 0f), 1);
            var demo = new DemoInput();
            for (int i = 0; i < WithPlayerTicks; i++)
            {
                demo.Tick();
                yield return 0;
                acts.Add(machine.Intent.Act);
            }

            DemoInput.ReleaseAll();
            Vector2 p = machine.PlanePosition;
            if (!float.IsFinite(p.X) || !float.IsFinite(p.Y) || p.X < Room.BoundsMin.X || p.X > Room.BoundsMax.X
                || p.Y < Room.BoundsMin.Y || p.Y > Room.BoundsMax.Y)
            {
                _problems.Add($"{s.Id}: ended outside the room at {p}");
            }

            Machines.Remove(machine);
            machine.QueueFree();
        }

        Report(species, observed);
        GetTree().Quit(_problems.Count == 0 ? 0 : 1);
    }

    private List<MachineSpecies> Load()
    {
        var species = new List<MachineSpecies>();
        foreach (string file in DirAccess.GetFilesAt(MachineSpecies.Folder).Where(f => f.EndsWith(".tres")).Order())
        {
            string path = $"{MachineSpecies.Folder}/{file}";
            if (ResourceLoader.Load(path) is not MachineSpecies s)
            {
                _problems.Add($"{path}: not a MachineSpecies");
                continue;
            }

            if (s.Id != file.GetBaseName())
            {
                _problems.Add($"{path}: id {s.Id} is not its file name");
            }

            species.Add(s);
        }

        if (species.Count == 0)
        {
            _problems.Add($"no species in {MachineSpecies.Folder}");
        }

        return species;
    }

    private void CheckData(MachineSpecies s, LineBook? lines)
    {
        void Problem(string what) => _problems.Add($"{s.ResourcePath}: {what}");
        if (string.IsNullOrWhiteSpace(s.DisplayName))
        {
            Problem("no display name");
        }

        if (s.Behavior is null)
        {
            Problem("no behavior");
        }

        if (lines is not null && lines.Find(s.CreedLine) is null)
        {
            Problem($"creed line {s.CreedLine} is not in {LineBook.Path}");
        }

        if (s.Assembly is null)
        {
            Problem("no body (Assembly)");
            return;
        }

        var joints = new HashSet<StringName>();
        foreach (RigJoint joint in s.Assembly.Joints)
        {
            if (!joints.Add(joint.Name))
            {
                Problem($"joint {joint.Name} defined twice");
            }

            if (!joint.Parent.IsEmpty && !joints.Contains(joint.Parent))
            {
                Problem($"joint {joint.Name} hangs from {joint.Parent}, which is not listed before it");
            }
        }

        foreach (PartSlot part in s.Assembly.Parts)
        {
            if (!joints.Contains(part.Joint))
            {
                Problem($"a part sits on unknown joint {part.Joint}");
            }

            if (part.Mesh is null)
            {
                Problem($"a part on {part.Joint} has no mesh");
            }
        }

        foreach (PartChain chain in s.Assembly.Chains.Where(c => !joints.Contains(c.Joint)))
        {
            Problem($"chain {chain.Name} hangs from unknown joint {chain.Joint}");
        }

        var legJoints = new HashSet<StringName>();
        foreach (MachineLeg leg in s.Legs)
        {
            foreach (StringName joint in new[] { leg.Upper, leg.Lower, leg.Foot })
            {
                if (!joints.Contains(joint))
                {
                    Problem($"leg joint {joint} does not exist");
                }
            }

            legJoints.Add(leg.Upper);
            legJoints.Add(leg.Lower);
        }

        foreach (JointDriver driver in s.Drivers)
        {
            if (!joints.Contains(driver.Joint))
            {
                Problem($"driver on unknown joint {driver.Joint}");
            }
            else if (legJoints.Contains(driver.Joint))
            {
                Problem($"driver on {driver.Joint}, which a leg already poses");
            }
        }
    }

    /// <summary>Where a species is tried: on the floor, in the air, or on top of the room's first metal block.</summary>
    private Vector3 SpawnFor(MachineSpecies s)
    {
        Vector3 floor = Room.SpawnPoint.GlobalPosition + new Vector3(8f, 0f, 0f);
        switch (s.Locomotion)
        {
            case Locomotion.Hover:
                return floor + new Vector3(0f, 3f, 0f);
            case Locomotion.Crawl:
                Block? metal = Room.Blocks().FirstOrDefault(b => b.Surface == SurfaceKind.Metal);
                if (metal is null)
                {
                    _problems.Add($"{s.Id}: a crawler, but {Room.Name} has no metal block to try it on");
                    return floor;
                }

                return metal.GlobalPosition + new Vector3(0f, metal.Size.Y * 0.5f, 0f);
            default:
                return floor;
        }
    }

    private void Report(List<MachineSpecies> species, Dictionary<StringName, SortedSet<MachineAct>> observed)
    {
        foreach (MachineSpecies s in species)
        {
            string acts = observed.TryGetValue(s.Id, out SortedSet<MachineAct>? set) ? string.Join(", ", set) : "not run";
            GD.Print($"  {s.Id,-14} {s.Layer,-9} {(s.Hostile ? "hostile" : "peaceful"),-9} {s.Behavior?.GetType().Name,-18} seen: {acts}");
        }

        // Likely duplicates: same behavior and same body (a variant, not a species; docs/long-term.md).
        var duplicates = species.Where(s => s.Behavior is not null && s.Assembly is not null)
            .GroupBy(s => (s.Behavior.GetType(), s.Assembly.ResourcePath))
            .Where(g => g.Count() > 1)
            .Select(g => string.Join(" = ", g.Select(s => s.Id)))
            .ToList();
        string layers = string.Join(", ", species.GroupBy(s => s.Layer).OrderBy(g => g.Key).Select(g => $"{g.Key} {g.Count()}"));
        int peaceful = species.Count(s => !s.Hostile);
        foreach (string problem in _problems)
        {
            GD.PushError($"SPECIES CHECK: {problem}");
        }

        if (_problems.Count == 0)
        {
            GD.Print($"SPECIES OK: {species.Count} species ({layers}), {peaceful} non-hostile, "
                + (duplicates.Count == 0 ? "no likely duplicates" : $"likely duplicates: {string.Join("; ", duplicates)}"));
        }
    }
}
