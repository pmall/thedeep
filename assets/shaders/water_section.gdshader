// The front of a swimmable WaterBody, seen from the side: the cut through the water on the play plane's front, drawn
// without reading the screen (docs/art-direction.md#stable-image). A premultiplied layer over what lies behind it (the
// pilgrim, walls, the pool's floor): its opacity grows with depth below the surface, sinking them into the biome's
// water color, so the play plane reads through it near the surface. A bright meniscus marks the surface line (kept a few
// pixels wide and anti-aliased), with a band of broad, soft caustic light under it. Colors are the biome's
// (lib/water.gdshaderinc), so it follows every layer.
shader_type spatial;
render_mode blend_premul_alpha, depth_draw_never, cull_back, unshaded, fog_disabled;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"
#include "res://assets/shaders/lib/water.gdshaderinc"

/// The surface's height (world Y), set by the WaterBody.
instance uniform float surface_y = 0.0;
/// How fast the view through the water sinks into its color with depth (per meter), and the clearest it ever is.
uniform float absorption : hint_range(0.0, 2.0) = 0.07;
uniform float clarity : hint_range(0.0, 1.0) = 0.72;
/// The surface line: its width (meters, never under `meniscus_pixels` on screen) and brightness.
uniform float meniscus_width : hint_range(0.0, 0.2) = 0.06;
uniform float meniscus_pixels : hint_range(1.0, 6.0) = 2.5;
uniform float meniscus_strength : hint_range(0.0, 4.0) = 0.8;
/// Caustic light just under the surface: its brightness and cell size (meters).
uniform float shimmer : hint_range(0.0, 2.0) = 0.06;
uniform float caustic_size : hint_range(0.5, 6.0) = 1.4;

varying vec3 v_world_pos;

void vertex() {
	v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

// Broad soft light cells: wide Voronoi borders drifting slowly, the cells squashed into bands along the surface.
float caustic_band(vec2 p, float t) {
	vec2 q = p / caustic_size * vec2(1.0, 1.8);
	vec2 warp = vec2(value_noise2(q * 0.5 + vec2(t * 0.08, 1.3)), value_noise2(q * 0.5 + vec2(5.2, -t * 0.07))) - 0.5;
	float a = voronoi_border(q + warp * 0.9 + vec2(t * 0.05, t * 0.03)).x;
	float b = voronoi_border(q * 1.4 + vec2(-t * 0.04, t * 0.06) + 3.7).x;
	return (1.0 - smoothstep(0.0, 0.22, a)) * 0.7 + (1.0 - smoothstep(0.0, 0.18, b)) * 0.4;
}

void fragment() {
	float t = TIME;
	float depth = max(surface_y - v_world_pos.y, 0.0);
	vec2 p = v_world_pos.xy;

	// The water's own color: paler near the surface (lit from above), the deep color further down.
	vec3 deep = srgb_to_linear(biome_water_deep.rgb);
	vec3 shallow = mix(deep, srgb_to_linear(biome_water.rgb), 0.5);
	vec3 water = mix(shallow, mix(deep, shallow, 0.25), smoothstep(0.0, 10.0, depth));
	float sunk = 1.0 - clarity * exp(-depth * absorption);
	vec3 color = water * sunk;

	// A band of caustic light under the surface, fading within a couple of meters.
	vec3 caustic = srgb_to_linear(biome_caustic.rgb) * biome_caustic_strength;
	color += caustic * shimmer * caustic_band(p, t * 0.6) * exp(-depth * 2.0);

	// The surface line, rippling a little.
	float line_y = 0.04 * (value_noise2(vec2(p.x * 0.8 + t * 0.4, 0.0)) - 0.5);
	float width = max(meniscus_width, meniscus_pixels * fwidth(v_world_pos.y));
	float meniscus = 1.0 - smoothstep(0.0, width, abs(depth - line_y));
	color += srgb_to_linear(biome_water.rgb) * meniscus * meniscus_strength;

	// Premultiplied: what the water adds over the scene behind, and how much of that scene it hides.
	ALBEDO = color;
	ALPHA = sunk;
}
