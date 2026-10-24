using Godot;

namespace TheDeep;

/// <summary>
/// The look of one depth layer (Flooded, Frozen, Volcanic, Core): palette, light, fog, grading and
/// particle tint. Rooms reference one (<see cref="Room.Biome"/>); swapping it re-themes the room
/// without touching geometry. The palette reaches materials through global shader uniforms
/// (<see cref="ShaderGlobals"/>), so any material can follow the biome.
/// </summary>
[GlobalClass]
public partial class BiomeProfile : Resource
{
    /// <summary>Where profiles live: res://resources/biomes/&lt;name&gt;.tres.</summary>
    public const string Folder = "res://resources/biomes";

    [Export] public string DisplayName { get; set; } = "";

    /// <summary>Rock surfaces on the play plane (not grippable). Must read apart from metal at a glance.</summary>
    [ExportGroup("Palette")]
    [Export] public Color Rock { get; set; } = new(0.5f, 0.5f, 0.5f);

    /// <summary>Metal surfaces on the play plane (mag-grip).</summary>
    [Export] public Color Metal { get; set; } = new(0.6f, 0.65f, 0.7f);

    /// <summary>Backdrop silhouettes behind the play plane (before fog).</summary>
    [Export] public Color Backdrop { get; set; } = new(0.3f, 0.3f, 0.3f);

    /// <summary>The few backdrop pieces carrying the decor accent.</summary>
    [Export] public Color BackdropAccent { get; set; } = new(0.5f, 0.2f, 0.5f);

    /// <summary>Foreground silhouettes framing the screen.</summary>
    [Export] public Color Foreground { get; set; } = new(0.03f, 0.03f, 0.04f);

    /// <summary>The layer's accent hue for emissive decor (glyphs, later).</summary>
    [Export] public Color Accent { get; set; } = new(0.8f, 0.3f, 0.8f);

    /// <summary>The void behind everything.</summary>
    [ExportGroup("Light")]
    [Export] public Color Background { get; set; } = new(0.05f, 0.05f, 0.07f);

    [Export] public Color AmbientColor { get; set; } = new(0.5f, 0.5f, 0.6f);
    [Export] public float AmbientEnergy { get; set; } = 0.5f;

    /// <summary>The room's key light (its DirectionalLight3D).</summary>
    [Export] public Color KeyColor { get; set; } = Colors.White;
    [Export] public float KeyEnergy { get; set; } = 1f;

    /// <summary>Color of the room's lamps; their energy and range stay per lamp.</summary>
    [Export] public Color LampColor { get; set; } = Colors.White;

    [ExportGroup("Fog")]
    [Export] public Color FogColor { get; set; } = new(0.5f, 0.5f, 0.6f);
    [Export] public float FogDensity { get; set; } = 1f;

    /// <summary>Distance from the camera where depth fog starts and where it is full.</summary>
    [Export] public float FogDepthBegin { get; set; } = 24f;
    [Export] public float FogDepthEnd { get; set; } = 75f;

    [ExportGroup("Grading")]
    [Export] public float Exposure { get; set; } = 1f;
    [Export] public float Brightness { get; set; } = 1f;
    [Export] public float Contrast { get; set; } = 1f;
    [Export] public float Saturation { get; set; } = 1f;

    /// <summary>What black becomes after grading (per channel); tints the shadows.</summary>
    [Export] public Color Lift { get; set; } = Colors.Black;

    /// <summary>What white becomes after grading (per channel); tints the highlights.</summary>
    [Export] public Color Gain { get; set; } = Colors.White;

    [Export] public float GlowIntensity { get; set; } = 0.6f;

    /// <summary>Tint of ambient particles (dust, silt, embers), used from roadmap M3.</summary>
    [ExportGroup("Particles")]
    [Export] public Color ParticleTint { get; set; } = Colors.White;

    /// <summary>Loads res://resources/biomes/<paramref name="name"/>.tres, or null if there is none.</summary>
    public static BiomeProfile? Load(string name)
    {
        string path = $"{Folder}/{name}.tres";
        return ResourceLoader.Exists(path) ? ResourceLoader.Load<BiomeProfile>(path) : null;
    }

    /// <summary>
    /// Re-themes a room: sets the palette globals, then the environment, key light and lamps.
    /// Only colors, energies and grading change; effects (SSAO, glow on/off…) stay as the room set them.
    /// </summary>
    public void Apply(Environment environment, DirectionalLight3D keyLight, Light3D[] lamps)
    {
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeRock, Rock);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeMetal, Metal);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeBackdrop, Backdrop);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeBackdropAccent, BackdropAccent);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeForeground, Foreground);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeAccent, Accent);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeParticle, ParticleTint);

        environment.BackgroundColor = Background;
        environment.AmbientLightColor = AmbientColor;
        environment.AmbientLightEnergy = AmbientEnergy;
        environment.FogLightColor = FogColor;
        environment.FogDensity = FogDensity;
        environment.FogDepthBegin = FogDepthBegin;
        environment.FogDepthEnd = FogDepthEnd;
        environment.TonemapExposure = Exposure;
        environment.AdjustmentEnabled = true;
        environment.AdjustmentBrightness = Brightness;
        environment.AdjustmentContrast = Contrast;
        environment.AdjustmentSaturation = Saturation;
        environment.AdjustmentColorCorrection = Lift == Colors.Black && Gain == Colors.White ? null : GradeLookup();
        environment.GlowIntensity = GlowIntensity;

        keyLight.LightColor = KeyColor;
        keyLight.LightEnergy = KeyEnergy;
        foreach (Light3D lamp in lamps)
        {
            lamp.LightColor = LampColor;
        }
    }

    /// <summary>A 1D lookup from <see cref="Lift"/> to <see cref="Gain"/>: a per-channel lift/gain grade.</summary>
    private GradientTexture1D GradeLookup() => new()
    {
        Width = 256,
        Gradient = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { Lift, Gain } },
    };
}
