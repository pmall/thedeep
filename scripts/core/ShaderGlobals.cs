using Godot;

namespace TheDeep;

/// <summary>
/// Global shader uniform names declared in project.godot ([shader_globals]). Use these instead of
/// string literals. <see cref="BiomeProfile"/> sets the palette ones for the current room.
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
}
