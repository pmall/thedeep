using System;
using System.Collections.Generic;
using System.Linq;

namespace TheDeep;

/// <summary>
/// A world's map as a planning graph (its network file; the prototype's is worlds/prototype/map-draft.md), pure: a short text format the
/// solver checks before the rooms exist. During production each built room's markers replace its part of the
/// draft; the markers stay the source of truth, and <see cref="Differences"/> keeps the draft in step with them.
/// One statement per line, '#' starts a comment:
/// <code>
/// start Narthex
/// room Narthex Flooded            # a room and its layer
/// way A -> B                      # leaving A for B needs nothing
/// way A -> B : MagGrip | Harpoon  # ... needs this
/// way A &lt;-> B                     # both ways, needing nothing (or ": req" for both)
/// relic Reliquary MagGrip
/// item Ossuary CryptKey           # an item (a key, a code) lying in a room; a way names it like an ability: way A -> B : CryptKey
/// boss Chapterhouse Precentor
/// goal Heart
/// milestone Brink ThirdLaw        # a story beat tied to depth (outside the ability graph), in story order
/// target orders 24                # at least this many achievable ability orders
/// target positions 2              # every ability obtainable at this many places in the order, at least
/// target rooms 40 60              # the scope: between this many rooms and this many
/// </code>
/// </summary>
public sealed class MapDraft
{
    /// <summary>What the map is held to (its <c>target</c> statements).</summary>
    public MapTargets Targets { get; private set; } = MapTargets.None;

    public MapGraph Graph { get; } = new();

    /// <summary>Each room's depth layer.</summary>
    public Dictionary<string, string> Layers { get; } = new();

    /// <summary>The layers in the order they are first named (the descent).</summary>
    public List<string> LayerOrder { get; } = new();

    /// <summary>Story milestones (law breaks) and the room they happen in, in story order.</summary>
    public List<(string Room, string Name)> Milestones { get; } = new();

    /// <summary>Boss fights and the room they happen in.</summary>
    public List<(string Room, string Name)> Bosses { get; } = new();

    /// <summary>Parses the statements inside the <c>```map</c> fenced blocks of a markdown file; line numbers in errors are the file's.</summary>
    public static MapDraft FromMarkdown(string markdown)
    {
        string[] lines = markdown.Split('\n');
        bool inside = false;
        for (int n = 0; n < lines.Length; n++)
        {
            string trimmed = lines[n].Trim();
            if (trimmed.StartsWith("```"))
            {
                bool opensMap = !inside && trimmed[3..].Trim() == "map";
                lines[n] = "";
                inside = opensMap;
                continue;
            }

            if (!inside)
            {
                lines[n] = "";
            }
        }

        return Parse(string.Join('\n', lines));
    }

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

        var named = draft.Graph.Rooms.Concat(draft.Milestones.Select(m => m.Room)).Concat(draft.Bosses.Select(b => b.Room));
        var unknown = named.Where(r => !draft.Layers.ContainsKey(r)).Distinct().ToList();
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
                if (Layers.ContainsKey(args[0]))
                {
                    throw new FormatException($"room {args[0]} is declared twice");
                }

                Layers[args[0]] = args[1];
                if (!LayerOrder.Contains(args[1]))
                {
                    LayerOrder.Add(args[1]);
                }

                Graph.AddRoom(args[0]);
                break;
            case "way":
                Way(rest);
                break;
            case "relic":
                Expect(args, 2);
                if (!Enum.TryParse(args[1], out Ability ability) || ability == Ability.None || !Enum.IsDefined(ability))
                {
                    throw new FormatException($"unknown ability {args[1]}");
                }

                Graph.AddRelic(new MapRelic(args[0], "Relic", ability));
                break;
            case "item":
                Expect(args, 2);
                if (Enum.TryParse(args[1], out Ability _))
                {
                    throw new FormatException($"{args[1]} is an ability: it lies in a relic, not an item");
                }

                Graph.AddItem(new MapItem(args[0], "Item", args[1]));
                break;
            case "goal":
                Graph.AddGoal(Expect(args, 1)[0]);
                break;
            case "milestone":
                Expect(args, 2);
                Milestones.Add((args[0], args[1]));
                break;
            case "boss":
                Expect(args, 2);
                Bosses.Add((args[0], args[1]));
                break;
            case "target":
                Target(args);
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
        if (ends.Length != 2 || ends[0] == "" || ends[1] == "" || ends[0].Contains(' ') || ends[1].Contains(' '))
        {
            throw new FormatException("a way is 'A -> B' or 'A <-> B'");
        }

        Graph.AddEdge(new MapEdge(ends[0], ends[1], ends[1], ends[0], r));
        if (both)
        {
            Graph.AddEdge(new MapEdge(ends[1], ends[0], ends[0], ends[1], r));
        }
    }

    private void Target(string[] args)
    {
        int Count(string word) => int.TryParse(word, out int n) && n >= 0 ? n : throw new FormatException($"{word} is not a count");
        Targets = args switch
        {
            ["orders", var n] => Targets with { Orders = Count(n) },
            ["positions", var n] => Targets with { Positions = Count(n) },
            ["rooms", var min, var max] when Count(min) <= Count(max) => Targets with { MinRooms = Count(min), MaxRooms = Count(max) },
            _ => throw new FormatException("a target is 'target orders N', 'target positions N' or 'target rooms MIN MAX'"),
        };
    }

    private static string[] Expect(string[] args, int count) =>
        args.Length == count ? args : throw new FormatException($"expected {count} word(s), got {args.Length}");

    /// <summary>
    /// Checks the draft as a whole-game design: the solver's guarantees (no softlock, the goal reachable, every
    /// room reachable), every ability the world uses on exactly one relic (and every ability a way needs used), the order freedom
    /// targets, the scope, and the
    /// milestones: each one on every route to the goal, and met in story order whatever the route.
    /// </summary>
    public (MapReport Report, List<string> Problems) Check()
    {
        var problems = new List<string>();
        MapReport report = MapSolver.Solve(Graph);
        if (Graph.Goals.Count == 0)
        {
            problems.Add("no goal");
        }
        else if (!report.GoalReachable)
        {
            problems.Add($"no goal can be reached from {Graph.Start}");
        }

        if (report.UnreachableRooms.Count > 0)
        {
            problems.Add($"can't be reached from {Graph.Start}: {string.Join(", ", report.UnreachableRooms)}");
        }

        foreach ((SolverState state, IReadOnlyList<SolverState> path) in report.Softlocks.Take(5))
        {
            problems.Add($"softlock at {state}: no goal can be reached from there; reached by {string.Join(" → ", path)}");
        }

        // A world needn't use all five abilities (docs/roadmap.md#open-decisions): one it uses lies in one relic, one it doesn't in none.
        Ability needed = Graph.Edges.SelectMany(e => e.Requires.Options).Aggregate(Ability.None, (all, option) => all | option.Abilities);
        foreach (Ability a in Enum.GetValues<Ability>().Where(a => a != Ability.None))
        {
            int relics = Graph.Relics.Count(i => i.Ability == a);
            if (relics > 1)
            {
                problems.Add($"{a} lies in {relics} relics (one at most)");
            }
            else if (relics == 0 && needed.HasFlag(a))
            {
                problems.Add($"a way needs {a}, but no relic holds it");
            }
            else if (report.Positions.TryGetValue(a, out SortedSet<int>? at) && at.Count < Targets.Positions)
            {
                string where = at.Count == 0 ? "no position" : $"only position {string.Join("/", at.Select(i => i + 1))}";
                problems.Add($"{a} can be taken at {where} in the order (target: {Targets.Positions} positions)");
            }
        }

        problems.AddRange(Graph.ItemProblems(Array.Empty<Requirement>()));
        if (report.Orders.Count < Targets.Orders)
        {
            problems.Add($"only {report.Orders.Count} of {report.PossibleOrders} ability orders are achievable (target {Targets.Orders})");
        }

        if (Graph.Rooms.Count < Targets.MinRooms || Graph.Rooms.Count > Targets.MaxRooms)
        {
            problems.Add($"{Graph.Rooms.Count} rooms, outside the scope of {Targets.MinRooms}–{Targets.MaxRooms}");
        }

        for (int m = 0; m < Milestones.Count; m++)
        {
            (string room, string name) = Milestones[m];
            if (MapSolver.Reaches(Graph, Graph.Goals, avoiding: room))
            {
                problems.Add($"milestone {name}: the goal can be reached without passing {room}");
            }

            if (m + 1 < Milestones.Count && MapSolver.Reaches(Graph, new[] { Milestones[m + 1].Room }, avoiding: room))
            {
                problems.Add($"milestone {Milestones[m + 1].Name} in {Milestones[m + 1].Room} can be reached before {name} in {room}");
            }
        }

        return (report, problems);
    }

    /// <summary>
    /// Where the draft disagrees with the rooms already built (their markers are the truth). For each built room:
    /// its exits to built rooms (target and requirement) must be the draft's ways from it to built rooms, its sealed
    /// exits (requirement) the draft's ways from it to rooms not built yet, and its relics and items the draft's. Goals are not
    /// compared: a built slice may end where the whole game does not.
    /// </summary>
    public List<string> Differences(MapGraph built, IReadOnlyList<(string Room, Requirement Requires)> sealedExits)
    {
        var problems = new List<string>();
        static string Way(string to, Requirement r) => r.ToString() == "" ? to : $"{to} : {r}";
        static List<string> Missing(IEnumerable<string> want, IEnumerable<string> have)
        {
            var left = have.ToList();
            var missing = new List<string>();
            foreach (string w in want)
            {
                if (!left.Remove(w))
                {
                    missing.Add(w);
                }
            }

            return missing;
        }

        void Compare(string room, string what, List<string> scene, List<string> draft)
        {
            foreach (string w in Missing(scene, draft))
            {
                problems.Add($"{room}: the scene has {what} {w}, the draft doesn't");
            }

            foreach (string w in Missing(draft, scene))
            {
                problems.Add($"{room}: the draft has {what} {w}, the scene doesn't");
            }
        }

        foreach (string room in built.Rooms)
        {
            if (!Layers.ContainsKey(room))
            {
                problems.Add($"{room} is built but not in the draft");
                continue;
            }

            var draftWays = Graph.From(room).ToList();
            Compare(room, "a way to",
                built.From(room).Select(e => Way(e.To, e.Requires)).ToList(),
                draftWays.Where(e => built.Rooms.Contains(e.To)).Select(e => Way(e.To, e.Requires)).ToList());
            Compare(room, "a sealed way",
                sealedExits.Where(s => s.Room == room).Select(s => Way("(not built)", s.Requires)).ToList(),
                draftWays.Where(e => !built.Rooms.Contains(e.To)).Select(e => Way("(not built)", e.Requires)).ToList());
            Compare(room, "a relic",
                built.RelicsIn(room).Select(i => i.Ability.ToString()).ToList(),
                Graph.RelicsIn(room).Select(i => i.Ability.ToString()).ToList());
            Compare(room, "an item",
                built.ItemsIn(room).Select(i => i.Item).ToList(),
                Graph.ItemsIn(room).Select(i => i.Item).ToList());
        }

        return problems;
    }

    /// <summary>One line: rooms per layer, bosses, milestones, orders and each ability's positions.</summary>
    public string Summary(MapReport report)
    {
        var text = new System.Text.StringBuilder();
        text.Append($"{Graph.Rooms.Count} rooms (")
            .Append(string.Join(", ", LayerOrder.Select(l => $"{l} {Layers.Count(r => r.Value == l)}")))
            .Append($"), {Graph.Edges.Count} ways, {Bosses.Count} bosses, {Milestones.Count} milestones, ")
            .Append($"{report.ReachableStates} states, {report.Orders.Count} of {report.PossibleOrders} ability orders");
        foreach ((Ability ability, SortedSet<int> at) in report.Positions)
        {
            text.Append($"; {ability} at {string.Join("/", at.Select(i => i + 1))}");
        }

        return text.ToString();
    }
}
