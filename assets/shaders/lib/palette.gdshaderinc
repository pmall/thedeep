// The biome palette, pushed by BiomeProfile.Apply into global uniforms (project.godot [shader_globals],
// names in ShaderGlobals.cs). Materials take their colors from here, so they follow the room's biome.

global uniform vec4 biome_rock;
global uniform vec4 biome_metal;
global uniform vec4 biome_backdrop;
global uniform vec4 biome_backdrop_accent;
global uniform vec4 biome_foreground;
global uniform vec4 biome_accent;
global uniform vec4 biome_rust;
global uniform vec4 biome_silt;

// Palette colors are written in sRGB, like any color picker; global uniforms arrive unconverted.
vec3 srgb_to_linear(vec3 c) {
	return mix(c / 12.92, pow((c + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c));
}

// The base color of a palette slot, linear. Slot order matches the materials' `slot` hint_enum
// (slots 5–7 only for materials that list them, like the machines').
vec3 palette_color(int slot) {
	vec4 color = biome_rock;
	if (slot == 1) {
		color = biome_metal;
	} else if (slot == 2) {
		color = biome_backdrop;
	} else if (slot == 3) {
		color = biome_backdrop_accent;
	} else if (slot == 4) {
		color = biome_foreground;
	} else if (slot == 5) {
		color = biome_rust;
	} else if (slot == 6) {
		color = biome_silt;
	} else if (slot == 7) {
		color = biome_accent;
	}
	return srgb_to_linear(color.rgb);
}
