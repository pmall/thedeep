using System;
using Godot;

namespace TheDeep;

/// <summary>Boots the test room: puts the player at the room's spawn point.</summary>
public partial class Main : Node3D
{
    /// <summary>User argument (after `--`) that runs the scripted smoke test, used by tools/check.sh.</summary>
    private const string SmokeArgument = "--smoke";

    /// <summary>User argument that runs the windowed capture check (see CaptureTestDriver).</summary>
    private const string CaptureTestArgument = "--capture-test";

    /// <summary>`--lookdev &lt;dir&gt; [--size WxH]`: look-dev run for tools/capture.sh (see LookDevDriver).</summary>
    private const string LookDevArgument = "--lookdev";
    private const string SizeArgument = "--size";

    /// <summary>`--biome &lt;name&gt;`: start with resources/biomes/&lt;name&gt;.tres instead of the room's own.</summary>
    private const string BiomeArgument = "--biome";

    [Export] public Room Room { get; set; } = null!;
    [Export] public Player Player { get; set; } = null!;
    [Export] public OwnerCapture Capture { get; set; } = null!;
    [Export] public SideCamera Camera { get; set; } = null!;
    [Export] public DebugHud Hud { get; set; } = null!;

    public override void _Ready()
    {
        Player.Respawn(Room.SpawnPoint.GlobalPosition);

        string[] args = OS.GetCmdlineUserArgs();
        if (ArgumentValue(args, BiomeArgument) is { } biomeName)
        {
            if (BiomeProfile.Load(biomeName) is { } biome)
            {
                Room.ApplyBiome(biome);
            }
            else
            {
                GD.PushError($"no biome profile named {biomeName} in {BiomeProfile.Folder}");
            }
        }

        if (Array.IndexOf(args, SmokeArgument) >= 0)
        {
            AddChild(new SmokeDriver { Player = Player });
        }
        else if (Array.IndexOf(args, CaptureTestArgument) >= 0)
        {
            AddChild(new CaptureTestDriver { Capture = Capture });
        }
        else if (ArgumentValue(args, LookDevArgument) is { } outputDir)
        {
            var driver = new LookDevDriver
            {
                Player = Player, Room = Room, Camera = Camera, Overlays = new CanvasLayer[] { Hud, Capture },
                OutputDir = outputDir,
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
