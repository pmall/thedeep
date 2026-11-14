using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace TheDeep;

/// <summary>A visited room as the map screen draws it: its name, its tile (screen pixels, y down), and whether the pilgrim is in it.</summary>
public sealed record MapTile(string Name, Box Rect, bool Here);

/// <summary>
/// Where the map screen draws, screen pixels on the game's canvas: the largest scale (<see cref="MaxScale"/>, pixels per meter), the
/// margin the rooms keep from the canvas's sides and top, the legend's baseline above the canvas's bottom (the last of the items held
/// on it too, the others a row each over it), the room kept between the rooms and the words under them, the gap between two rooms'
/// tiles, and the legend's mark (its width before the words) and the spacing between its entries.
/// </summary>
public readonly record struct MapPlaces(float MaxScale, float Margin, float Baseline, float Row, float Clear, float Gap, float Mark, float Spacing);

/// <summary>
/// Pure: the map screen's layout, laid once as it opens (the map doesn't change while it shows). The visited rooms are fitted in the
/// map's area (<see cref="Area"/>: the canvas less its margins and the words under the map), the same scale both ways, the map's up
/// the world's up, never larger than <see cref="MapPlaces.MaxScale"/>. Each room is a tile, its camera bounds; two rooms whose bounds
/// overlap (their walls at a shared doorway) are cut apart where they meet, and every tile is drawn in by half the gap, so each room
/// reads on its own. Each way of a visited room is marked on its tile's edge, the one it leaves by (<see cref="WayMark"/>), and the
/// legend lists each kind of mark shown once (<see cref="LegendKey"/>). Screen pixels on the game's canvas, y down; meters on the map,
/// y up.
/// </summary>
public sealed class MapSheet
{
    /// <summary>How far a mark keeps from its tile's corners, pixels (less on a short edge).</summary>
    public const float Corner = 6f;

    private readonly MapPlaces _places;
    private readonly Vector2 _center;
    private readonly Vector2 _middle;
    private readonly Vector2 _hereOffset;
    private readonly int _heldRows;

    private MapSheet(Vector2 canvas, MapPlaces places, int heldRows, Box area, float scale, Vector2 center, Vector2 hereOffset)
    {
        Canvas = canvas;
        _places = places;
        _heldRows = heldRows;
        Area = area;
        Scale = scale;
        _center = center;
        _middle = new Vector2((area.MinX + area.MaxX) * 0.5f, (area.MinY + area.MaxY) * 0.5f);
        _hereOffset = hereOffset;
    }

    /// <summary>The canvas laid out for, pixels.</summary>
    public Vector2 Canvas { get; }

    /// <summary>Where the rooms are fitted: the canvas less its margins and the words under the map.</summary>
    public Box Area { get; }

    /// <summary>Pixels per meter.</summary>
    public float Scale { get; }

    /// <summary>The visited rooms' tiles, by name; none apart from another by less than the gap.</summary>
    public IReadOnlyList<MapTile> Tiles { get; private set; } = Array.Empty<MapTile>();

    /// <summary>The tile of the room the pilgrim is in; null when the map doesn't know it.</summary>
    public MapTile? Here { get; private set; }

    /// <summary>The marks of the visited rooms' ways, each on its tile's edge.</summary>
    public IReadOnlyList<WayMark> Marks { get; private set; } = Array.Empty<WayMark>();

    /// <summary>The legend's entries, one per kind of mark shown: the open ways, the abilities and the items lacked, then the unbuilt.</summary>
    public IReadOnlyList<LegendKey> Legend { get; private set; } = Array.Empty<LegendKey>();

    /// <summary>The legend's baseline, pixels from the canvas's top.</summary>
    public float LegendBaseline => Canvas.Y - _places.Baseline;

    /// <summary>The baseline of the items held's row <paramref name="row"/> (the first highest, the last on the legend's).</summary>
    public float HeldBaseline(int row) => LegendBaseline - (_heldRows - 1 - row) * _places.Row;

    /// <summary>Where a point of the map (meters) is on the screen.</summary>
    public Vector2 ToScreen(Vector2 map) => _middle + new Vector2(map.X - _center.X, _center.Y - map.Y) * Scale;

    /// <summary>
    /// The pilgrim's mark for a pilgrim at <paramref name="roomLocal"/> in its room: where that is on the map, kept within its tile by
    /// <paramref name="inset"/> pixels (the tile is drawn in from the room's bounds); null when the map doesn't know its room.
    /// </summary>
    public Vector2? Pilgrim(Vector2 roomLocal, float inset)
    {
        if (Here is not { } here)
        {
            return null;
        }

        Box r = here.Rect;
        Vector2 p = ToScreen(_hereOffset + roomLocal);
        float ix = MathF.Min(inset, (r.MaxX - r.MinX) * 0.5f), iy = MathF.Min(inset, (r.MaxY - r.MinY) * 0.5f);
        return new Vector2(Math.Clamp(p.X, r.MinX + ix, r.MaxX - ix), Math.Clamp(p.Y, r.MinY + iy, r.MaxY - iy));
    }

    /// <summary>
    /// Where the legend's entries start, <paramref name="widths"/> being their words' widths: one row centered on the canvas, each
    /// entry its mark then its words, <see cref="MapPlaces.Spacing"/> between entries.
    /// </summary>
    public IReadOnlyList<float> LegendRow(IReadOnlyList<float> widths)
    {
        float total = widths.Sum(w => _places.Mark + w) + _places.Spacing * Math.Max(0, widths.Count - 1);
        var starts = new float[widths.Count];
        float x = (Canvas.X - total) * 0.5f;
        for (int i = 0; i < widths.Count; i++)
        {
            starts[i] = x;
            x += _places.Mark + widths[i] + _places.Spacing;
        }

        return starts;
    }

    /// <summary>
    /// What is wrong with the layout, one line each (none for a sound one), the legend's words being <paramref name="legendWidths"/>
    /// wide: a tile out of the map's area or closer to another than the gap, a mark off every tile's edge on its side, no tile for the
    /// pilgrim's room, a legend out of the canvas's margins, the items held over the map.
    /// </summary>
    public IEnumerable<string> Problems(IReadOnlyList<float> legendWidths)
    {
        const float slack = 0.01f;
        Box area = Area with { MinX = Area.MinX - slack, MinY = Area.MinY - slack, MaxX = Area.MaxX + slack, MaxY = Area.MaxY + slack };
        float apart = _places.Gap - slack;
        for (int i = 0; i < Tiles.Count; i++)
        {
            MapTile tile = Tiles[i];
            if (!area.Contains(tile.Rect))
            {
                yield return $"the {tile.Name}'s tile {tile.Rect} is out of the map's area {Area}";
            }

            for (int j = i + 1; j < Tiles.Count; j++)
            {
                Box near = Tiles[j].Rect with
                {
                    MinX = Tiles[j].Rect.MinX - apart,
                    MinY = Tiles[j].Rect.MinY - apart,
                    MaxX = Tiles[j].Rect.MaxX + apart,
                    MaxY = Tiles[j].Rect.MaxY + apart,
                };
                if (near.Overlaps(tile.Rect))
                {
                    yield return $"the {tile.Name}'s and the {Tiles[j].Name}'s tiles are closer than {_places.Gap} px";
                }
            }
        }

        foreach (WayMark mark in Marks)
        {
            if (!Tiles.Any(t => OnEdge(t.Rect, mark.Side, mark.At) == mark.At))
            {
                yield return $"a {mark.GetType().Name} mark at {mark.At} is on no tile's {mark.Side} edge";
            }
        }

        if (Tiles.Count > 0 && Here is null)
        {
            yield return "no tile is the pilgrim's room";
        }

        IReadOnlyList<float> starts = LegendRow(legendWidths);
        if (starts.Count > 0 && (starts[0] < _places.Margin - slack || starts[^1] + _places.Mark + legendWidths[^1] > Canvas.X - _places.Margin + slack))
        {
            yield return $"the legend runs from {starts[0]} to {starts[^1] + _places.Mark + legendWidths[^1]} px, out of the margins of a canvas {Canvas.X} px wide";
        }

        if (HeldBaseline(0) - _places.Clear < Area.MaxY - slack)
        {
            yield return $"the items held reach over the map (their first row at {HeldBaseline(0)} px, the map down to {Area.MaxY})";
        }
    }

    /// <summary>
    /// Lays out <paramref name="map"/> on a canvas of <paramref name="canvas"/> pixels, with <paramref name="heldRows"/> items held
    /// listed under it, for a pilgrim owning <paramref name="owned"/> and holding <paramref name="held"/>; <paramref name="kindOf"/>
    /// tells an item's kind from its id.
    /// </summary>
    public static MapSheet Lay(MapState map, Vector2 canvas, MapPlaces places, int heldRows, Ability owned, ItemSet held, Func<string, ItemKind> kindOf)
    {
        heldRows = Math.Max(1, heldRows);
        float bottom = canvas.Y - (places.Baseline + (heldRows - 1) * places.Row + places.Clear);
        var area = new Box(places.Margin, places.Margin, canvas.X - places.Margin, MathF.Max(places.Margin + 1f, bottom));
        List<MapRoom> rooms = map.Rooms.OrderBy(r => r.Name, StringComparer.Ordinal).ToList();
        if (rooms.Count == 0)
        {
            return new MapSheet(canvas, places, heldRows, area, places.MaxScale, Vector2.Zero, Vector2.Zero);
        }

        Vector2 min = rooms.Select(r => r.MapMin).Aggregate(Vector2.Min), max = rooms.Select(r => r.MapMax).Aggregate(Vector2.Max);
        Vector2 extent = Vector2.Max(max - min, Vector2.One);
        float scale = MathF.Min(places.MaxScale, MathF.Min((area.MaxX - area.MinX) / extent.X, (area.MaxY - area.MinY) / extent.Y));
        MapRoom? here = map.Find(map.Current);
        var sheet = new MapSheet(canvas, places, heldRows, area, scale, (min + max) * 0.5f, here?.Offset ?? Vector2.Zero);

        Box[] tiles = Separate(rooms.Select(r => sheet.Rect(r.MapMin, r.MapMax)).ToArray(), places.Gap);
        var byName = new Dictionary<string, MapTile>();
        for (int i = 0; i < rooms.Count; i++)
        {
            byName[rooms[i].Name] = new MapTile(rooms[i].Name, tiles[i], rooms[i].Name == map.Current);
        }

        sheet.Tiles = byName.Values.ToList();
        sheet.Here = byName.GetValueOrDefault(map.Current);
        sheet.Marks = sheet.MarksOf(map, byName, owned, held, kindOf);
        sheet.Legend = LegendOf(sheet.Marks);
        return sheet;
    }

    /// <summary>The screen box of the map's box from <paramref name="min"/> to <paramref name="max"/> (meters).</summary>
    private Box Rect(Vector2 min, Vector2 max)
    {
        Vector2 a = ToScreen(new Vector2(min.X, max.Y)), b = ToScreen(new Vector2(max.X, min.Y));
        return new Box(a.X, a.Y, b.X, b.Y);
    }

    private List<WayMark> MarksOf(MapState map, Dictionary<string, MapTile> tiles, Ability owned, ItemSet held, Func<string, ItemKind> kindOf)
    {
        var marks = new List<WayMark>();
        foreach (MapWay way in map.Ways.OrderBy(w => w.Room, StringComparer.Ordinal).ThenBy(w => w.Exit, StringComparer.Ordinal))
        {
            bool taken = map.Taken(way.Room, way.Exit);
            if ((way.Hidden && !taken) || map.Find(way.Room) is not { } room || !tiles.TryGetValue(way.Room, out MapTile? tile))
            {
                continue;
            }

            Vector2 at = OnEdge(tile.Rect, way.Side, ToScreen(room.Offset + way.Position));
            Requirement need = Requirement.TryParse(way.Requires, out Requirement parsed, out _) ? parsed : Requirement.None;
            if (taken)
            {
                marks.Add(new WayMark.Taken(at, way.Side));
            }
            else if (!way.Built)
            {
                marks.Add(new WayMark.Unbuilt(at, way.Side));
            }
            else if (need.IsMetBy(owned, held))
            {
                marks.Add(new WayMark.Open(at, way.Side));
            }
            else
            {
                Ability[] abilities = AbilitySet.All.Where(a => (owned & a) == 0 && need.Options.Any(o => (o.Abilities & a) != 0)).ToArray();
                ItemKind[] locks = need.Items.Ids.Where(id => !held.Contains(id)).Select(kindOf).Distinct().Order().ToArray();
                marks.Add(new WayMark.Gated(at, way.Side, abilities, locks));
            }
        }

        return marks;
    }

    private static List<LegendKey> LegendOf(IReadOnlyList<WayMark> marks)
    {
        var legend = new List<LegendKey>();
        if (marks.Any(m => m is WayMark.Open))
        {
            legend.Add(new LegendKey.Open());
        }

        List<WayMark.Gated> gated = marks.OfType<WayMark.Gated>().ToList();
        legend.AddRange(AbilitySet.All.Where(a => gated.Any(g => g.Abilities.Contains(a))).Select(a => new LegendKey.Gate(a)));
        legend.AddRange(Enum.GetValues<ItemKind>().Where(k => gated.Any(g => g.Locks.Contains(k))).Select(k => new LegendKey.Lock(k)));
        if (marks.Any(m => m is WayMark.Unbuilt))
        {
            legend.Add(new LegendKey.Unbuilt());
        }

        return legend;
    }

    /// <summary>The point of <paramref name="tile"/>'s edge on <paramref name="side"/> nearest <paramref name="p"/>, clear of its corners.</summary>
    private static Vector2 OnEdge(Box tile, ExitSide side, Vector2 p)
    {
        float cx = MathF.Min(Corner, (tile.MaxX - tile.MinX) * 0.5f), cy = MathF.Min(Corner, (tile.MaxY - tile.MinY) * 0.5f);
        float x = Math.Clamp(p.X, tile.MinX + cx, tile.MaxX - cx), y = Math.Clamp(p.Y, tile.MinY + cy, tile.MaxY - cy);
        return side switch
        {
            ExitSide.West => new Vector2(tile.MinX, y),
            ExitSide.East => new Vector2(tile.MaxX, y),
            ExitSide.Down => new Vector2(x, tile.MaxY),
            ExitSide.Up => new Vector2(x, tile.MinY),
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, null),
        };
    }

    /// <summary>
    /// The rooms' boxes as tiles: two that overlap are cut where they meet (across the axis they overlap least on, at the middle of
    /// their overlap), then each is drawn in by half of <paramref name="gap"/>, so no two tiles come closer than the gap. A box too
    /// small to lose that much keeps a pixel.
    /// </summary>
    public static Box[] Separate(IReadOnlyList<Box> boxes, float gap)
    {
        Box[] cut = boxes.ToArray();
        for (int i = 0; i < boxes.Count; i++)
        {
            for (int j = i + 1; j < boxes.Count; j++)
            {
                Box a = boxes[i], b = boxes[j];
                if (!a.Overlaps(b))
                {
                    continue;
                }

                float across = MathF.Min(a.MaxX, b.MaxX) - MathF.Max(a.MinX, b.MinX), down = MathF.Min(a.MaxY, b.MaxY) - MathF.Max(a.MinY, b.MinY);
                if (across <= down)
                {
                    (int left, int right) = a.MinX + a.MaxX <= b.MinX + b.MaxX ? (i, j) : (j, i);
                    float mid = (boxes[left].MaxX + boxes[right].MinX) * 0.5f;
                    cut[left] = cut[left] with { MaxX = MathF.Min(cut[left].MaxX, mid) };
                    cut[right] = cut[right] with { MinX = MathF.Max(cut[right].MinX, mid) };
                }
                else
                {
                    (int top, int under) = a.MinY + a.MaxY <= b.MinY + b.MaxY ? (i, j) : (j, i);
                    float mid = (boxes[top].MaxY + boxes[under].MinY) * 0.5f;
                    cut[top] = cut[top] with { MaxY = MathF.Min(cut[top].MaxY, mid) };
                    cut[under] = cut[under] with { MinY = MathF.Max(cut[under].MinY, mid) };
                }
            }
        }

        for (int i = 0; i < cut.Length; i++)
        {
            Box c = cut[i];
            float inX = MathF.Max(0f, MathF.Min(gap * 0.5f, (c.MaxX - c.MinX - 1f) * 0.5f));
            float inY = MathF.Max(0f, MathF.Min(gap * 0.5f, (c.MaxY - c.MinY - 1f) * 0.5f));
            cut[i] = new Box(c.MinX + inX, c.MinY + inY, c.MaxX - inX, c.MaxY - inY);
        }

        return cut;
    }
}
