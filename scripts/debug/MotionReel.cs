using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// The scripted reels shot by look-dev (LookDevDriver, tools/capture.sh), one or more per room:
/// - the pilgrim's motion reel in the test room: idle, run start, a run cycle, stopping and turning, then
///   a jump, a dash off a ledge and a hard landing from the metal slab;
/// - the machine reel in the arena: each prototype species reacting to the player (a Votary striking who
///   passes in front of it, the Gleaner bolting, the Limpet waking to a landing, the Lamplighter following).
/// Each segment starts with a respawn and a reset of the room's machines; inputs and shots are listed by
/// physics tick within the segment. Frames land in &lt;run&gt;/&lt;Folder&gt;/, and tools/motion_sheet.py lays
/// them out as &lt;Sheet&gt;.png.
/// </summary>
public static class MotionReel
{
    public static readonly Reel[] Reels =
    {
        new("DrownedHall", "motion", "Motion", 10f, new Segment[]
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
        }),
        new("MachineArena", "machines", "Machines", 7f, new Segment[]
        {
            // The pilgrim steps in front of the first Votary, between it and its shrine.
            new(new Vector3(3.5f, 0f, 0f), 1, 140,
                new Press[] { new(30, InputActions.MoveRight, true), new(48, InputActions.MoveRight, false) },
                new Shot[]
                {
                    new(20, "votary pray"), new(50, "votary notice"), new(64, "votary wind-up"), new(76, "votary wind-up"),
                    new(83, "votary blow"), new(88, "votary blow"), new(105, "votary recover"), new(135, "votary watch"),
                },
                Camera: new Vector3(6.5f, 1.4f, 6.5f)),
            // It walks toward the Gleaner, which bolts with its scrap.
            new(new Vector3(21f, 0f, 0f), 1, 130,
                new Press[] { new(40, InputActions.MoveRight, true), new(70, InputActions.MoveRight, false) },
                new Shot[]
                {
                    new(15, "gleaner glean"), new(35, "gleaner glean"), new(55, "gleaner glean"), new(66, "gleaner bolt"),
                    new(72, "gleaner bolt"), new(80, "gleaner flee"), new(90, "gleaner flee"), new(105, "gleaner flee"),
                    new(125, "gleaner settle"),
                },
                Subject: "Gleaner"),
            // It lands a jump near the reliquary: the Limpet wakes, crawls down to it and drills.
            new(new Vector3(43f, 0f, 0f), 1, 320,
                new Press[]
                {
                    new(30, InputActions.Jump, true), new(50, InputActions.Jump, false),
                    new(240, InputActions.MoveRight, true), new(245, InputActions.MoveRight, false),
                },
                new Shot[]
                {
                    new(20, "limpet asleep"), new(80, "limpet heard"), new(105, "limpet waking"), new(130, "limpet hunt"),
                    new(170, "limpet hunt"), new(210, "limpet hunt"), new(250, "limpet reach"), new(275, "limpet wind-up"),
                    new(290, "limpet drill"), new(310, "limpet recover"),
                },
                Camera: new Vector3(45.5f, 2.6f, 10f)),
            // It runs right then back: the Lamplighter follows, high and behind, and swaps sides over its head.
            new(new Vector3(3f, 0f, 0f), 1, 190,
                new Press[]
                {
                    new(20, InputActions.MoveRight, true), new(80, InputActions.MoveRight, false),
                    new(110, InputActions.MoveLeft, true), new(140, InputActions.MoveLeft, false),
                },
                new Shot[]
                {
                    new(10, "lamplighter"), new(40, "lamplighter follow"), new(60, "lamplighter follow"), new(85, "lamplighter follow"),
                    new(115, "lamplighter swap"), new(125, "lamplighter swap"), new(135, "lamplighter swap"),
                    new(160, "lamplighter follow"), new(185, "lamplighter follow"),
                },
                Subject: "Lamplighter"),
        }),
    };

    /// <summary>A room's reels.</summary>
    public static IEnumerable<Reel> For(string room)
    {
        foreach (Reel reel in Reels)
        {
            if (reel.Room == room)
            {
                yield return reel;
            }
        }
    }

    /// <param name="Room">The room the reel's positions belong to.</param>
    /// <param name="Folder">Where its frames go in the run directory.</param>
    /// <param name="Sheet">The contact sheet's name (&lt;Sheet&gt;.png).</param>
    /// <param name="CameraDistance">Camera distance from the play plane when following the player or a machine.</param>
    public sealed record Reel(string Room, string Folder, string Sheet, float CameraDistance, IReadOnlyList<Segment> Segments);

    /// <param name="Tick">Physics tick within the segment.</param>
    public readonly record struct Press(int Tick, StringName Action, bool Down);

    public readonly record struct Shot(int Tick, string Label);

    /// <param name="Start">Where the player respawns (room coordinates), facing <paramref name="Facing"/>.</param>
    /// <param name="Camera">A fixed camera (room coordinates; Z is the distance), or null to follow.</param>
    /// <param name="Subject">The machine the camera follows (its node name), or null to follow the player.</param>
    public sealed record Segment(Vector3 Start, int Facing, int Length, IReadOnlyList<Press> Presses, IReadOnlyList<Shot> Shots,
        Vector3? Camera = null, string? Subject = null);
}
