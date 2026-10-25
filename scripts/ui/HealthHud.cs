using Godot;

namespace TheDeep;

/// <summary>
/// The pilgrim's health, top left: a short row of thin diamonds, filled for each hit it can still take.
/// Quiet when nothing happens, brighter for a moment when health changes; a lost diamond flashes and shrinks.
/// </summary>
public partial class HealthHud : CanvasLayer
{
    [Export] public Player Player { get; set; } = null!;

    [Export] public Color Fill { get; set; } = new(0.94f, 0.92f, 1f, 0.95f);
    [Export] public Color Empty { get; set; } = new(0.94f, 0.92f, 1f, 0.35f);

    /// <summary>Opacity at rest, and how long it stays bright after a change.</summary>
    [Export] public float RestOpacity { get; set; } = 0.55f;
    [Export] public float BrightTime { get; set; } = 2.5f;

    private Control _canvas = null!;
    private int _shown;
    private float _bright;
    private float _lostFlash;
    private int _lostIndex = -1;

    public override void _Ready()
    {
        Layer = 70;
        _canvas = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        _canvas.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _canvas.Draw += DrawPips;
        AddChild(_canvas);
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
            }

            _shown = v.Current;
            _bright = BrightTime;
        }

        _bright = Mathf.Max(0f, _bright - dt);
        _lostFlash = Mathf.Max(0f, _lostFlash - dt * 2.5f);
        _canvas.QueueRedraw();
    }

    private void DrawPips()
    {
        Vitality v = Player.Vitality;
        float ui = _canvas.Size.Y / 1080f;
        float opacity = Mathf.Lerp(RestOpacity, 1f, Mathf.Clamp(_bright / 0.6f, 0f, 1f));
        float size = 9f * ui, gap = 24f * ui;
        var origin = new Vector2(48f * ui, 46f * ui);
        for (int i = 0; i < v.Max; i++)
        {
            Vector2 c = origin + new Vector2(i * gap, 0f);
            bool full = i < v.Current;
            float s = size;
            Color color = (full ? Fill : Empty) with { A = (full ? Fill.A : Empty.A) * opacity };
            if (i == _lostIndex && _lostFlash > 0f)
            {
                s *= 1f + 0.8f * _lostFlash;
                color = Fill with { A = Mathf.Max(color.A, _lostFlash) };
            }

            var diamond = new[] { c + new Vector2(0f, -s), c + new Vector2(s * 0.7f, 0f), c + new Vector2(0f, s), c + new Vector2(-s * 0.7f, 0f) };
            if (full)
            {
                _canvas.DrawColoredPolygon(diamond, color);
            }
            else
            {
                _canvas.DrawPolyline(new[] { diamond[0], diamond[1], diamond[2], diamond[3], diamond[0] }, color, Mathf.Max(1f, 1.4f * ui), true);
            }
        }
    }
}
