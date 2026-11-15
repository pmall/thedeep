using System;
using System.Collections.Generic;
using Godot;

namespace TheDeep;

/// <summary>
/// Every check and tool that runs in place of the game, in one table: the argument that starts it (after `--`), whether the argument
/// takes a value, and how <see cref="Main"/>'s nodes are handed to the driver. A new driver is one row here and its own file; Main
/// and the launch options (<see cref="LaunchOptions"/>) read the table and know none of them by name. Each driver's docblock says
/// what it proves.
/// </summary>
public static class Drivers
{
    private const string SaveCheckWrite = "--save-check-write";
    private const string UiShotFlag = "--ui-shot";

    private static readonly Row[] Rows =
    {
        Run("--validate-rooms", (m, o) => new RoomValidator
        {
            World = m.World,
            Banner = m.Session.Banner,
            Hint = m.Hint,
            Tuning = m.Player.Tuning,
            ClimbRise = m.Travel.ClimbRise,
            StartRoom = m.GameStart,
            Report = o.MapReport,
            ReachReport = o.ReachReport ?? "",
            SlackReport = o.SlackReport,
            ReachWith = o.Abilities,
        }),
        Run("--validate-travel", (m, o) => new TravelValidator { World = m.World, Player = m.Player, Travel = m.Travel, Shard = o.Shard }),
        Run("--grip-check", (m, _) => new GripCheck { Player = m.Player, World = m.World }),
        Run("--harpoon-check", (m, _) => new HarpoonCheck { Player = m.Player, World = m.World, Pointer = m.Pointer }),
        Run("--drill-check", (m, _) => new DrillCheck { Player = m.Player, World = m.World, Hints = m.Hints }),
        Run("--ballast-check", (m, _) => new BallastCheck { Player = m.Player, World = m.World, Hints = m.Hints }),
        Run("--fight-check", (m, _) => new FightCheck { Player = m.Player, World = m.World }),
        Run("--secret-check", (m, _) => new SecretCheck { Player = m.Player, World = m.World, Secrets = m.Secrets, Session = m.Session }),
        Run("--arena-check", (m, _) => new ArenaCheck { Player = m.Player, World = m.World, Session = m.Session, Banner = m.Session.Banner, Camera = m.Camera }),
        Run("--dialogue-check", (m, _) => new DialogueCheck
        {
            Player = m.Player,
            World = m.World,
            Conversations = m.Conversations,
            Box = m.DialogueBox,
            Session = m.Session,
            Banner = m.Session.Banner,
        }),
        Run("--plate-check", (m, _) => new PlateCheck { Player = m.Player, World = m.World, Banner = m.Session.Banner }),
        Run("--map-screen-check", (m, _) => new MapScreenCheck { World = m.World, Travel = m.Travel, Map = m.MapScreen }),
        Run(SaveCheckWrite, (m, _) => new SaveCheck { Write = true, Player = m.Player, World = m.World, Session = m.Session }),
        new Row("--save-check-read", false, true, (m, _) => new SaveCheck { Write = false, Player = m.Player, World = m.World, Session = m.Session }),
        Run("--powerup-check", (m, _) => new PowerUpCheck { Player = m.Player, World = m.World, PowerUps = m.PowerUps, Session = m.Session, Banner = m.Session.Banner }),
        Run("--key-check", (m, _) => new KeyCheck { Player = m.Player, World = m.World, Keyring = m.Keyring, Session = m.Session, Hint = m.Hint }),
        Run("--code-check", (m, _) => new KeyCheck { Player = m.Player, World = m.World, Keyring = m.Keyring, Session = m.Session, Hint = m.Hint, Checked = KeyCheck.Pyre }),
        Run("--heat-check", (m, _) => new HeatCheck { Player = m.Player, World = m.World, Hints = m.Hints }),
        Run("--burn-probe", (m, _) => new BurnProbe { Player = m.Player, World = m.World }),
        Run("--route-check", (m, o) => new RouteCheck
        {
            Player = m.Player,
            World = m.World,
            Machines = m.Machines,
            PowerUps = m.PowerUps,
            Limits = m.Player.Tuning.Planner(m.Travel.ClimbRise),
            Shard = o.Shard,
        }),
        Run("--combat-check", (m, _) => new CombatCheck { Player = m.Player, World = m.World, Session = m.Session }),
        Run("--encounter-check", (m, _) => new EncounterCheck { Player = m.Player, World = m.World, Session = m.Session, Banner = m.Session.Banner, Hint = m.Hint }),
        Run("--playthrough-check", (m, _) => new PlaythroughCheck { World = m.World, Player = m.Player, Session = m.Session }),
        Run("--validate-species", (m, _) => new SpeciesValidator { World = m.World, Room = m.World.Room, Player = m.Player, Machines = m.Machines }),
        Run(UiShotFlag, UiShots, takesValue: true),
        Run("--bake-surfaces", (_, _) => new SurfaceBake()),
        Run("--smoke", (m, _) => new SmokeDriver { Player = m.Player }),
        Run("--capture-test", (m, _) => new CaptureTestDriver { Capture = m.Capture }),
        Run("--lookdev", LookDev, takesValue: true),
    };

    /// <summary>The flags that run a driver, for <see cref="LaunchOptions.Parse"/>.</summary>
    public static IReadOnlyList<DriverFlag> Flags { get; } = Array.ConvertAll(Rows, r => new DriverFlag(r.Flag, r.TakesValue));

    /// <summary>Whether the run is a check or tool that is no plain launch: it never reads or writes the save, nor starts with the abilities a route gives.</summary>
    public static bool Driven(LaunchOptions options) => Find(options) is { NormalLaunch: false };

    /// <summary>Whether the run is the save check's writing run, the one driver that may write a (scratch) save.</summary>
    public static bool WritesScratchSave(LaunchOptions options) => options.Driver == SaveCheckWrite;

    /// <summary>Whether the run is the UI shots, which see a room as a player comes to it (the Pyre's heat stands at its door).</summary>
    public static bool ShowsRoomAsPlayed(LaunchOptions options) => options.Driver == UiShotFlag;

    /// <summary>The driver node the run's flag starts, or null for a game launch.</summary>
    public static Node? Build(Main main, LaunchOptions options) => Find(options)?.Build(main, options);

    /// <summary>One row: the driver <paramref name="Build"/> makes when <paramref name="Flag"/> is given.</summary>
    /// <param name="Flag">The argument that runs it.</param>
    /// <param name="TakesValue">Whether the flag is followed by a value (an output directory).</param>
    /// <param name="NormalLaunch">True for a driver that is a plain launch (the save check's reading run): it resumes the save as the game does.</param>
    /// <param name="Build">The driver node, given the game and the launch's options.</param>
    private sealed record Row(string Flag, bool TakesValue, bool NormalLaunch, Func<Main, LaunchOptions, Node> Build);

    private static Row? Find(LaunchOptions options)
    {
        foreach (Row row in Rows)
        {
            if (row.Flag == options.Driver)
            {
                return row;
            }
        }

        return null;
    }

    private static Row Run(string flag, Func<Main, LaunchOptions, Node> build, bool takesValue = false) => new(flag, takesValue, false, build);

    private static Node UiShots(Main main, LaunchOptions options)
    {
        var shot = new UiShot
        {
            World = main.World,
            Player = main.Player,
            Travel = main.Travel,
            Hint = main.Hint,
            Map = main.MapScreen,
            EndCard = main.Session.EndCard,
            Banner = main.Session.Banner,
            Manual = main.Hud.Manual,
            DebugKeys = main.Hud.DebugKeys,
            Conversations = main.Conversations,
            OutputDir = options.DriverValue ?? "",
        };
        if (options.Hops is { } hops)
        {
            shot.Hops = hops;
        }

        return shot;
    }

    private static Node LookDev(Main main, LaunchOptions options)
    {
        main.Travel.Enabled = false;
        var driver = new LookDevDriver
        {
            Player = main.Player,
            Room = main.World.Room,
            Camera = main.Camera,
            Machines = main.Machines,
            Overlays = new CanvasLayer[] { main.Hud, main.Capture, main.Health, main.Session.Banner, main.Hint },
            OutputDir = options.DriverValue ?? "",
            PerfFile = options.Perf ?? "perf.json",
        };
        if (options.Size is { } size)
        {
            driver.RenderSize = new Vector2I(size.Width, size.Height);
        }

        return driver;
    }
}
