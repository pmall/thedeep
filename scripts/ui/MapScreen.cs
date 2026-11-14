using System.Collections.Generic;
using System.Linq;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// The minimal map (the map action: M, Tab, LB), laid out by the core's <see cref="MapSheet"/> as it opens: each room visited so far a
/// tile placed from the exits crossed (<see cref="World.Map"/>), the current room's in the room's accent with the pilgrim a soft
/// pulsing dot in it; each way taken a short bridge across the gap to the room it joins; each way not taken yet a mark on its tile's
/// edge: a dot for one the pilgrim can take now, small rings in the gate's colors (<see cref="GateKinds"/>, <see cref="ItemKinds"/> for
/// a locked door) for one it can't yet, a faint ring for one leading where nothing is built. A legend names the marks shown, bottom
/// center, and the items held are listed bottom left, each on the shared <see cref="TextStyle"/>'s plate. Opening it pauses the game
/// and eases in over a dim veil; it closes the same way.
/// </summary>
public partial class MapScreen : CanvasLayer
{
    [Export] public World World { get; set; } = null!;
    [Export] public Player Player { get; set; } = null!;

    /// <summary>Largest scale, pixels per meter on the game's canvas; smaller when the visited area is large.</summary>
    [Export] public float MaxScale { get; set; } = 6f;

    /// <summary>The margin the map keeps from the canvas's sides and top, pixels (clear of the health and the room's name).</summary>
    [Export] public float Margin { get; set; } = 96f;

    /// <summary>The legend's baseline, pixels above the canvas's bottom.</summary>
    [Export] public float Baseline { get; set; } = 80f;

    /// <summary>Between two rows of the items held, pixels.</summary>
    [Export] public float Row { get; set; } = 24f;

    /// <summary>Room kept between the map and the words under it, pixels (their plate included).</summary>
    [Export] public float Clear { get; set; } = 48f;

    /// <summary>The gap between two rooms' tiles, which a way taken bridges, pixels.</summary>
    [Export] public float Gap { get; set; } = 8f;

    /// <summary>A legend entry's mark: its width before the words, pixels; and the spacing between two entries.</summary>
    [Export] public float Mark { get; set; } = 18f;

    [Export] public float Spacing { get; set; } = 30f;

    /// <summary>The radius of a way's mark, pixels (a gate's ring a little larger).</summary>
    [Export] public float MarkRadius { get; set; } = 4f;

    /// <summary>The rooms' outlines, the bridges of the ways taken (and a way leading where nothing is built).</summary>
    [Export] public Color Line { get; set; } = new(0.86f, 0.86f, 0.95f, 0.55f);

    /// <summary>The veil the map is drawn over.</summary>
    [Export] public Color Veil { get; set; } = new(0.02f, 0.02f, 0.035f, 0.82f);

    private Control? _canvas;
    private Control Canvas => _canvas ??= new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
    private StyleBoxFlat? _plate;
    private readonly List<(string Text, Color Color, LegendKey Key)> _legend = new();
    private readonly List<(string Name, Color Color)> _held = new();
    private float[] _legendWidths = System.Array.Empty<float>();
    private IReadOnlyList<float> _legendStarts = System.Array.Empty<float>();
    private Rect2? _legendPlate;
    private Rect2? _heldPlate;
    private Color _here;
    private float _shown;
    private bool _open;
    private float _time;

    /// <summary>The map is on screen and the game paused.</summary>
    public bool IsOpen => _open;

    /// <summary>How far the map has eased in, 0 (gone) to 1 (shown).</summary>
    public float Shown => _shown;

    /// <summary>The map laid out as it last opened; null before it first opens.</summary>
    public MapSheet? Sheet { get; private set; }

    /// <summary>The legend's words' widths as the map last opened, pixels, one per entry of <see cref="MapSheet.Legend"/>.</summary>
    public IReadOnlyList<float> LegendWidths => _legendWidths;

    public override void _Ready()
    {
        Layer = 80;
        ProcessMode = ProcessModeEnum.Always;
        _plate = TextStyle.Shared.NewPlate();
        Canvas.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        Canvas.Draw += DrawMap;
        AddChild(Canvas);
        Visible = false;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(InputActions.Map))
        {
            Toggle();
            GetViewport().SetInputAsHandled();
        }
    }

    /// <summary>Opens the map (laid out anew) and pauses the game, or closes it and resumes.</summary>
    public void Toggle()
    {
        _open = !_open;
        if (_open)
        {
            Lay();
        }

        GetTree().Paused = _open;
        Visible = true;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        _shown = Motion.Approach(_shown, _open ? 1f : 0f, dt / TextStyle.Shared.Panel);
        Visible = _shown > 0f;
        if (Visible)
        {
            // Eases in rising a little into place.
            float ease = _shown * _shown * (3f - 2f * _shown);
            Canvas.Modulate = Colors.White with { A = ease };
            Canvas.Position = new Vector2(0f, (1f - ease) * 12f);
            Canvas.QueueRedraw();
        }
    }

    /// <summary>Lays the map out for what the pilgrim knows, owns and holds now (the map doesn't change while it shows).</summary>
    private void Lay()
    {
        TextStyle style = TextStyle.Shared;
        _here = style.Accent(World.Room.Biome);
        ItemSet held = Player.Items.Held;
        _held.Clear();
        foreach (string id in held.Ids)
        {
            ItemDefinition? item = World.Definition.Item(id);
            if (ItemKinds.Carried(item?.Kind ?? ItemKind.Key))
            {
                _held.Add((item is null ? id : Words(item.NameLine), ItemKinds.ColorOf(item?.Kind ?? ItemKind.Key)));
            }
        }

        var places = new MapPlaces(MaxScale, Margin, Baseline, Row, Clear, Gap, Mark, Spacing);
        Vector2 canvas = GetViewport().GetVisibleRect().Size;
        MapSheet sheet = MapSheet.Lay(World.Map, canvas.ToCore(), places, _held.Count, Player.Abilities.Unlocked, held,
            id => World.Definition.Item(id)?.Kind ?? ItemKind.Key);
        Sheet = sheet;

        _legend.Clear();
        foreach (LegendKey key in sheet.Legend)
        {
            _legend.Add(key switch
            {
                LegendKey.Open => (Words(GameLines.MapWayOpen), style.Ink, key),
                LegendKey.Gate g => (Words(GateKinds.MapWord(g.Ability)), GateKinds.ColorOf(g.Ability), key),
                LegendKey.Lock l => (Words(ItemKinds.MapWord(l.Kind)), ItemKinds.ColorOf(l.Kind), key),
                LegendKey.Unbuilt => (Words(GameLines.MapWayUnbuilt), Line, key),
                _ => throw new System.ArgumentOutOfRangeException(nameof(key), key, null),
            });
        }

        _legendWidths = _legend.Select(e => TextWidth(e.Text)).ToArray();
        _legendStarts = sheet.LegendRow(_legendWidths);
        _legendPlate = _legend.Count == 0 ? null
            : PlateAround(_legendStarts[0], _legendStarts[^1] + Mark + _legendWidths[^1], sheet.LegendBaseline, sheet.LegendBaseline);
        _heldPlate = _held.Count == 0 ? null
            : PlateAround(Margin, Margin + Mark + _held.Max(h => TextWidth(h.Name)), sheet.HeldBaseline(0), sheet.HeldBaseline(_held.Count - 1));
    }

    private static string Words(StringName line) => LineBook.Shared.Find(line)?.Text ?? "";

    private static float TextWidth(string text) =>
        TextStyle.Shared.Font.GetStringSize(text, HorizontalAlignment.Left, -1, TextStyle.Shared.Caption).X;

    /// <summary>The style's plate around rows of caption words from <paramref name="left"/> to <paramref name="right"/>, their baselines from <paramref name="first"/> to <paramref name="last"/>.</summary>
    private static Rect2 PlateAround(float left, float right, float first, float last)
    {
        TextStyle style = TextStyle.Shared;
        Font font = style.Font;
        float top = first - font.GetAscent(style.Caption) - style.Padding.Y, bottom = last + font.GetDescent(style.Caption) + style.Padding.Y;
        return new Rect2(left - style.Padding.X, top, right - left + 2f * style.Padding.X, bottom - top);
    }

    private void DrawMap()
    {
        Canvas.DrawRect(new Rect2(-Canvas.Position, Canvas.Size), Veil);
        if (Sheet is not { } sheet)
        {
            return;
        }

        IReadOnlyList<MapTile> tiles = sheet.Tiles;
        for (int i = 0; i < tiles.Count; i++)
        {
            MapTile tile = tiles[i];
            Color line = tile.Here ? _here : Line;
            var rect = new Rect2(tile.Rect.MinX, tile.Rect.MinY, tile.Rect.MaxX - tile.Rect.MinX, tile.Rect.MaxY - tile.Rect.MinY);
            Canvas.DrawRect(rect, line with { A = tile.Here ? 0.16f : 0.07f });
            Canvas.DrawRect(rect, line, filled: false, width: 1.5f);
        }

        float pulse = 0.75f + 0.25f * Mathf.Sin(_time * 2.2f);
        IReadOnlyList<WayMark> marks = sheet.Marks;
        for (int i = 0; i < marks.Count; i++)
        {
            DrawWay(marks[i], pulse);
        }

        Vector3 p = Player.GlobalPosition;
        if (sheet.Pilgrim(new NVector2(p.X, p.Y + 1f), 2f * MarkRadius) is { } at)
        {
            float beat = 0.5f + 0.5f * Mathf.Sin(_time * 3f);
            Canvas.DrawCircle(at.ToGodot(), (2f + beat) * MarkRadius, _here with { A = 0.22f });
            Canvas.DrawCircle(at.ToGodot(), 1.2f * MarkRadius, _here);
        }

        DrawLegend(sheet);
        DrawHeld(sheet);
    }

    /// <summary>A way's mark on its tile's edge (<see cref="WayMark"/>), on a spot of the veil so it reads over the outline.</summary>
    private void DrawWay(WayMark mark, float pulse)
    {
        Vector2 at = mark.At.ToGodot();
        Vector2 outward = mark.Side switch
        {
            ExitSide.West => Vector2.Left,
            ExitSide.East => Vector2.Right,
            ExitSide.Down => Vector2.Down,
            _ => Vector2.Up,
        };
        float ring = 1.25f * MarkRadius;
        switch (mark)
        {
            case WayMark.Taken:
                // Half the bridge, from just inside the edge to the gap's middle: the room behind draws the other half.
                Vector2 across = new(outward.Y, outward.X);
                Vector2 a = at - outward * 1.5f - across * MarkRadius, b = at + outward * (0.5f * Gap) + across * MarkRadius;
                Vector2 min = new(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y)), max = new(Mathf.Max(a.X, b.X), Mathf.Max(a.Y, b.Y));
                Canvas.DrawRect(new Rect2(min, max - min), Line);
                break;
            case WayMark.Open:
                Color ink = TextStyle.Shared.Ink;
                Canvas.DrawCircle(at, ring + 1f, Veil);
                Canvas.DrawCircle(at, 2f * MarkRadius, ink with { A = 0.14f * pulse });
                Canvas.DrawCircle(at, MarkRadius, ink with { A = 0.95f * pulse });
                break;
            case WayMark.Gated gated:
                // The rings along the edge, centered on the way.
                Vector2 along = new(Mathf.Abs(outward.Y), Mathf.Abs(outward.X));
                int count = gated.Abilities.Count + gated.Locks.Count;
                for (int i = 0; i < count; i++)
                {
                    Color color = i < gated.Abilities.Count ? GateKinds.ColorOf(gated.Abilities[i]) : ItemKinds.ColorOf(gated.Locks[i - gated.Abilities.Count]);
                    Vector2 c = at + along * ((i - 0.5f * (count - 1)) * 2.6f * ring);
                    Canvas.DrawCircle(c, ring + 1f, Veil);
                    Canvas.DrawArc(c, ring, 0f, Mathf.Tau, 20, color with { A = 0.95f }, 2f);
                }

                break;
            case WayMark.Unbuilt:
                Canvas.DrawCircle(at, ring + 1f, Veil);
                Canvas.DrawArc(at, ring, 0f, Mathf.Tau, 20, Line with { A = 0.4f }, 1.5f);
                break;
        }
    }

    /// <summary>The legend, bottom center on its plate: what each mark is.</summary>
    private void DrawLegend(MapSheet sheet)
    {
        if (_legendPlate is { } plate && _plate is { } box)
        {
            Canvas.DrawStyleBox(box, plate);
        }

        TextStyle style = TextStyle.Shared;
        float y = sheet.LegendBaseline, ring = 1.25f * MarkRadius;
        for (int i = 0; i < _legend.Count && i < _legendStarts.Count; i++)
        {
            (string text, Color color, LegendKey key) = _legend[i];
            float x = _legendStarts[i];
            var c = new Vector2(x + ring, y - 0.33f * style.Caption);
            if (key is LegendKey.Open)
            {
                Canvas.DrawCircle(c, MarkRadius, color);
            }
            else
            {
                Canvas.DrawArc(c, ring, 0f, Mathf.Tau, 20, color, 2f);
            }

            Canvas.DrawString(style.Font, new Vector2(x + Mark, y), text, HorizontalAlignment.Left, -1, style.Caption, style.Dim);
        }
    }

    /// <summary>The items held, bottom left on their plate: a filled mark in the kind's color and the item's name, one per row.</summary>
    private void DrawHeld(MapSheet sheet)
    {
        if (_heldPlate is { } plate && _plate is { } box)
        {
            Canvas.DrawStyleBox(box, plate);
        }

        TextStyle style = TextStyle.Shared;
        for (int i = 0; i < _held.Count; i++)
        {
            float y = sheet.HeldBaseline(i);
            (string name, Color color) = _held[i];
            Canvas.DrawRect(new Rect2(Margin, y - 0.33f * style.Caption - MarkRadius, 2f * MarkRadius, 2f * MarkRadius), color);
            Canvas.DrawString(style.Font, new Vector2(Margin + Mark, y), name, HorizontalAlignment.Left, -1, style.Caption, style.Dim);
        }
    }
}
