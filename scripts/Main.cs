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

    /// <summary>
    /// `--abilities &lt;list&gt;`: start with these abilities (`all`, `none`, or names joined by commas: `MagGrip,Drill`),
    /// instead of what a route to the `--room` needs.
    /// </summary>
    private const string AbilitiesArgument = "--abilities";

    /// <summary>The whole game's draft map: `--room` asks its solver what a route to the room needs.</summary>
    private const string DraftPath = "res://docs/map-draft.md";

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

    /// <summary>User argument that runs the scripted water and Ballast check (see BallastCheck), used by tools/check.sh.</summary>
    private const string BallastCheckArgument = "--ballast-check";

    /// <summary>User argument that runs the scripted Heat plating check (see HeatCheck), used by tools/check.sh.</summary>
    private const string HeatCheckArgument = "--heat-check";

    /// <summary>User argument that plays the whole run from a new game to the end (see PlaythroughCheck), used by tools/check.sh.</summary>
    private const string PlaythroughCheckArgument = "--playthrough-check";

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
    [Export] public HintLine Hint { get; set; } = null!;
    [Export] public MapScreen MapScreen { get; set; } = null!;
    [Export] public GateHints Hints { get; set; } = null!;

    /// <summary>`--ui-shot &lt;dir&gt;`: renders a gate hint and the map screen to PNGs (see UiShot; windowed).</summary>
    private const string UiShotArgument = "--ui-shot";

    /// <summary>`--new`: start a new game, ignoring (and not touching) the save until the first rest.</summary>
    private const string NewGameArgument = "--new";

    /// <summary>Arguments that run a check or a look-dev driver: those runs never read or write the save.</summary>
    private static readonly string[] DriverArguments =
    {
        SmokeArgument, CaptureTestArgument, LookDevArgument, ValidateRoomsArgument, ValidateTravelArgument, ValidateSpeciesArgument,
        UiShotArgument, PlaythroughCheckArgument, GripCheckArgument, CombatCheckArgument, HarpoonCheckArgument, EncounterCheckArgument, DrillCheckArgument, BallastCheckArgument, HeatCheckArgument,
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
        if (!DriverArguments.Any(a => Array.IndexOf(args, a) >= 0))
        {
            GrantStartAbilities(args);
        }

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
            AddChild(new RoomValidator
            {
                StartRoom = _gameStart, Report = Array.IndexOf(args, "--map-report") >= 0, ReachReport = ArgumentValue(args, "--reach-report") ?? "", SlackReport = Array.IndexOf(args, "--slack-report") >= 0,
                ReachWith = ArgumentValue(args, AbilitiesArgument) is { } with ? ParseAbilities(with) : null,
            });
        }
        else if (Array.IndexOf(args, ValidateTravelArgument) >= 0)
        {
            AddChild(new TravelValidator { World = World, Player = Player, Travel = Travel });
        }
        else if (Array.IndexOf(args, GripCheckArgument) >= 0)
        {
            AddChild(new GripCheck { Player = Player, World = World });
        }
        else if (Array.IndexOf(args, HarpoonCheckArgument) >= 0)
        {
            AddChild(new HarpoonCheck { Player = Player, World = World });
        }
        else if (Array.IndexOf(args, DrillCheckArgument) >= 0)
        {
            AddChild(new DrillCheck { Player = Player, World = World, Hints = Hints });
        }
        else if (Array.IndexOf(args, BallastCheckArgument) >= 0)
        {
            AddChild(new BallastCheck { Player = Player, World = World, Hints = Hints });
        }
        else if (Array.IndexOf(args, HeatCheckArgument) >= 0)
        {
            AddChild(new HeatCheck { Player = Player, World = World, Hints = Hints });
        }
        else if (Array.IndexOf(args, CombatCheckArgument) >= 0)
        {
            AddChild(new CombatCheck { Player = Player, World = World, Session = Session });
        }
        else if (Array.IndexOf(args, EncounterCheckArgument) >= 0)
        {
            AddChild(new EncounterCheck { Player = Player, World = World, Session = Session });
        }
        else if (Array.IndexOf(args, PlaythroughCheckArgument) >= 0)
        {
            AddChild(new PlaythroughCheck { World = World, Player = Player, Session = Session });
        }
        else if (Array.IndexOf(args, ValidateSpeciesArgument) >= 0)
        {
            AddChild(new SpeciesValidator { Room = room, Player = Player, Machines = Machines });
        }
        else if (ArgumentValue(args, UiShotArgument) is { } shotDir)
        {
            AddChild(new UiShot { World = World, Player = Player, Travel = Travel, Hint = Hint, Map = MapScreen, EndCard = Session.EndCard, Manual = Hud.Manual, DebugKeys = Hud.DebugKeys, OutputDir = shotDir });
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
                Player = Player, Room = room, Camera = Camera, Machines = Machines, Overlays = new CanvasLayer[] { Hud, Capture, Health, Session.Banner, Hint },
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

    /// <summary>
    /// A room picked with `--room` starts with the abilities a route there needs (the draft map's solver: the fewest a pilgrim
    /// can own arriving there), so any room can be tried as a player would come to it; `--abilities` says otherwise.
    /// </summary>
    private void GrantStartAbilities(string[] args)
    {
        Ability? grant = null;
        if (ArgumentValue(args, AbilitiesArgument) is { } list)
        {
            grant = ParseAbilities(list);
            if (grant is null)
            {
                GD.PushError($"--abilities {list}: expected all, none, or ability names joined by commas ({string.Join(", ", AbilitySet.All)})");
            }
        }
        else if (Array.IndexOf(args, RoomArgument) >= 0 && FileAccess.FileExists(DraftPath))
        {
            try
            {
                MapDraft draft = MapDraft.FromMarkdown(FileAccess.GetFileAsString(DraftPath));
                grant = MapSolver.Arrival(draft.Graph, World.RoomName);
            }
            catch (FormatException e)
            {
                GD.PushError($"map draft ({DraftPath}): {e.Message}");
            }
        }

        if (grant is { } owned)
        {
            Player.Abilities.Set(owned, true);
            GD.Print($"[start] {World.RoomName} with {(owned == Ability.None ? "no abilities" : owned.ToString())}");
        }
    }

    /// <summary>`all`, `none`, or ability names joined by commas; null if a name is unknown.</summary>
    private static Ability? ParseAbilities(string list)
    {
        string trimmed = list.Trim();
        if (trimmed.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return AbilitySet.All.Aggregate(Ability.None, (a, b) => a | b);
        }

        if (trimmed.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return Ability.None;
        }

        Ability owned = Ability.None;
        foreach (string name in trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Enum.TryParse(name, ignoreCase: true, out Ability a) || a == Ability.None)
            {
                return null;
            }

            owned |= a;
        }

        return owned;
    }

    /// <summary>The argument following <paramref name="name"/>, or null.</summary>
    private static string? ArgumentValue(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
