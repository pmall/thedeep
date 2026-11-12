using Godot;

namespace TheDeep;

/// <summary>
/// The pilgrim's health, top left: a short row of diamonds, filled for each hit it can still take, each on a soft dark backing so it
/// reads on the palest wall. Quiet when nothing happens, brighter for a moment when health changes. A blow that hurts flashes the
/// screen's edges in the hurt color and the lost diamond bursts in it and shrinks; with one hit left, the last diamond beats in that
/// color until health comes back.
/// </summary>
public partial class HealthHud : CanvasLayer
{
    [Export] public Player Player { get; set; } = null!;

    [Export] public Color Fill { get; set; } = new(0.94f, 0.92f, 1f, 0.95f);
    [Export] public Color Empty { get; set; } = new(0.94f, 0.92f, 1f, 0.4f);

    /// <summary>The backing under each diamond, and the color of a hurt: the lost diamond, the screen's edges, the last diamond's beat.</summary>
    [Export] public Color Backing { get; set; } = new(0.03f, 0.03f, 0.07f, 0.45f);
    [Export] public Color HurtColor { get; set; } = new(1f, 0.36f, 0.26f, 1f);

    /// <summary>Opacity at rest, and how long it stays bright after a change.</summary>
    [Export] public float RestOpacity { get; set; } = 0.8f;
    [Export] public float BrightTime { get; set; } = 2.5f;

    /// <summary>A diamond's half height and the step between two, in pixels at 1080p.</summary>
    [Export] public float PipSize { get; set; } = 13f;
    [Export] public float PipGap { get; set; } = 34f;

    /// <summary>The edge flash of a hurt: its opacity, how far in from the edges it reaches (share of the screen's height) and how
    /// long it lasts (seconds).</summary>
    [Export] public float FlashStrength { get; set; } = 0.38f;
    [Export] public float FlashDepth { get; set; } = 0.22f;
    [Export] public float FlashTime { get; set; } = 0.4f;

    /// <summary>The last diamond's beat with one hit left, beats per second.</summary>
    [Export] public float BeatRate { get; set; } = 1.3f;

    private Control? _canvas;
    private Control Canvas => _canvas ??= new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
    private int _shown;
    private float _bright;
    private float _lostFlash;
    private int _lostIndex = -1;
    private float _edgeFlash;
    private float _beat;

    // Reused every draw: per-frame code allocates nothing (instructions/principles.md#performance).
    private readonly Vector2[] _diamond = new Vector2[4];
    private readonly Vector2[] _outline = new Vector2[5];
    private readonly Vector2[] _band = new Vector2[4];
    private readonly Color[] _bandColors = new Color[4];

    public override void _Ready()
    {
        Layer = 70;
        Canvas.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        Canvas.Draw += DrawHud;
        AddChild(Canvas);
        _shown = Player.Vitality?.Current ?? 0;
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        Vitality v = Player.Vitality;
        if (v.Current != _shown)
        {
            if (v.Current < _shown)
            {
                _lostIndex = v.Current;
                _lostFlash = 1f;
                _edgeFlash = 1f;
            }

            _shown = v.Current;
            _bright = BrightTime;
        }

        _bright = Mathf.Max(0f, _bright - dt);
        _lostFlash = Mathf.Max(0f, _lostFlash - dt * 2.5f);
        _edgeFlash = Mathf.Max(0f, _edgeFlash - dt / FlashTime);
        _beat = LastHit(v) ? _beat + dt * BeatRate : 0f;
        Canvas.QueueRedraw();
    }

    /// <summary>One hit left out of more.</summary>
    private static bool LastHit(Vitality v) => v.Current == 1 && v.Max > 1;

    private void DrawHud()
    {
        if (_edgeFlash > 0f)
        {
            DrawEdgeFlash();
        }

        Vitality v = Player.Vitality;
        float ui = Canvas.Size.Y / 1080f;
        bool lastHit = LastHit(v);
        float opacity = lastHit ? 1f : Mathf.Lerp(RestOpacity, 1f, Mathf.Clamp(_bright / 0.6f, 0f, 1f));
        float size = PipSize * ui, gap = PipGap * ui;
        var origin = new Vector2(50f * ui, 50f * ui);
        for (int i = 0; i < v.Max; i++)
        {
            Vector2 c = origin + new Vector2(i * gap, 0f);
            bool full = i < v.Current;
            float s = size;
            Color color = (full ? Fill : Empty) with { A = (full ? Fill.A : Empty.A) * opacity };
            if (lastHit && i == 0)
            {
                // The last hit beats: a quick swell and a slow ebb, in the hurt color.
                float pulse = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(_beat * Mathf.Tau), 3f);
                s *= 1f + 0.22f * pulse;
                color = Fill.Lerp(HurtColor, 0.45f + 0.45f * pulse);
            }

            if (i == _lostIndex && _lostFlash > 0f)
            {
                s *= 1f + 0.8f * _lostFlash;
                color = Empty.Lerp(HurtColor, _lostFlash) with { A = Mathf.Max(color.A, _lostFlash) };
            }

            Diamond(c, s + 3f * ui, _diamond);
            Canvas.DrawColoredPolygon(_diamond, Backing with { A = Backing.A * opacity });
            Diamond(c, s, _diamond);
            if (full || (i == _lostIndex && _lostFlash > 0f))
            {
                Canvas.DrawColoredPolygon(_diamond, color);
            }
            else
            {
                for (int k = 0; k < 4; k++)
                {
                    _outline[k] = _diamond[k];
                }

                _outline[4] = _diamond[0];
                Canvas.DrawPolyline(_outline, color, Mathf.Max(1f, 1.6f * ui), true);
            }
        }
    }

    /// <summary>The hurt color washing in from the four edges, fading toward the middle.</summary>
    private void DrawEdgeFlash()
    {
        Vector2 size = Canvas.Size;
        float depth = FlashDepth * size.Y;
        Color edge = HurtColor with { A = FlashStrength * _edgeFlash * _edgeFlash };
        Color clear = HurtColor with { A = 0f };
        var inner0 = new Vector2(depth, depth);
        var inner1 = new Vector2(size.X - depth, size.Y - depth);
        Band(Vector2.Zero, new Vector2(size.X, 0f), new Vector2(inner1.X, inner0.Y), inner0, edge, clear);
        Band(new Vector2(size.X, size.Y), new Vector2(0f, size.Y), new Vector2(inner0.X, inner1.Y), inner1, edge, clear);
        Band(new Vector2(0f, size.Y), Vector2.Zero, inner0, new Vector2(inner0.X, inner1.Y), edge, clear);
        Band(new Vector2(size.X, 0f), size, inner1, new Vector2(inner1.X, inner0.Y), edge, clear);
    }

    /// <summary>A quad from the outer edge <paramref name="a"/>–<paramref name="b"/> in to <paramref name="c"/>–<paramref name="d"/>,
    /// shaded from <paramref name="outer"/> to <paramref name="inner"/>.</summary>
    private void Band(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color outer, Color inner)
    {
        _band[0] = a;
        _band[1] = b;
        _band[2] = c;
        _band[3] = d;
        _bandColors[0] = outer;
        _bandColors[1] = outer;
        _bandColors[2] = inner;
        _bandColors[3] = inner;
        Canvas.DrawPolygon(_band, _bandColors);
    }

    /// <summary>A diamond of half height <paramref name="s"/> around <paramref name="c"/>, into <paramref name="points"/>.</summary>
    private static void Diamond(Vector2 c, float s, Vector2[] points)
    {
        points[0] = c + new Vector2(0f, -s);
        points[1] = c + new Vector2(s * 0.7f, 0f);
        points[2] = c + new Vector2(0f, s);
        points[3] = c + new Vector2(-s * 0.7f, 0f);
    }
}
