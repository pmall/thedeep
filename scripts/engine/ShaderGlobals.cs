using Godot;

namespace TheDeep;

/// <summary>
/// Global shader uniform names declared in project.godot ([shader_globals]). Use these instead of
/// string literals. <see cref="BiomeProfile"/> sets the biome and vignette ones for the current room,
/// <see cref="WaterBody"/> the water ones.
/// </summary>
public static class ShaderGlobals
{
    public static readonly StringName BiomeRock = "biome_rock";
    public static readonly StringName BiomeMetal = "biome_metal";
    public static readonly StringName BiomeBackdrop = "biome_backdrop";
    public static readonly StringName BiomeBackdropAccent = "biome_backdrop_accent";
    public static readonly StringName BiomeForeground = "biome_foreground";
    public static readonly StringName BiomeAccent = "biome_accent";
    public static readonly StringName BiomeParticle = "biome_particle";
    public static readonly StringName BiomeRust = "biome_rust";
    public static readonly StringName BiomeSilt = "biome_silt";
    public static readonly StringName BiomeFrost = "biome_frost";
    public static readonly StringName BiomeMasonry = "biome_masonry";
    public static readonly StringName BiomeFrostColor = "biome_frost_color";

    /// <summary>The baked frost set (<see cref="FrostSurface"/>) every material that frosts samples; project.godot sets it once.</summary>
    public static readonly StringName FrostSurfaceMap = "frost_surface";

    public static readonly StringName FrostNormalMap = "frost_normal";
    public static readonly StringName BiomeHaze = "biome_haze";
    public static readonly StringName BiomeShaft = "biome_shaft";
    public static readonly StringName BiomeWater = "biome_water";
    public static readonly StringName BiomeWaterDeep = "biome_water_deep";
    public static readonly StringName BiomeCaustic = "biome_caustic";
    public static readonly StringName BiomeCausticStrength = "biome_caustic_strength";
    public static readonly StringName WaterLevel = "water_level";
    public static readonly StringName WaterFloor = "water_floor";
    public static readonly StringName WaterArea = "water_area";
    public static readonly StringName Vignette = "vignette";
    public static readonly StringName VignetteColor = "vignette_color";
}
