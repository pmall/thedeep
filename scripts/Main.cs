using System;
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

    /// <summary>User argument that checks every machine species and quits (see SpeciesValidator), used by tools/check.sh.</summary>
    private const string ValidateSpeciesArgument = "--validate-species";

    [Export] public World World { get; set; } = null!;
    [Export] public Player Player { get; set; } = null!;
    [Export] public MachineDirector Machines { get; set; } = null!;
    [Export] public OwnerCapture Capture { get; set; } = null!;
    [Export] public SideCamera Camera { get; set; } = null!;
    [Export] public DebugHud Hud { get; set; } = null!;
    [Export] public RoomTravel Travel { get; set; } = null!;

    public override void _EnterTree()
    {
        // Before the world is ready, so it loads the requested room instead of its start room.
        if (ArgumentValue(OS.GetCmdlineUserArgs(), RoomArgument) is { } roomName)
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
            AddChild(new RoomValidator());
        }
        else if (Array.IndexOf(args, ValidateTravelArgument) >= 0)
        {
            AddChild(new TravelValidator { World = World, Player = Player, Travel = Travel });
        }
        else if (Array.IndexOf(args, GripCheckArgument) >= 0)
        {
            AddChild(new GripCheck { Player = Player });
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
                Player = Player, Room = room, Camera = Camera, Machines = Machines, Overlays = new CanvasLayer[] { Hud, Capture },
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
