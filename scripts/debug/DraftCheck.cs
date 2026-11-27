using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Godot;

namespace TheDeep;

/// <summary>
/// The network and room-table part of <see cref="RoomValidator"/>: loads the world's <see cref="MapDraft"/> and
/// <see cref="RoomTable"/>, tracks built power-ups, bosses, ambushes and hidden ways, checks agreement against the built
/// rooms, and writes the SVG network drawing (<see cref="MapDrawing"/>) when requested.
/// </summary>
internal sealed class DraftCheck
{
    private readonly List<(string Room, PowerStat Stat)> _powerUps = new();
    private readonly List<(string Room, string Name)> _bosses = new();
    private readonly List<string> _ambushes = new();
    private readonly List<(string Room, string To)> _hiddenWays = new();
    private MapDraft? _draft;

    /// <summary>Loads the world's network file; records a problem if malformed.</summary>
    public void Load(WorldDefinition definition, List<string> problems)
    {
        switch (definition.LoadNetwork())
        {
            case WorldData<MapDraft>.Loaded(var draft):
                _draft = draft;
                break;
            case WorldData<MapDraft>.Malformed(var message):
                problems.Add($"map draft ({definition.Network}): {message}");
                _draft = null;
                break;
            default:
                _draft = new MapDraft();
                break;
        }
    }

    /// <summary>Records the built room's power-ups, bosses, ambushes and hidden exits for draft comparison.</summary>
    public void Record(Room room, string name, List<RoomExit> exits)
    {
        _powerUps.AddRange(room.All<PowerUp>().Select(p => (name, p.Stat)));
        _bosses.AddRange(room.All<BossSeal>().Select(s => (name, s.Boss.Species.DisplayName)));
        _ambushes.AddRange(room.All<Ambush>().Select(_ => name));
        _hiddenWays.AddRange(exits.Where(e => e.Hidden && e.TargetRoom != "").Select(e => (name, World.NameOf(e.TargetRoom))));
    }

    /// <summary>
    /// Checks the network's targets, layers, room table and agreement with the built rooms; returns the summary line(s).
    /// </summary>
    public string Check(WorldDefinition definition, WorldCheck world, bool report, List<string> problems)
    {
        if (_draft is not { } draft)
        {
            return "";
        }

        (MapReport solved, List<string> draftProblems) = draft.Check();
        problems.AddRange(draftProblems.Select(p => $"map draft: {p}"));
        problems.AddRange(draft.Differences(world.Graph, world.Sealed, _hiddenWays).Select(p => $"map draft differs from the rooms: {p}"));
        problems.AddRange(PowerUpDifferences(draft).Select(p => $"map draft differs from the rooms: {p}"));
        problems.AddRange(draft.ArenaDifferences(world.Graph.Rooms, _bosses, _ambushes).Select(p => $"map draft differs from the rooms: {p}"));
        problems.AddRange(draft.LayerOrder.Where(l => definition.Layer(l) is null)
            .Select(l => $"map draft: layer {l} is not one of the world's layers ({string.Join(", ", definition.Layers.Select(w => w.Name))})"));

        RoomTable? table = LoadTable(definition, problems);
        if (table is not null)
        {
            problems.AddRange(table.Check(draft).Select(p => $"room table ({definition.RoomTable}): {p}"));
        }

        var text = new StringBuilder($"DRAFT OK: {draft.Summary(solved)}");
        if (table is not null)
        {
            text.Append(CultureInfo.InvariantCulture, $"; room table: {table.Rows.Count} rows, held to the network");
        }

        if (report)
        {
            text.Append(CultureInfo.InvariantCulture, $"\ndrawing: {WriteDrawing(definition, draft, table)}");
            if (table is not null)
            {
                text.Append("\nroom table (kinds):");
                foreach (RoomRow row in table.Rows)
                {
                    text.Append(CultureInfo.InvariantCulture, $"\n  {row.Room} ({row.Layer}): {string.Join(", ", row.Kinds).ToLowerInvariant()}");
                }
            }

            text.Append("\ndraft ways:");
            foreach (MapEdge e in draft.Graph.Edges)
            {
                string needs = e.Requires.ToString();
                text.Append(CultureInfo.InvariantCulture, $"\n  {e.From} → {e.To}{(needs == "" ? "" : $"  needs {needs}")}");
            }

            foreach (Ability[] order in solved.Orders)
            {
                text.Append(CultureInfo.InvariantCulture, $"\n  draft order: {string.Join(" → ", order)}");
            }
        }

        return text.ToString();
    }

    private IEnumerable<string> PowerUpDifferences(MapDraft draft)
    {
        static string Of(IEnumerable<(string Room, PowerStat Stat)> list, string room) =>
            string.Join(", ", list.Where(p => p.Room == room).Select(p => p.Stat.ToString().ToLowerInvariant()).Order(StringComparer.Ordinal));

        foreach (string room in draft.PowerUps.Concat(_powerUps).Select(p => p.Room).Distinct())
        {
            string said = Of(draft.PowerUps, room), built = Of(_powerUps, room);
            if (said != built)
            {
                yield return $"{room}: the network's power-ups are [{said}], the room holds [{built}]";
            }
        }
    }

    private static RoomTable? LoadTable(WorldDefinition definition, List<string> problems)
    {
        string path = definition.RoomTable;
        switch (definition.LoadRoomTable())
        {
            case WorldData<RoomTable>.Loaded(var table):
                return table;
            case WorldData<RoomTable>.Malformed(var message):
                problems.Add($"room table ({path}): {message}");
                return null;
            default:
                problems.Add($"room table: the world names none, or {path} is missing (docs/roadmap.md#stage-plan)");
                return null;
        }
    }

    private static string WriteDrawing(WorldDefinition definition, MapDraft draft, RoomTable? table)
    {
        string world = definition.Network.GetBaseDir().GetFile();
        string path = $"res://lookdev/map-{world}.svg";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath("res://lookdev"));
        using FileAccess file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        file.StoreString(MapDrawing.Svg(draft, table, $"{world}: the network"));
        return ProjectSettings.GlobalizePath(path);
    }
}
