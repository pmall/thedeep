using System;
using System.Linq;
using Godot;

namespace TheDeep;

/// <summary>
/// One drawn look, every switch and value of it, so several directions are kept at once and compared in play
/// (<see cref="ToonLook"/> shows one; F9 cycles them, `--look &lt;name&gt;` starts with one). Lives in
/// res://resources/looks/&lt;name&gt;.tres. Each part has its own value and 0 drops it, so a preset is the plain look plus
/// only what it names: the world's ink, grain and dirt (lib/look, lib/dirt), the figure's drawing (android_toon.gdshader
/// and its outline), the poses held on twos, and a grade over the whole frame (<see cref="ToneMap"/>).
/// </summary>
[GlobalClass]
public partial class LookPreset : Resource
{
    /// <summary>Where presets live.</summary>
    public const string Folder = "res://resources/looks";

    /// <summary>The name F9 prints and `--look` takes: the file's name.</summary>
    public string Name => ResourcePath.GetFile().GetBaseName();

    /// <summary>How strong the ink along the world's edges is, 0–1.</summary>
    [ExportGroup("World")]
    [Export(PropertyHint.Range, "0,1")] public float WorldInk { get; set; } = 0.8f;

    /// <summary>The ink line's width, pixels: the world's edges and the figure's outline.</summary>
    [Export] public float InkPixels { get; set; } = 1.5f;

    /// <summary>The ink's color (linear: the global uniform arrives unconverted).</summary>
    [Export] public Color InkColor { get; set; } = new(0.018f, 0.01f, 0.032f);

    /// <summary>How strong the paper grain is on the world and the figure, 0–1.</summary>
    [Export(PropertyHint.Range, "0,1")] public float Grain { get; set; } = 0.6f;

    /// <summary>How strong the watercolor stains on the world are, 0–1 (lib/dirt).</summary>
    [Export(PropertyHint.Range, "0,1")] public float Wash { get; set; } = 1f;

    /// <summary>How strong the drips down the world's side faces are, 0–1 (lib/dirt).</summary>
    [Export(PropertyHint.Range, "0,1")] public float Drips { get; set; } = 1f;

    /// <summary>How strong the ink hatching in the world's dark and dirty places is, 0–2 (lib/dirt; past 1 it spreads into
    /// the merely shaded).</summary>
    [Export(PropertyHint.Range, "0,2")] public float Hatch { get; set; } = 1f;

    /// <summary>How strong the brush strokes through every surface's paint are, 0–1 (lib/look).</summary>
    [Export(PropertyHint.Range, "0,1")] public float Brush { get; set; }

    /// <summary>How much of the light on the world is drawn as marks instead of shaded, 0–1 (lib/look; needs
    /// <see cref="WorldBands"/>, whose light draws them). The figure's lit side takes the same marks by <see cref="Scratch"/>.</summary>
    [Export(PropertyHint.Range, "0,1")] public float Mark { get; set; }

    /// <summary>Which marks the light is drawn in.</summary>
    [Export] public LookMarks MarkKind { get; set; }

    /// <summary>The marks' cell at the finest, meters (they grow with distance to stay seven pixels wide at least).</summary>
    [Export(PropertyHint.Range, "0.02,0.3")] public float MarkCell { get; set; } = 0.09f;

    /// <summary>How far the light is pushed toward full before it is drawn: above 1, faces in plain light come out clean and
    /// only the shade and the shadows carry marks.</summary>
    [Export(PropertyHint.Range, "1,4")] public float MarkGain { get; set; } = 1f;

    /// <summary>The room's ambient light as a share of its biome's: under 1, the direct light (and its marks) carries more of
    /// the image.</summary>
    [Export(PropertyHint.Range, "0,2")] public float Ambient { get; set; } = 1f;

    /// <summary>Whether the world is lit in flat bands as the figure is (lib/bands), instead of smoothly.</summary>
    [Export] public bool WorldBands { get; set; }

    /// <summary>How many steps of light the world is painted in (lib/bands).</summary>
    [Export(PropertyHint.Range, "1,6")] public float BandSteps { get; set; } = 3f;

    /// <summary>How soft the world's band edges are, as a share of a step (lib/bands).</summary>
    [Export(PropertyHint.Range, "0,0.5")] public float BandSoftness { get; set; } = 0.1f;

    /// <summary>The least light, as a share of a light's full, that a face it reaches at all takes (lib/bands).</summary>
    [Export(PropertyHint.Range, "0,1")] public float BandShade { get; set; } = 0.15f;

    /// <summary>The world's hard highlight (lib/bands).</summary>
    [Export(PropertyHint.Range, "0,2")] public float BandGloss { get; set; } = 0.2f;

    /// <summary>The figure's outline width as a share of <see cref="InkPixels"/> (0: none).</summary>
    [ExportGroup("Figure")]
    [Export(PropertyHint.Range, "0,3")] public float FigureInk { get; set; } = 0.7f;

    /// <summary>How long each shown pose holds, seconds (0: smooth).</summary>
    [Export] public float PoseHoldSeconds { get; set; } = 1f / 15f;

    /// <summary>Where the lit side turns to shade, as a cosine (android_toon.gdshader's toon_threshold).</summary>
    [Export(PropertyHint.Range, "-1,1")] public float Threshold { get; set; } = 0.1f;

    /// <summary>How soft that turn is (toon_softness).</summary>
    [Export(PropertyHint.Range, "0,0.5")] public float Softness { get; set; } = 0.2f;

    /// <summary>The light left in the shade (toon_shade).</summary>
    [Export(PropertyHint.Range, "0,1")] public float Shade { get; set; } = 0.42f;

    /// <summary>The hard highlight's strength (toon_gloss).</summary>
    [Export(PropertyHint.Range, "0,4")] public float Gloss { get; set; } = 0.18f;

    /// <summary>The rim a light behind the figure draws (toon_rim).</summary>
    [Export(PropertyHint.Range, "0,4")] public float Rim { get; set; } = 0.5f;

    /// <summary>How far the black lifts toward a dark grey (toon_lift).</summary>
    [Export(PropertyHint.Range, "0,0.4")] public float Lift { get; set; } = 0.035f;

    /// <summary>How much of the world's dust the dark parts carry (toon_dust).</summary>
    [Export(PropertyHint.Range, "0,1")] public float Dust { get; set; } = 0.8f;

    /// <summary>How far the worn chips along the parts' edges reach, meters (toon_chips).</summary>
    [Export(PropertyHint.Range, "0,0.03")] public float Chips { get; set; } = 0.009f;

    /// <summary>How bright the drawn rim of the smooth look's edge light is (toon_edge).</summary>
    [Export(PropertyHint.Range, "0,1")] public float Edge { get; set; } = 0.1f;

    /// <summary>How strong the light drawn out of the figure's black on its lit side is, in the world's marks
    /// (<see cref="MarkKind"/>), as on a scratchboard (toon_scratch; 0: none).</summary>
    [Export(PropertyHint.Range, "0,2")] public float Scratch { get; set; }

    /// <summary>How far the figure's black takes the ink's color, so it is made of the same ink as the world's lines
    /// (toon_ink_body).</summary>
    [Export(PropertyHint.Range, "0,1")] public float InkBody { get; set; }

    /// <summary>How much of the frame is printed in the grade's three inks (<see cref="ToneMap"/>; 0: no grade).</summary>
    [ExportGroup("Grade")]
    [Export(PropertyHint.Range, "0,1")] public float ToneAmount { get; set; }

    /// <summary>The grade's three inks, sRGB: the darks, the middle and the lights.</summary>
    [Export] public Color ToneShadow { get; set; } = new(0.06f, 0.04f, 0.1f);

    [Export] public Color ToneMid { get; set; } = new(0.38f, 0.38f, 0.48f);

    [Export] public Color ToneLight { get; set; } = new(0.94f, 0.93f, 0.9f);

    /// <summary>How much of their own hue saturated colors keep through the grade, 0–1.</summary>
    [Export(PropertyHint.Range, "0,1")] public float ToneKeep { get; set; } = 0.8f;

    /// <summary>How far values are pushed apart around the middle before the grade (1: as they are).</summary>
    [Export(PropertyHint.Range, "0.5,3")] public float ToneContrast { get; set; } = 1f;

    /// <summary>The grade, or null when the look leaves the frame's colors as they are.</summary>
    public ToneMap? Tone => ToneAmount <= 0f
        ? null
        : new ToneMap(ToneShadow.ToCore(), ToneMid.ToCore(), ToneLight.ToCore(), ToneAmount, ToneKeep, ToneContrast);

    /// <summary>Every preset in <see cref="Folder"/>, sorted by name.</summary>
    public static LookPreset[] All() =>
        DirAccess.GetFilesAt(Folder).Where(f => f.EndsWith(".tres", StringComparison.Ordinal)).Order(StringComparer.Ordinal)
            .Select(f => ResourceLoader.Load<LookPreset>($"{Folder}/{f}")).ToArray();
}
