using Godot;

namespace TheDeep;

/// <summary>
/// How the game's painted signature lays its paint (docs/art-direction.md#signature): the brush, the pigment and paper,
/// the tone. A layer's <see cref="BiomeProfile.Paint"/> points to one; <see cref="PaintPass"/> paints every frame with it.
/// Every strength at 0 leaves that part of the look out. Radii are in half-resolution pixels (the brush works at half
/// resolution), the grain cell in screen pixels.
/// </summary>
[GlobalClass]
public partial class PaintStyle : Resource
{
    /// <summary>Where styles live: res://resources/paint/&lt;name&gt;.tres.</summary>
    public const string Folder = "res://resources/paint";

    [Export] public string DisplayName { get; set; } = "";

    /// <summary>The brush's radius on the play plane, where detail matters (half-res pixels).</summary>
    [ExportGroup("Brush")]
    [Export(PropertyHint.Range, "0.5,8")] public float RadiusNear { get; set; } = 2.5f;

    /// <summary>The brush's radius deep in the haze, where the backdrop is simplified (half-res pixels).</summary>
    [Export(PropertyHint.Range, "0.5,12")] public float RadiusFar { get; set; } = 6f;

    /// <summary>How sharply the calmest part of the brush wins: higher keeps edges crisper and areas flatter.</summary>
    [Export(PropertyHint.Range, "1,18")] public float Sharpness { get; set; } = 8f;

    /// <summary>How little the strokes stretch along edges: lower stretches them more (the filter's alpha).</summary>
    [Export(PropertyHint.Range, "0.1,4")] public float Anisotropy { get; set; } = 1f;

    /// <summary>0 a pilgrim's hand; 1 strokes snapped to 0/45/90 degrees and stretched: a machine's hand.</summary>
    [Export(PropertyHint.Range, "0,1")] public float Mechanical { get; set; }

    /// <summary>The whole pass's share of the frame (1 painted, 0 the raw render).</summary>
    [Export(PropertyHint.Range, "0,1")] public float Strength { get; set; } = 1f;

    /// <summary>Full-resolution detail laid back over the paint near the play plane, so gameplay edges stay sharp.</summary>
    [Export(PropertyHint.Range, "0,1")] public float Detail { get; set; } = 0.5f;

    /// <summary>How far the hand wobbles (screen pixels): edges come out hand-cut instead of traced.</summary>
    [Export(PropertyHint.Range, "0,8")] public float Wobble { get; set; } = 2f;

    /// <summary>How much of the frame is laid in visible strokes (0 none: the brush filter alone).</summary>
    [ExportGroup("Strokes")]
    [Export(PropertyHint.Range, "0,1")] public float Strokes { get; set; } = 0.85f;

    /// <summary>The strokes' grid cell, in screen pixels on the play plane (a stroke is about 1.6 cells long, 0.6 wide).</summary>
    [Export(PropertyHint.Range, "3,40")] public float StrokeSize { get; set; } = 10f;

    /// <summary>How much broader strokes are deep in the haze than on the play plane (1 the same).</summary>
    [Export(PropertyHint.Range, "1,4")] public float StrokeGrowth { get; set; } = 2f;

    /// <summary>How much each stroke's paint varies from the next.</summary>
    [Export(PropertyHint.Range, "0,1")] public float StrokeJitter { get; set; } = 0.5f;

    /// <summary>Paint pooling at a stroke's edge: the darker side of an edge darkens this much more.</summary>
    [ExportGroup("Pigment")]
    [Export(PropertyHint.Range, "0,1")] public float Pooling { get; set; } = 0.3f;

    /// <summary>Paint laid unevenly: slow variation of the pigment's density across an area.</summary>
    [Export(PropertyHint.Range, "0,1")] public float Density { get; set; } = 0.15f;

    /// <summary>Pigment settling in the paper's grain, in flat midtones.</summary>
    [Export(PropertyHint.Range, "0,1")] public float Granulation { get; set; } = 0.15f;

    /// <summary>The paper grain's cell, in screen pixels at any depth.</summary>
    [Export(PropertyHint.Range, "1,12")] public float GrainCell { get; set; } = 3f;

    /// <summary>Specks of the machines' script in the paper (0 none).</summary>
    [Export(PropertyHint.Range, "0,1")] public float Glyphs { get; set; }

    /// <summary>How many values the paint is laid in.</summary>
    [ExportGroup("Tone")]
    [Export(PropertyHint.Range, "2,16")] public float Bands { get; set; } = 7f;

    /// <summary>How far colors are pulled into those values (0 smooth gradients).</summary>
    [Export(PropertyHint.Range, "0,1")] public float BandStrength { get; set; } = 0.3f;

    /// <summary>How far each hue is pulled toward the nearest of the layer's pigments (<see cref="BiomeProfile.Pigments"/>).</summary>
    [Export(PropertyHint.Range, "0,1")] public float PalettePull { get; set; } = 0.2f;


    /// <summary>Loads res://resources/paint/<paramref name="name"/>.tres, or null if there is none.</summary>
    public static PaintStyle? Load(string name)
    {
        string path = $"{Folder}/{name}.tres";
        return ResourceLoader.Exists(path) ? ResourceLoader.Load<PaintStyle>(path) : null;
    }
}
