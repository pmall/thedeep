// The front of a SlagBody on the play plane, seen from the side: the cut through the molten slag from its surface down to its
// bed. Brightest and yellowest at the surface, darkening with depth to a dull ember at the bed; broad slow currents roll along
// it (smooth noise of meters-wide cells, never per-pixel detail: docs/art-direction.md#stable-image); a bright line marks the
// surface (kept a few pixels wide and anti-aliased). Premultiplied over what lies behind (the bed and the banks' feet show
// through it as dark shapes, thinly near the surface, more toward the bed), unshaded: slag is its own light. Colors are the
// biome's accent, so it follows any layer that holds slag.
shader_type spatial;
render_mode blend_premul_alpha, depth_draw_never, cull_back, unshaded, fog_disabled;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"

/// The surface's and the bed's heights (world Y), set by the SlagBody.
instance uniform float surface_y = 0.0;
instance uniform float bed_y = -2.0;
/// How bright the slag glows at its surface, and what is left of it at the bed.
uniform float heat : hint_range(0.0, 12.0) = 1.3;
uniform float bed_glow : hint_range(0.0, 1.0) = 0.1;
/// How much of what lies behind it hides: at the surface and at the bed.
uniform float cover_top : hint_range(0.0, 1.0) = 0.97;
uniform float cover_bed : hint_range(0.0, 1.0) = 0.8;
/// The currents: their cell size (meters), speed (m/s along X) and strength.
uniform float current_size : hint_range(0.5, 6.0) = 1.6;
uniform float current_speed : hint_range(0.0, 1.0) = 0.12;
uniform float current_strength : hint_range(0.0, 1.0) = 0.5;
/// The surface line: its width (meters, never under `line_pixels` on screen) and brightness.
uniform float line_width : hint_range(0.0, 0.3) = 0.08;
uniform float line_pixels : hint_range(1.0, 6.0) = 3.0;
uniform float line_strength : hint_range(0.0, 4.0) = 1.2;

varying vec3 v_world_pos;

void vertex() {
	v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float t = TIME;
	float depth = max(surface_y - v_world_pos.y, 0.0);
	float span = max(surface_y - bed_y, 0.01);
	float down = clamp(depth / span, 0.0, 1.0);

	// The glow: yellow-white just under the surface, the accent below, an ember at the bed.
	vec3 accent = srgb_to_linear(biome_accent.rgb);
	vec3 white = vec3(1.0, 0.82, 0.45);
	vec3 hot = mix(mix(white, accent, smoothstep(0.0, 0.2, down)), accent * 0.35, smoothstep(0.2, 1.0, down));
	float glow = mix(1.0, bed_glow, pow(down, 0.7));

	// Broad currents rolling along the body, squashed into bands, slower and dimmer toward the bed.
	vec2 q = v_world_pos.xy / current_size * vec2(0.6, 1.4) + vec2(t * current_speed / current_size, 0.0);
	float current = value_noise2(q) * 0.65 + value_noise2(q * 1.9 + vec2(4.3, -t * 0.03)) * 0.35;
	glow *= 1.0 - current_strength * (1.0 - down * 0.5) + current_strength * 2.0 * current * (1.0 - down * 0.5);

	vec3 color = hot * heat * glow;

	// The surface line, rippling a little.
	float line_y = 0.03 * (value_noise2(vec2(v_world_pos.x * 0.7 + t * 0.2, 0.0)) - 0.5);
	float width = max(line_width, line_pixels * fwidth(v_world_pos.y));
	float line = 1.0 - smoothstep(0.0, width, abs(depth - line_y));
	color += white * heat * line_strength * line;

	float cover = mix(cover_top, cover_bed, down);
	ALBEDO = color * cover;
	ALPHA = cover;
}
