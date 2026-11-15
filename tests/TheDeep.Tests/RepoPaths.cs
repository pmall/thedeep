using System;
using System.IO;

namespace TheDeep.Tests;

/// <summary>Where the project's files are, for tests that read them: the world files under <c>worlds/</c> and the resources.</summary>
public static class RepoPaths
{
    /// <summary>The project root: the first folder at or above the test binaries holding <c>AGENTS.md</c>.</summary>
    public static string Root()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("no AGENTS.md above the test binaries");
    }

    /// <summary>A file of the project, from its path under the root (forward slashes).</summary>
    public static string Of(string relative) => Path.Combine(Root(), relative);

    /// <summary>The design file of <paramref name="world"/>: <c>map-draft.md</c> for the prototype, <c>network.md</c> for the sequel.</summary>
    public static string Network(string world) => Path.Combine(Root(), "worlds", world, world == "prototype" ? "map-draft.md" : "network.md");

    /// <summary>The room table (<c>rooms.tsv</c>) of <paramref name="world"/>.</summary>
    public static string Rooms(string world) => Path.Combine(Root(), "worlds", world, "rooms.tsv");
}
