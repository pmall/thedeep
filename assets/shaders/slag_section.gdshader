// The front of a SlagBody on the play plane, seen from the side: the cut through the molten slag from its surface down to its
// bed. A deep orange-gold just under the surface (never white: only the surface line runs hotter), cooling through the biome's
// accent to a red-black at the bed. Slow convection rolls along it, folded by darker cooling veins near the surface that turn
// into glowing seams in the dark depths; every shape is smooth noise tens of centimeters wide and
// anti-aliased, never per-pixel detail (docs/art-direction.md#stable-image). The bed (a rubble line) and the banks' feet
// (talus widening toward the bed) show through it as dark silhouettes, drawn here since nothing stands behind the cut. A bright
// incandescent line marks the surface (a few pixels wide, anti-aliased). Unshaded, nearly opaque: slag is its own light.
// Colors follow the biome's accent, so it fits any layer that holds slag.
shader_type spatial;
render_mode blend_premul_alpha, depth_draw_never, cull_back, unshaded, fog_disabled;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"

/// The surface's and the bed's heights (world Y) and the front's half width (meters), set by the SlagBody.
instance uniform float surface_y = 0.0;
instance uniform float bed_y = -2.0;
instance uniform float half_width = 2.0;
/// How bright the slag glows just under its surface, and what is left of it at the bed.
uniform float heat : hint_range(0.0, 4.0) = 0.8;
uniform float bed_glow : hint_range(0.0, 1.0) = 0.05;
/// How fast it cools with depth: meters over which the glow falls to about a third.
uniform float falloff : hint_range(0.2, 4.0) = 1.0;
/// How much of what lies behind it hides.
uniform float cover : hint_range(0.0, 1.0) = 0.98;
/// The convection cells: size (meters), drift (m/s along X) and how much they light and darken the melt.
uniform float cell_size : hint_range(0.3, 4.0) = 1.1;
uniform float current_speed : hint_range(0.0, 1.0) = 0.1;
uniform float current_strength : hint_range(0.0, 1.0) = 0.6;
/// The veins where the flow folds: their width (in noise units, about a tenth of a cell) and strength.
uniform float vein_width : hint_range(0.0, 0.3) = 0.05;
uniform float vein_strength : hint_range(0.0, 1.0) = 0.7;
/// The bed's and the banks' silhouettes: the rubble's height and the talus' width at the bed (meters).
uniform float bed_rubble : hint_range(0.0, 1.0) = 0.35;
uniform float foot_width : hint_range(0.0, 2.0) = 0.7;
/// The surface line: its width (meters, never under `line_pixels` on screen) and brightness.
uniform float line_width : hint_range(0.0, 0.3) = 0.05;
uniform float line_pixels : hint_range(1.0, 6.0) = 3.0;
uniform float line_strength : hint_range(0.0, 6.0) = 3.0;

varying vec3 v_world_pos;
varying float v_local_x;

void vertex() {
	v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_local_x = VERTEX.x;
}

// A 0-to-1 edge `width` wide around `edge`, anti-aliased to at least a pixel and a half.
float soft_step(float edge, float width, float x) {
	float w = max(width, 1.5 * fwidth(x));
	return smoothstep(edge - w, edge + w, x);
}

void fragment() {
	float t = TIME;
	float depth = max(surface_y - v_world_pos.y, 0.0);
	float span = max(surface_y - bed_y, 0.01);
	float down = clamp(depth / span, 0.0, 1.0);

	// The melt's colour by depth, in meters (a deep body is dark long before its bed) and toward the bed: gold under the
	// surface, the accent, a red-black below.
	vec3 accent = srgb_to_linear(biome_accent.rgb);
	vec3 gold = vec3(1.0, 0.4, 0.06);
	vec3 ember = vec3(0.16, 0.018, 0.004);
	float cool = max(1.0 - exp(-depth / falloff), down);
	vec3 hot = mix(gold, accent, smoothstep(0.0, 0.4, cool));
	hot = mix(hot, ember, smoothstep(0.35, 1.0, cool));
	float glow = mix(1.0, bed_glow, pow(cool, 0.9));

	// Convection: smooth warped noise rolling along the body, stretched by its flow.
	vec2 p = v_world_pos.xy / cell_size * vec2(0.55, 1.3) + vec2(t * current_speed / cell_size, 0.0);
	vec2 warp = vec2(value_noise2(p * 0.5 + vec2(2.3, t * 0.02)), value_noise2(p * 0.5 + vec2(-4.1, 7.7 - t * 0.02)));
	vec2 q = p + (warp - 0.5) * 1.6;
	float roll = value_noise2(q * 0.8) * 0.7 + value_noise2(q * 1.7 + 5.3) * 0.3;
	glow *= 1.0 + current_strength * (roll * 2.0 - 1.0);

	// Veins where the flow folds (a contour of the warped noise): cooler skin near the surface, glowing seams in the dark.
	float fold = value_noise2(q * 1.1 + vec2(11.0, 3.0)) - 0.5;
	float fine = value_noise2(q * 2.3 + vec2(-6.0, 1.0)) - 0.5;
	float vein = max(1.0 - soft_step(vein_width * 0.5, vein_width * 0.5, abs(fold)),
			0.6 * (1.0 - soft_step(vein_width * 0.35, vein_width * 0.35, abs(fine))));
	vec3 color = hot * heat * glow;
	float seam = smoothstep(0.3, 0.7, cool);
	color = mix(color, color * (1.0 - vein_strength), vein * (1.0 - seam));
	color += accent * heat * 0.25 * vein * seam * (1.0 - smoothstep(0.85, 1.0, down));

	// The bed's rubble and the banks' feet, dark through the melt.
	float x = v_world_pos.x;
	float rubble = bed_y + bed_rubble * (0.35 + 0.65 * value_noise2(vec2(x * 1.3, 3.0)) * value_noise2(vec2(x * 0.45, 9.0)));
	float talus = foot_width * pow(down, 1.6) * (0.7 + 0.6 * value_noise2(vec2(v_world_pos.y * 1.1, 1.0)));
	float edge = half_width - abs(v_local_x);
	float shadow = max(1.0 - soft_step(rubble, 0.02, v_world_pos.y), 1.0 - soft_step(talus, 0.02, edge));
	// Felt before it is reached: the melt dims over the bed and beside the banks.
	float near = max(1.0 - smoothstep(0.0, 0.5, v_world_pos.y - rubble), 1.0 - smoothstep(0.0, 0.6, edge - talus));
	color *= 1.0 - 0.35 * near;
	color = mix(color, ember * 0.12 + color * 0.08, shadow);

	// The surface line, rippling a little: incandescent, hotter than anything below it.
	float line_y = 0.03 * (value_noise2(vec2(v_world_pos.x * 0.7 + t * 0.2, 0.0)) - 0.5);
	float width = max(line_width, line_pixels * fwidth(v_world_pos.y));
	float line = 1.0 - smoothstep(0.0, width, abs(depth - line_y));
	color += vec3(1.0, 0.62, 0.22) * line_strength * line;

	ALBEDO = color * cover;
	ALPHA = cover;
}
