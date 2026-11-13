using Godot;

namespace TheDeep;

/// <summary>
/// How the game's on-screen text looks, as data: one family for every text brick (the banner, the hint line, the dialogue box, the
/// action prompts, the controls manual, the map's words, the end card, the debug readout), so none keeps a look of its own. It holds
/// the font (Jost, assets/fonts, under the OFL beside it), three sizes (<see cref="Caption"/>, <see cref="Body"/>, <see cref="Title"/>),
/// the inks (<see cref="Ink"/>, <see cref="Dim"/>, and the accent, which is the biome's own (<see cref="BiomeProfile.Accent"/>,
/// docs/art-direction.md#look) lifted toward the ink so it reads as text), the dark plate text sits on where it needs one, the rim
/// that keeps text without a plate legible on a pale wall, the fade times, and where the lines said stand on screen
/// (<see cref="Places"/>).
/// <para>The game's own is resources/ui/text_style.tres; a world may give its own in its place (<see cref="WorldDefinition.TextStyle"/>),
/// made the shared one by the <see cref="World"/> as it enters the tree, before any text brick is ready. Sizes are pixels on the
/// game's canvas (1600 × 900, stretched to the window).</para>
/// </summary>
[GlobalClass]
public partial class TextStyle : Resource
{
    /// <summary>The game's own style, used by every world that gives none.</summary>
    public const string GamePath = "res://resources/ui/text_style.tres";

    private static TextStyle? _shared;

    /// <summary>The style of the world being played (the game's own until a world says otherwise).</summary>
    public static TextStyle Shared => _shared ??= ResourceLoader.Load<TextStyle>(GamePath);

    /// <summary>Makes <paramref name="style"/> the shared style; none: the game's own.</summary>
    public static void Use(TextStyle? style) => _shared = style ?? ResourceLoader.Load<TextStyle>(GamePath);

    /// <summary>The one font every text brick writes in.</summary>
    [Export] public Font Font { get; set; } = null!;

    /// <summary>Small words: a speaker's name, a legend, the manual's rows, the debug readout.</summary>
    [ExportGroup("Sizes")]
    [Export] public int Caption { get; set; } = 15;

    /// <summary>A line read: a hint, a dialogue line, a banner's line, a prompt's verb.</summary>
    [Export] public int Body { get; set; } = 22;

    /// <summary>A name announced: a banner's title, the end card's.</summary>
    [Export] public int Title { get; set; } = 40;

    /// <summary>The ink of what is read first.</summary>
    [ExportGroup("Ink")]
    [Export] public Color Ink { get; set; } = new(0.94f, 0.92f, 1f);

    /// <summary>The ink of what is read after: a banner's line, a legend, a header.</summary>
    [Export] public Color Dim { get; set; } = new(0.78f, 0.77f, 0.86f);

    /// <summary>How far the biome's accent is lifted toward <see cref="Ink"/> for text and marks, 0–1.</summary>
    [Export(PropertyHint.Range, "0,1")] public float AccentLift { get; set; } = 0.45f;

    /// <summary>The plate under text that needs one (the dialogue, a prompt, the manual): its color, opaque.</summary>
    [ExportGroup("Plate")]
    [Export] public Color Plate { get; set; } = new(0.03f, 0.03f, 0.05f);

    /// <summary>The plate's opacity.</summary>
    [Export(PropertyHint.Range, "0,1")] public float PlateOpacity { get; set; } = 0.62f;

    /// <summary>The plate's corner radius, pixels.</summary>
    [Export] public int CornerRadius { get; set; } = 6;

    /// <summary>Room between the plate's edge and its text, pixels across and down.</summary>
    [Export] public Vector2 Padding { get; set; } = new(20f, 12f);

    /// <summary>The plate's thin outline, and its width in pixels (zero for none).</summary>
    [Export] public Color Outline { get; set; } = new(0.94f, 0.92f, 1f, 0.08f);

    [Export] public int OutlineWidth { get; set; } = 1;

    /// <summary>The dark rim under text standing on the world without a plate (a banner, the hint line): a shade cast a little
    /// down, spread around each letter, so pale text reads on a pale wall.</summary>
    [ExportGroup("Rim")]
    [Export] public Color Rim { get; set; } = new(0.01f, 0.01f, 0.03f, 0.6f);

    /// <summary>How far the rim spreads around each letter, pixels.</summary>
    [Export] public int RimWidth { get; set; } = 2;

    /// <summary>How far down the rim is cast, pixels.</summary>
    [Export] public int RimDrop { get; set; } = 2;

    /// <summary>The top of the banner's plate in its high place, pixels below the canvas's top: under the health and the room's name.</summary>
    [ExportGroup("Places")]
    [Export] public float BannerTop { get; set; } = 96f;

    /// <summary>The bottom of the hint line's plate, pixels above the canvas's bottom.</summary>
    [Export] public float HintBottom { get; set; } = 64f;

    /// <summary>Between the hint line's plate and a banner's gone low over it, pixels: two plates, never one block.</summary>
    [Export] public float PlaceGap { get; set; } = 18f;

    /// <summary>Seconds for a line to ease in (a banner, a hint).</summary>
    [ExportGroup("Fades")]
    [Export] public float Appear { get; set; } = 0.5f;

    /// <summary>Seconds for a line to ease out once held.</summary>
    [Export] public float Leave { get; set; } = 0.9f;

    /// <summary>Seconds for a line to ease out when another waits behind it.</summary>
    [Export] public float Hurried { get; set; } = 0.35f;

    /// <summary>Seconds for what the player opens or comes up to (the dialogue, the map, a prompt) to ease in or out.</summary>
    [Export] public float Panel { get; set; } = 0.2f;

    /// <summary>Seconds for the end card's lines to ease in or out.</summary>
    [Export] public float Slow { get; set; } = 1.5f;

    /// <summary>Where the banner and the hint line stand, one layout for both.</summary>
    public TextPlaces Places => new(BannerTop, HintBottom, PlaceGap);

    /// <summary>How tall a plate holding one row of text at <paramref name="size"/> stands, its padding included, pixels.</summary>
    public float RowPlate(int size) => Font.GetHeight(size) + 2f * Padding.Y;

    /// <summary>The accent of <paramref name="biome"/> as text and marks wear it.</summary>
    public Color Accent(BiomeProfile biome) => biome.Accent.Lerp(Ink, AccentLift) with { A = 1f };

    /// <summary>A new plate as a style box, every length times <paramref name="scale"/> (a prompt's face is drawn larger than the screen).</summary>
    public StyleBoxFlat NewPlate(float scale = 1f)
    {
        var box = new StyleBoxFlat
        {
            AntiAliasing = true,
            CornerDetail = 8,
            BgColor = Plate with { A = PlateOpacity },
            BorderColor = Outline,
            ContentMarginLeft = Padding.X * scale,
            ContentMarginRight = Padding.X * scale,
            ContentMarginTop = Padding.Y * scale,
            ContentMarginBottom = Padding.Y * scale,
        };
        box.SetCornerRadiusAll(Mathf.RoundToInt(CornerRadius * scale));
        box.SetBorderWidthAll(Mathf.RoundToInt(OutlineWidth * scale));
        return box;
    }

    /// <summary>
    /// Dresses <paramref name="label"/> as a speaker's name over words spoken: a caption in capitals, in <paramref name="accent"/> (the
    /// room's, <see cref="Accent"/>).
    /// </summary>
    public void DressSpeaker(Label label, Color accent)
    {
        Dress(label, Caption, accent);
        label.Uppercase = true;
    }

    /// <summary>Dresses <paramref name="label"/> in the font at <paramref name="size"/> in <paramref name="ink"/>; with <paramref name="rim"/>, ringed for the world behind it.</summary>
    public void Dress(Label label, int size, Color ink, bool rim = false)
    {
        label.AddThemeFontOverride("font", Font);
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", ink);
        if (rim)
        {
            label.AddThemeColorOverride("font_shadow_color", Rim);
            label.AddThemeConstantOverride("shadow_outline_size", RimWidth);
            label.AddThemeConstantOverride("shadow_offset_x", 0);
            label.AddThemeConstantOverride("shadow_offset_y", RimDrop);
        }
    }
}
