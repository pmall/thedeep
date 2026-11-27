// Shared rock core for rock.gdshader (strata), basalt.gdshader (columnar basalt) and cracked.gdshader (fissured strata):
// masonry courses over natural rock, broad stains, worn edges, puddles, silt, height grime, wetness, rime, drawn-look dirt
// and ink, caustics, haze and underwater attenuation. Each rock shader supplies its own base Rock sample and finishes with
// ROCK_FINISH(r, crevice_silt).
// Needs nothing included before it.

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"
#include "res://assets/shaders/lib/haze.gdshaderinc"
#include "res://assets/shaders/lib/surface.gdshaderinc"
#include "res://assets/shaders/lib/water.gdshaderinc"
#include "res://assets/shaders/lib/frost.gdshaderinc"
#include "res://assets/shaders/lib/look.gdshaderinc"
#include "res://assets/shaders/lib/dirt.gdshaderinc"

uniform int slot : hint_enum("Rock", "Metal", "Backdrop", "Backdrop accent", "Foreground") = 0;

/// How much of the rock was built, from the biome (BiomeProfile.Masonry): 0 natural rock, 1 courses of cut stone.
global uniform float biome_masonry;

group_uniforms masonry;
/// The baked masonry set (assets/textures/surfaces/, SurfaceMaps): every rock material sets both.
uniform sampler2D masonry_normal : hint_normal, repeat_enable, filter_linear_mipmap_anisotropic;
uniform sampler2D masonry_surface : hint_default_black, repeat_enable, filter_linear_mipmap_anisotropic;
/// Share of the biome's masonry this material takes (0 keeps it natural rock everywhere).
uniform float masonry_share : hint_range(0.0, 1.0) = 1.0;
/// Tone difference between blocks.
uniform float block_contrast : hint_range(0.0, 0.3) = 0.08;

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
/// Dry roughness: the set's own varies around this.
uniform float roughness : hint_range(0.0, 1.0) = 0.9;
/// 0 = dry rock, 1 = wet stone.
uniform float wetness : hint_range(0.0, 1.0) = 0.0;
/// Turns detail off for pieces behind the play plane: tone only, softened, with no relief, silt or worn edges.
uniform bool detail = true;
/// Albedo multiplier. Below 1 pushes a piece back behind the play plane (midground), so the play
/// plane stays the brightest, most readable layer.
uniform float shade : hint_range(0.0, 1.0) = 1.0;
/// Share of the biome's rime this material takes (lib/frost).
uniform float frost_share : hint_range(0.0, 1.0) = 1.0;
group_uniforms;

// The baked masonry set's tile (MasonrySurface.TileMeters), its courses, and the cells its alpha tells apart (SurfaceMaps).
const float MASONRY_TILE = 4.0;
const float MASONRY_COURSES = 7.0;
const float MASONRY_CELLS = 15.0;
// Where the masonry stands when the biome built only part of it: a block stands where a broad field (its cells MASONRY_REACH
// meters across) and the block's own hash, weighted by MASONRY_RAGGED, fall under the share built.
const float MASONRY_REACH = 9.0;
const float MASONRY_RAGGED = 0.1;
// A fault or column joint's fewest pixels at any distance.
const float FAULT_JOINT_PIXELS = 3.0;

struct Rock {
	float cavity; // 1 deep in a fissure
	float tone;   // albedo multiplier
	float rough;  // dry roughness
	vec2 slope;   // the baked normal's red and green, -1..1 (along and up the face)
	float built;  // 1 on a block of masonry, 0 on natural rock
};

// Cut stone on a face: `q` the position along and up the face (meters), `built` the share of blocks standing.
struct Masonry {
	float stands; // 1 where a block stands
	float tone;
	float cavity;
	float rough;
	vec2 slope;
};

// A joint's groove at `to_joint` meters from it: `half_width` wide, widened (and faded to match) where it would span fewer
// than FAULT_JOINT_PIXELS, so it never thins to a shimmering line far off.
float joint_at(float to_joint, float half_width) {
	float width = max(half_width, FAULT_JOINT_PIXELS * fwidth(to_joint));
	return (1.0 - smoothstep(0.0, width, to_joint)) * half_width / width;
}

Masonry masonry_at(vec2 q, float built) {
	// The world's course, shown as a course of the tile picked by its hash and slid along by another.
	float course_height = MASONRY_TILE / MASONRY_COURSES;
	float course = floor(q.y / course_height);
	float within = q.y / course_height - course;
	float picked = floor(hash12(vec2(course, 41.7)) * MASONRY_COURSES);
	float slide = hash12(vec2(course, 13.1)) * MASONRY_TILE;
	float along = q.x + slide;
	vec2 uv = vec2(along, -(picked + within) * course_height) / MASONRY_TILE;
	// The mip from the unbroken position, so a course's edge is not a seam of blurred texels.
	vec2 dx = dFdx(q) * vec2(1.0, -1.0) / MASONRY_TILE;
	vec2 dy = dFdy(q) * vec2(1.0, -1.0) / MASONRY_TILE;
	vec4 surface = textureGrad(masonry_surface, uv, dx, dy);

	// The block: its cell in the tile's course, which repeat of the tile, which course. Every course of the tile has a joint
	// at its edge, so a block is never split between two repeats.
	float cell = floor(surface.a * 255.0 / 16.0 + 0.5) - 1.0;
	float repeat_ = floor(along / MASONRY_TILE);
	float id = hash12(vec2(repeat_ * (MASONRY_CELLS + 1.0) + cell, course + 0.37));
	// Whether it stands: the broad field taken at the block (near enough: its repeat and its place in it), and its own hash.
	vec2 at = vec2(repeat_ * MASONRY_TILE + cell * 0.8 - slide, course * course_height) / MASONRY_REACH;
	float field = value_noise2(at + 17.0) * (1.0 - MASONRY_RAGGED) + hash12(vec2(id * 97.0, 5.3)) * MASONRY_RAGGED;

	Masonry m;
	m.stands = built >= 1.0 ? 1.0 : step(field, built);
	// The block's own tone, on the stone only: the mortar keeps the set's.
	float stone = 1.0 - surface.g;
	m.tone = surface.r * 2.0 * (1.0 + (id - 0.5) * 2.0 * block_contrast * stone);
	m.cavity = surface.g;
	m.rough = surface.b;
	m.slope = detail ? textureGrad(masonry_normal, uv, dx, dy).rg * 2.0 - 1.0 : vec2(0.0);
	return m;
}

// Overlays biome masonry courses and long vertical stains onto natural rock sample `r`.
Rock rock_overlay(Rock r, vec2 q, float py, float warp, float up) {
	float built = biome_masonry * masonry_share;
	if (built > 0.0) {
		Masonry m = masonry_at(q, built);
		float block_tone = 1.0 + (m.tone - 1.0) * (detail ? 1.0 : 0.6) + (warp - 0.5) * 0.25;
		r.tone = mix(r.tone * 0.9, block_tone, m.stands);
		r.cavity = mix(r.cavity, m.cavity * (detail ? 1.0 : 0.5), m.stands);
		r.rough = mix(r.rough, m.rough, m.stands);
		r.slope = mix(r.slope, m.slope, m.stands);
		r.built = m.stands;
	}

	// Long stains running down the faces: wide walls never read as one flat tone. In world space, so they run on across pieces.
	float stain = smoothstep(0.45, 0.8, value_noise2(vec2(q.x * 0.6, py * 0.06)));
	r.tone *= 1.0 - 0.14 * stain * (1.0 - up);
	return r;
}

void vertex() {
	SURFACE_VERTEX
}

// Finishes fragment() for a rock shader after computing Surface `f`, world position `p`, seed `seed`, face normal
// `face_world_n` and Rock sample `r`. `crevice_silt` is 0.8 for strata/cracked rock and 0.0 for columnar basalt.
#define ROCK_FINISH(r, crevice_silt) \
	vec3 n = f.normal; \
	float chipped = 0.0; \
	if (detail) { \
		float chip_noise = value_noise3(p * 3.1 + seed * 5.0); \
		float width = wear_width * (0.4 + 1.2 * chip_noise); \
		chipped = (1.0 - smoothstep(0.0, width * 1.6, f.edge)) * edge_wear; \
		vec2 slope = r.slope / max(1.0, length(r.slope)); \
		n = surface_relief(f, slope); \
	} \
	bool side = abs(face_world_n.y) < 0.5; \
	float puddle = 0.0; \
	if (!side && wetness > 0.0) { \
		puddle = smoothstep(0.42, 0.5, value_noise3(p * 0.45 + seed * 3.0)) * (1.0 - r.cavity); \
		n = normalize(mix(n, f.normal, puddle)); \
	} \
	vec3 world_n = view_to_world(n, INV_VIEW_MATRIX); \
	vec3 base = palette_color(slot) * mix(0.92 + 0.16 * seed, 1.0, r.built); \
	vec3 albedo = base * r.tone; \
	albedo = mix(albedo, base * 1.25, chipped * 0.6); \
	float settle = smoothstep(0.35, 0.9, world_n.y) * smoothstep(0.35, 0.65, fbm3(p * 1.7 + 4.0, 2)); \
	float crevice = r.cavity * (crevice_silt); \
	float silt = detail ? clamp(max(crevice, settle) * silt_amount, 0.0, 1.0) : 0.0; \
	albedo = mix(albedo, srgb_to_linear(biome_silt.rgb), silt * 0.75); \
	float grime = side ? (1.0 - smoothstep(0.0, grime_height, f.height + (value_noise3(p * 2.3) - 0.5) * 0.5)) : 0.0; \
	albedo *= 1.0 - grime * grime_strength; \
	albedo *= 1.0 - r.cavity * 0.3; \
	float wet = wetness * (side ? 0.8 : 0.6 + 0.4 * puddle); \
	albedo *= 1.0 - 0.45 * wet; \
	float rough = mix(clamp(r.rough + roughness - 0.9, 0.0, 1.0), side ? 0.32 : 0.28, wet); \
	rough = mix(rough, 0.04, puddle * wetness); \
	Frost fr = frost_at(p, face_world_n, world_n, r.cavity, detail ? f.edge : 1e3, frost_share); \
	albedo = mix(albedo, frost_color(r.cavity) * fr.tone, fr.cover); \
	rough = mix(rough, fr.rough, fr.cover); \
	n = detail ? frost_bump(n, fr, VIEW_MATRIX) : n; \
	LookDirt dirt = look_dirt(albedo, f, p); \
	albedo = dirt.albedo; \
	rough = mix(rough, 1.0, dirt.matte); \
	albedo = look_drawn(albedo, f.uv, f.edge); \
	look_mark_at(f.uv); \
	ALBEDO = albedo * shade; \
	EMISSION = ALBEDO * caustics_at(v_world_pos, face_world_n, TIME); \
	ROUGHNESS = rough; \
	METALLIC = 0.0; \
	SPECULAR = mix(0.35, 0.5, wet); \
	AO = 1.0 - r.cavity * 0.6; \
	AO_LIGHT_AFFECT = 0.25; \
	NORMAL = n; \
	HAZE \
	UNDERWATER \
	SPECULAR *= uw_keep;
