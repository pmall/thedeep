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

    /// <summary>The layer's accent hue for emissive decor (faith glyphs).</summary>
    [Export] public Color Accent { get; set; } = new(0.8f, 0.3f, 0.8f);

    /// <summary>Corrosion on metal (creases, bottoms, the rust material). Keep it muted: color means gameplay.</summary>
    [Export] public Color Rust { get; set; } = new(0.42f, 0.3f, 0.26f);

    /// <summary>Silt or moss settled in rock crevices and on ledges.</summary>
    [Export] public Color Silt { get; set; } = new(0.6f, 0.6f, 0.62f);

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

    /// <summary>Color of the room's light shafts (spot lights seen in the volumetric fog); energy stays per shaft.</summary>
    [Export] public Color ShaftColor { get; set; } = Colors.White;

    /// <summary>How brightly the key light lights the volumetric fog (its haze everywhere), against the shafts.</summary>
    [Export] public float KeyFogEnergy { get; set; } = 0.5f;

    [ExportGroup("Fog")]
    [Export] public Color FogColor { get; set; } = new(0.5f, 0.5f, 0.6f);
    [Export] public float FogDensity { get; set; } = 1f;

    /// <summary>Distance from the camera where depth fog starts and where it is full.</summary>
    [Export] public float FogDepthBegin { get; set; } = 24f;
    [Export] public float FogDepthEnd { get; set; } = 75f;

    /// <summary>Density of the volumetric fog: the haze that light shafts and lamps are seen in.</summary>
    [Export] public float VolumetricDensity { get; set; } = 0.02f;

    /// <summary>Color the volumetric fog scatters light with.</summary>
    [Export] public Color VolumetricAlbedo { get; set; } = Colors.White;

    /// <summary>Light the volumetric fog gives off by itself, so unlit haze is never black.</summary>
    [Export] public Color VolumetricEmission { get; set; } = Colors.Black;

    [Export] public float VolumetricEmissionEnergy { get; set; } = 1f;

    /// <summary>Forward scattering: above 0, shafts glow brighter when the camera looks into the light.</summary>
    [Export] public float VolumetricAnisotropy { get; set; } = 0.2f;

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

    /// <summary>How much the screen's edges darken (0 none, 1 full), so the bright stage sits in a dark frame.</summary>
    [ExportGroup("Post")]
    [Export] public float Vignette { get; set; } = 0.5f;

    /// <summary>What the edges darken toward (multiplied in); a tint, not black, keeps the frame in the palette.</summary>
    [Export] public Color VignetteColor { get; set; } = new(0.2f, 0.2f, 0.25f);

    /// <summary>Film grain strength (0 none; around 0.03 is subtle).</summary>
    [Export] public float Grain { get; set; } = 0.03f;

    /// <summary>Tint of ambient particles (dust, silt, drips, bubbles; later embers or ice).</summary>
    [ExportGroup("Particles")]
    [Export] public Color ParticleTint { get; set; } = Colors.White;

    /// <summary>Loads res://resources/biomes/<paramref name="name"/>.tres, or null if there is none.</summary>
    public static BiomeProfile? Load(string name)
    {
        string path = $"{Folder}/{name}.tres";
        return ResourceLoader.Exists(path) ? ResourceLoader.Load<BiomeProfile>(path) : null;
    }

    /// <summary>
    /// Re-themes a room: sets the palette globals, then the environment, key light, lamps and light shafts.
    /// Only colors, energies and grading change; effects (SSAO, glow on/off…) stay as the room set them.
    /// </summary>
    public void Apply(Environment environment, DirectionalLight3D keyLight, Light3D[] lamps, Light3D[] shafts)
    {
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeRock, Rock);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeMetal, Metal);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeBackdrop, Backdrop);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeBackdropAccent, BackdropAccent);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeForeground, Foreground);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeAccent, Accent);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeParticle, ParticleTint);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeRust, Rust);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeSilt, Silt);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.PostVignette, Vignette);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.PostVignetteColor, VignetteColor);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.PostGrain, Grain);

        environment.BackgroundColor = Background;
        environment.AmbientLightColor = AmbientColor;
        environment.AmbientLightEnergy = AmbientEnergy;
        environment.FogLightColor = FogColor;
        environment.FogDensity = FogDensity;
        environment.FogDepthBegin = FogDepthBegin;
        environment.FogDepthEnd = FogDepthEnd;
        environment.VolumetricFogDensity = VolumetricDensity;
        environment.VolumetricFogAlbedo = VolumetricAlbedo;
        environment.VolumetricFogEmission = VolumetricEmission;
        environment.VolumetricFogEmissionEnergy = VolumetricEmissionEnergy;
        environment.VolumetricFogAnisotropy = VolumetricAnisotropy;
        environment.TonemapExposure = Exposure;
        environment.AdjustmentEnabled = true;
        environment.AdjustmentBrightness = Brightness;
        environment.AdjustmentContrast = Contrast;
        environment.AdjustmentSaturation = Saturation;
        environment.AdjustmentColorCorrection = Lift == Colors.Black && Gain == Colors.White ? null : GradeLookup();
        environment.GlowIntensity = GlowIntensity;

        keyLight.LightColor = KeyColor;
        keyLight.LightEnergy = KeyEnergy;
        keyLight.LightVolumetricFogEnergy = KeyFogEnergy;
        foreach (Light3D lamp in lamps)
        {
            lamp.LightColor = LampColor;
        }

        foreach (Light3D shaft in shafts)
        {
            shaft.LightColor = ShaftColor;
        }
    }

    /// <summary>A 1D lookup from <see cref="Lift"/> to <see cref="Gain"/>: a per-channel lift/gain grade.</summary>
    private GradientTexture1D GradeLookup() => new()
    {
        Width = 256,
        Gradient = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { Lift, Gain } },
    };
}
