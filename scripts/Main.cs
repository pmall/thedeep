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

    [Export] public Room Room { get; set; } = null!;
    [Export] public Player Player { get; set; } = null!;
    [Export] public OwnerCapture Capture { get; set; } = null!;

    public override void _Ready()
    {
        Player.Respawn(Room.SpawnPoint.GlobalPosition);

        string[] args = OS.GetCmdlineUserArgs();
        if (Array.IndexOf(args, SmokeArgument) >= 0)
        {
            AddChild(new SmokeDriver { Player = Player });
        }
        else if (Array.IndexOf(args, CaptureTestArgument) >= 0)
        {
            AddChild(new CaptureTestDriver { Capture = Capture });
        }
    }
}
