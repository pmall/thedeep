// Rock: layered strata, silt settled on ledges, chipped edges, grime near the
// ground. With `wetness` it becomes wet stone (darker, glossy, puddles on top faces). Matte and warm
// so it never reads as metal (mag-grip). The beds and their fine detail are the baked strata set
// (SurfaceMaps has the sampling contract), laid in world meters on each face so they run on across
// neighbouring pieces; faults cut the face into columns a few meters wide, each showing its own part
// of the tile, so the tile never repeats along a wall. The shader adds only what is broad: the beds'
// bend, stains, silt, grime, wetness, rime, masonry tone, worn edges, puddles. The surface frame
// (lib/surface) adds edge wear and grime by height.
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
/// The baked strata set (assets/textures/surfaces/, SurfaceMaps): every rock material sets both.
uniform sampler2D strata_normal : hint_normal, repeat_enable, filter_linear_mipmap_anisotropic;
uniform sampler2D strata_surface : hint_default_black, repeat_enable, filter_linear_mipmap_anisotropic;
/// Beds per meter of height: the set is scaled to it (at its own scale it has STRATA_BEDS).
uniform float strata_frequency : hint_range(0.1, 4.0) = 1.5;
/// How far the beds bend up and down over a few meters, meters.
uniform float strata_warp : hint_range(0.0, 2.0) = 0.4;
/// Tone difference between beds (the set's own is 0.2).
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
/// Dry roughness: the set's own varies around this.
uniform float roughness : hint_range(0.0, 1.0) = 0.9;
/// 0 = dry rock, 1 = wet stone.
uniform float wetness : hint_range(0.0, 1.0) = 0.0;
/// Turns detail off for pieces behind the play plane: the strata's tone only, softened, with no relief, silt or worn edges.
uniform bool detail = true;
/// Albedo multiplier. Below 1 pushes a piece back behind the play plane (midground), so the play
/// plane stays the brightest, most readable layer.
uniform float shade : hint_range(0.0, 1.0) = 1.0;
/// Share of the biome's rime this material takes (lib/frost).
uniform float frost_share : hint_range(0.0, 1.0) = 1.0;
group_uniforms;

// The baked set's tile (StrataSurface.TileMeters) and its beds per meter (StrataSurface.Beds over it).
const float STRATA_TILE = 4.0;
const float STRATA_BEDS = 1.5;
// Faults: the columns' mean width, in tiles (under one, so a column seldom shows the same part of the tile twice), how far
// their edges stray from even spacing (a share of that width), how far every edge leans (a set of faults runs one way), how
// far that lean changes over a few meters and how far an edge jags (tiles). Columns come in blocks of FAULT_BLOCK: inside a
// block the beds run on, dropping a little at each fault (up to FAULT_THROW tiles); between blocks they break, each block
// with its own beds, a little thicker or thinner.
const float FAULT_PITCH = 0.85;
const float FAULT_JITTER = 0.18;
const float FAULT_SHEAR = 0.35;
const float FAULT_LEAN = 0.25;
const float FAULT_JAG = 0.012;
const float FAULT_BLOCK = 3.0;
const float FAULT_THROW = 0.05;

struct Rock {
	float height; // meters, relative (the procedural fissures only; the beds' relief is in `slope`)
	float cavity; // 1 deep in a fissure
	float tone;   // albedo multiplier
	float rough;  // dry roughness
	vec2 slope;   // the baked normal's red and green, -1..1 (along and up the face)
};

// The hash of the block of cut stone at a point of a face: `along` runs with the courses, `across` over them.
float masonry_id(float along, float across, float seed) {
	float course = floor(across / course_height);
	float len = block_length * (0.75 + 0.5 * hash12(vec2(course, seed + 1.7)));
	float block = floor(along / len + hash12(vec2(course, 9.1)));
	return hash12(vec2(block, course + seed * 13.0));
}

// Where a fault edge between columns lies (in column widths), `k` the edge's index.
float fault_edge(float k) {
	return k + (hash12(vec2(k, 7.3)) - 0.5) * 2.0 * FAULT_JITTER;
}

Rock rock_at(vec3 p, vec3 t, vec3 b, float seed, float up) {
	Rock r;
	vec2 q = vec2(dot(p, t), dot(p, b));
	float warp = fbm3(p * 0.3 + 11.0, 3);

	// The face in tiles of the set, the beds bent over a few meters.
	float scale = strata_frequency / STRATA_BEDS;
	vec2 m = vec2(q.x, q.y + (warp - 0.5) * 2.0 * strata_warp) * scale / STRATA_TILE;

	// Faults: columns of uneven width, their edges leaning and jagged; each column takes its own part of the tile along the
	// face, so no part shows twice side by side.
	float lean = (value_noise2(vec2(m.x * 0.25, m.y * 0.35)) - 0.5) * 2.0 * FAULT_LEAN;
	float jag = (value_noise2(vec2(m.x * 2.0, m.y * 6.0)) - 0.5) * 2.0 * FAULT_JAG;
	float x = (m.x + m.y * FAULT_SHEAR + lean + jag) / FAULT_PITCH;
	float cell = floor(x);
	float edge = fault_edge(cell);
	float column = x < edge ? cell - 1.0 : cell;
	float left = x < edge ? fault_edge(cell - 1.0) : edge;
	float right = x < edge ? edge : fault_edge(cell + 1.0);
	bool near_left = x - left < right - x;
	float to_fault = min(x - left, right - x) * FAULT_PITCH * STRATA_TILE / scale; // meters
	float block = floor(column / FAULT_BLOCK);
	// The nearest fault breaks the beds if it bounds a block.
	bool breaks = mod(near_left ? column : column + 1.0, FAULT_BLOCK) == 0.0;
	float thickness = 0.85 + 0.3 * hash12(vec2(block, 3.3));
	vec2 shift = vec2(hash12(vec2(column, 1.9)), hash12(vec2(block, 5.1)) + (hash12(vec2(column, 6.7)) - 0.5) * 2.0 * FAULT_THROW);

	// One sample of each map; the mip comes from the unbroken position, so a fault is not a seam of blurred texels.
	vec2 dx = dFdx(m) * vec2(1.0, -thickness);
	vec2 dy = dFdy(m) * vec2(1.0, -thickness);
	vec2 uv = vec2(m.x, -m.y * thickness) + shift;
	vec3 surface = textureGrad(strata_surface, uv, dx, dy).rgb;
	r.slope = detail ? textureGrad(strata_normal, uv, dx, dy).rg * 2.0 - 1.0 : vec2(0.0);

	// A fault shows a dark joint a few centimeters wide: always where the beds break, coming and going where they run on.
	// None on what faces up, where it would read as paving.
	float opens = breaks ? 1.0 : 0.6 * smoothstep(0.45, 0.75, value_noise2(vec2(column * 3.7, m.y * 3.0)));
	float fault_joint = (1.0 - smoothstep(0.015, 0.06, to_fault)) * opens * (1.0 - up);

	// Tone: the set's beds, at this material's contrast, over a broad drift. What faces up is a bedding plane, the beds faint.
	float contrast = strata_contrast / 0.2 * (detail ? 1.0 : 0.6) * (1.0 - 0.5 * up);
	r.slope *= 1.0 - 0.5 * up;
	r.tone = 1.0 + (surface.r * 2.0 - 1.0) * contrast + (warp - 0.5) * 0.35;
	r.cavity = max(surface.g, fault_joint * 0.8) * (detail ? 1.0 : 0.5);
	r.rough = surface.b;
	r.height = 0.0;

	// Masonry: courses along a wall (or round an arch), flagstones on what faces up. Cut stone is smoother.
	float built = biome_masonry * masonry_share;
	if (built > 0.0) {
		float across = up > 0.5 ? q.y : p.y;
		float block_tone = 0.9 + 0.2 * masonry_id(q.x, across, seed) + (warp - 0.5) * 0.25;
		r.tone = mix(r.tone, block_tone * (1.0 + (surface.r * 2.0 - 1.0) * 0.4), built);
		r.cavity *= 1.0 - 0.7 * built;
		r.slope *= 1.0 - 0.6 * built;
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
		// The beds' relief from the baked normal (tangent space: red along the face, green up it).
		vec3 ts = vec3(r.slope, sqrt(max(0.0, 1.0 - dot(r.slope, r.slope))));
		n = normalize(f.tangent * ts.x + f.bitangent * ts.y + n * ts.z);
		// The fissures of cracked rock, wide enough to bump from their screen-space slope (lib/surface).
		if (fracture > 0.0) {
			n = surface_bump(f, n, surface_slope(r.height, VERTEX, f.normal, f.tangent, f.bitangent));
		}
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
	float rough = mix(clamp(r.rough + roughness - 0.9, 0.0, 1.0), side ? 0.32 : 0.28, wet);
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
