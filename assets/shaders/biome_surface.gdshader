// Placeholder surface whose color comes from the current biome palette: BiomeProfile pushes the
// palette into global uniforms (lib/palette, declared in project.godot [shader_globals]).
// Still used by the foreground silhouettes; the material library reads the same palette.
shader_type spatial;

#include "res://assets/shaders/lib/palette.gdshaderinc"

uniform int slot : hint_enum("Rock", "Metal", "Backdrop", "Backdrop accent", "Foreground") = 0;
uniform float metallic : hint_range(0.0, 1.0) = 0.0;
uniform float roughness : hint_range(0.0, 1.0) = 1.0;

void fragment() {
	ALBEDO = palette_color(slot);
	METALLIC = metallic;
	ROUGHNESS = roughness;
}
