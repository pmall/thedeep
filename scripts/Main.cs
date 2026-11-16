using System;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// Boots the game: the world loads the start room, then the player goes to its spawn point. What the launch was asked to do is read
/// once from the command line (<see cref="LaunchOptions"/>); a command line the game cannot follow quits with the reason, so it never
/// runs something else. The checks and tools that run in place of the game are the table <see cref="Drivers"/>.
/// </summary>
public partial class Main : Node3D
{
    /// <summary>Seconds of play before the `--shot` screenshot.</summary>
    private const double ShotDelay = 1.5;

    /// <summary>The exit code of a launch whose command line is refused.</summary>
    private const int RefusedExitCode = 2;

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

    /// <summary>Where a new game starts (the world's start room before a save or `--room` replaces it): the map check's start.</summary>
    public string GameStart { get; private set; } = "";

    private LaunchOptions _options = new();
    private bool _refused;

    public override void _EnterTree()
    {
        switch (LaunchOptions.Parse(OS.GetCmdlineUserArgs(), Drivers.Flags))
        {
            case LaunchParse.Invalid(var message):
                Refuse(message);
                return;
            case LaunchParse.Parsed(var options):
                _options = options;
                break;
        }

        if (_options.World is { } worldName)
        {
            if (WorldDefinition.Load(worldName) is not { } definition)
            {
                Refuse($"no world named {worldName} ({WorldDefinition.Folder}/{worldName}/{WorldDefinition.FileName})");
                return;
            }

            World.Definition = definition;
        }

        if (_options.SaveName is { } saveName)
        {
            World.Definition.SaveName = saveName;
        }

        GameStart = World.Definition.StartRoom.ResourcePath;
        bool driven = Drivers.Driven(_options);
        bool pickedRoom = _options.Room is not null;

        // A normal launch resumes from the save: the world starts in the room of the last shrine rested at, and the session holds
        // the save before any node is ready, so every node that wires that room sees what is done there.
        if (!driven && !pickedRoom && !_options.NewGame && SaveFile.Read(World.Definition.SavePath) is { } save
            && ResourceLoader.Exists(World.RoomPath(save.Room)))
        {
            Session.Resume(save);
            World.FirstRoom = GD.Load<PackedScene>(World.RoomPath(save.Room));
        }

        // The save check's writing run saves too, but only on a scratch save name: a check never writes a real save.
        bool scratchWrite = Drivers.WritesScratchSave(_options)
            && World.Definition.SaveName.StartsWith(SaveCheck.ScratchPrefix, StringComparison.Ordinal);
        Session.Persist = (!driven && !pickedRoom) || scratchWrite;

        // Before the world is ready, so it loads the requested room instead of its start room.
        if (_options.Room is { } roomName)
        {
            string path = World.RoomPath(roomName);
            if (!ResourceLoader.Exists(path))
            {
                Refuse($"no room named {roomName} ({path})");
                return;
            }

            World.FirstRoom = GD.Load<PackedScene>(path);
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
        foreach (Node child in this.Children())
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
        if (_refused)
        {
            return;
        }

        Room room = World.Room;
        Player.Respawn(Session.RespawnPoint(room), 1);

        // The UI shots see a room as a player comes to it (the Pyre's heat stands at its door).
        if ((!Drivers.Driven(_options) || Drivers.ShowsRoomAsPlayed(_options)) && !GrantStartAbilities())
        {
            return;
        }

        if (_options.Biome is { } biomeName)
        {
            if (BiomeProfile.Load(biomeName) is not { } biome)
            {
                Refuse($"no biome profile named {biomeName} in {BiomeProfile.Folder}");
                return;
            }

            room.ApplyBiome(biome);
        }

        if (_options.Shot)
        {
            Capture.Saved += _ => GetTree().Quit();
            GetTree().CreateTimer(ShotDelay).Timeout += Capture.TakeScreenshot;
        }

        if (Drivers.Build(this, _options) is { } driver)
        {
            AddChild(driver);
        }
    }

    /// <summary>Says why the launch cannot do what was asked and quits: it does nothing else instead.</summary>
    private void Refuse(string message)
    {
        _refused = true;
        GD.PushError($"launch refused: {message}");
        GetTree().Quit(RefusedExitCode);
    }

    /// <summary>
    /// A room picked with `--room` starts with the abilities and items a route there needs (the world's network and its solver: the
    /// fewest a pilgrim can hold arriving there), so any room can be tried as a player would come to it; `--abilities` says otherwise
    /// (and holds no items), `--items` adds items. False if the launch was refused (an item the world lacks).
    /// </summary>
    private bool GrantStartAbilities()
    {
        Ability? grant = _options.Abilities;
        ItemSet items = ItemSet.Empty;
        if (grant is null && _options.Room is not null)
        {
            switch (World.Definition.LoadNetwork())
            {
                case WorldData<MapDraft>.Loaded(var draft):
                    if (MapSolver.Arrival(draft.Graph, World.RoomName) is { } arrival)
                    {
                        (grant, items) = (arrival.Owned, arrival.Held);
                    }

                    break;
                case WorldData<MapDraft>.Malformed(var message):
                    GD.PushError($"the world's network ({World.Definition.Network}): {message}");
                    break;
                case WorldData<MapDraft>.Missing:
                    // A world without a network grants nothing.
                    break;
            }
        }

        if (_options.Items is { } named)
        {
            foreach (string id in named)
            {
                if (World.Definition.Item(id) is null)
                {
                    Refuse($"--items: {id} is not one of the world's items ({string.Join(", ", World.Definition.Items.Select(i => i.Id))})");
                    return false;
                }
            }

            items = items.Union(ItemSet.Of(named));
            grant ??= Ability.None;
        }

        if (grant is { } owned)
        {
            Player.Abilities.Set(owned, true);
            Player.Items.Restore(items);
            GD.Print($"[start] {World.RoomName} with {(owned == Ability.None ? "no abilities" : owned.ToString())}" +
                     (items.Count == 0 ? "" : $", holding {items}"));
        }

        return true;
    }
}
