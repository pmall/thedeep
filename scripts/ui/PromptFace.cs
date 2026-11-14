using Godot;

namespace TheDeep;

/// <summary>
/// What an <see cref="ActionPrompt"/> shows, drawn on the screen like every other text brick: the button (a
/// <see cref="ButtonGlyph"/>), then the verb as body text, on the plate, in the shared <see cref="TextStyle"/>; for a hold, a ring
/// around the button that fills as it is held. Sizes itself to fit, so the prompt stays centered.
/// </summary>
public partial class PromptFace : Control
{
    /// <summary>The glyphs' size against the manual's: a cap about as tall as the verb's letters.</summary>
    private const float GlyphScale = 1.2f;
    private const float Gap = 8f;
    private const float RingWidth = 3f;

    private readonly ButtonPainter _painter = new(GlyphScale);
    private StyleBoxFlat? _plate;
    private StyleBoxFlat Plate => _plate ??= Style.NewPlate();

    private static TextStyle Style => TextStyle.Shared;

    /// <summary>The verb's size, pixels.</summary>
    private static int VerbFont => Style.Body;
    private string _verb = "";
    private ButtonGlyph? _glyph;
    private bool _hold;
    private float _glyphWidth;
    private float _ring;

    /// <summary>How full the hold's ring is, 0–1 (a hold only).</summary>
    public float Fill { get; set; }

    /// <summary>Shows <paramref name="glyph"/> (none: the verb alone) and <paramref name="verb"/>, with a ring when <paramref name="hold"/>, sized to fit.</summary>
    public void Show(ButtonGlyph? glyph, string verb, bool hold)
    {
        _glyph = glyph;
        if (glyph is not null)
        {
            glyph.Lit = RestLit;
        }

        _verb = verb;
        _hold = hold;
        Font font = Style.Font;
        float textWidth = font.GetStringSize(verb, HorizontalAlignment.Left, -1f, VerbFont).X;
        _glyphWidth = glyph is null ? 0f : _painter.Width(font, glyph);
        float glyphHeight = _painter.CapHeight + 2f * GlyphScale;
        _ring = hold ? 0.5f * Mathf.Sqrt(_glyphWidth * _glyphWidth + glyphHeight * glyphHeight) + RingWidth : 0f;
        float glyphBox = glyph is null ? 0f : (hold ? 2f * _ring + RingWidth : _glyphWidth) + Gap;
        // A row as tall as the hint line's, unless the button or its ring stands taller.
        float height = Mathf.Max(font.GetHeight(VerbFont), hold ? 2f * _ring + RingWidth : glyphHeight);
        Vector2 margin = Style.Padding;
        Size = new Vector2(Mathf.Ceil(2f * margin.X + glyphBox + textWidth), Mathf.Ceil(2f * margin.Y + height));
        QueueRedraw();
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
            _plate?.Dispose();
        }
    }

    public override void _Draw()
    {
        Font font = Style.Font;
        float mid = Size.Y * 0.5f;
        // The plate under it all, so it reads on bright walls and lit glyphs alike.
        DrawStyleBox(Plate, new Rect2(Vector2.Zero, Size));
        float x = Style.Padding.X;
        if (_glyph is { } glyph)
        {
            float box = _hold ? 2f * _ring + RingWidth : _glyphWidth;
            Vector2 center = new(x + box * 0.5f, mid);
            if (_hold)
            {
                DrawCircle(center, _ring, Style.Plate with { A = Style.PlateOpacity });
                DrawArc(center, _ring, 0f, Mathf.Tau, 64, ButtonPainter.Ink with { A = 0.3f }, RingWidth, true);
                if (Fill > 0f)
                {
                    DrawArc(center, _ring, -Mathf.Pi * 0.5f, -Mathf.Pi * 0.5f + Mathf.Tau * Fill, 64, Style.Ink, RingWidth, true);
                }
            }

            _painter.Draw(this, font, glyph, center, 1f, Vector2.Zero);
            x += box + Gap;
        }

        // Centered on the plate's middle line: half the font's ascent over its descent below the baseline.
        int verbFont = VerbFont;
        Vector2 baseline = new(x, mid + 0.5f * (font.GetAscent(verbFont) - font.GetDescent(verbFont)));
        // The rim too, all round the letters: the plate is translucent, and a lit glyph behind it would wash them out.
        int rim = 2 * (Style.RimWidth + Style.RimDrop);
        DrawStringOutline(font, baseline, _verb, HorizontalAlignment.Left, -1f, verbFont, rim, Style.Rim);
        DrawString(font, baseline, _verb, HorizontalAlignment.Left, -1f, verbFont, Style.Ink);
    }
}
