using Godot;

namespace TheDeep;

/// <summary>
/// Draws a <see cref="ButtonGlyph"/> on a canvas at a size: a key cap (an arrow key as its arrow), a face button in its color, a
/// shoulder, a trigger, a stick with its knob, a d-pad whose arms light with their directions; the lit glyph fills with ink. The
/// <see cref="ControlsManual"/> draws its rows at scale 1, an <see cref="ActionPrompt"/> larger.
/// </summary>
public sealed class ButtonPainter : System.IDisposable
{
    /// <summary>The buttons' ink and their dark ground.</summary>
    public static readonly Color Ink = new(0.78f, 0.86f, 0.88f);
    public static readonly Color Dark = new(0.04f, 0.05f, 0.07f);

    private static readonly Color[] FaceColors =
    {
        new(0.47f, 0.82f, 0.52f), new(0.92f, 0.46f, 0.42f), new(0.48f, 0.62f, 0.96f), new(0.95f, 0.82f, 0.42f),
    };

    private readonly float _s;
    private readonly StyleBoxFlat _cap = new() { AntiAliasing = true, CornerDetail = 5 };

    /// <summary>A painter drawing <paramref name="scale"/> times the manual's size (a cap 18 pixels high at 1).</summary>
    public ButtonPainter(float scale = 1f)
    {
        _s = scale;
        _cap.SetBorderWidthAll(Mathf.Max(1, Mathf.RoundToInt(scale)));
        Round(5f);
    }

    /// <summary>Frees its cap's style.</summary>
    public void Dispose() => _cap.Dispose();

    /// <summary>A key cap's height, pixels.</summary>
    public float CapHeight => 18f * _s;

    /// <summary>The font size written on caps.</summary>
    public int CapFont => Mathf.RoundToInt(12f * _s);

    /// <summary>How wide <paramref name="glyph"/> is drawn, pixels.</summary>
    public float Width(Font font, ButtonGlyph glyph) => glyph.Shape switch
    {
        ButtonNames.PadShape.Face or ButtonNames.PadShape.Stick or ButtonNames.PadShape.Dpad => CapHeight + 2f * _s,
        ButtonNames.PadShape.Shoulder or ButtonNames.PadShape.Trigger => 34f * _s,
        _ when ButtonNames.ArrowOf(glyph.Text) >= 0 => CapHeight + 3f * _s,
        _ => Mathf.Max(CapHeight + 3f * _s, font.GetStringSize(glyph.Text, HorizontalAlignment.Left, -1f, CapFont).X + 12f * _s),
    };

    /// <summary>
    /// Draws <paramref name="glyph"/> on <paramref name="canvas"/> centered on <paramref name="mid"/>, at opacity <paramref name="a"/>;
    /// a live stick's knob sits where <paramref name="stick"/> pushes it.
    /// </summary>
    public void Draw(CanvasItem canvas, Font font, ButtonGlyph glyph, Vector2 mid, float a, Vector2 stick)
    {
        float lit = glyph.Lit;
        float s = _s;
        switch (glyph.Shape)
        {
            case ButtonNames.PadShape.Face:
            {
                Color tint = FaceColors[glyph.Face];
                float r = CapHeight * 0.5f + s;
                canvas.DrawCircle(mid, r, Dark.Lerp(tint, lit * 0.9f) with { A = (0.5f + 0.4f * lit) * a });
                canvas.DrawArc(mid, r, 0f, Mathf.Tau, 48, tint with { A = (0.7f + 0.3f * lit) * a }, 1.2f * s, true);
                canvas.DrawString(font, new Vector2(mid.X - r, mid.Y + 4.5f * s), glyph.Text, HorizontalAlignment.Center, 2f * r, CapFont,
                    tint.Lerp(Dark, lit) with { A = a });
                break;
            }
            case ButtonNames.PadShape.Stick:
            {
                float r = CapHeight * 0.5f + s;
                canvas.DrawCircle(mid, r, Dark with { A = 0.5f * a });
                canvas.DrawArc(mid, r, 0f, Mathf.Tau, 48, Ink with { A = (0.55f + 0.4f * lit) * a }, 1.2f * s, true);
                Vector2 knob = mid + (glyph.Push == Vector2.Zero ? stick : glyph.Push) * 4f * s;
                canvas.DrawCircle(knob, r - 4.5f * s, Ink with { A = (0.18f + 0.7f * lit) * a });
                canvas.DrawString(font, new Vector2(knob.X - r, knob.Y + 4f * s), glyph.Text, HorizontalAlignment.Center, 2f * r,
                    Mathf.RoundToInt(11f * s), Ink.Lerp(Dark, lit) with { A = 0.9f * a });
                break;
            }
            case ButtonNames.PadShape.Dpad:
            {
                // A cross; each arm lights with its direction.
                float arm = 5.5f * s;
                float half = 3.2f * s;
                canvas.DrawRect(new Rect2(mid - new Vector2(half, arm + half), new Vector2(2f * half, 2f * (arm + half))), Dark with { A = 0.5f * a });
                canvas.DrawRect(new Rect2(mid - new Vector2(arm + half, half), new Vector2(2f * (arm + half), 2f * half)), Dark with { A = 0.5f * a });
                foreach (StringName action in glyph.Actions)
                {
                    Vector2 dir = DirectionOf(action);
                    bool held = Input.IsActionPressed(action);
                    Vector2 c = mid + dir * (arm * 0.5f + half * 0.5f);
                    Vector2 extent = dir.X != 0f ? new Vector2(arm, 2f * half) : new Vector2(2f * half, arm);
                    canvas.DrawRect(new Rect2(c - extent * 0.5f, extent), Ink with { A = (held ? 0.95f : 0.45f) * a });
                }

                break;
            }
            case ButtonNames.PadShape.Trigger:
                _cap.SetCornerRadiusAll(Mathf.RoundToInt(3f * s));
                _cap.CornerRadiusTopLeft = _cap.CornerRadiusTopRight = Mathf.RoundToInt(9f * s);
                DrawCap(canvas, font, glyph.Text, mid, 34f * s, CapHeight + 2f * s, lit, a);
                Round(5f);
                break;
            case ButtonNames.PadShape.Shoulder:
                Round(8f);
                DrawCap(canvas, font, glyph.Text, mid, 34f * s, CapHeight - 3f * s, lit, a);
                Round(5f);
                break;
            default:
                DrawCap(canvas, font, glyph.Text, mid, Width(font, glyph), CapHeight, lit, a);
                break;
        }
    }

    private void Round(float radius) => _cap.SetCornerRadiusAll(Mathf.RoundToInt(radius * _s));

    private void DrawCap(CanvasItem canvas, Font font, string text, Vector2 mid, float width, float height, float lit, float a)
    {
        Rect2 rect = new(mid - new Vector2(width, height) * 0.5f, new Vector2(width, height));
        _cap.BgColor = Dark.Lerp(Ink, lit * 0.85f) with { A = (0.5f + 0.4f * lit) * a };
        _cap.BorderColor = Ink with { A = (0.45f + 0.45f * lit) * a };
        canvas.DrawStyleBox(_cap, rect);
        Color ink = Ink.Lerp(Dark, lit) with { A = 0.95f * a };
        int arrow = ButtonNames.ArrowOf(text);
        if (arrow >= 0)
        {
            // Arrow keys: a small triangle, pointing its way (0 up, 1 left, 2 down, 3 right).
            Vector2 dir = arrow switch { 0 => Vector2.Up, 1 => Vector2.Left, 2 => Vector2.Down, _ => Vector2.Right };
            Vector2 side = new(-dir.Y, dir.X);
            float s = _s;
            canvas.DrawColoredPolygon(new[] { mid + dir * 4f * s, mid - dir * 3f * s + side * 4f * s, mid - dir * 3f * s - side * 4f * s }, ink);
            return;
        }

        canvas.DrawString(font, new Vector2(rect.Position.X, mid.Y + CapFont * 0.36f), text, HorizontalAlignment.Center, width, CapFont, ink);
    }

    private static Vector2 DirectionOf(StringName action) =>
        action == InputActions.MoveUp ? Vector2.Up : action == InputActions.MoveDown ? Vector2.Down : action == InputActions.MoveLeft ? Vector2.Left : Vector2.Right;
}
