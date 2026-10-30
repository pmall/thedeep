using System;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>Boots the game: the world loads the start room, then the player goes to its spawn point.</summary>
public partial class Main : Node3D
{
    /// <summary>User argument (after `--`) that runs the scripted smoke test, used by tools/check.sh.</summary>
    private const string SmokeArgument = "--smoke";

    /// <summary>User argument that runs the windowed capture check (see CaptureTestDriver).</summary>
    private const string CaptureTestArgument = "--capture-test";

    /// <summary>`--lookdev &lt;dir&gt; [--size WxH]`: look-dev run for tools/capture.sh (see LookDevDriver).</summary>
    private const string LookDevArgument = "--lookdev";
    private const string SizeArgument = "--size";

    /// <summary>`--perf &lt;file&gt;`: file name of the look-dev frame times (default perf.json).</summary>
    private const string PerfArgument = "--perf";

    /// <summary>`--biome &lt;name&gt;`: start with resources/biomes/&lt;name&gt;.tres instead of the room's own.</summary>
    private const string BiomeArgument = "--biome";

    /// <summary>`--room &lt;name&gt;`: start in scenes/rooms/&lt;name&gt;.tscn instead of the world's start room.</summary>
    private const string RoomArgument = "--room";

    /// <summary>User argument that checks every room scene and quits (see RoomValidator), used by tools/check.sh.</summary>
    private const string ValidateRoomsArgument = "--validate-rooms";

    /// <summary>User argument that crosses every open exit and quits (see TravelValidator), used by tools/check.sh.</summary>
    private const string ValidateTravelArgument = "--validate-travel";

    /// <summary>User argument that runs the scripted Mag-grip check (see GripCheck), used by tools/check.sh.</summary>
    private const string GripCheckArgument = "--grip-check";

    /// <summary>User argument that runs the scripted combat and session check (see CombatCheck), used by tools/check.sh.</summary>
    private const string CombatCheckArgument = "--combat-check";

    /// <summary>User argument that runs the scripted boss and law-break check (see EncounterCheck), used by tools/check.sh.</summary>
    private const string EncounterCheckArgument = "--encounter-check";

    /// <summary>User argument that runs the scripted Harpoon check (see HarpoonCheck), used by tools/check.sh.</summary>
    private const string HarpoonCheckArgument = "--harpoon-check";

    /// <summary>User argument that runs the scripted Drill check (see DrillCheck), used by tools/check.sh.</summary>
    private const string DrillCheckArgument = "--drill-check";

    /// <summary>User argument that checks every machine species and quits (see SpeciesValidator), used by tools/check.sh.</summary>
    private const string ValidateSpeciesArgument = "--validate-species";

    [Export] public World World { get; set; } = null!;
    [Export] public Player Player { get; set; } = null!;
    [Export] public MachineDirector Machines { get; set; } = null!;
    [Export] public OwnerCapture Capture { get; set; } = null!;
    [Export] public SideCamera Camera { get; set; } = null!;
    [Export] public DebugHud Hud { get; set; } = null!;
    [Export] public RoomTravel Travel { get; set; } = null!;
    [Export] public Session Session { get; set; } = null!;
    [Export] public HealthHud Health { get; set; } = null!;

    /// <summary>`--new`: start a new game, ignoring (and not touching) the save until the first rest.</summary>
    private const string NewGameArgument = "--new";

    /// <summary>Arguments that run a check or a look-dev driver: those runs never read or write the save.</summary>
    private static readonly string[] DriverArguments =
    {
        SmokeArgument, CaptureTestArgument, LookDevArgument, ValidateRoomsArgument, ValidateTravelArgument, ValidateSpeciesArgument,
        GripCheckArgument, CombatCheckArgument, HarpoonCheckArgument, EncounterCheckArgument, DrillCheckArgument,
    };

    private SaveData? _resume;

    /// <summary>Where a new game starts (the world's start room before a save or `--room` replaces it): the map check's start.</summary>
    private string _gameStart = "";

    public override void _EnterTree()
    {
        _gameStart = World.StartRoom.ResourcePath;
        string[] args = OS.GetCmdlineUserArgs();
        bool driven = DriverArguments.Any(a => Array.IndexOf(args, a) >= 0);
        bool pickedRoom = Array.IndexOf(args, RoomArgument) >= 0;

        // A normal launch resumes from the save: the world starts in the room of the last shrine rested at.
        if (!driven && !pickedRoom && Array.IndexOf(args, NewGameArgument) < 0 && SaveFile.Read() is { } save
            && ResourceLoader.Exists(World.RoomPath(save.Room)))
        {
            _resume = save;
            World.StartRoom = GD.Load<PackedScene>(World.RoomPath(save.Room));
        }

        Session.Persist = !driven && !pickedRoom;

        // Before the world is ready, so it loads the requested room instead of its start room.
        if (ArgumentValue(args, RoomArgument) is { } roomName)
        {
            string path = World.RoomPath(roomName);
            if (ResourceLoader.Exists(path))
            {
                World.StartRoom = GD.Load<PackedScene>(path);
            }
            else
            {
                GD.PushError($"no room named {roomName} ({path})");
            }
        }
    }

    public override void _Ready()
    {
        Room room = World.Room;
        Player.Respawn(room.SpawnPoint.GlobalPosition);
        if (_resume is not null)
        {
            Session.Resume(_resume);
        }

        string[] args = OS.GetCmdlineUserArgs();
        if (ArgumentValue(args, BiomeArgument) is { } biomeName)
        {
            if (BiomeProfile.Load(biomeName) is { } biome)
            {
                room.ApplyBiome(biome);
            }
            else
            {
                GD.PushError($"no biome profile named {biomeName} in {BiomeProfile.Folder}");
            }
        }

        if (Array.IndexOf(args, ValidateRoomsArgument) >= 0)
        {
            AddChild(new RoomValidator { StartRoom = _gameStart, Report = Array.IndexOf(args, "--map-report") >= 0 });
        }
        else if (Array.IndexOf(args, ValidateTravelArgument) >= 0)
        {
            AddChild(new TravelValidator { World = World, Player = Player, Travel = Travel });
        }
        else if (Array.IndexOf(args, GripCheckArgument) >= 0)
        {
            AddChild(new GripCheck { Player = Player });
        }
        else if (Array.IndexOf(args, HarpoonCheckArgument) >= 0)
        {
            AddChild(new HarpoonCheck { Player = Player, World = World });
        }
        else if (Array.IndexOf(args, DrillCheckArgument) >= 0)
        {
            AddChild(new DrillCheck { Player = Player, World = World });
        }
        else if (Array.IndexOf(args, CombatCheckArgument) >= 0)
        {
            AddChild(new CombatCheck { Player = Player, World = World, Session = Session });
        }
        else if (Array.IndexOf(args, EncounterCheckArgument) >= 0)
        {
            AddChild(new EncounterCheck { Player = Player, World = World, Session = Session });
        }
        else if (Array.IndexOf(args, ValidateSpeciesArgument) >= 0)
        {
            AddChild(new SpeciesValidator { Room = room, Player = Player, Machines = Machines });
        }
        else if (Array.IndexOf(args, SmokeArgument) >= 0)
        {
            AddChild(new SmokeDriver { Player = Player });
        }
        else if (Array.IndexOf(args, CaptureTestArgument) >= 0)
        {
            AddChild(new CaptureTestDriver { Capture = Capture });
        }
        else if (ArgumentValue(args, LookDevArgument) is { } outputDir)
        {
            Travel.Enabled = false;
            var driver = new LookDevDriver
            {
                Player = Player, Room = room, Camera = Camera, Machines = Machines, Overlays = new CanvasLayer[] { Hud, Capture, Health, Session.Banner },
                OutputDir = outputDir, PerfFile = ArgumentValue(args, PerfArgument) ?? "perf.json",
            };
            if (ArgumentValue(args, SizeArgument) is { } size && size.Split('x') is [var w, var h]
                && int.TryParse(w, out int width) && int.TryParse(h, out int height))
            {
                driver.RenderSize = new Vector2I(width, height);
            }

            AddChild(driver);
        }
    }

    /// <summary>The argument following <paramref name="name"/>, or null.</summary>
    private static string? ArgumentValue(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
