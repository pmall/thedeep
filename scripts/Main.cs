using System;
using System.Globalization;
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

    /// <summary>`--shot`: plays normally, takes one F12 screenshot after <see cref="ShotDelay"/> (the camera settled), then quits.</summary>
    private const string ShotArgument = "--shot";

    /// <summary>Seconds of play before the `--shot` screenshot.</summary>
    private const double ShotDelay = 1.5;

    /// <summary>`--world &lt;name&gt;`: play worlds/&lt;name&gt;/world.tres instead of the main scene's world (the prototype).</summary>
    private const string WorldArgument = "--world";

    /// <summary>`--room &lt;name&gt;`: start in the world's room of that name instead of its start room.</summary>
    private const string RoomArgument = "--room";

    /// <summary>
    /// `--abilities &lt;list&gt;`: start with these abilities (`all`, `none`, or names joined by commas: `MagGrip,Drill`),
    /// instead of what a route to the `--room` needs.
    /// </summary>
    private const string AbilitiesArgument = "--abilities";

    /// <summary>`--items &lt;ids&gt;`: start holding these items (the world's item ids joined by commas: `SacristyKey,KilnLever`).</summary>
    private const string ItemsArgument = "--items";

    /// <summary>`--shard K/N` with `--validate-travel`: cross only every Nth exit from the Kth (1-based), so the check can run in parts.</summary>
    private const string ShardArgument = "--shard";

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

    /// <summary>User argument that runs the fight moveset check (see FightCheck), used by tools/check.sh.</summary>
    private const string FightCheckArgument = "--fight-check";

    /// <summary>User argument that runs the hidden passage check (see SecretCheck), used by tools/check.sh.</summary>
    private const string SecretCheckArgument = "--secret-check";

    /// <summary>User argument that runs the ambush check (see AmbushCheck), used by tools/check.sh.</summary>
    private const string AmbushCheckArgument = "--ambush-check";

    /// <summary>User argument that runs the dialogue check (see DialogueCheck), used by tools/check.sh.</summary>
    private const string DialogueCheckArgument = "--dialogue-check";

    /// <summary>User argument that runs the power-up check (see PowerUpCheck), used by tools/check.sh.</summary>
    private const string PowerUpCheckArgument = "--powerup-check";

    /// <summary>User argument that runs the key and locked door check (see KeyCheck), used by tools/check.sh.</summary>
    private const string KeyCheckArgument = "--key-check";

    /// <summary>User argument that runs the code and sealed door check (see KeyCheck.Pyre), used by tools/check.sh.</summary>
    private const string CodeCheckArgument = "--code-check";

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
    [Export] public AnchorPointer Pointer { get; set; } = null!;

    [Export] public MapScreen MapScreen { get; set; } = null!;
    [Export] public GateHints Hints { get; set; } = null!;
    [Export] public Keyring Keyring { get; set; } = null!;
    [Export] public PowerUps PowerUps { get; set; } = null!;
    [Export] public Conversations Conversations { get; set; } = null!;
    [Export] public DialogueBox DialogueBox { get; set; } = null!;
    [Export] public Secrets Secrets { get; set; } = null!;

    /// <summary>`--ui-shot &lt;dir&gt;`: renders a gate hint and the map screen to PNGs (see UiShot; windowed).</summary>
    private const string UiShotArgument = "--ui-shot";

    /// <summary>`--bake-surfaces`: bakes the materials' texture sets to PNGs and quits (see SurfaceBake; headless is fine).</summary>
    private const string BakeSurfacesArgument = "--bake-surfaces";

    /// <summary>`--new`: start a new game, ignoring (and not touching) the save until the first rest.</summary>
    private const string NewGameArgument = "--new";

    /// <summary>Arguments that run a check or a look-dev driver: those runs never read or write the save.</summary>
    private static readonly string[] DriverArguments =
    {
        SmokeArgument, CaptureTestArgument, LookDevArgument, ValidateRoomsArgument, ValidateTravelArgument, ValidateSpeciesArgument,
        UiShotArgument, PlaythroughCheckArgument, GripCheckArgument, CombatCheckArgument, HarpoonCheckArgument, EncounterCheckArgument, DrillCheckArgument, BallastCheckArgument, HeatCheckArgument, KeyCheckArgument, CodeCheckArgument, PowerUpCheckArgument, DialogueCheckArgument, AmbushCheckArgument, SecretCheckArgument, FightCheckArgument, BakeSurfacesArgument,
    };

    private SaveData? _resume;

    /// <summary>Where a new game starts (the world's start room before a save or `--room` replaces it): the map check's start.</summary>
    private string _gameStart = "";

    public override void _EnterTree()
    {
        string[] args = OS.GetCmdlineUserArgs();
        if (ArgumentValue(args, WorldArgument) is { } worldName)
        {
            if (WorldDefinition.Load(worldName) is { } definition)
            {
                World.Definition = definition;
            }
            else
            {
                GD.PushError($"no world named {worldName} ({WorldDefinition.Folder}/{worldName}/{WorldDefinition.FileName})");
            }
        }

        _gameStart = World.Definition.StartRoom.ResourcePath;
        bool driven = DriverArguments.Any(a => Array.IndexOf(args, a) >= 0);
        bool pickedRoom = Array.IndexOf(args, RoomArgument) >= 0;

        // A normal launch resumes from the save: the world starts in the room of the last shrine rested at.
        if (!driven && !pickedRoom && Array.IndexOf(args, NewGameArgument) < 0 && SaveFile.Read(World.Definition.SavePath) is { } save
            && ResourceLoader.Exists(World.RoomPath(save.Room)))
        {
            _resume = save;
            World.FirstRoom = GD.Load<PackedScene>(World.RoomPath(save.Room));
        }

        Session.Persist = !driven && !pickedRoom;

        // Before the world is ready, so it loads the requested room instead of its start room.
        if (ArgumentValue(args, RoomArgument) is { } roomName)
        {
            string path = World.RoomPath(roomName);
            if (ResourceLoader.Exists(path))
            {
                World.FirstRoom = GD.Load<PackedScene>(path);
            }
            else
            {
                GD.PushError($"no room named {roomName} ({path})");
            }
        }
    }

    /// <summary>
    /// Runs once, when the game quits (every way: a check's <c>Quit</c>, <c>--quit-after</c>, the window closed), before the
    /// engine frees its servers. The game's nodes are freed here and every C# wrapper they leave finalized now, on the main
    /// thread, as <see cref="World.Load"/> does between rooms: a wrapper left to the GC's finalizer thread lets go of its
    /// engine object (a shape, a mesh) whenever that thread runs, which at exit can be while the engine is tearing down the
    /// servers that object lives in (a crash, or RIDs reported leaked at exit). Twice: a finalizer can release the last
    /// reference to another wrapped object.
    /// </summary>
    public override void _ExitTree()
    {
        foreach (Node child in GetChildren())
        {
            child.Free();
        }

        for (int pass = 0; pass < 2; pass++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
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

        if (Array.IndexOf(args, ShotArgument) >= 0)
        {
            Capture.Saved += _ => GetTree().Quit();
            GetTree().CreateTimer(ShotDelay).Timeout += Capture.TakeScreenshot;
        }

        if (Array.IndexOf(args, ValidateRoomsArgument) >= 0)
        {
            AddChild(new RoomValidator
            {
                World = World,
                Tuning = Player.Tuning,
                ClimbRise = Travel.ClimbRise,
                StartRoom = _gameStart,
                Report = Array.IndexOf(args, "--map-report") >= 0,
                ReachReport = ArgumentValue(args, "--reach-report") ?? "",
                SlackReport = Array.IndexOf(args, "--slack-report") >= 0,
                ReachWith = ArgumentValue(args, AbilitiesArgument) is { } with ? ParseAbilities(with) : null,
            });
        }
        else if (Array.IndexOf(args, ValidateTravelArgument) >= 0)
        {
            AddChild(new TravelValidator { World = World, Player = Player, Travel = Travel, Shard = ParseShard(ArgumentValue(args, ShardArgument)) });
        }
        else if (Array.IndexOf(args, GripCheckArgument) >= 0)
        {
            AddChild(new GripCheck { Player = Player, World = World });
        }
        else if (Array.IndexOf(args, HarpoonCheckArgument) >= 0)
        {
            AddChild(new HarpoonCheck { Player = Player, World = World, Pointer = Pointer });
        }
        else if (Array.IndexOf(args, DrillCheckArgument) >= 0)
        {
            AddChild(new DrillCheck { Player = Player, World = World, Hints = Hints });
        }
        else if (Array.IndexOf(args, BallastCheckArgument) >= 0)
        {
            AddChild(new BallastCheck { Player = Player, World = World, Hints = Hints });
        }
        else if (Array.IndexOf(args, FightCheckArgument) >= 0)
        {
            AddChild(new FightCheck { Player = Player, World = World });
        }
        else if (Array.IndexOf(args, SecretCheckArgument) >= 0)
        {
            AddChild(new SecretCheck { Player = Player, World = World, Secrets = Secrets, Session = Session });
        }
        else if (Array.IndexOf(args, AmbushCheckArgument) >= 0)
        {
            AddChild(new AmbushCheck { Player = Player, World = World, Session = Session });
        }
        else if (Array.IndexOf(args, DialogueCheckArgument) >= 0)
        {
            AddChild(new DialogueCheck
            {
                Player = Player,
                World = World,
                Conversations = Conversations,
                Box = DialogueBox,
                Session = Session,
            });
        }
        else if (Array.IndexOf(args, PowerUpCheckArgument) >= 0)
        {
            AddChild(new PowerUpCheck { Player = Player, World = World, PowerUps = PowerUps, Session = Session });
        }
        else if (Array.IndexOf(args, KeyCheckArgument) >= 0)
        {
            AddChild(new KeyCheck { Player = Player, World = World, Keyring = Keyring, Session = Session });
        }
        else if (Array.IndexOf(args, CodeCheckArgument) >= 0)
        {
            AddChild(new KeyCheck { Player = Player, World = World, Keyring = Keyring, Session = Session, Checked = KeyCheck.Pyre });
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
            AddChild(new SpeciesValidator { World = World, Room = room, Player = Player, Machines = Machines });
        }
        else if (ArgumentValue(args, UiShotArgument) is { } shotDir)
        {
            AddChild(new UiShot { World = World, Player = Player, Travel = Travel, Hint = Hint, Map = MapScreen, EndCard = Session.EndCard, Manual = Hud.Manual, DebugKeys = Hud.DebugKeys, Dialogue = DialogueBox, OutputDir = shotDir });
        }
        else if (Array.IndexOf(args, BakeSurfacesArgument) >= 0)
        {
            AddChild(new SurfaceBake());
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
                Player = Player,
                Room = room,
                Camera = Camera,
                Machines = Machines,
                Overlays = new CanvasLayer[] { Hud, Capture, Health, Session.Banner, Hint },
                OutputDir = outputDir,
                PerfFile = ArgumentValue(args, PerfArgument) ?? "perf.json",
            };
            if (ArgumentValue(args, SizeArgument) is { } size && size.Split('x') is [var w, var h]
                && int.TryParse(w, NumberStyles.Integer, CultureInfo.InvariantCulture, out int width) && int.TryParse(h, NumberStyles.Integer, CultureInfo.InvariantCulture, out int height))
            {
                driver.RenderSize = new Vector2I(width, height);
            }

            AddChild(driver);
        }
    }

    /// <summary>
    /// A room picked with `--room` starts with the abilities and items a route there needs (the world's network and its solver: the
    /// fewest a pilgrim can hold arriving there), so any room can be tried as a player would come to it; `--abilities` says otherwise
    /// (and holds no items), `--items` adds items.
    /// </summary>
    private void GrantStartAbilities(string[] args)
    {
        Ability? grant = null;
        ItemSet items = ItemSet.Empty;
        if (ArgumentValue(args, AbilitiesArgument) is { } list)
        {
            grant = ParseAbilities(list);
            if (grant is null)
            {
                GD.PushError($"--abilities {list}: expected all, none, or ability names joined by commas ({string.Join(", ", AbilitySet.All)})");
            }
        }
        else if (Array.IndexOf(args, RoomArgument) >= 0 && FileAccess.FileExists(World.Definition.Network))
        {
            try
            {
                MapDraft draft = MapDraft.FromMarkdown(FileAccess.GetFileAsString(World.Definition.Network));
                if (MapSolver.Arrival(draft.Graph, World.RoomName) is { } arrival)
                {
                    (grant, items) = (arrival.Owned, arrival.Held);
                }
            }
            catch (FormatException e)
            {
                GD.PushError($"the world's network ({World.Definition.Network}): {e.Message}");
            }
        }

        if (ArgumentValue(args, ItemsArgument) is { } ids)
        {
            string[] named = ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (string unknown in named.Where(id => World.Definition.Item(id) is null))
            {
                GD.PushError($"--items: {unknown} is not one of the world's items ({string.Join(", ", World.Definition.Items.Select(i => i.Id))})");
            }

            items = items.Union(ItemSet.Of(named.Where(id => World.Definition.Item(id) is not null)));
            grant ??= Ability.None;
        }

        if (grant is { } owned)
        {
            Player.Abilities.Set(owned, true);
            Player.Items.Restore(items);
            GD.Print($"[start] {World.RoomName} with {(owned == Ability.None ? "no abilities" : owned.ToString())}" +
                     (items.Count == 0 ? "" : $", holding {items}"));
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
    /// <summary>`K/N` (1 ≤ K ≤ N) as a part of a list; the whole list when absent or malformed (said in the log).</summary>
    private static (int Index, int Count) ParseShard(string? text)
    {
        if (text is null)
        {
            return (1, 1);
        }

        string[] parts = text.Split('/');
        if (parts.Length == 2 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int k) && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n >= 1 && k >= 1 && k <= n)
        {
            return (k, n);
        }

        GD.PushError($"{ShardArgument} takes K/N with 1 <= K <= N, not {text}");
        return (1, 1);
    }

    private static string? ArgumentValue(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
}
