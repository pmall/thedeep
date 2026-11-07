// Underwater haze for a WaterBody's FogVolume: dense, in the biome's deep water color, thickening just
// under the surface. Seen through the water surface now; around the player once Ballast lets them dive.
// `lit` turns its color toward the biome's water tint, so the haze catches the room's light instead of
// only swallowing it (a thinner haze with a wall to see through it).
shader_type fog;

#include "res://assets/shaders/lib/palette.gdshaderinc"

global uniform float water_level;
global uniform vec4 biome_water_deep;
global uniform vec4 biome_water;

uniform float density : hint_range(0.0, 4.0) = 0.5;
uniform float lit : hint_range(0.0, 1.0) = 0.0;

void fog() {
	float depth = water_level - WORLD_POSITION.y;
	DENSITY = density * smoothstep(0.0, 0.4, depth);
	ALBEDO = srgb_to_linear(mix(biome_water_deep.rgb, biome_water.rgb, lit));
}
