using Godot;

namespace TheDeep;

/// <summary>
/// The Harpoon's target when the camera doesn't frame it: a small pulsing chevron held inside the screen's edge,
/// pointing at the ring the harpoon would fire at now (<see cref="Player.AnchorInSight"/>), so the pilgrim knows a
/// zip is there to take. Shown only with the Harpoon and only while that ring is off screen (<see cref="EdgePointer"/>);
/// eased in and out. Colored like the ring (the Harpoon's gate color, <see cref="GateKinds"/>), so the two read as one thing.
/// </summary>
public partial class AnchorPointer : CanvasLayer
{
    [Export] public Player Player { get; set; } = null!;
    [Export] public Camera3D Camera { get; set; } = null!;

    [Export] public Color Tint { get; set; } = GateKinds.ColorOf(Ability.Harpoon);
    [Export] public Color Backing { get; set; } = new(0.06f, 0.04f, 0.1f, 0.55f);

    /// <summary>How far inside the screen's edge the chevron sits, and its size (pixels at 1080 rows).</summary>
    [Export] public float Margin { get; set; } = 46f;
    [Export] public float Size { get; set; } = 15f;

    /// <summary>Pulses per second.</summary>
    [Export] public float PulseRate { get; set; } = 1.6f;

    private Control? _canvas;
    private Control Canvas => _canvas ??= new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
    private readonly Vector2[] _chevron = new Vector2[3];
    private Vector2 _at;
    private Vector2 _toward = Vector2.Up;
    private float _shown;
    private float _time;
    private float _scale = 1f;

    /// <summary>Whether the chevron is showing now (for the checks).</summary>
    public bool Pointing { get; private set; }

    public override void _Ready()
    {
        Layer = 69;
        Canvas.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        Canvas.Draw += DrawChevron;
        AddChild(Canvas);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;
        Pointing = false;
        if (Player.AnchorInSight is { } anchor && Player.Abilities.Has(Ability.Harpoon) && IsInstanceValid(anchor))
        {
            Vector2 screen = Camera.GetViewport().GetVisibleRect().Size;
            _scale = screen.Y / 1080f;
            Vector2 at = Camera.UnprojectPosition(anchor.GlobalPosition);
            if (EdgePointer.For(at.ToCore(), screen.ToCore(), Margin * _scale) is { } mark)
            {
                _at = mark.Position.ToGodot();
                _toward = mark.Direction.ToGodot();
                Pointing = true;
            }
        }

        _shown = Motion.Approach(_shown, Pointing ? 1f : 0f, dt * (Pointing ? 7f : 4f));
        Canvas.Visible = _shown > 0.001f;
        if (Canvas.Visible)
        {
            Canvas.QueueRedraw();
        }
    }

    private void DrawChevron()
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(_time * PulseRate * Mathf.Tau);
        float s = Size * _scale * (1f + 0.12f * pulse);
        Vector2 side = new(-_toward.Y, _toward.X);
        Vector2 back = _at - _toward * s * 0.5f;
        _chevron[0] = back + side * s;
        _chevron[1] = _at + _toward * s * 0.5f;
        _chevron[2] = back - side * s;
        float a = _shown * (0.75f + 0.25f * pulse);
        // A dark disc under it so it reads on pale stone, a glow that breathes, then the chevron.
        Canvas.DrawCircle(_at, s * 1.55f, Backing with { A = Backing.A * _shown });
        Canvas.DrawCircle(_at, s * (1.25f + 0.55f * pulse), Tint with { A = 0.22f * a });
        Canvas.DrawColoredPolygon(_chevron, Tint with { A = a * 0.9f });
        Canvas.DrawPolyline(_chevron, Tint.Lightened(0.35f) with { A = a }, 3f * _scale, true);
    }
}
