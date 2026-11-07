using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace TheDeep;

/// <summary>
/// The drawing of a world's network for the owner to read (docs/roadmap.md#stage-plan), pure: an SVG made from its
/// <see cref="MapDraft"/> and its <see cref="RoomTable"/>, never drawn by hand. Each layer is a band, the surface at the top and the
/// descent going down; each room a box with its name, its kinds and what lies in it (relics, items, a boss, power-ups). A way is a line labelled
/// with what it needs (names longer than <see cref="ShortName"/> letters abbreviated to their capitals, spelled out in a legend);
/// the two directions of a way that need the same thing are one line, a one-way way has an arrowhead, a hidden way is dashed.
/// The layout is deterministic: rooms keep their layer's band, are ordered in it by a few barycenter passes over their neighbours
/// (fewer crossings), and a crowded layer wraps onto several rows. Every room box carries <c>data-room</c>, every way line
/// <c>data-edges</c>: the indices of the <see cref="MapGraph.Edges"/> it draws.
/// </summary>
public static class MapDrawing
{
    /// <summary>A requirement name up to this long is written in full on its way.</summary>
    public const int ShortName = 9;

    private const int PerRow = 6, BoxW = 184, BoxH = 62, GapX = 60, RowGap = 120, BandPad = 34, Left = 120, Top = 70;

    /// <summary>The SVG document; <paramref name="table"/> may be null (no kinds then).</summary>
    public static string Svg(MapDraft network, RoomTable? table, string title)
    {
        MapGraph graph = network.Graph;
        List<string> layers = network.LayerOrder.ToList();
        Dictionary<string, List<string>> byLayer = layers.ToDictionary(l => l, l => network.Layers.Where(r => r.Value == l).Select(r => r.Key).ToList());
        Order(byLayer, layers, graph);

        // Rows: each layer's ordered rooms split evenly into rows of at most PerRow.
        var at = new Dictionary<string, (double X, double Y, int Row)>();
        var bands = new List<(string Layer, double Y, double H)>();
        int widest = Math.Max(1, byLayer.Values.Select(r => Math.Min(PerRow, r.Count)).DefaultIfEmpty(1).Max());
        double width = Left + widest * (BoxW + GapX) + 40;
        double y = Top;
        int row = 0;
        foreach (string layer in layers)
        {
            List<string> rooms = byLayer[layer];
            int rows = Math.Max(1, (rooms.Count + PerRow - 1) / PerRow);
            int per = Math.Max(1, (rooms.Count + rows - 1) / rows);
            double bandTop = y;
            for (int i = 0; i < rows; i++, row++)
            {
                List<string> chunk = rooms.Skip(i * per).Take(per).ToList();
                double span = chunk.Count * BoxW + (chunk.Count - 1) * GapX;
                double x0 = Left + (widest * (BoxW + GapX) - GapX - span) / 2;
                for (int k = 0; k < chunk.Count; k++)
                {
                    at[chunk[k]] = (x0 + k * (BoxW + GapX), y + BandPad, row);
                }

                y += BoxH + RowGap;
            }

            bands.Add((layer, bandTop, y - bandTop));
        }

        double height = y + 30;
        Dictionary<string, string> abbreviations = Abbreviations(graph.Edges.Select(e => e.Requires.ToString()));
        int legendLines = abbreviations.Count == 0 ? 0 : 1 + (abbreviations.Count + 3) / 4;
        height += legendLines * 18;

        var svg = new StringBuilder();
        svg.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{N(width)}\" height=\"{N(height)}\" viewBox=\"0 0 {N(width)} {N(height)}\" font-family=\"sans-serif\">\n");
        svg.Append("<defs><marker id=\"arrow\" viewBox=\"0 0 10 10\" refX=\"10\" refY=\"5\" markerWidth=\"9\" markerHeight=\"9\" orient=\"auto-start-reverse\">"
                   + "<path d=\"M0,0 L10,5 L0,10 z\" fill=\"#333\"/></marker></defs>\n");
        svg.Append($"<rect width=\"100%\" height=\"100%\" fill=\"#fbfaf7\"/>\n");
        svg.Append($"<text x=\"20\" y=\"36\" font-size=\"22\" font-weight=\"bold\" fill=\"#222\">{X(title)}</text>\n");
        svg.Append($"<text x=\"20\" y=\"56\" font-size=\"12\" fill=\"#555\">{graph.Rooms.Count} rooms, {graph.Edges.Count} ways. Arrow: one way; dashed: hidden; label: what the way needs.</text>\n");
        for (int i = 0; i < bands.Count; i++)
        {
            (string layer, double by, double bh) = bands[i];
            svg.Append($"<rect x=\"0\" y=\"{N(by)}\" width=\"{N(width)}\" height=\"{N(bh)}\" fill=\"{(i % 2 == 0 ? "#efece4" : "#f6f4ee")}\"/>\n");
            svg.Append($"<text x=\"16\" y=\"{N(by + 24)}\" font-size=\"15\" font-weight=\"bold\" fill=\"#6a5f4a\">{X(layer)}</text>\n");
        }

        foreach ((MapEdge[] edges, int[] indices, bool both, double offset) in Lines(graph))
        {
            MapEdge e = edges[0];
            if (!at.TryGetValue(e.From, out var a) || !at.TryGetValue(e.To, out var b))
            {
                continue;
            }

            string d = Path(a, b, offset, out double lx, out double ly);
            bool hidden = network.IsHidden(e.From, e.To);
            string label = Abbreviate(e.Requires.ToString(), abbreviations);
            svg.Append($"<g class=\"way\" data-from=\"{X(e.From)}\" data-to=\"{X(e.To)}\" data-edges=\"{string.Join(",", indices)}\">");
            svg.Append($"<path d=\"{d}\" fill=\"none\" stroke=\"{(hidden ? "#8a5a9c" : "#444")}\" stroke-width=\"1.6\"{(hidden ? " stroke-dasharray=\"6 4\"" : "")}{(both ? "" : " marker-end=\"url(#arrow)\"")}/>");
            if (label != "")
            {
                svg.Append($"<text x=\"{N(lx)}\" y=\"{N(ly + 4)}\" font-size=\"11\" text-anchor=\"middle\" fill=\"#8a2a10\" stroke=\"#fbfaf7\" stroke-width=\"4\" paint-order=\"stroke\">{X(label)}</text>");
            }

            svg.Append("</g>\n");
        }

        foreach (string room in layers.SelectMany(l => byLayer[l]))
        {
            (double rx, double ry, _) = at[room];
            RoomRow? r = table?.Row(room);
            IReadOnlyList<RoomKind> kinds = r?.Kinds ?? Array.Empty<RoomKind>();
            bool marked = room == graph.Start || graph.Goals.Contains(room);
            bool secret = kinds.Contains(RoomKind.Secret);
            var holds = graph.RelicsIn(room).Select(x => $"relic {x.Ability}")
                .Concat(graph.ItemsIn(room).Select(x => $"item {x.Item}"))
                .Concat(network.Bosses.Where(x => x.Room == room).Select(x => $"boss {x.Name}"))
                .Concat(network.PowerUps.Where(x => x.Room == room).Select(x => $"power-up {x.Stat.ToString().ToLowerInvariant()}"));
            svg.Append($"<g class=\"room\" data-room=\"{X(room)}\">");
            svg.Append($"<rect x=\"{N(rx)}\" y=\"{N(ry)}\" width=\"{BoxW}\" height=\"{BoxH}\" rx=\"6\" fill=\"#ffffff\" stroke=\"{(marked ? "#8a2a10" : "#333")}\" stroke-width=\"{(marked ? 3 : 1.4)}\"{(secret ? " stroke-dasharray=\"5 3\"" : "")}/>");
            svg.Append($"<text x=\"{N(rx + 8)}\" y=\"{N(ry + 19)}\" font-size=\"14\" font-weight=\"bold\" fill=\"#1a1a1a\">{X(Fit(room, 22))}</text>");
            svg.Append($"<text x=\"{N(rx + 8)}\" y=\"{N(ry + 37)}\" font-size=\"11\" fill=\"#2b5d8a\">{X(Fit(string.Join(" · ", kinds).ToLowerInvariant(), 32))}</text>");
            svg.Append($"<text x=\"{N(rx + 8)}\" y=\"{N(ry + 53)}\" font-size=\"10.5\" fill=\"#555\">{X(Fit(string.Join(" · ", holds), 32))}</text>");
            svg.Append("</g>\n");
        }

        if (abbreviations.Count > 0)
        {
            double ly = y + 10;
            svg.Append($"<text x=\"20\" y=\"{N(ly)}\" font-size=\"12\" font-weight=\"bold\" fill=\"#222\">Abbreviations</text>\n");
            var entries = abbreviations.OrderBy(p => p.Value, StringComparer.Ordinal).ToList();
            for (int i = 0; i < entries.Count; i++)
            {
                svg.Append($"<text x=\"{N(20 + (i % 4) * 260)}\" y=\"{N(ly + 18 * (1 + i / 4))}\" font-size=\"12\" fill=\"#333\">{X($"{entries[i].Value} = {entries[i].Key}")}</text>\n");
            }
        }

        svg.Append("</svg>\n");
        return svg.ToString();
    }

    /// <summary>Orders each layer's rooms by the mean position of their neighbours in other layers, a few passes down and up.</summary>
    private static void Order(Dictionary<string, List<string>> byLayer, List<string> layers, MapGraph graph)
    {
        var neighbours = new Dictionary<string, HashSet<string>>();
        foreach (MapEdge e in graph.Edges)
        {
            (neighbours.TryGetValue(e.From, out var f) ? f : neighbours[e.From] = new()).Add(e.To);
            (neighbours.TryGetValue(e.To, out var t) ? t : neighbours[e.To] = new()).Add(e.From);
        }

        var layerOf = byLayer.SelectMany(p => p.Value.Select(r => (r, p.Key))).ToDictionary(p => p.r, p => p.Key);
        for (int pass = 0; pass < 6; pass++)
        {
            var position = byLayer.SelectMany(p => p.Value.Select((r, i) => (r, (i + 0.5) / p.Value.Count))).ToDictionary(p => p.r, p => p.Item2);
            IEnumerable<string> sweep = pass % 2 == 0 ? layers : Enumerable.Reverse(layers);
            foreach (string layer in sweep)
            {
                List<string> rooms = byLayer[layer];
                var keyed = rooms.Select((r, i) =>
                {
                    var others = neighbours.TryGetValue(r, out var n) ? n.Where(o => layerOf.TryGetValue(o, out var l) && l != layer).ToList() : new List<string>();
                    double own = position[r];
                    return (r, i, key: others.Count == 0 ? own : (others.Average(o => position[o]) + own) / 2);
                }).OrderBy(k => k.key).ThenBy(k => k.i).Select(k => k.r).ToList();
                byLayer[layer] = keyed;
                for (int i = 0; i < keyed.Count; i++)
                {
                    position[keyed[i]] = (i + 0.5) / keyed.Count;
                }
            }
        }
    }

    /// <summary>The lines to draw: the two directions of a way needing the same thing merge; parallel lines between two rooms fan out.</summary>
    private static List<(MapEdge[] Edges, int[] Indices, bool Both, double Offset)> Lines(MapGraph graph)
    {
        IReadOnlyList<MapEdge> edges = graph.Edges;
        var used = new bool[edges.Count];
        var lines = new List<(MapEdge[] Edges, int[] Indices, bool Both, double Offset)>();
        for (int i = 0; i < edges.Count; i++)
        {
            if (used[i])
            {
                continue;
            }

            used[i] = true;
            MapEdge e = edges[i];
            int back = Enumerable.Range(0, edges.Count).FirstOrDefault(j => !used[j] && edges[j].From == e.To && edges[j].To == e.From
                                                                       && edges[j].Requires.ToString() == e.Requires.ToString(), -1);
            if (back >= 0)
            {
                used[back] = true;
                lines.Add((new[] { e, edges[back] }, new[] { i, back }, true, 0));
            }
            else
            {
                lines.Add((new[] { e }, new[] { i }, false, 0));
            }
        }

        // Fan out the lines sharing a pair of rooms (the offset is along x between rows, along y on one row).
        foreach (var group in lines.Select((l, k) => (l, k)).GroupBy(p => string.CompareOrdinal(p.l.Edges[0].From, p.l.Edges[0].To) < 0
                     ? (p.l.Edges[0].From, p.l.Edges[0].To) : (p.l.Edges[0].To, p.l.Edges[0].From)).ToList())
        {
            var members = group.ToList();
            for (int m = 0; m < members.Count; m++)
            {
                (var l, int k) = members[m];
                lines[k] = (l.Edges, l.Indices, l.Both, (m - (members.Count - 1) / 2.0) * 14);
            }
        }

        return lines;
    }

    /// <summary>The path between two boxes (a curve between rows, a straight line or an arch along one row) and its label's point.</summary>
    private static string Path((double X, double Y, int Row) a, (double X, double Y, int Row) b, double offset, out double lx, out double ly)
    {
        if (a.Row == b.Row)
        {
            bool right = b.X > a.X;
            int apart = (int)Math.Round(Math.Abs(b.X - a.X) / (BoxW + GapX));
            if (apart <= 1)
            {
                double x1 = right ? a.X + BoxW : a.X, x2 = right ? b.X : b.X + BoxW;
                double yy = a.Y + BoxH / 2.0 + offset;
                lx = (x1 + x2) / 2;
                ly = yy - 8;
                return $"M{N(x1)},{N(yy)} L{N(x2)},{N(yy)}";
            }

            double sx = a.X + BoxW / 2.0 + offset, ex = b.X + BoxW / 2.0 + offset, top = a.Y;
            double lift = 30 + 12 * apart;
            lx = (sx + ex) / 2;
            ly = top - lift * 0.75;
            return $"M{N(sx)},{N(top)} C{N(sx)},{N(top - lift)} {N(ex)},{N(top - lift)} {N(ex)},{N(top)}";
        }

        bool down = b.Row > a.Row;
        double ax = a.X + BoxW / 2.0 + offset, bx = b.X + BoxW / 2.0 + offset;
        double ay = down ? a.Y + BoxH : a.Y, byy = down ? b.Y : b.Y + BoxH;
        double bend = (byy - ay) / 2;
        lx = (ax + bx) / 2;
        ly = (ay + byy) / 2;
        return $"M{N(ax)},{N(ay)} C{N(ax)},{N(ay + bend)} {N(bx)},{N(byy - bend)} {N(bx)},{N(byy)}";
    }

    /// <summary>Each requirement name longer than <see cref="ShortName"/> and its abbreviation (its capitals), unless they clash: such a name stays whole.</summary>
    private static Dictionary<string, string> Abbreviations(IEnumerable<string> requirements)
    {
        var names = requirements.SelectMany(r => Regex.Matches(r, "[A-Za-z][A-Za-z0-9]*").Select(m => m.Value))
            .Where(n => n.Length > ShortName).Distinct().OrderBy(n => n, StringComparer.Ordinal).ToList();
        var shortOf = names.ToDictionary(n => n, n => new string(n.Where(c => char.IsUpper(c) || char.IsDigit(c)).ToArray()));
        return names.Where(n => shortOf[n].Length >= 2 && shortOf.Count(p => p.Value == shortOf[n]) == 1).ToDictionary(n => n, n => shortOf[n]);
    }

    private static string Abbreviate(string requirement, Dictionary<string, string> abbreviations) =>
        Regex.Replace(requirement, "[A-Za-z][A-Za-z0-9]*", m => abbreviations.TryGetValue(m.Value, out string? s) ? s : m.Value);

    private static string Fit(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

    private static string N(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    private static string X(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
