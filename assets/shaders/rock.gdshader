// Rock: layered strata, cracks, silt settled in crevices and on ledges, chipped edges, grime near the
// ground. With `wetness` it becomes wet stone (darker, glossy, puddles on top faces). Matte and warm
// so it never reads as metal (mag-grip). Pattern in world space: strata and cracks run on across
// neighbouring blocks; the box (lib/box) only adds edges, bevels and grime by height.
shader_type spatial;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"
#include "res://assets/shaders/lib/box.gdshaderinc"

uniform int slot : hint_enum("Rock", "Metal", "Backdrop", "Backdrop accent", "Foreground") = 0;

group_uniforms strata;
/// Layers per meter of height.
uniform float strata_frequency : hint_range(0.1, 4.0) = 1.3;
/// How much the layers wave (meters).
uniform float strata_warp : hint_range(0.0, 2.0) = 0.7;
/// Tone difference between layers.
uniform float strata_contrast : hint_range(0.0, 0.5) = 0.2;

group_uniforms cracks;
/// Voronoi cells per meter.
uniform float crack_scale : hint_range(0.2, 4.0) = 0.8;
uniform float crack_width : hint_range(0.0, 0.1) = 0.014;
/// Share of cell borders that are actually cracked.
uniform float crack_amount : hint_range(0.0, 1.0) = 0.28;

group_uniforms deposits;
/// Silt or moss in crevices and on up-facing surfaces (biome_silt).
uniform float silt_amount : hint_range(0.0, 1.0) = 0.6;
/// Darkening near the bottom of each block (meters over which it fades).
uniform float grime_height : hint_range(0.0, 4.0) = 1.2;
uniform float grime_strength : hint_range(0.0, 1.0) = 0.45;

group_uniforms edges;
uniform float bevel_radius : hint_range(0.0, 0.3) = 0.07;
/// Paler, freshly exposed stone along the edges.
uniform float edge_wear : hint_range(0.0, 1.0) = 0.5;

group_uniforms surface;
uniform float roughness : hint_range(0.0, 1.0) = 0.9;
/// 0 = dry rock, 1 = wet stone.
uniform float wetness : hint_range(0.0, 1.0) = 0.0;
/// Turns detail off for distant, blurred pieces (backdrop): only tone and strata remain.
uniform bool detail = true;
group_uniforms;

struct Rock {
	float height; // meters, relative
	float cavity; // 1 deep in a crack or a strata joint
	float tone;   // albedo multiplier
};

Rock rock_at(vec3 p, vec3 t, vec3 b, float seed) {
	Rock r;
	vec2 q = vec2(dot(p, t), dot(p, b));
	float warp = fbm3(p * 0.3 + seed * 11.0, 3);

	// Strata: horizontal layers of uneven thickness, some proud of the wall, joined by grooves.
	float s = p.y * strata_frequency + (warp - 0.5) * strata_warp * 2.0 * strata_frequency;
	float layer = floor(s);
	float within = fract(s);
	float layer_hash = hash12(vec2(layer, 3.7));
	float joint = 1.0 - smoothstep(0.0, 0.07, within) * smoothstep(1.0, 0.9, within);
	// Some layers are split once more, so the spacing never looks regular.
	float split = 0.3 + 0.4 * fract(layer_hash * 7.13);
	if (layer_hash < 0.45) {
		joint = max(joint, (1.0 - smoothstep(0.0, 0.05, abs(within - split))) * 0.7);
	}
	r.tone = 1.0 + (layer_hash - 0.5) * 2.0 * strata_contrast + (warp - 0.5) * 0.35;
	r.height = layer_hash * 0.035 - joint * 0.025;
	r.cavity = joint * 0.5;
	if (!detail) {
		return r;
	}

	// Cracks: borders of warped Voronoi cells, only where a low-frequency mask allows.
	vec2 cq = q * crack_scale + (vec2(fbm2(q * 0.9 + seed, 2), fbm2(q * 0.9 + 5.2 + seed, 2)) - 0.5) * 0.8;
	vec2 cell = voronoi_border(cq);
	// Cracks come and go, and taper: their width follows a noise along them.
	float presence = smoothstep(1.0 - crack_amount, 1.0 - crack_amount + 0.12, fbm2(q * 0.35 + seed * 3.0, 2));
	float width = crack_width * crack_scale * presence * (0.4 + 0.9 * value_noise2(q * 3.0 + 1.3));
	float crack = 1.0 - smoothstep(0.0, max(width, 1e-4), cell.x);
	crack *= step(0.001, presence);
	// Each cell is a slab with its own slight tilt in tone.
	r.tone *= 0.94 + 0.12 * cell.y;

	float grain = fbm3(p * 7.0, 2);
	r.height += grain * 0.012 + cell.y * 0.006 - crack * 0.025;
	r.cavity = max(r.cavity, crack * 0.8);
	r.tone *= 0.9 + 0.2 * grain;
	return r;
}

void vertex() {
	BOX_VERTEX
}

void fragment() {
	BoxFace f = box_face();
	vec3 t = box_to_world(f.tangent, MODEL_MATRIX);
	vec3 b = box_to_world(f.bitangent, MODEL_MATRIX);
	vec3 p = v_world_pos;
	float seed = v_seed;

	Rock r = rock_at(p, t, b, seed);
	vec3 n = f.normal;
	float chipped = 0.0;
	if (detail) {
		// Bump from the height field by finite differences along the face.
		const float e = 0.006;
		float hx = rock_at(p + t * e, t, b, seed).height;
		float hy = rock_at(p + b * e, t, b, seed).height;
		// Chipped edges: the bevel varies along the edge.
		float chip_noise = value_noise3(p * 3.1 + seed * 5.0);
		float radius = bevel_radius * (0.4 + 1.2 * chip_noise);
		chipped = (1.0 - smoothstep(0.0, radius * 1.6, f.edge)) * edge_wear;
		n = box_bump(f, box_bevel_normal(f, radius), vec2(hx - r.height, hy - r.height) / e);
	} else {
		n = box_bevel_normal(f, bevel_radius);
	}
	bool side = abs(f.normal.y) < 0.5;
	// Puddles on top faces: still water is flat, so it reflects cleanly (SSR).
	float puddle = 0.0;
	if (!side && wetness > 0.0) {
		puddle = smoothstep(0.42, 0.5, value_noise3(p * 0.45 + seed * 3.0)) * (1.0 - r.cavity);
		n = normalize(mix(n, f.normal, puddle));
	}
	vec3 world_n = box_to_world(n, MODEL_MATRIX);

	vec3 base = palette_color(slot) * (0.92 + 0.16 * seed);
	vec3 albedo = base * r.tone;
	albedo = mix(albedo, base * 1.25, chipped * 0.6);

	// Silt settles in crevices and on whatever faces up.
	float settle = smoothstep(0.35, 0.9, world_n.y) * smoothstep(0.35, 0.65, fbm3(p * 1.7 + 4.0, 2));
	float silt = detail ? clamp(max(r.cavity * 0.8, settle) * silt_amount, 0.0, 1.0) : 0.0;
	albedo = mix(albedo, srgb_to_linear(biome_silt.rgb), silt * 0.75);

	// Grime by height: dirt and water stains near the bottom of each block's sides.
	float grime = side ? (1.0 - smoothstep(0.0, grime_height, f.height + (value_noise3(p * 2.3) - 0.5) * 0.5)) : 0.0;
	albedo *= 1.0 - grime * grime_strength;
	albedo *= 1.0 - r.cavity * 0.3;

	// Wet stone: darker and glossy, with mirror-like puddles on top faces.
	float wet = wetness * (side ? 0.8 : 0.6 + 0.4 * puddle);
	albedo *= 1.0 - 0.45 * wet;
	float rough = mix(roughness, side ? 0.32 : 0.28, wet);
	rough = mix(rough, 0.04, puddle * wetness);

	ALBEDO = albedo;
	ROUGHNESS = rough;
	METALLIC = 0.0;
	SPECULAR = mix(0.35, 0.5, wet);
	AO = 1.0 - r.cavity * 0.6;
	AO_LIGHT_AFFECT = 0.25;
	NORMAL = box_to_view(n, MODEL_MATRIX, VIEW_MATRIX);
}
