// The front of a WaterBody on the play plane, seen from the side: the cut through the water on the play plane's front, drawn
// without reading the screen (docs/art-direction.md#stable-image). What lies in the water sinks into it on its own
// surface (lib/water.gdshaderinc, UNDERWATER), so this layer only marks the water: a bright meniscus on the surface line
// (kept a few pixels wide and anti-aliased, between the water's tint and its caustic light), a band of broad, soft caustic light under it,
// the light from above held in the water (a glow under the surface and soft leaning shafts, both fading with depth, so the front
// reads as a body of water lit from above, never a flat slab), and a thin veil of the water's
// color over what has no murk of its own (the pilgrim, which stays the highest-contrast figure). Premultiplied, sorted
// with the other transparents by distance, so particles in front of it stay untouched. Colors are the biome's, so it
// follows every layer.
shader_type spatial;
render_mode blend_premul_alpha, depth_draw_never, cull_back, unshaded, fog_disabled;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"
#include "res://assets/shaders/lib/water.gdshaderinc"

/// The surface's height (world Y), set by the WaterBody.
instance uniform float surface_y = 0.0;
/// The veil over what lies behind: at the surface, and how much it thickens per meter of depth (up to `veil_max`).
uniform float veil : hint_range(0.0, 1.0) = 0.06;
uniform float veil_per_meter : hint_range(0.0, 0.2) = 0.01;
uniform float veil_max : hint_range(0.0, 1.0) = 0.2;
/// The surface line: its width (meters, never under `meniscus_pixels` on screen) and brightness.
uniform float meniscus_width : hint_range(0.0, 0.2) = 0.06;
uniform float meniscus_pixels : hint_range(1.0, 6.0) = 3.0;
uniform float meniscus_strength : hint_range(0.0, 4.0) = 1.4;
/// Light from above held in the water: the water's tint glowing under the surface, fading over `glow_depth` meters, so
/// the front reads as a body lit from above that darkens with depth, never a flat slab.
uniform float glow : hint_range(0.0, 1.0) = 0.12;
uniform float glow_depth : hint_range(0.5, 20.0) = 3.0;
/// Shafts of that light reaching down into the water: their brightness, width (meters), lean (meters across per meter
/// down) and how many meters down they fade.
uniform float rays : hint_range(0.0, 1.0) = 0.3;
uniform float ray_width : hint_range(0.5, 6.0) = 1.6;
uniform float ray_lean : hint_range(-1.0, 1.0) = 0.3;
uniform float ray_depth : hint_range(0.5, 20.0) = 8.0;
/// Caustic light just under the surface: its brightness and cell size (meters).
uniform float shimmer : hint_range(0.0, 2.0) = 0.06;
uniform float caustic_size : hint_range(0.5, 6.0) = 1.4;

varying vec3 v_world_pos;

void vertex() {
	v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

// Soft shafts of light hanging down from the surface, leaning, slowly drifting and breathing: two layers of smooth noise
// across the shafts only, so each is meters wide and never shimmers.
float light_rays(vec2 p, float t) {
	float u = (p.x + p.y * ray_lean) / ray_width;
	float a = value_noise2(vec2(u + t * 0.05, t * 0.04));
	float b = value_noise2(vec2(u * 1.9 - t * 0.03, 7.1 + t * 0.05));
	float r = smoothstep(0.45, 0.95, a) * 0.7 + smoothstep(0.5, 0.95, b) * 0.5;
	return r;
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
	float sunk = min(veil + veil_per_meter * depth, veil_max);
	vec3 tint = srgb_to_linear(biome_water.rgb);
	vec3 color = water * sunk + tint * glow * exp(-depth / glow_depth);
	color += tint * rays * light_rays(p, t) * exp(-depth / ray_depth) * smoothstep(0.0, 0.3, depth);

	// A band of caustic light under the surface, fading within a couple of meters.
	vec3 caustic = srgb_to_linear(biome_caustic.rgb) * biome_caustic_strength;
	color += caustic * shimmer * caustic_band(p, t * 0.6) * exp(-depth * 2.0);

	// The surface line, rippling a little.
	float line_y = 0.04 * (value_noise2(vec2(p.x * 0.8 + t * 0.4, 0.0)) - 0.5);
	float width = max(meniscus_width, meniscus_pixels * fwidth(v_world_pos.y));
	float meniscus = 1.0 - smoothstep(0.0, width, abs(depth - line_y));
	color += mix(srgb_to_linear(biome_water.rgb), srgb_to_linear(biome_caustic.rgb), 0.5) * meniscus * meniscus_strength;

	// Premultiplied: what the water adds over the scene behind, and how much of that scene it hides.
	ALBEDO = color;
	ALPHA = sunk;
}
