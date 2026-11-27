using System;
using System.Collections.Generic;
using System.Globalization;

namespace TheDeep;

/// <summary>
/// The user arguments of a launch (what follows `--` on Godot's command line), read once and pure: every argument is known, every
/// value present and well formed, no argument given twice, at most one driver. What the values name (a world, a room, an item, a
/// biome) is for the engine to find; the arguments are listed in `instructions/testing.md#commands`.
/// </summary>
public sealed record LaunchOptions
{
    /// <summary>The world to play (`--world`), or null for the main scene's.</summary>
    public string? World { get; init; }

    /// <summary>The room to start in (`--room`), or null for the world's start room (or the save's).</summary>
    public string? Room { get; init; }

    /// <summary>The abilities to start with (`--abilities`), or null for what a route to the room needs.</summary>
    public Ability? Abilities { get; init; }

    /// <summary>The ids of the items to start holding (`--items`), or null if not given.</summary>
    public IReadOnlyList<string>? Items { get; init; }

    /// <summary>The biome profile to start with (`--biome`), or null for the room's own.</summary>
    public string? Biome { get; init; }

    /// <summary>The save to use (`--save-name`), or null for the world's own.</summary>
    public string? SaveName { get; init; }

    /// <summary>`--new`: a new game, ignoring the save.</summary>
    public bool NewGame { get; init; }

    /// <summary>`--shot`: one screenshot after a moment of play, then quit.</summary>
    public bool Shot { get; init; }

    /// <summary>The drawn look to start in (`--look`, a preset's name: ToonLook), or null for the plain one.</summary>
    public string? Look { get; init; }

    /// <summary>`--shard K/N`: the Kth part (1-based) of N of a check's list; the whole list (1/1) when not given.</summary>
    public (int Index, int Count) Shard { get; init; } = (1, 1);

    /// <summary>`--map-report` (with the room check): prints the map.</summary>
    public bool MapReport { get; init; }

    /// <summary>`--slack-report` (with the room check): the reach planner's report for the clumsy pilgrim.</summary>
    public bool SlackReport { get; init; }

    /// <summary>`--reach-report &lt;room&gt;` (with the room check): where the reach planner stops in that room, or null.</summary>
    public string? ReachReport { get; init; }

    /// <summary>`--perf &lt;file&gt;` (with the look-dev run): the frame times' file name, or null.</summary>
    public string? Perf { get; init; }

    /// <summary>`--size WxH` (with the look-dev run): the render size in pixels, or null.</summary>
    public (int Width, int Height)? Size { get; init; }

    /// <summary>`--hops &lt;n&gt;` (with the UI shots): how many exits to cross before the map, or null.</summary>
    public int? Hops { get; init; }

    /// <summary>The driver flag given (one of those <see cref="Parse"/> was told), or null for a game launch.</summary>
    public string? Driver { get; init; }

    /// <summary>The driver's value (`--lookdev &lt;dir&gt;`, `--ui-shot &lt;dir&gt;`), or null if its flag takes none.</summary>
    public string? DriverValue { get; init; }

    /// <summary>
    /// Reads <paramref name="args"/>. <paramref name="drivers"/> are the flags that run a check or a tool, which only the
    /// caller knows: one at most may be given.
    /// </summary>
    public static LaunchParse Parse(IReadOnlyList<string> args, IReadOnlyList<DriverFlag> drivers)
    {
        var options = new LaunchOptions();
        var seen = new HashSet<string>();
        for (int i = 0; i < args.Count; i++)
        {
            string flag = args[i];
            if (!seen.Add(flag))
            {
                return new LaunchParse.Invalid($"{flag} is given twice");
            }

            DriverFlag? driver = Find(drivers, flag);
            bool takesValue = driver?.TakesValue ?? ValueFlags.Contains(flag);
            if (driver is null && !takesValue && !Switches.Contains(flag))
            {
                return new LaunchParse.Invalid($"unknown argument {flag}");
            }

            string value = "";
            if (takesValue)
            {
                if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    return new LaunchParse.Invalid($"{flag} takes a value");
                }

                value = args[++i];
            }

            if (driver is { } run)
            {
                if (options.Driver is { } other)
                {
                    return new LaunchParse.Invalid($"{other} and {flag} both run something: give one");
                }

                options = options with { Driver = flag, DriverValue = run.TakesValue ? value : null };
            }
            else if (Apply(options, flag, value) is { } applied)
            {
                options = applied;
            }
            else
            {
                return new LaunchParse.Invalid(Complaint(flag, value));
            }
        }

        return new LaunchParse.Parsed(options);
    }

    /// <summary>`all`, `none`, or ability names joined by commas (any case); null if anything else, a number included.</summary>
    public static Ability? ParseAbilities(string list)
    {
        string trimmed = list.Trim();
        if (trimmed.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            Ability every = Ability.None;
            foreach (Ability a in AbilitySet.All)
            {
                every |= a;
            }

            return every;
        }

        if (trimmed.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return Ability.None;
        }

        Ability owned = Ability.None;
        foreach (string name in trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            Ability? named = null;
            foreach (Ability a in AbilitySet.All)
            {
                if (a.ToString().Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    named = a;
                }
            }

            if (named is not { } found)
            {
                return null;
            }

            owned |= found;
        }

        return owned == Ability.None ? null : owned;
    }

    /// <summary>`K/N` with 1 &lt;= K &lt;= N; null if anything else.</summary>
    public static (int Index, int Count)? ParseShard(string text)
    {
        string[] parts = text.Split('/');
        return parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out int k)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int n)
            && k >= 1 && k <= n
            ? (k, n)
            : null;
    }

    private static readonly HashSet<string> Switches = new() { "--new", "--shot", "--map-report", "--slack-report" };

    private static readonly HashSet<string> ValueFlags = new()
    {
        "--world", "--room", "--abilities", "--items", "--biome", "--look", "--save-name", "--shard", "--reach-report", "--perf", "--size", "--hops",
    };

    private static DriverFlag? Find(IReadOnlyList<DriverFlag> drivers, string flag)
    {
        for (int i = 0; i < drivers.Count; i++)
        {
            if (drivers[i].Name == flag)
            {
                return drivers[i];
            }
        }

        return null;
    }

    /// <summary>The options with <paramref name="flag"/> applied; null if its <paramref name="value"/> is malformed.</summary>
    private static LaunchOptions? Apply(LaunchOptions options, string flag, string value)
    {
        switch (flag)
        {
            case "--new":
                return options with { NewGame = true };
            case "--shot":
                return options with { Shot = true };
            case "--map-report":
                return options with { MapReport = true };
            case "--slack-report":
                return options with { SlackReport = true };
            case "--world":
                return options with { World = value };
            case "--room":
                return options with { Room = value };
            case "--biome":
                return options with { Biome = value };
            case "--look":
                return options with { Look = value };
            case "--save-name":
                return options with { SaveName = value };
            case "--reach-report":
                return options with { ReachReport = value };
            case "--perf":
                return options with { Perf = value };
            case "--abilities":
                return ParseAbilities(value) is { } abilities ? options with { Abilities = abilities } : null;
            case "--items":
                return options with { Items = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) };
            case "--shard":
                return ParseShard(value) is { } shard ? options with { Shard = shard } : null;
            case "--hops":
                return int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int hops) ? options with { Hops = hops } : null;
            case "--size":
                string[] sides = value.Split('x');
                return sides.Length == 2
                    && int.TryParse(sides[0], NumberStyles.None, CultureInfo.InvariantCulture, out int width)
                    && int.TryParse(sides[1], NumberStyles.None, CultureInfo.InvariantCulture, out int height)
                    && width > 0 && height > 0
                    ? options with { Size = (width, height) }
                    : null;
            default:
                return null;
        }
    }

    private static string Complaint(string flag, string value) => flag switch
    {
        "--abilities" => $"--abilities {value}: expected all, none, or ability names joined by commas ({string.Join(", ", AbilitySet.All)})",
        "--shard" => $"--shard takes K/N with 1 <= K <= N, not {value}",
        "--hops" => $"--hops takes a number of exits, not {value}",
        "--size" => $"--size takes WxH in pixels, not {value}",
        _ => $"{flag} {value}: not understood",
    };
}
