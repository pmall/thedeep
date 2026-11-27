// Columnar basalt: upright hexagonal prisms side by side on walls, their polygonal ends on tops and undersides, cut by
// ball-and-socket cross joints; shared rock masonry, wetness, frost, dirt and drawn-look ink live in lib/rock.gdshaderinc.
shader_type spatial;

#include "res://assets/shaders/lib/rock.gdshaderinc"

group_uniforms columns;
/// The baked basalt set (assets/textures/surfaces/, SurfaceMaps): the face of a column, set by every columnar material.
uniform sampler2D basalt_normal : hint_normal, repeat_enable, filter_linear_mipmap_anisotropic;
uniform sampler2D basalt_surface : hint_default_black, repeat_enable, filter_linear_mipmap_anisotropic;
/// Tone difference between columns (and, a little, between the pieces of one).
uniform float column_contrast : hint_range(0.0, 0.3) = 0.14;
/// How much of its facets' turn from the face a prism shows (1 the true prism): the light picks out each column.
uniform float column_facet : hint_range(0.0, 0.6) = 0.12;
group_uniforms;

// The baked basalt set's tile (BasaltSurface.TileMeters) and hexagonal column net geometry.
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

// The span of an uneven lattice (edge k at k, strayed by up to `jitter` either way) that holds `x`: its index and its edges.
vec3 uneven_span(float x, float seed, float jitter) {
	float cell = floor(x);
	float edge = cell + (hash12(vec2(cell, seed)) - 0.5) * 2.0 * jitter;
	if (x < edge) {
		return vec3(cell - 1.0, cell - 1.0 + (hash12(vec2(cell - 1.0, seed)) - 0.5) * 2.0 * jitter, edge);
	}
	return vec3(cell, edge, cell + 1.0 + (hash12(vec2(cell + 1.0, seed)) - 0.5) * 2.0 * jitter);
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

	// Its borders with the columns round it.
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

	// On a wall, the prism facets blended across the arris; on a top or underside, the column's end.
	float soft = max(0.012, 1.5 * fwidth(next_hit - hit));
	vec2 shown = mix(facet, next_facet, 0.5 * (1.0 - smoothstep(0.0, soft, next_hit - hit)));
	shown = shown * turn;
	vec3 facet_n = vec3(shown.x, 0.0, shown.y);
	float end_width = max(0.05, 2.0 * fwidth(y) * drum_height);
	float round_ends = 0.2 * (1.0 - smoothstep(0.0, end_width, to_top)) - 0.2 * (1.0 - smoothstep(0.0, end_width, to_bottom));
	float rounded = (across - 0.5) * 2.0 * COLUMN_ROUND;
	float far = smoothstep(0.02, 0.07, length(fwidth(p)));
	vec2 side_slope = vec2(dot(facet_n, t), dot(facet_n, b)) * column_facet * (1.0 - 0.3 * far) + vec2(rounded, round_ends);
	vec2 dome = ((l - site) * turn) * (hash12(cell + 2.3) < 0.5 ? CAP_DOME : -CAP_DOME);
	vec3 dome_n = vec3(dome.x, 0.0, dome.y);
	vec2 cap_slope = vec2(dot(dome_n, t), dot(dome_n, b));
	r.slope = (fine + mix(side_slope, cap_slope, cap)) * (detail ? 1.0 : 0.0);

	float side_joint = max(joint_at(to_side, COLUMN_JOINT), joint_at(min(to_top, to_bottom), DRUM_JOINT));
	float joint = mix(side_joint, joint_at(border * COLUMN_SPACING, COLUMN_JOINT), cap);
	float contrast = column_contrast * (detail ? 1.0 : 0.6);
	float set_back = mix(1.0 - 0.35 * (1.0 - smoothstep(0.15, 0.7, width)), 1.0, cap);
	r.tone = (surface.r * 2.0) * (1.0 + ((column_id - 0.5) + (id - 0.5) * 0.6) * 2.0 * contrast) * set_back + (warp - 0.5) * 0.15;
	r.cavity = max(surface.g, joint) * (detail ? 1.0 : 0.5);
	r.rough = surface.b;
	r.built = 0.0;
	return r;
}

void fragment() {
	Surface f = SURFACE;
	vec3 t = view_to_world(f.tangent, INV_VIEW_MATRIX);
	vec3 b = view_to_world(f.bitangent, INV_VIEW_MATRIX);
	vec3 p = v_world_pos;
	float seed = v_seed;
	vec3 face_world_n = view_to_world(f.normal, INV_VIEW_MATRIX);
	float up = abs(face_world_n.y);
	vec2 q = vec2(dot(p, t), dot(p, b));
	float warp = fbm3(p * 0.3 + 11.0, 3);
	Rock r = rock_overlay(basalt_at(p, t, b, face_world_n, warp, up), q, p.y, warp, up);
	ROCK_FINISH(r, 0.0)
}
