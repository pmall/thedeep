// Rock: layered strata, columnar basalt or courses of cut stone, silt settled on ledges, chipped edges, grime near the
// ground. With `wetness` it becomes wet stone (darker, glossy, puddles on top faces). Matte and warm
// so it never reads as metal (mag-grip). The beds and the blocks with their fine detail are baked sets
// (SurfaceMaps has the sampling contract), laid in world meters on each face so they run on across
// neighbouring pieces; faults cut the strata into columns a few meters wide, their pitch and lean
// changing over the face, each showing its own part of the tile, and each course of masonry is a course of the tile picked at random and slid along, so
// neither tile repeats along a wall. Columnar rock (`columnar`, basalt) has no beds: it is upright prisms,
// the cells of a near-hexagonal net in plan, so a wall shows them side by side, each with the facets it
// turns to the face and cut across by joints at its own heights, and a top or an underside their polygonal
// ends; every piece its own tone over its own part of the baked basalt set. Where the biome built (biome_masonry), whole blocks stand over the
// rock, more of them the more it built, and where they are missing the rock shows, recessed. Cracked
// rock lays a baked web of fissures over it all (CracksSurface), as baked in some regions and mirrored and
// a little larger in others, so the web never shows twice along a wall. The shader adds only what is broad: the beds' bend, each block's tone, stains, silt, grime, wetness,
// rime, worn edges, puddles. The surface frame (lib/surface) adds edge wear and grime by height.
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

group_uniforms columns;
/// Columnar jointing in place of the strata (basalt): upright prisms, side by side on a wall, their ends on a top or an underside.
uniform bool columnar = false;
/// The baked basalt set (assets/textures/surfaces/, SurfaceMaps): the face of a column, set by every columnar material.
uniform sampler2D basalt_normal : hint_normal, repeat_enable, filter_linear_mipmap_anisotropic;
uniform sampler2D basalt_surface : hint_default_black, repeat_enable, filter_linear_mipmap_anisotropic;
/// Tone difference between columns (and, a little, between the pieces of one).
uniform float column_contrast : hint_range(0.0, 0.3) = 0.14;
/// How much of its facets' turn from the face a prism shows (1 the true prism): the light picks out each column.
uniform float column_facet : hint_range(0.0, 0.6) = 0.12;

group_uniforms masonry;
/// The baked masonry set (assets/textures/surfaces/, SurfaceMaps): every rock material sets both.
uniform sampler2D masonry_normal : hint_normal, repeat_enable, filter_linear_mipmap_anisotropic;
uniform sampler2D masonry_surface : hint_default_black, repeat_enable, filter_linear_mipmap_anisotropic;
/// Share of the biome's masonry this material takes (0 keeps it natural rock everywhere).
uniform float masonry_share : hint_range(0.0, 1.0) = 1.0;
/// Tone difference between blocks.
uniform float block_contrast : hint_range(0.0, 0.3) = 0.08;

group_uniforms cracks;
/// Cracked rock (the Drill breaks it): how strongly the baked web of deep fissures shows over the whole face, so it reads
/// apart from sound rock. A material with it above 0 sets the cracks set (assets/textures/surfaces/, SurfaceMaps).
uniform float fracture : hint_range(0.0, 1.0) = 0.0;
uniform sampler2D cracks_normal : hint_normal, repeat_enable, filter_linear_mipmap_anisotropic;
uniform sampler2D cracks_surface : hint_default_white, repeat_enable, filter_linear_mipmap_anisotropic;

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
// their edges stray from even spacing (a share of that width), the lean every edge shares, how far a broad bend of the
// faults reaches (tiles: over some ten meters it turns their lean one way or the other and draws them closer or apart, never
// enough to fold them), a finer lean and how far an edge jags (tiles). Columns come in blocks of FAULT_BLOCK: inside a
// block the beds run on, dropping a little at each fault (up to FAULT_THROW tiles); between blocks they break, each block
// with its own beds, a little thicker or thinner.
const float FAULT_PITCH = 0.85;
const float FAULT_JITTER = 0.3;
const float FAULT_SHEAR = 0.12;
const float FAULT_BEND = 1.3;
const float FAULT_LEAN = 0.2;
const float FAULT_JAG = 0.012;
const float FAULT_BLOCK = 3.0;
const float FAULT_THROW = 0.05;

// The baked basalt set's tile (BasaltSurface.TileMeters). The columns are upright prisms whose sections are the cells of a
// near-hexagonal net in the horizontal plane (rows staggered, each site strayed by COLUMN_JITTER of the spacing), so every
// face of a piece cuts the same prisms: a wall shows them side by side, a top or an underside their polygonal ends, and two
// pieces side by side carry them on. The net's spacing (meters); how far it is turned (radians), so no wall runs along a row
// of it; how far it drifts (meters) over regions COLUMN_REGION meters across, so its rhythm never repeats along a wall. A
// piece of column's height at least and at most (meters), how far a cross joint strays (a share of it) and bows (meters, a
// ball-and-socket joint); the joints' half widths (meters, widened on screen by joint_at); how far a column's end domes or
// dishes (the normal's slope per spacing from its middle) and how far a column's sides round off (the slope at its edges).
const float BASALT_TILE = 4.0;
const float COLUMN_SPACING = 2.5;
const float COLUMN_JITTER = 0.22;
const float COLUMN_TURN = 0.38;
const float COLUMN_DRIFT = 1.0;
const float COLUMN_REGION = 24.0;
const float DRUM_MIN = 3.0;
const float DRUM_MAX = 7.5;
const float DRUM_JITTER = 0.3;
const float DRUM_BOW = 0.08;
const float COLUMN_JOINT = 0.07;
const float DRUM_JOINT = 0.06;
const float CAP_DOME = 0.25;
const float COLUMN_ROUND = 0.08;

// The baked masonry set's tile (MasonrySurface.TileMeters), its courses, and the cells its alpha tells apart (SurfaceMaps).
const float MASONRY_TILE = 4.0;
const float MASONRY_COURSES = 7.0;
const float MASONRY_CELLS = 15.0;
// Where the masonry stands when the biome built only part of it: a block stands where a broad field (its cells MASONRY_REACH
// meters across) and the block's own hash, weighted by MASONRY_RAGGED, fall under the share built.
const float MASONRY_REACH = 9.0;
const float MASONRY_RAGGED = 0.1;

// The baked cracks set's tile (CracksSurface.TileMeters); the mirrored layer's scale against it and where it starts (meters);
// the cells per meter of the regions each way round shows in.
const float CRACKS_TILE = 4.0;
const float CRACKS_MIRROR_SCALE = 1.13;
const vec2 CRACKS_MIRROR_OFFSET = vec2(1.71, 0.53);
const float CRACKS_REGION_FREQUENCY = 0.16;
// A fault's joint: its half width, meters, and the fewest pixels its falloff spans at any distance.
const float FAULT_JOINT = 0.07;
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

// Where a fault edge between columns lies (in column widths), `k` the edge's index.
float fault_edge(float k) {
	return k + (hash12(vec2(k, 7.3)) - 0.5) * 2.0 * FAULT_JITTER;
}

// The span of an uneven lattice (edge k at k, strayed by up to `jitter` either way) that holds `x`: its index and its edges.
vec3 uneven_span(float x, float seed, float jitter) {
	float cell = floor(x);
	float edge = cell + (hash12(vec2(cell, seed)) - 0.5) * 2.0 * jitter;
	if (x < edge) {
		return vec3(cell - 1.0, cell - 1.0 + (hash12(vec2(cell - 1.0, seed)) - 0.5) * 2.0 * jitter, edge);
	}
	return vec3(cell, edge, cell + 1.0 + (hash12(vec2(cell + 1.0, seed)) - 0.5) * 2.0 * jitter);
}

// A joint's groove at `to_joint` meters from it: `half_width` wide, widened (and faded to match) where it would span fewer
// than FAULT_JOINT_PIXELS, so it never thins to a shimmering line far off.
float joint_at(float to_joint, float half_width) {
	float width = max(half_width, FAULT_JOINT_PIXELS * fwidth(to_joint));
	return (1.0 - smoothstep(0.0, width, to_joint)) * half_width / width;
}

// The site of the columns' net at lattice cell `c`, in spacings: rows 0.866 apart, every other one slid by half.
vec2 column_site(vec2 c) {
	return vec2(c.x + 0.5 * mod(c.y, 2.0), c.y * 0.866) + (hash22(c + vec2(17.0, 31.0)) - 0.5) * 2.0 * COLUMN_JITTER;
}

// Columnar basalt at `p` (world, meters) on a face along `t` and up `b`, its normal `n` (world), `up` how far it faces up.
Rock basalt_at(vec3 p, vec3 t, vec3 b, vec3 n, float warp, float up) {
	Rock r;
	// Into the net: turned, and drifted by region.
	mat2 turn = mat2(vec2(cos(COLUMN_TURN), sin(COLUMN_TURN)), vec2(-sin(COLUMN_TURN), cos(COLUMN_TURN)));
	vec2 region = p.xz / COLUMN_REGION;
	vec2 drift = (vec2(value_noise2(region), value_noise2(region + 7.3)) - 0.5) * 2.0 * COLUMN_DRIFT;
	vec2 l = turn * (p.xz + drift) / COLUMN_SPACING;

	// The column: the nearest site.
	float row = floor(l.y / 0.866 + 0.5);
	float first = floor(l.x - 0.5 * mod(row, 2.0) + 0.5);
	vec2 cell = vec2(0.0);
	vec2 site = vec2(0.0);
	float best = 1e9;
	for (int j = -1; j <= 1; j++) {
		for (int i = -1; i <= 1; i++) {
			vec2 g = vec2(first + float(i), row + float(j));
			vec2 s = column_site(g);
			float d = dot(s - l, s - l);
			if (d < best) {
				best = d;
				cell = g;
				site = s;
			}
		}
	}

	// Its borders with the columns round it. Out of the face (`out_`) and along it (`along`), in the net: how far the border
	// nearest every way (its end's joint), the nearest ahead and behind along the face (the column's width there, its joints
	// on the face), and the two first met going out of the face (the prism's facets the face shows, meeting on an arris).
	vec2 flat_n = length(n.xz) > 1e-3 ? normalize(n.xz) : vec2(0.0, 1.0);
	vec2 out_ = turn * flat_n;
	vec2 along = turn * vec2(-flat_n.y, flat_n.x);
	float border = 1e9;
	float ahead = 1e9;
	float behind = 1e9;
	float hit = 1e9;
	float next_hit = 1e9;
	vec2 facet = out_;
	vec2 next_facet = out_;
	for (int j = -1; j <= 1; j++) {
		for (int i = -1; i <= 1; i++) {
			vec2 g = cell + vec2(float(i), float(j));
			if (i == 0 && j == 0) {
				continue;
			}
			vec2 s = column_site(g);
			vec2 e = normalize(s - site);
			float gap = dot(0.5 * (site + s) - l, e);
			border = min(border, gap);
			float a = dot(along, e);
			if (a > 1e-3) {
				ahead = min(ahead, gap / a);
			} else if (a < -1e-3) {
				behind = min(behind, gap / -a);
			}
			float o = dot(out_, e);
			if (o > 1e-3) {
				float h = gap / o;
				if (h < hit) {
					next_hit = hit;
					next_facet = facet;
					hit = h;
					facet = e;
				} else if (h < next_hit) {
					next_hit = h;
					next_facet = e;
				}
			}
		}
	}
	float width = (ahead + behind) * COLUMN_SPACING;
	float across = behind / (ahead + behind);
	float to_side = min(ahead, behind) * COLUMN_SPACING;
	float cap = smoothstep(0.6, 0.85, up);

	// The piece of it: each column its own heights and offset; the cross joint bows and tilts a little.
	float column_id = hash12(cell * vec2(1.31, 0.77) + 4.7);
	float drum_height = mix(DRUM_MIN, DRUM_MAX, hash12(cell + 9.1));
	float centred = mix(across - 0.5, 0.0, cap);
	float bow = centred * centred * 4.0 * DRUM_BOW;
	float tilt = (hash12(cell + 8.3) - 0.5) * 0.3 * centred * width;
	float y = (p.y + column_id * DRUM_MAX - bow + tilt) / drum_height;
	vec3 drum = uneven_span(y, dot(cell, vec2(1.37, 2.11)) + 0.5, DRUM_JITTER);
	float to_top = (drum.z - y) * drum_height;
	float to_bottom = (y - drum.y) * drum_height;
	float id = hash12(vec2(column_id * 7.1, drum.x + 0.37));

	// Its fine face: its own part of the basalt set, the mip from the unbroken position.
	vec2 q = vec2(dot(p, t), dot(p, b));
	vec2 dx = dFdx(q) * vec2(1.0, -1.0) / BASALT_TILE;
	vec2 dy = dFdy(q) * vec2(1.0, -1.0) / BASALT_TILE;
	vec2 uv = vec2(q.x, -q.y) / BASALT_TILE + vec2(hash12(vec2(id, 1.3)), hash12(vec2(id, 2.9)));
	vec3 surface = textureGrad(basalt_surface, uv, dx, dy).rgb;
	vec2 fine = detail ? textureGrad(basalt_normal, uv, dx, dy).rg * 2.0 - 1.0 : vec2(0.0);

	// On a wall, the prism: the facet the face meets first going out, blended into the next over its arris (a few
	// centimeters, at least a pixel or two), its ends rounded into the cross joints. On a top or an underside, the column's end,
	// a shallow dome or dish of its own.
	float soft = max(0.012, 1.5 * fwidth(next_hit - hit));
	vec2 shown = mix(facet, next_facet, 0.5 * (1.0 - smoothstep(0.0, soft, next_hit - hit)));
	shown = shown * turn;
	vec3 facet_n = vec3(shown.x, 0.0, shown.y);
	float end_width = max(0.05, 2.0 * fwidth(y) * drum_height);
	float round_ends = 0.2 * (1.0 - smoothstep(0.0, end_width, to_top)) - 0.2 * (1.0 - smoothstep(0.0, end_width, to_bottom));
	float rounded = (across - 0.5) * 2.0 * COLUMN_ROUND;
	// Far off, where the arrises would crowd the joints into stripes, the facets flatten: each column reads whole.
	float far = smoothstep(0.02, 0.07, length(fwidth(p)));
	vec2 side_slope = vec2(dot(facet_n, t), dot(facet_n, b)) * column_facet * (1.0 - 0.3 * far) + vec2(rounded, round_ends);
	vec2 dome = ((l - site) * turn) * (hash12(cell + 2.3) < 0.5 ? CAP_DOME : -CAP_DOME);
	vec3 dome_n = vec3(dome.x, 0.0, dome.y);
	vec2 cap_slope = vec2(dot(dome_n, t), dot(dome_n, b));
	r.slope = (fine + mix(side_slope, cap_slope, cap)) * (detail ? 1.0 : 0.0);

	float side_joint = max(joint_at(to_side, COLUMN_JOINT), joint_at(min(to_top, to_bottom), DRUM_JOINT));
	float joint = mix(side_joint, joint_at(border * COLUMN_SPACING, COLUMN_JOINT), cap);
	float contrast = column_contrast * (detail ? 1.0 : 0.6);
	// A sliver of column between two wider ones stands back from the face, in their shadow.
	float set_back = mix(1.0 - 0.35 * (1.0 - smoothstep(0.15, 0.7, width)), 1.0, cap);
	r.tone = (surface.r * 2.0) * (1.0 + ((column_id - 0.5) + (id - 0.5) * 0.6) * 2.0 * contrast) * set_back + (warp - 0.5) * 0.15;
	r.cavity = max(surface.g, joint) * (detail ? 1.0 : 0.5);
	r.rough = surface.b;
	r.built = 0.0;
	return r;
}

// Layered strata on a face: `q` along and up it (meters).
Rock strata_at(vec2 q, float warp, float up) {
	Rock r;

	// The face in tiles of the set, the beds bent over a few meters.
	float scale = strata_frequency / STRATA_BEDS;
	vec2 m = vec2(q.x, q.y + (warp - 0.5) * 2.0 * strata_warp) * scale / STRATA_TILE;

	// Faults: columns of uneven width, their edges leaning and jagged; each column takes its own part of the tile along the
	// face, so no part shows twice side by side.
	float bend = (value_noise2(vec2(m.x * 0.12, m.y * 0.3) + 31.0) - 0.5) * 2.0 * FAULT_BEND;
	float lean = (value_noise2(vec2(m.x * 0.25, m.y * 0.35)) - 0.5) * 2.0 * FAULT_LEAN;
	float jag = (value_noise2(vec2(m.x * 2.0, m.y * 6.0)) - 0.5) * 2.0 * FAULT_JAG;
	float x = (m.x + m.y * FAULT_SHEAR + bend + lean + jag) / FAULT_PITCH;
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
	// None on what faces up, where it would read as paving. A soft groove with no hard core (joint_at).
	float opens = breaks ? 1.0 : 0.6 * smoothstep(0.45, 0.75, value_noise2(vec2(column * 3.7, m.y * 3.0)));
	float fault_joint = joint_at(to_fault, FAULT_JOINT) * opens * (1.0 - up);

	// Tone: the set's beds, at this material's contrast, over a broad drift. What faces up is a bedding plane, the beds faint.
	float contrast = strata_contrast / 0.2 * (detail ? 1.0 : 0.6) * (1.0 - 0.5 * up);
	r.slope *= 1.0 - 0.5 * up;
	r.tone = 1.0 + (surface.r * 2.0 - 1.0) * contrast + (warp - 0.5) * 0.35;
	r.cavity = max(surface.g, fault_joint * 0.8) * (detail ? 1.0 : 0.5);
	r.rough = surface.b;
	r.built = 0.0;
	return r;
}

Rock rock_at(vec3 p, vec3 t, vec3 b, vec3 n, float up) {
	vec2 q = vec2(dot(p, t), dot(p, b));
	float warp = fbm3(p * 0.3 + 11.0, 3);
	Rock r = columnar ? basalt_at(p, t, b, n, warp, up) : strata_at(q, warp, up);

	// Masonry: courses along a wall, flagstones on what faces up, over the strata where a block stands; where one is missing,
	// the rock behind shows, in its shadow.
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
	float stain = smoothstep(0.45, 0.8, value_noise2(vec2(q.x * 0.6, p.y * 0.06)));
	r.tone *= 1.0 - 0.14 * stain * (1.0 - up);
	if (fracture > 0.0) {
		// The web of fissures over whatever the rock is: two ways round, each in its own broad regions.
		vec2 uv = vec2(q.x, -q.y) / CRACKS_TILE;
		vec2 mirrored = vec2(-q.x, -q.y) / (CRACKS_TILE * CRACKS_MIRROR_SCALE) + CRACKS_MIRROR_OFFSET / CRACKS_TILE;
		float second = smoothstep(0.45, 0.55, value_noise2(q * CRACKS_REGION_FREQUENCY + 23.0));
		vec3 crack = mix(texture(cracks_surface, uv).rgb, texture(cracks_surface, mirrored).rgb, second);
		r.tone *= mix(1.0, crack.r * 2.0, fracture);
		r.cavity = max(r.cavity, crack.g * fracture * (detail ? 1.0 : 0.5));
		r.rough = mix(r.rough, crack.b, fracture);
		if (detail) {
			// The mirrored layer runs the other way along the face, so its slope along does too.
			vec2 slope = mix(texture(cracks_normal, uv).rg * 2.0 - 1.0, (texture(cracks_normal, mirrored).rg * 2.0 - 1.0) * vec2(-1.0, 1.0), second);
			r.slope = r.slope * (1.0 - 0.5 * fracture) + slope * fracture;
		}
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
	Rock r = rock_at(p, t, b, face_up_n, up);
	vec3 n = f.normal;
	float chipped = 0.0;
	if (detail) {
		// Worn edges: the band's width varies along the edge.
		float chip_noise = value_noise3(p * 3.1 + seed * 5.0);
		float width = wear_width * (0.4 + 1.2 * chip_noise);
		chipped = (1.0 - smoothstep(0.0, width * 1.6, f.edge)) * edge_wear;
		// The relief from the baked normals (tangent space: red along the face, green up it).
		vec2 slope = r.slope / max(1.0, length(r.slope));
		vec3 ts = vec3(slope, sqrt(max(0.0, 1.0 - dot(slope, slope))));
		n = normalize(f.tangent * ts.x + f.bitangent * ts.y + n * ts.z);
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

	// Each piece its own tone, except on masonry, whose courses run on across pieces.
	vec3 base = palette_color(slot) * mix(0.92 + 0.16 * seed, 1.0, r.built);
	vec3 albedo = base * r.tone;
	albedo = mix(albedo, base * 1.25, chipped * 0.6);

	// Silt settles in crevices and on whatever faces up; columns' joints are open cracks, dark, with ash only on their ledges.
	float settle = smoothstep(0.35, 0.9, world_n.y) * smoothstep(0.35, 0.65, fbm3(p * 1.7 + 4.0, 2));
	float crevice = columnar ? 0.0 : r.cavity * 0.8;
	float silt = detail ? clamp(max(crevice, settle) * silt_amount, 0.0, 1.0) : 0.0;
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
	Frost fr = frost_at(p, face_world_n, world_n, r.cavity, detail ? f.edge : 1e3, frost_share);
	albedo = mix(albedo, frost_color(r.cavity) * fr.tone, fr.cover);
	rough = mix(rough, fr.rough, fr.cover);
	n = detail ? frost_bump(n, fr, VIEW_MATRIX) : n;

	ALBEDO = albedo * shade;
	EMISSION = ALBEDO * caustics_at(v_world_pos, view_to_world(f.normal, INV_VIEW_MATRIX), TIME);
	ROUGHNESS = rough;
	METALLIC = 0.0;
	SPECULAR = mix(0.35, 0.5, wet);
	AO = 1.0 - r.cavity * 0.6;
	AO_LIGHT_AFFECT = 0.25;
	NORMAL = n;
	HAZE
	UNDERWATER
	SPECULAR *= uw_keep;
}
