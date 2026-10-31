using System.Linq;
using Godot;
using NVector2 = System.Numerics.Vector2;

namespace TheDeep;

/// <summary>
/// The minimal map (the map action: M, Tab, LB): the rooms visited so far as thin outlines placed from the
/// exits crossed (<see cref="World.Map"/>), the current room brighter, and the pilgrim as a soft pulsing
/// dot. The ways not yet taken show where they leave their room: a dot for one the pilgrim can take now, small rings in
/// the gate's colors (<see cref="GateKinds"/>) for one it can't yet, a faint ring for one leading where nothing is built;
/// a legend names what is shown. Opening it pauses the game and eases in over a dim veil; it closes the same way.
/// </summary>
public partial class MapScreen : CanvasLayer
{
    [Export] public World World { get; set; } = null!;
    [Export] public Player Player { get; set; } = null!;

    /// <summary>Largest scale, pixels per meter at 1080p; smaller when the visited area is large.</summary>
    [Export] public float MaxScale { get; set; } = 7f;

    /// <summary>Share of the screen the visited area may fill.</summary>
    [Export] public float Fill { get; set; } = 0.7f;

    [Export] public Color Line { get; set; } = new(0.86f, 0.86f, 0.95f, 0.55f);
    [Export] public Color Here { get; set; } = new(0.8f, 0.62f, 1f, 0.95f);
    [Export] public Color Veil { get; set; } = new(0.02f, 0.02f, 0.035f, 0.82f);

    /// <summary>Seconds to ease in or out.</summary>
    [Export] public float FadeTime { get; set; } = 0.22f;

    private Control _canvas = null!;
    private readonly System.Collections.Generic.Dictionary<string, Requirement> _requirements = new();
    private Font _font = null!;
    private float _shown;
    private bool _open;
    private float _time;

    public bool IsOpen => _open;

    public override void _Ready()
    {
        Layer = 80;
        ProcessMode = ProcessModeEnum.Always;
        _canvas = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _canvas.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _canvas.Draw += DrawMap;
        _font = ThemeDB.FallbackFont;
        AddChild(_canvas);
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

    public void Toggle()
    {
        _open = !_open;
        GetTree().Paused = _open;
        Visible = true;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        _shown = Motion.Approach(_shown, _open ? 1f : 0f, dt / FadeTime);
        Visible = _shown > 0f;
        if (Visible)
        {
            _canvas.QueueRedraw();
        }
    }

    private void DrawMap()
    {
        float ease = _shown * _shown * (3f - 2f * _shown);
        Vector2 size = _canvas.Size;
        _canvas.DrawRect(new Rect2(Vector2.Zero, size), Veil with { A = Veil.A * ease });
        if (World.Map.Rooms.Count == 0)
        {
            return;
        }

        // Fit the visited area on screen, the map's up being the world's up.
        NVector2 min = new(float.MaxValue), max = new(float.MinValue);
        foreach (MapRoom room in World.Map.Rooms)
        {
            min = NVector2.Min(min, room.MapMin);
            max = NVector2.Max(max, room.MapMax);
        }

        float ui = size.Y / 1080f;
        NVector2 extent = NVector2.Max(max - min, new NVector2(1f));
        float scale = Mathf.Min(MaxScale * ui, Mathf.Min(size.X * Fill / extent.X, size.Y * Fill / extent.Y));
        NVector2 center = (min + max) * 0.5f;
        Vector2 Screen(NVector2 p) => size * 0.5f + new Vector2(p.X - center.X, center.Y - p.Y) * scale
                                      + new Vector2(0f, (1f - ease) * 12f * ui);

        float width = Mathf.Max(1f, 1.5f * ui);
        foreach (MapRoom room in World.Map.Rooms)
        {
            bool here = room.Name == World.Map.Current;
            Vector2 a = Screen(new NVector2(room.MapMin.X, room.MapMax.Y)), b = Screen(new NVector2(room.MapMax.X, room.MapMin.Y));
            var rect = new Rect2(a, b - a);
            Color line = here ? Here : Line;
            _canvas.DrawRect(rect, line with { A = (here ? 0.14f : 0.06f) * ease });
            _canvas.DrawRect(rect, line with { A = line.A * ease }, filled: false, width: width);
        }

        DrawWays(Screen, ui, ease);

        if (World.Map.Find(World.Map.Current) is { } current)
        {
            Vector3 p = Player.GlobalPosition;
            Vector2 dot = Screen(current.Offset + new NVector2(p.X, p.Y + 1f));
            float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 3f);
            _canvas.DrawCircle(dot, (6f + 4f * pulse) * ui, Here with { A = 0.18f * ease });
            _canvas.DrawCircle(dot, 3.5f * ui, Here with { A = ease });
        }
    }

    /// <summary>The ways not yet taken, at their exits, and the legend of what is shown.</summary>
    private void DrawWays(System.Func<NVector2, Vector2> screen, float ui, float ease)
    {
        Ability owned = Player.Abilities.Unlocked;
        var shown = new System.Collections.Generic.SortedSet<Ability>();
        bool open = false, unbuilt = false;
        float pulse = 0.75f + 0.25f * Mathf.Sin(_time * 2.2f);
        foreach (MapWay way in World.Map.Ways)
        {
            if (World.Map.Taken(way.Room, way.Exit) || World.Map.Find(way.Room) is not { } room)
            {
                continue;
            }

            Vector2 at = screen(room.Offset + way.Position);
            Requirement need = RequirementOf(way.Requires);
            if (!way.Built)
            {
                unbuilt = true;
                _canvas.DrawArc(at, 4.5f * ui, 0f, Mathf.Tau, 20, Line with { A = 0.35f * ease }, Mathf.Max(1f, 1.4f * ui));
            }
            else if (need.IsMetBy(owned))
            {
                open = true;
                _canvas.DrawCircle(at, 8f * ui, Colors.White with { A = 0.12f * ease * pulse });
                _canvas.DrawCircle(at, 4f * ui, Colors.White with { A = 0.92f * ease * pulse });
            }
            else
            {
                Ability[] kinds = AbilitySet.All.Where(a => need.Options.Any(o => o.HasFlag(a)) && !owned.HasFlag(a)).ToArray();
                for (int i = 0; i < kinds.Length; i++)
                {
                    shown.Add(kinds[i]);
                    Vector2 c = at + new Vector2((i - 0.5f * (kinds.Length - 1)) * 13f * ui, 0f);
                    _canvas.DrawArc(c, 5f * ui, 0f, Mathf.Tau, 20, GateKinds.ColorOf(kinds[i]) with { A = 0.95f * ease }, Mathf.Max(1.5f, 2f * ui));
                }
            }
        }

        // The legend, bottom center: what each mark is.
        var entries = new System.Collections.Generic.List<(string Text, Color Color, bool Filled)>();
        if (open)
        {
            entries.Add((LineBook.Shared.Find("map.way.open")?.Text ?? "", Colors.White, true));
        }

        foreach (Ability a in shown)
        {
            entries.Add((LineBook.Shared.Find(GateKinds.MapWord(a))?.Text ?? "", GateKinds.ColorOf(a), false));
        }

        if (unbuilt)
        {
            entries.Add((LineBook.Shared.Find("map.way.unbuilt")?.Text ?? "", Line, false));
        }

        int size = Mathf.RoundToInt(17f * ui);
        float gap = 34f * ui, mark = 20f * ui;
        float total = entries.Sum(e => mark + _font.GetStringSize(e.Text, HorizontalAlignment.Left, -1, size).X) + gap * Mathf.Max(0, entries.Count - 1);
        Vector2 size2 = _canvas.Size;
        float x = 0.5f * (size2.X - total), y = size2.Y - 56f * ui;
        foreach ((string text, Color color, bool filled) in entries)
        {
            var c = new Vector2(x + 6f * ui, y - 0.33f * size);
            if (filled)
            {
                _canvas.DrawCircle(c, 4f * ui, color with { A = ease });
            }
            else
            {
                _canvas.DrawArc(c, 5f * ui, 0f, Mathf.Tau, 20, color with { A = ease }, Mathf.Max(1.5f, 2f * ui));
            }

            _canvas.DrawString(_font, new Vector2(x + mark, y), text, HorizontalAlignment.Left, -1, size, new Color(0.86f, 0.86f, 0.95f, 0.8f * ease));
            x += mark + _font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X + gap;
        }
    }

    private Requirement RequirementOf(string text)
    {
        if (!_requirements.TryGetValue(text, out Requirement? r))
        {
            r = Requirement.TryParse(text, out Requirement parsed, out _) ? parsed : Requirement.None;
            _requirements[text] = r;
        }

        return r;
    }
}
