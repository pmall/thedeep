// Underwater haze for a WaterBody's FogVolume: dense, in the biome's deep water color, thickening just
// under the surface. Seen through the water surface now; around the player once Ballast lets them dive.
shader_type fog;

#include "res://assets/shaders/lib/palette.gdshaderinc"

global uniform float water_level;
global uniform vec4 biome_water_deep;

uniform float density : hint_range(0.0, 4.0) = 0.5;

void fog() {
	float depth = water_level - WORLD_POSITION.y;
	DENSITY = density * smoothstep(0.0, 0.4, depth);
	ALBEDO = srgb_to_linear(biome_water_deep.rgb);
}
