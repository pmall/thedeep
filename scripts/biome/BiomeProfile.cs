using Godot;

namespace TheDeep;

/// <summary>
/// The look of one depth layer (Flooded, Frozen, Volcanic, Core): palette, light, fog, grading and
/// particle tint. Rooms reference one (<see cref="Room.Biome"/>); swapping it re-themes the room
/// without touching geometry. The palette reaches materials through global shader uniforms
/// (<see cref="ShaderGlobals"/>), so any material can follow the biome. The room check holds every profile of a world to
/// <see cref="BiomeRules"/> (<see cref="Palette"/>).
/// </summary>
[GlobalClass]
public partial class BiomeProfile : Resource
{
    /// <summary>Where profiles live: res://resources/biomes/&lt;name&gt;.tres.</summary>
    public const string Folder = "res://resources/biomes";

    [Export] public string DisplayName { get; set; } = "";

    /// <summary>Rock surfaces on the play plane (not grippable): darker than <see cref="Metal"/> by <see cref="BiomeRules.MetalOverRock"/> in value at least, so the two read apart at a glance.</summary>
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

    /// <summary>
    /// The layer's decor accent: emissive decor (faith glyphs, glow strips) and the few backdrop pieces that carry it. Never a
    /// hazard's color, which is <see cref="Molten"/>'s (color means gameplay, `docs/art-direction.md#ambiance`).
    /// </summary>
    [Export] public Color Accent { get; set; } = new(0.8f, 0.3f, 0.8f);

    /// <summary>Corrosion on metal (creases, bottoms, the rust material). Muted (<see cref="BiomeRules.RustChroma"/>): color means gameplay.</summary>
    [Export] public Color Rust { get; set; } = new(0.42f, 0.3f, 0.26f);

    /// <summary>Silt or moss settled in rock crevices and on ledges.</summary>
    [Export] public Color Silt { get; set; } = new(0.6f, 0.6f, 0.62f);

    /// <summary>
    /// Rime on rock and metal (lib/frost.gdshaderinc): 0 none, 1 caps on every ledge and hoarfrost in the crevices.
    /// Metal takes a smaller share, so grippable metal stays readable.
    /// </summary>
    [Export(PropertyHint.Range, "0,1")] public float Frost { get; set; }

    /// <summary>
    /// How much of the layer's rock was built (rock.gdshader): 0 natural strata, 1 courses of cut stone (a cathedral's walls,
    /// voussoirs round its arches, flagstones underfoot). Still matte rock: it never reads as metal.
    /// </summary>
    [Export(PropertyHint.Range, "0,1")] public float Masonry { get; set; }

    /// <summary>The rime's color.</summary>
    [Export] public Color FrostColor { get; set; } = new(0.88f, 0.92f, 0.98f);

    /// <summary>What the rime turns to in the shade of crevices and creases, multiplied into <see cref="FrostColor"/> (white keeps it).</summary>
    [Export] public Color FrostShade { get; set; } = new(0.9163f, 0.9547f, 1f);

    /// <summary>The ice's body seen straight on, multiplied into <see cref="FrostColor"/> before it sinks into <see cref="WaterDeep"/> (ice.gdshader).</summary>
    [Export] public Color IceTint { get; set; } = new(0.68f, 0.7484f, 0.8434f);

    /// <summary>
    /// The hazards' heat (slag, magma, heat columns: `docs/art-direction.md#volcanic-look`), coolest to hottest: the dull red
    /// it cools to (<see cref="MoltenCool"/>), its own glow and the color of the light it casts (<see cref="Molten"/>), and
    /// white-hot (<see cref="MoltenHot"/>). Apart from the decor <see cref="Accent"/>, and the most saturated color of the
    /// layer's light, so a hazard reads as one at a glance on any layer (<see cref="BiomeRules"/>).
    /// </summary>
    [ExportGroup("Molten")]
    [Export] public Color MoltenCool { get; set; } = new(0.5f, 0.06f, 0.02f);

    [Export] public Color Molten { get; set; } = new(1f, 0.4f, 0.08f);

    [Export] public Color MoltenHot { get; set; } = new(1f, 0.85f, 0.55f);

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

    /// <summary>Color of the room's light shafts and of their beams; energy stays per shaft.</summary>
    [Export] public Color ShaftColor { get; set; } = Colors.White;

    /// <summary>
    /// How much of each shaft's light reaches what it falls on, against its beam (1 all). Under 1 where the layer's surfaces
    /// are pale: rime under a shaft otherwise burns to a flat white pool, while the beam in the air keeps its brightness.
    /// </summary>
    [Export(PropertyHint.Range, "0,1")] public float ShaftLight { get; set; } = 1f;

    /// <summary>The murk's color: what depth fog and mist veil things in (the Environment's fog light color).</summary>
    [ExportGroup("Fog")]
    [Export] public Color FogColor { get; set; } = new(0.5f, 0.5f, 0.6f);

    /// <summary>The most depth fog, however far (1 hides what is far entirely).</summary>
    [Export] public float FogDensity { get; set; } = 1f;

    /// <summary>
    /// Depth behind the play plane (meters) where depth fog starts and where it is full, however far the camera stands
    /// (<see cref="ApplyDepthFog"/>): the play plane stays clear and all that stands behind it sinks into the murk, in the
    /// gameplay framing and in a wide shot alike. A start just short of 0 lets the fog touch the play plane's own faces.
    /// </summary>
    [Export] public float FogDepthBegin { get; set; } = 2f;
    [Export] public float FogDepthEnd { get; set; } = 53f;

    /// <summary>How depth fog grows from its start to its end: under 1 it thickens soon behind the start (murk right behind the play plane), over 1 late.</summary>
    [Export(PropertyHint.Range, "0.1,4")] public float FogDepthCurve { get; set; } = 1f;

    /// <summary>Mist pooling low in a room: meters above the bottom of the room's camera bounds where it begins.</summary>
    [Export] public float MistTop { get; set; }

    /// <summary>How fast the mist thickens below <see cref="MistTop"/>, per meter (the Environment's height fog density); 0 for none.</summary>
    [Export(PropertyHint.Range, "0,0.5")] public float MistDensity { get; set; }

    /// <summary>The far distance's color: backdrop skylines sink into it (they do their own haze).</summary>
    [Export] public Color HazeColor { get; set; } = new(0.5f, 0.5f, 0.6f);

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

    /// <summary>The water's tint: what its surface takes where it reflects, and its section's color near the surface.</summary>
    [ExportGroup("Water")]
    [Export] public Color WaterTint { get; set; } = new(0.3f, 0.32f, 0.4f);

    /// <summary>The water's own color with depth: what things under the surface sink into.</summary>
    [Export] public Color WaterDeep { get; set; } = new(0.05f, 0.07f, 0.1f);

    /// <summary>Color of the caustic light under and just above water.</summary>
    [Export] public Color CausticColor { get; set; } = Colors.White;

    /// <summary>Caustic light strength (0 none).</summary>
    [Export] public float CausticStrength { get; set; } = 0.5f;

    /// <summary>How much the screen's edges darken (0 none, 1 full), so the bright stage sits in a dark frame (vignette.gdshader).</summary>
    [ExportGroup("Vignette")]
    [Export(PropertyHint.Range, "0,1")] public float Vignette { get; set; }

    /// <summary>What the edges darken toward (multiplied in); a tint, not black, keeps the frame in the palette.</summary>
    [Export] public Color VignetteColor { get; set; } = new(0.2f, 0.2f, 0.25f);

    /// <summary>Tint of ambient particles (dust, silt, drips, bubbles; later embers or ice).</summary>
    [ExportGroup("Particles")]
    [Export] public Color ParticleTint { get; set; } = Colors.White;

    /// <summary>
    /// Light rising from below and in front (the glow of magma, of the heart), shadowless: it lights undersides and the
    /// walls behind the play plane that the key light leaves black. Energy 0 for none.
    /// </summary>
    [ExportGroup("Light")]
    [Export] public Color UnderglowColor { get; set; } = new(1f, 0.5f, 0.2f);

    [Export] public float UnderglowEnergy { get; set; }

    /// <summary>The architecture standing in the haze behind the layer's rooms (<see cref="BackdropArchitecture"/>).</summary>
    [ExportGroup("Backdrop")]
    [Export] public BackdropStyle Architecture { get; set; }

    /// <summary>The far skyline and the nearer one (backdrop_silhouette materials); null shows none.</summary>
    [Export] public Material? FarSkyline { get; set; }

    [Export] public Material? NearSkyline { get; set; }

    /// <summary>Loads res://resources/biomes/<paramref name="name"/>.tres, or null if there is none.</summary>
    public static BiomeProfile? Load(string name)
    {
        string path = $"{Folder}/{name}.tres";
        return ResourceLoader.Exists(path) ? ResourceLoader.Load<BiomeProfile>(path) : null;
    }

    /// <summary>
    /// Re-themes a room whose camera bounds bottom out at <paramref name="floorY"/>, seen from
    /// <paramref name="cameraDistance"/> in front of the play plane: sets the palette and vignette globals, then the
    /// environment, key light, lamps, light shafts and glyph lights.
    /// Only colors, energies, fog and grading change; effects (SSAO, glow on/off…) stay as the room set them.
    /// </summary>
    public void Apply(Environment environment, float floorY, float cameraDistance, DirectionalLight3D keyLight, Light3D[] lamps, Light3D[] shafts, Light3D[] glyphLights)
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
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeFrost, Frost);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeMasonry, Masonry);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeFrostColor, FrostColor);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeFrostShade, FrostShade);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeIce, IceTint);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeMoltenCool, MoltenCool);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeMolten, Molten);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeMoltenHot, MoltenHot);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.GlyphWhiten, GlyphWhiten);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeHaze, HazeColor);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeShaft, ShaftColor);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeWater, WaterTint);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeWaterDeep, WaterDeep);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeCaustic, CausticColor);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.BiomeCausticStrength, CausticStrength);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.Vignette, Vignette);
        RenderingServer.GlobalShaderParameterSet(ShaderGlobals.VignetteColor, VignetteColor);

        environment.BackgroundColor = Background;
        environment.AmbientLightColor = AmbientColor;
        environment.AmbientLightEnergy = AmbientEnergy;
        environment.FogLightColor = FogColor;
        environment.FogDensity = FogDensity;
        environment.FogDepthCurve = FogDepthCurve;
        ApplyDepthFog(environment, cameraDistance);
        environment.FogHeight = floorY + MistTop;
        environment.FogHeightDensity = MistDensity;
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

        Color shaftLight = (ShaftColor.SrgbToLinear() * ShaftLight).LinearToSrgb();
        foreach (Light3D shaft in shafts)
        {
            shaft.LightColor = shaftLight with { A = 1f };
        }

        foreach (Light3D glyphLight in glyphLights)
        {
            glyphLight.LightColor = GlyphLightColor;
        }
    }

    /// <summary>
    /// Places the depth fog behind the play plane for a camera <paramref name="cameraDistance"/> meters in front of it
    /// (the Environment measures fog from the camera). Sets only what changed, so calling it every frame costs nothing.
    /// </summary>
    public void ApplyDepthFog(Environment environment, float cameraDistance)
    {
        float begin = cameraDistance + FogDepthBegin;
        float end = cameraDistance + FogDepthEnd;
        if (environment.FogDepthBegin != begin)
        {
            environment.FogDepthBegin = begin;
        }

        if (environment.FogDepthEnd != end)
        {
            environment.FogDepthEnd = end;
        }
    }

    /// <summary>
    /// How far the faith glyphs take the accent toward white, so it never saturates decor (color means gameplay); the glyph
    /// shader reads it as a global (<see cref="ShaderGlobals.GlyphWhiten"/>), their lights through <see cref="GlyphLightColor"/>.
    /// </summary>
    public const float GlyphWhiten = 0.35f;

    /// <summary>The faith glyphs' light: the accent toward white, as the glyph shader draws it.</summary>
    public Color GlyphLightColor => Accent.Lerp(Colors.White, GlyphWhiten);

    /// <summary>What <see cref="BiomeRules"/> reads of this profile.</summary>
    public BiomePalette Palette() => new()
    {
        Rock = Rock.ToCore(),
        Metal = Metal.ToCore(),
        Rust = Rust.ToCore(),
        MoltenCool = MoltenCool.ToCore(),
        Molten = Molten.ToCore(),
        MoltenHot = MoltenHot.ToCore(),
        Light =
        [
            (nameof(AmbientColor), AmbientColor.ToCore()),
            (nameof(KeyColor), KeyColor.ToCore()),
            (nameof(LampColor), LampColor.ToCore()),
            (nameof(ShaftColor), ShaftColor.ToCore()),
            (nameof(FogColor), FogColor.ToCore()),
            (nameof(HazeColor), HazeColor.ToCore()),
            (nameof(Background), Background.ToCore()),
            (nameof(ParticleTint), ParticleTint.ToCore()),
            (nameof(CausticColor), CausticColor.ToCore()),
        ],
        Shares = [(nameof(Frost), Frost), (nameof(Masonry), Masonry), (nameof(ShaftLight), ShaftLight), (nameof(Vignette), Vignette)],
    };

    /// <summary>A 1D lookup from <see cref="Lift"/> to <see cref="Gain"/>: a per-channel lift/gain grade.</summary>
    private GradientTexture1D GradeLookup() => new()
    {
        Width = 256,
        Gradient = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { Lift, Gain } },
    };
}
