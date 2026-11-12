using Godot;

namespace TheDeep;

/// <summary>
/// What an <see cref="ActionPrompt"/> shows, drawn into its own small viewport: the button (a <see cref="ButtonGlyph"/>), then the
/// verb, on a dark plate; for a hold, a ring around the button that fills as it is held. Sizes itself to fit, so the prompt stays centered.
/// </summary>
public partial class PromptFace : Control
{
    /// <summary>The glyphs' size against the manual's: a cap about as tall as the verb's letters.</summary>
    private const float GlyphScale = 2.6f;
    private const int VerbFont = 48;
    private const int VerbOutline = 10;
    private const float Gap = 18f;
    private const float RingWidth = 7f;
    private const float Margin = 16f;

    private static readonly Color Verb = new(0.94f, 0.92f, 1f);
    private static readonly Color Outline = new(0.05f, 0.04f, 0.08f, 0.6f);

    private readonly ButtonPainter _painter = new(GlyphScale);
    private readonly StyleBoxFlat _plate = new() { AntiAliasing = true, CornerDetail = 8, BgColor = ButtonPainter.Dark with { A = 0.6f } };
    private string _verb = "";
    private ButtonGlyph? _glyph;
    private bool _hold;
    private float _glyphWidth;
    private float _ring;

    /// <summary>How full the hold's ring is, 0–1 (a hold only).</summary>
    public float Fill { get; set; }

    /// <summary>Shows <paramref name="glyph"/> (none: the verb alone) and <paramref name="verb"/>, with a ring when <paramref name="hold"/>; the size it needs, pixels.</summary>
    public Vector2I Show(ButtonGlyph? glyph, string verb, bool hold)
    {
        _glyph = glyph;
        if (glyph is not null)
        {
            glyph.Lit = RestLit;
        }

        _verb = verb;
        _hold = hold;
        Font font = GetThemeDefaultFont();
        float textWidth = font.GetStringSize(verb, HorizontalAlignment.Left, -1f, VerbFont).X;
        _glyphWidth = glyph is null ? 0f : _painter.Width(font, glyph);
        float glyphHeight = _painter.CapHeight + 2f * GlyphScale;
        _ring = hold ? 0.5f * Mathf.Sqrt(_glyphWidth * _glyphWidth + glyphHeight * glyphHeight) + RingWidth : 0f;
        float glyphBox = glyph is null ? 0f : (hold ? 2f * _ring + RingWidth : _glyphWidth) + Gap;
        float height = Mathf.Max(VerbFont * 1.4f, hold ? 2f * _ring + RingWidth : glyphHeight);
        var size = new Vector2I(Mathf.CeilToInt(2f * Margin + glyphBox + textWidth + VerbOutline), Mathf.CeilToInt(2f * Margin + height));
        Size = size;
        QueueRedraw();
        return size;
    }

    /// <summary>How lit the button stands unheld: a pale cap with dark letters reads on any ground.</summary>
    private const float RestLit = 0.8f;

    /// <summary>Lights the button fully while its action is held.</summary>
    public void Light(float dt) => _glyph?.Light(dt, RestLit);

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            _painter.Dispose();
            _plate.Dispose();
        }
    }

    public override void _Draw()
    {
        Font font = GetThemeDefaultFont();
        float mid = Size.Y * 0.5f;
        // A dark plate under it all, so it reads on bright walls and lit glyphs alike.
        _plate.SetCornerRadiusAll(Mathf.RoundToInt(mid - 2f));
        DrawStyleBox(_plate, new Rect2(new Vector2(2f, 2f), Size - new Vector2(4f, 4f)));
        float x = Margin;
        if (_glyph is { } glyph)
        {
            float box = _hold ? 2f * _ring + RingWidth : _glyphWidth;
            Vector2 center = new(x + box * 0.5f, mid);
            if (_hold)
            {
                DrawCircle(center, _ring, Outline);
                DrawArc(center, _ring, 0f, Mathf.Tau, 64, ButtonPainter.Ink with { A = 0.3f }, RingWidth, true);
                if (Fill > 0f)
                {
                    DrawArc(center, _ring, -Mathf.Pi * 0.5f, -Mathf.Pi * 0.5f + Mathf.Tau * Fill, 64, Verb, RingWidth, true);
                }
            }

            _painter.Draw(this, font, glyph, center, 1f, Vector2.Zero);
            x += box + Gap;
        }

        Vector2 baseline = new(x, mid + VerbFont * 0.36f);
        DrawStringOutline(font, baseline, _verb, HorizontalAlignment.Left, -1f, VerbFont, VerbOutline, Outline);
        DrawString(font, baseline, _verb, HorizontalAlignment.Left, -1f, VerbFont, Verb);
    }
}
