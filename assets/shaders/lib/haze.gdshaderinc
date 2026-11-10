// Aerial haze for architecture far behind the play plane (the backdrop's naves, stacks, foundries): the farther behind
// the play plane, the more of the biome's haze color veils it: the lit color dimmed, the haze added as emission (writing
// FOG instead turned every material using the shader black).
// Needs lib/palette (srgb_to_linear). Call HAZE at the end of fragment(), after ALBEDO and EMISSION.

global uniform vec4 biome_haze;

/// Share of haze per meter behind the play plane (0: none, the play plane's own materials).
uniform float haze_per_meter : hint_range(0.0, 0.1) = 0.0;
/// The most haze, however far.
uniform float haze_max : hint_range(0.0, 1.0) = 0.9;

float backdrop_haze(vec3 world_pos) {
	return clamp(-world_pos.z * haze_per_meter, 0.0, haze_max);
}

#define HAZE \
	{ \
		float hz = backdrop_haze(v_world_pos); \
		ALBEDO *= 1.0 - hz; \
		EMISSION = EMISSION * (1.0 - hz) + srgb_to_linear(biome_haze.rgb) * hz; \
	}
