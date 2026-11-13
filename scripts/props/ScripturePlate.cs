using Godot;

namespace TheDeep;

/// <summary>
/// A plate of scripture on a lectern, copied by the machines: standing at it offers its prompt (an <see cref="ActionPrompt"/>,
/// "Read"), and reading (interact) shows its words in the banner (<see cref="Session"/>). Lore, nothing more. The node is where the
/// pilgrim stands to read, on the floor in front of the lectern. Its words are lines in the line book. Its glyphs' light swells to
/// <see cref="Glow"/> times its own while its words are on screen (<see cref="Banner"/>), and settles back once they have gone. Its
/// look is the scene's materials, or a placement's own (<see cref="PostMaterial"/>, <see cref="PlateMaterial"/>,
/// <see cref="GlyphMaterial"/>), so a world or a biome dresses it without touching it.
/// </summary>
public partial class ScripturePlate : Node3D, IAnnounces
{
    [Export] public OmniLight3D Light { get; set; } = null!;
    [Export] public ActionPrompt Prompt { get; set; } = null!;

    /// <summary>The lectern's post, its plate and the glyphs on it.</summary>
    [Export] public MeshInstance3D Post { get; set; } = null!;
    [Export] public MeshInstance3D Plate { get; set; } = null!;
    [Export] public MeshInstance3D Glyphs { get; set; } = null!;

    /// <summary>Who wrote it (the banner's title) and what it says.</summary>
    [Export] public StringName TitleLine { get; set; } = "";
    [Export] public StringName TextLine { get; set; } = "";

    /// <summary>Optional looks replacing the scene's: the post's, the plate's, the glyphs'.</summary>
    [ExportGroup("Look")]
    [Export] public Material? PostMaterial { get; set; }

    [Export] public Material? PlateMaterial { get; set; }

    [Export] public Material? GlyphMaterial { get; set; }

    /// <summary>How many times its own the glyphs' light stands while its words are on screen.</summary>
    [ExportGroup("Glow")]
    [Export] public float Glow { get; set; } = 3f;

    /// <summary>Seconds for the glow to swell as its words come, and to settle once they have gone.</summary>
    [Export] public float GlowRise { get; set; } = 0.4f;

    [Export] public float GlowFade { get; set; } = 1.5f;

    /// <inheritdoc/>
    public System.Collections.Generic.IEnumerable<StringName> LineIds => new[] { TitleLine, TextLine };

    /// <summary>What the banner says as it is read.</summary>
    public Announcement Said => new(TitleLine, TextLine);

    /// <inheritdoc/>
    public System.Collections.Generic.IEnumerable<Announcement> Announcements => new[] { Said };

    /// <summary>The banner its words show in, set by the session when the room loads; none: it never glows.</summary>
    public Banner? Banner { get; set; }

    /// <summary>How far the glow has swelled, 0–1.</summary>
    public float Glowing => _glow;

    private float _baseEnergy;
    private float _glow;

    public override void _Ready()
    {
        _baseEnergy = Light.LightEnergy;
        Post.MaterialOverride = PostMaterial ?? Post.MaterialOverride;
        Plate.MaterialOverride = PlateMaterial ?? Plate.MaterialOverride;
        Glyphs.MaterialOverride = GlyphMaterial ?? Glyphs.MaterialOverride;
    }

    public override void _Process(double delta)
    {
        bool read = Banner is { } banner && banner.Current == Said;
        _glow = Motion.Approach(_glow, read ? 1f : 0f, (float)delta / Mathf.Max(0.01f, read ? GlowRise : GlowFade));
        Light.LightEnergy = _baseEnergy * (1f + (Glow - 1f) * _glow);
    }
}
