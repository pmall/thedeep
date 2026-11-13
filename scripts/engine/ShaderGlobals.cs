using Godot;

namespace TheDeep;

/// <summary>
/// Global shader uniform names declared in project.godot ([shader_globals]). Use these instead of
/// string literals. <see cref="BiomeProfile"/> sets the biome and vignette ones for the current room,
/// <see cref="WaterBody"/> the water ones.
/// </summary>
public static class ShaderGlobals
{
    /// <summary>The biome's rock color (<see cref="BiomeProfile.Rock"/>).</summary>
    public static readonly StringName BiomeRock = "biome_rock";
    /// <summary>The biome's metal color (<see cref="BiomeProfile.Metal"/>).</summary>
    public static readonly StringName BiomeMetal = "biome_metal";
    /// <summary>The color of the biome's backdrop silhouettes (<see cref="BiomeProfile.Backdrop"/>).</summary>
    public static readonly StringName BiomeBackdrop = "biome_backdrop";
    /// <summary>The color of the few backdrop pieces carrying the decor accent
    /// (<see cref="BiomeProfile.BackdropAccent"/>).</summary>
    public static readonly StringName BiomeBackdropAccent = "biome_backdrop_accent";
    /// <summary>The color of the foreground silhouettes framing the screen (<see cref="BiomeProfile.Foreground"/>).</summary>
    public static readonly StringName BiomeForeground = "biome_foreground";
    /// <summary>The biome's decor accent, for emissive decor, never a hazard (<see cref="BiomeProfile.Accent"/>).</summary>
    public static readonly StringName BiomeAccent = "biome_accent";
    /// <summary>The tint of the room's ambient particles (<see cref="BiomeProfile.ParticleTint"/>).</summary>
    public static readonly StringName BiomeParticle = "biome_particle";
    /// <summary>The color of corrosion on metal (<see cref="BiomeProfile.Rust"/>).</summary>
    public static readonly StringName BiomeRust = "biome_rust";
    /// <summary>The color of what settles in rock crevices and on ledges (<see cref="BiomeProfile.Silt"/>).</summary>
    public static readonly StringName BiomeSilt = "biome_silt";
    /// <summary>How much rime rock and metal carry, 0–1 (<see cref="BiomeProfile.Frost"/>).</summary>
    public static readonly StringName BiomeFrost = "biome_frost";
    /// <summary>How much of the biome's rock is cut stone, 0–1 (<see cref="BiomeProfile.Masonry"/>).</summary>
    public static readonly StringName BiomeMasonry = "biome_masonry";
    /// <summary>The rime's color (<see cref="BiomeProfile.FrostColor"/>).</summary>
    public static readonly StringName BiomeFrostColor = "biome_frost_color";
    /// <summary>What the rime turns to in the shade of crevices (<see cref="BiomeProfile.FrostShade"/>).</summary>
    public static readonly StringName BiomeFrostShade = "biome_frost_shade";
    /// <summary>The ice's body seen straight on (<see cref="BiomeProfile.IceTint"/>).</summary>
    public static readonly StringName BiomeIce = "biome_ice";
    /// <summary>The dull red the hazards' heat cools to (<see cref="BiomeProfile.MoltenCool"/>).</summary>
    public static readonly StringName BiomeMoltenCool = "biome_molten_cool";
    /// <summary>The hazards' own glow (<see cref="BiomeProfile.Molten"/>).</summary>
    public static readonly StringName BiomeMolten = "biome_molten";
    /// <summary>The hazards' white-hot (<see cref="BiomeProfile.MoltenHot"/>).</summary>
    public static readonly StringName BiomeMoltenHot = "biome_molten_hot";
    /// <summary>How far the faith glyphs take the accent toward white (<see cref="BiomeProfile.GlyphWhiten"/>, its one source).</summary>
    public static readonly StringName GlyphWhiten = "glyph_whiten";

    /// <summary>The baked frost set (<see cref="FrostSurface"/>) every material that frosts samples; project.godot sets it once.</summary>
    public static readonly StringName FrostSurfaceMap = "frost_surface";

    /// <summary>The frost's normal map, sampled by every material that frosts; project.godot sets it once.</summary>
    public static readonly StringName FrostNormalMap = "frost_normal";
    /// <summary>The far distance's color, which backdrop skylines sink into (<see cref="BiomeProfile.HazeColor"/>).</summary>
    public static readonly StringName BiomeHaze = "biome_haze";
    /// <summary>The color of the room's light shafts (<see cref="BiomeProfile.ShaftColor"/>).</summary>
    public static readonly StringName BiomeShaft = "biome_shaft";
    /// <summary>The water's tint near its surface (<see cref="BiomeProfile.WaterTint"/>).</summary>
    public static readonly StringName BiomeWater = "biome_water";
    /// <summary>The water's color with depth (<see cref="BiomeProfile.WaterDeep"/>).</summary>
    public static readonly StringName BiomeWaterDeep = "biome_water_deep";
    /// <summary>The color of the caustic light under and just above water (<see cref="BiomeProfile.CausticColor"/>).</summary>
    public static readonly StringName BiomeCaustic = "biome_caustic";
    /// <summary>How strong the caustic light is, 0 for none (<see cref="BiomeProfile.CausticStrength"/>).</summary>
    public static readonly StringName BiomeCausticStrength = "biome_caustic_strength";
    /// <summary>The height (world Y, meters) of the room's water surface; far below everything when the room has no
    /// water.</summary>
    public static readonly StringName WaterLevel = "water_level";
    /// <summary>The height (world Y, meters) of the water's bed: its surface less its depth.</summary>
    public static readonly StringName WaterFloor = "water_floor";
    /// <summary>How thick the room's water is (<see cref="WaterBody.Murk"/>).</summary>
    public static readonly StringName WaterMurk = "water_murk";
    /// <summary>The room's water seen from above, in meters: min X, min Z, max X, max Z.</summary>
    public static readonly StringName WaterArea = "water_area";
    /// <summary>How much the screen's edges darken, 0–1 (<see cref="BiomeProfile.Vignette"/>).</summary>
    public static readonly StringName Vignette = "vignette";
    /// <summary>What the screen's edges darken toward (<see cref="BiomeProfile.VignetteColor"/>).</summary>
    public static readonly StringName VignetteColor = "vignette_color";
}
