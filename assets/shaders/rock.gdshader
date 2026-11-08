// Rock: layered strata, silt settled on ledges, chipped edges, grime near the
// ground. With `wetness` it becomes wet stone (darker, glossy, puddles on top faces). Matte and warm
// so it never reads as metal (mag-grip). Pattern in world space: strata run on across
// neighbouring pieces; the surface frame (lib/surface) only adds edge wear and grime by height.
shader_type spatial;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"
#include "res://assets/shaders/lib/haze.gdshaderinc"
#include "res://assets/shaders/lib/surface.gdshaderinc"
#include "res://assets/shaders/lib/water.gdshaderinc"
#include "res://assets/shaders/lib/frost.gdshaderinc"

uniform int slot : hint_enum("Rock", "Metal", "Backdrop", "Backdrop accent", "Foreground") = 0;

/// How much of the rock was built, from the biome (BiomeProfile.Masonry): 0 strata, 1 courses of cut stone.
global uniform float biome_masonry;

group_uniforms strata;
/// Layers per meter of height.
uniform float strata_frequency : hint_range(0.1, 4.0) = 1.3;
/// How much the layers wave (meters).
uniform float strata_warp : hint_range(0.0, 2.0) = 0.7;
/// Tone difference between layers.
uniform float strata_contrast : hint_range(0.0, 0.5) = 0.2;

group_uniforms masonry;
/// Height of a course of cut stone, meters, and the blocks' length along it.
uniform float course_height : hint_range(0.2, 2.0) = 0.7;
uniform float block_length : hint_range(0.3, 4.0) = 1.5;
/// Share of the biome's masonry this material takes (0 keeps it natural rock everywhere).
uniform float masonry_share : hint_range(0.0, 1.0) = 1.0;

// Cracked rock (the Drill breaks it): a web of deep fissures over the whole face, so it reads apart from sound rock.
uniform float fracture : hint_range(0.0, 1.0) = 0.0;

group_uniforms deposits;
/// Silt or moss in crevices and on up-facing surfaces (biome_silt).
uniform float silt_amount : hint_range(0.0, 1.0) = 0.6;
/// Darkening near the bottom of each block (meters over which it fades).
uniform float grime_height : hint_range(0.0, 4.0) = 1.2;
uniform float grime_strength : hint_range(0.0, 1.0) = 0.45;

group_uniforms edges;
/// Width of the worn band along edges, meters (varies along the edge).
uniform float wear_width : hint_range(0.0, 0.3) = 0.07;
/// Paler, freshly exposed stone along the edges.
uniform float edge_wear : hint_range(0.0, 1.0) = 0.5;

group_uniforms surface;
uniform float roughness : hint_range(0.0, 1.0) = 0.9;
/// 0 = dry rock, 1 = wet stone.
uniform float wetness : hint_range(0.0, 1.0) = 0.0;
/// Turns detail off for distant, blurred pieces (backdrop): only tone and strata remain.
uniform bool detail = true;
/// Albedo multiplier. Below 1 pushes a piece back behind the play plane (midground), so the play
/// plane stays the brightest, most readable layer.
uniform float shade : hint_range(0.0, 1.0) = 1.0;
/// Share of the biome's rime this material takes (lib/frost).
uniform float frost_share : hint_range(0.0, 1.0) = 1.0;
group_uniforms;

struct Rock {
	float height; // meters, relative
	float cavity; // 1 deep in a fissure
	float tone;   // albedo multiplier
};

// The hash of the block of cut stone at a point of a face: `along` runs with the courses, `across` over them.
float masonry_id(float along, float across, float seed) {
	float course = floor(across / course_height);
	float len = block_length * (0.75 + 0.5 * hash12(vec2(course, seed + 1.7)));
	float block = floor(along / len + hash12(vec2(course, 9.1)));
	return hash12(vec2(block, course + seed * 13.0));
}

Rock rock_at(vec3 p, vec3 t, vec3 b, float seed, float up) {
	Rock r;
	vec2 q = vec2(dot(p, t), dot(p, b));
	float warp = fbm3(p * 0.3 + seed * 11.0, 3);

	// Strata: horizontal layers of uneven thickness. Faults shift whole runs of layers every few meters, so a wide face
	// reads as rock rather than as long parallel bands.
	float fault = floor(q.x / (3.5 + 2.0 * hash12(vec2(floor(q.x / 5.0), seed))));
	float throw_ = (hash12(vec2(fault, seed + 4.0)) - 0.5) * 0.9;
	float s = (p.y + throw_) * strata_frequency + (warp - 0.5) * strata_warp * 2.0 * strata_frequency;
	float layer_hash = hash12(vec2(floor(s), 3.7));
	r.tone = 1.0 + (layer_hash - 0.5) * 2.0 * strata_contrast + (warp - 0.5) * 0.35;
	r.height = 0.0;
	r.cavity = 0.0;

	// Masonry: courses along a wall (or round an arch), flagstones on what faces up.
	float built = biome_masonry * masonry_share;
	if (built > 0.0) {
		float across = up > 0.5 ? q.y : p.y;
		float block_tone = 0.9 + 0.2 * masonry_id(q.x, across, seed) + (warp - 0.5) * 0.25;
		r.tone = mix(r.tone, block_tone, built);
	}

	// Long stains running down the faces: wide walls never read as one flat tone.
	float stain = smoothstep(0.45, 0.8, value_noise2(vec2(q.x * 0.6 + seed * 3.0, p.y * 0.06)));
	r.tone *= 1.0 - 0.14 * stain * (1.0 - up);
	if (fracture > 0.0) {
		// Big warped cells split by wide dark fissures, a pale broken rim along each.
		vec2 fq = q * 1.1 + (vec2(fbm2(q * 0.7 + seed * 2.0, 2), fbm2(q * 0.7 + 3.1 + seed, 2)) - 0.5) * 0.9;
		vec2 fc = voronoi_border(fq);
		float fissure = 1.0 - smoothstep(0.02, 0.07, fc.x);
		float rim = (1.0 - smoothstep(0.07, 0.14, fc.x)) * (1.0 - fissure);
		r.height -= fissure * 0.08 * fracture;
		r.cavity = max(r.cavity, fissure * fracture);
		r.tone *= mix(1.0, (1.0 - 0.75 * fissure) * (1.0 + 0.25 * rim) * (0.9 + 0.2 * fc.y), fracture);
	}
	return r;
}

void vertex() {
	SURFACE_VERTEX
}

void fragment() {
	Surface f = SURFACE;
	vec3 t = view_to_world(f.tangent, INV_VIEW_MATRIX);
	vec3 b = view_to_world(f.bitangent, INV_VIEW_MATRIX);
	vec3 p = v_world_pos;
	float seed = v_seed;

	vec3 face_up_n = view_to_world(f.normal, INV_VIEW_MATRIX);
	float up = abs(face_up_n.y);
	Rock r = rock_at(p, t, b, seed, up);
	vec3 n = f.normal;
	float chipped = 0.0;
	if (detail) {
		// Worn edges: the band's width varies along the edge.
		float chip_noise = value_noise3(p * 3.1 + seed * 5.0);
		float width = wear_width * (0.4 + 1.2 * chip_noise);
		chipped = (1.0 - smoothstep(0.0, width * 1.6, f.edge)) * edge_wear;
		// Bump from the height field's screen-space slope (one evaluation, lib/surface).
		n = surface_bump(f, n, surface_slope(r.height, VERTEX, f.normal, f.tangent, f.bitangent));
	}
	vec3 face_world_n = view_to_world(f.normal, INV_VIEW_MATRIX);
	bool side = abs(face_world_n.y) < 0.5;
	// Puddles on top faces: still water is flat, so it reflects cleanly.
	float puddle = 0.0;
	if (!side && wetness > 0.0) {
		puddle = smoothstep(0.42, 0.5, value_noise3(p * 0.45 + seed * 3.0)) * (1.0 - r.cavity);
		n = normalize(mix(n, f.normal, puddle));
	}
	vec3 world_n = view_to_world(n, INV_VIEW_MATRIX);

	vec3 base = palette_color(slot) * (0.92 + 0.16 * seed);
	vec3 albedo = base * r.tone;
	albedo = mix(albedo, base * 1.25, chipped * 0.6);

	// Silt settles in crevices and on whatever faces up.
	float settle = smoothstep(0.35, 0.9, world_n.y) * smoothstep(0.35, 0.65, fbm3(p * 1.7 + 4.0, 2));
	float silt = detail ? clamp(max(r.cavity * 0.8, settle) * silt_amount, 0.0, 1.0) : 0.0;
	albedo = mix(albedo, srgb_to_linear(biome_silt.rgb), silt * 0.75);

	// Grime by height: dirt and water stains near the bottom of each piece's sides.
	float grime = side ? (1.0 - smoothstep(0.0, grime_height, f.height + (value_noise3(p * 2.3) - 0.5) * 0.5)) : 0.0;
	albedo *= 1.0 - grime * grime_strength;
	albedo *= 1.0 - r.cavity * 0.3;

	// Wet stone: darker and glossy, with mirror-like puddles on top faces.
	float wet = wetness * (side ? 0.8 : 0.6 + 0.4 * puddle);
	albedo *= 1.0 - 0.45 * wet;
	float rough = mix(roughness, side ? 0.32 : 0.28, wet);
	rough = mix(rough, 0.04, puddle * wetness);

	// Rime over it all: matte and pale.
	Frost fr = frost_at(p, world_n, face_world_n.y, r.cavity, detail ? f.edge : 1e3, frost_share);
	albedo = mix(albedo, frost_color(r.cavity), fr.cover);
	rough = mix(rough, 0.62, fr.cover);

	ALBEDO = albedo * shade;
	EMISSION = ALBEDO * caustics_at(v_world_pos, view_to_world(f.normal, INV_VIEW_MATRIX), TIME);
	ROUGHNESS = rough;
	METALLIC = 0.0;
	SPECULAR = mix(0.35, 0.5, wet);
	AO = 1.0 - r.cavity * 0.6;
	AO_LIGHT_AFFECT = 0.25;
	NORMAL = n;
	HAZE
}
