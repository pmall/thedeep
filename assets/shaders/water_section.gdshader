// The front of a swimmable WaterBody, seen from the side: the cut through the water on the play plane's front.
// What lies behind it (the pilgrim, walls, the pool's floor) shows through, bent by a slow waver and sunk into
// the biome's deep-water color with depth below the surface; a bright meniscus marks the surface line, with a
// band of caustic light under it. Colors are the biome's (lib/water.gdshaderinc), so it follows every layer.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, unshaded, fog_disabled;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"
#include "res://assets/shaders/lib/water.gdshaderinc"

uniform sampler2D screen : hint_screen_texture, filter_linear_mipmap, repeat_disable;

/// The surface's height (world Y), set by the WaterBody.
instance uniform float surface_y = 0.0;
/// How fast the view through the water sinks into its color with depth (per meter), and the clearest it ever is.
uniform float absorption : hint_range(0.0, 2.0) = 0.07;
uniform float clarity : hint_range(0.0, 1.0) = 0.72;
/// How far the view through the water wavers (share of the screen).
uniform float waver : hint_range(0.0, 0.02) = 0.004;
/// The surface line: its width (meters) and brightness.
uniform float meniscus_width : hint_range(0.0, 0.2) = 0.05;
uniform float meniscus_strength : hint_range(0.0, 4.0) = 0.8;
/// Caustic light just under the surface.
uniform float shimmer : hint_range(0.0, 2.0) = 0.12;

varying vec3 v_world_pos;

void vertex() {
	v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float t = TIME;
	float depth = max(surface_y - v_world_pos.y, 0.0);
	vec2 p = v_world_pos.xy;
	vec2 wobble = vec2(value_noise2(p * 0.7 + vec2(t * 0.25, 0.0)), value_noise2(p * 0.7 + vec2(3.1, t * 0.2))) - 0.5;
	vec2 uv = SCREEN_UV + wobble * waver * smoothstep(0.0, 0.6, depth);
	vec3 behind = textureLod(screen, uv, 0.0).rgb;

	// The water's own color: paler near the surface (lit from above), the deep color further down.
	vec3 deep = srgb_to_linear(biome_water_deep.rgb);
	vec3 shallow = mix(deep, srgb_to_linear(biome_water.rgb), 0.5);
	vec3 water = mix(shallow, mix(deep, shallow, 0.25), smoothstep(0.0, 10.0, depth));
	float sunk = 1.0 - clarity * exp(-depth * absorption);
	vec3 color = mix(behind, water, sunk);

	// A band of caustic light under the surface, fading within a couple of meters.
	vec3 caustic = srgb_to_linear(biome_caustic.rgb) * biome_caustic_strength;
	color += caustic * shimmer * caustic_pattern(vec2(p.x * 1.8, p.y * 3.5), t * 0.6) * exp(-depth * 2.0);

	// The surface line, rippling a little.
	float line_y = 0.03 * (value_noise2(vec2(p.x * 1.3 + t * 0.5, 0.0)) - 0.5);
	float meniscus = 1.0 - smoothstep(0.0, meniscus_width, abs(depth - line_y));
	color += srgb_to_linear(biome_water.rgb) * meniscus * meniscus_strength;

	ALBEDO = color;
	ALPHA = 1.0;
}
