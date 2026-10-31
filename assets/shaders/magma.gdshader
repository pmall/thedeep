// Magma and slag (a slag river's surface, a pool in a kiln's floor): plates of dark crust drifting slowly on a molten
// web that glows in the biome's accent, brightest and yellowest at the heart of each crack, breathing. Projected from the
// world (top faces from above, side faces from the front), so it needs no UVs. Pair it with a HeatZone over the pool.
shader_type spatial;
render_mode cull_back;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"

/// Plate size (larger = smaller plates), the drift (m/s along X), and how hot the cracks glow.
uniform float scale : hint_range(0.1, 4.0) = 0.7;
uniform float drift : hint_range(0.0, 1.0) = 0.06;
uniform float heat : hint_range(0.0, 12.0) = 4.0;
/// How wide the molten web is between plates.
uniform float crack_width : hint_range(0.0, 0.4) = 0.12;

varying vec3 v_world_pos;
varying vec3 v_world_normal;

void vertex() {
	v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

void fragment() {
	float t = TIME;
	vec2 p = abs(v_world_normal.y) > 0.5 ? v_world_pos.xz : vec2(v_world_pos.x + v_world_pos.z * 0.3, v_world_pos.y * 1.4);
	vec2 q = p * scale + vec2(t * drift * scale, 0.0);
	q += (vec2(value_noise2(q * 0.4 + 3.1), value_noise2(q * 0.4 - 1.7)) - 0.5) * 0.8;
	float border = voronoi_border(q).x;
	float crack = 1.0 - smoothstep(crack_width * 0.2, crack_width, border);
	float core = 1.0 - smoothstep(0.0, crack_width * 0.35, border);
	float breathe = 0.75 + 0.25 * sin(t * 0.9 + value_noise2(q * 0.3) * 6.2832);
	vec3 accent = srgb_to_linear(biome_accent.rgb);
	vec3 hot = mix(accent, vec3(1.0, 0.85, 0.45), core * 0.7);
	vec3 crust = srgb_to_linear(biome_rock.rgb) * (0.22 + 0.12 * fbm2(q * 2.3, 3));
	ALBEDO = crust * (1.0 - crack);
	EMISSION = hot * heat * crack * breathe + accent * 0.18 * heat * 0.1 * (1.0 - crack);
	ROUGHNESS = mix(0.85, 0.4, crack);
	SPECULAR = 0.3;
}
