using System;
using System.Collections.Generic;
using System.Linq;

namespace TheDeep;

/// <summary>
/// The whole game's map as a draft planning graph (docs/map-draft.md, M7), pure: a short text format the
/// solver checks before the rooms exist. During production each built room's markers replace its part of the
/// draft; the markers stay the source of truth. One statement per line, '#' starts a comment:
/// <code>
/// start Narthex
/// room Narthex Flooded            # a room and its layer
/// way A -> B                      # leaving A for B needs nothing
/// way A -> B : MagGrip | Harpoon  # ... needs this
/// way A <-> B                     # both ways, needing nothing (or ": req" for both)
/// relic Reliquary MagGrip
/// goal Heart
/// milestone Chancel ThirdLaw      # a story beat tied to depth (outside the ability graph)
/// </code>
/// </summary>
public sealed class MapDraft
{
    public MapGraph Graph { get; } = new();

    /// <summary>Each room's depth layer.</summary>
    public Dictionary<string, string> Layers { get; } = new();

    /// <summary>Story milestones (law breaks) and the room they happen in.</summary>
    public List<(string Room, string Name)> Milestones { get; } = new();

    public static MapDraft Parse(string text)
    {
        var draft = new MapDraft();
        string[] lines = text.Split('\n');
        for (int n = 0; n < lines.Length; n++)
        {
            string line = lines[n];
            int hash = line.IndexOf('#');
            if (hash >= 0)
            {
                line = line[..hash];
            }

            line = line.Trim();
            if (line == "")
            {
                continue;
            }

            try
            {
                draft.Statement(line);
            }
            catch (FormatException e)
            {
                throw new FormatException($"line {n + 1}: {e.Message} ({line})");
            }
        }

        if (draft.Graph.Start == "")
        {
            throw new FormatException("no start room");
        }

        var unknown = draft.Graph.Rooms.Where(r => !draft.Layers.ContainsKey(r)).ToList();
        if (unknown.Count > 0)
        {
            throw new FormatException($"rooms used but never declared: {string.Join(", ", unknown)}");
        }

        return draft;
    }

    private void Statement(string line)
    {
        string[] words = line.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries);
        string rest = words.Length > 1 ? words[1].Trim() : "";
        string[] args = rest.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        switch (words[0])
        {
            case "start":
                Graph.Start = Expect(args, 1)[0];
                break;
            case "room":
                Expect(args, 2);
                Layers[args[0]] = args[1];
                Graph.AddRoom(args[0]);
                break;
            case "way":
                Way(rest);
                break;
            case "relic":
                Expect(args, 2);
                if (!Enum.TryParse(args[1], out Ability ability) || ability == Ability.None)
                {
                    throw new FormatException($"unknown ability {args[1]}");
                }

                Graph.AddItem(new MapItem(args[0], "Relic", ability));
                break;
            case "goal":
                Graph.AddGoal(Expect(args, 1)[0]);
                break;
            case "milestone":
                Expect(args, 2);
                Milestones.Add((args[0], args[1]));
                break;
            default:
                throw new FormatException($"unknown statement {words[0]}");
        }
    }

    private void Way(string rest)
    {
        string requires = "";
        int colon = rest.IndexOf(':');
        if (colon >= 0)
        {
            requires = rest[(colon + 1)..].Trim();
            rest = rest[..colon].Trim();
        }

        Requirement r = Requirement.Parse(requires);
        bool both = rest.Contains("<->");
        string[] ends = rest.Split(both ? "<->" : "->", StringSplitOptions.TrimEntries);
        if (ends.Length != 2 || ends[0] == "" || ends[1] == "")
        {
            throw new FormatException("a way is 'A -> B' or 'A <-> B'");
        }

        Graph.AddEdge(new MapEdge(ends[0], ends[1], ends[1], ends[0], r));
        if (both)
        {
            Graph.AddEdge(new MapEdge(ends[1], ends[0], ends[0], ends[1], r));
        }
    }

    private static string[] Expect(string[] args, int count) =>
        args.Length == count ? args : throw new FormatException($"expected {count} word(s), got {args.Length}");
}
