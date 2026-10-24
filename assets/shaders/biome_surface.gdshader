// Placeholder surface whose color comes from the current biome palette: BiomeProfile pushes the
// palette into the global uniforms below (declared in project.godot [shader_globals]).
// Roadmap M1 replaces this with the real material library, which reads the same palette.
shader_type spatial;

global uniform vec4 biome_rock;
global uniform vec4 biome_metal;
global uniform vec4 biome_backdrop;
global uniform vec4 biome_backdrop_accent;
global uniform vec4 biome_foreground;

uniform int slot : hint_enum("Rock", "Metal", "Backdrop", "Backdrop accent", "Foreground") = 0;
uniform float metallic : hint_range(0.0, 1.0) = 0.0;
uniform float roughness : hint_range(0.0, 1.0) = 1.0;

// Palette colors are written in sRGB, like any color picker; global uniforms arrive unconverted.
vec3 srgb_to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));
}

void fragment() {
	vec4 color = biome_rock;
	if (slot == 1) {
		color = biome_metal;
	} else if (slot == 2) {
		color = biome_backdrop;
	} else if (slot == 3) {
		color = biome_backdrop_accent;
	} else if (slot == 4) {
		color = biome_foreground;
	}
	ALBEDO = srgb_to_linear(color.rgb);
	METALLIC = metallic;
	ROUGHNESS = roughness;
}
