using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// The scripted moves shot by the look-dev motion reel (LookDevDriver, tools/capture.sh): idle, run start,
/// a run cycle, stopping and turning on the test room's floor, then a jump, a dash off a ledge and a hard
/// landing from the metal slab. Each segment starts with a respawn; inputs and shots are listed by
/// physics tick within the segment. Only the test room has these spots; other rooms skip the reel.
/// </summary>
public static class MotionReel
{
    /// <summary>Camera distance from the play plane for the reel: close enough to read the body.</summary>
    public const float CameraDistance = 10f;

    /// <summary>The room the reel's positions belong to.</summary>
    public const string Room = "TestRoom";

    public static readonly Segment[] Segments =
    {
        new(new Vector3(3f, 0f, 0f), 1, 112,
            new Press[]
            {
                new(25, InputActions.MoveRight, true), new(70, InputActions.MoveRight, false),
                new(90, InputActions.MoveLeft, true), new(110, InputActions.MoveLeft, false),
            },
            new Shot[]
            {
                new(2, "idle"), new(20, "idle"), new(27, "start"), new(31, "start"), new(37, "start"),
                new(44, "run"), new(48, "run"), new(52, "run"), new(56, "run"), new(60, "run"), new(64, "run"),
                new(72, "stop"), new(78, "stop"), new(86, "stop"),
                new(92, "turn"), new(95, "turn"), new(98, "turn"), new(104, "turn"),
            }),
        new(new Vector3(34.5f, 9.5f, 0f), 1, 150,
            new Press[]
            {
                new(10, InputActions.MoveRight, true), new(30, InputActions.Jump, true), new(48, InputActions.Jump, false),
                new(80, InputActions.Dash, true), new(82, InputActions.Dash, false), new(130, InputActions.MoveRight, false),
            },
            new Shot[]
            {
                new(32, "jump"), new(38, "jump"), new(46, "rise"), new(53, "apex"), new(62, "fall"), new(70, "fall"),
                new(78, "land"), new(82, "dash"), new(86, "dash"), new(92, "dash"),
                new(100, "drop"), new(112, "drop"), new(122, "land"), new(127, "land"), new(134, "land"), new(146, "settle"),
            }),
    };

    /// <param name="Tick">Physics tick within the segment.</param>
    public readonly record struct Press(int Tick, StringName Action, bool Down);

    public readonly record struct Shot(int Tick, string Label);

    /// <param name="Start">Where the player respawns (room coordinates), facing <paramref name="Facing"/>.</param>
    public sealed record Segment(Vector3 Start, int Facing, int Length, IReadOnlyList<Press> Presses, IReadOnlyList<Shot> Shots);
}
