// Water in the room and the biome's water colors. A WaterBody pushes its surface height, floor, area and murk into
// the `water_*` globals (none in the room: the level sits far below, so nothing draws); BiomeProfile
// pushes the colors. Rock and metal include this to draw caustics on surfaces under and just above water;
// every material of the room calls UNDERWATER (or underwater_at) to sink into the water what lies in it.
// Needs noise.gdshaderinc and palette.gdshaderinc first.

global uniform float water_level;
global uniform float water_floor;
// How thick this body's water is (WaterBody.Murk): scales its murk and how fast the room's light fades in it.
global uniform float water_murk;
// World X/Z rectangle of the water: x min, z min, x max, z max.
global uniform vec4 water_area;
// The water's tint: what its surface takes where it reflects, and its section's color near the surface.
global uniform vec4 biome_water;
// The water's own color with depth (absorption, underwater haze).
global uniform vec4 biome_water_deep;
global uniform vec4 biome_caustic;
global uniform float biome_caustic_strength;

// The water's murk, worked out on each surface from the water's known box, so nothing reads the screen
// (docs/art-direction.md#stable-image). Two parts, both scaled by the body's `water_murk`:
// - light: a surface under water gets less of the room's light the deeper it lies: UNDERWATER_LIGHT_STEP less just under
//   the surface, down to UNDERWATER_LIGHT_FLOOR (underwater_light);
// - murk: the water the view crosses to reach it veils it in the water's color, thicker with depth down to
//   UNDERWATER_DEPTH_CAP and never past UNDERWATER_PATH_MAX however long the path (so the far walls of a deep room still
//   show their structure), its color from the biome's water tint toward its deep color (UNDERWATER_SHALLOWEST of the way
//   at the surface, UNDERWATER_DEEPEST far down). The water in front of the play plane adds its own murk, UNDERWATER_FRONT_TOP
//   just under the surface and growing with depth up to UNDERWATER_FRONT, so the play plane's own faces read darker and
//   bluer from the surface line down. Only water entered through the box's front or sides counts: the view through the
//   top crosses the surface, whose own opacity already sinks it.
// The murk darkens rather than replaces: a surface keeps at least UNDERWATER_KEEP_MIN of its own light and the veil
// covers at most the rest, so deep walls keep their blotches, joints and shadows under a dark veil instead of a flat color.
// A surface beyond the box seen through the water (a backdrop behind the lake) takes the light at the mid-depth of the
// water the view crosses. Its emission (a backdrop's haze, caustics) dims with that light and the murk, with no
// UNDERWATER_KEEP_MIN floor (the floor keeps lit structure, which a flat haze has none of), so a pale haze doesn't shine
// through the lake. A gameplay glow (a machine's eye, its glow pattern: docs/art-direction.md#look) is its own light, not
// the room's: UNDERWATER_GLOW dims it only by the murk the view crosses, so it stays readable at any depth.
// So the play plane reads clearly near the surface and sinks into murk deeper down, and walls and backdrops farther
// behind it sink further.
const float UNDERWATER_MURK = 0.025;
const float UNDERWATER_MURK_PER_DEPTH = 0.004;
const float UNDERWATER_DEPTH_CAP = 6.0;
const float UNDERWATER_PATH_MAX = 0.5;
const float UNDERWATER_FRONT_TOP = 0.3;
const float UNDERWATER_FRONT = 0.35;
const float UNDERWATER_FRONT_FALL = 0.3;
const float UNDERWATER_DARKEN = 0.35;
const float UNDERWATER_SHALLOWEST = 0.5;
const float UNDERWATER_DEEPEST = 1.0;
const float UNDERWATER_KEEP_MIN = 0.35;
const float UNDERWATER_LIGHT_STEP = 0.25;
const float UNDERWATER_LIGHT_FALL = 0.5;
const float UNDERWATER_LIGHT_FLOOR = 0.3;

// The share of the room's light that reaches `depth` meters under the surface (1 above it).
float underwater_light(float depth) {
	float under = smoothstep(0.0, 0.05, depth);
	float loss = UNDERWATER_LIGHT_STEP + (1.0 - UNDERWATER_LIGHT_FLOOR - UNDERWATER_LIGHT_STEP) * (1.0 - exp(-depth * UNDERWATER_LIGHT_FALL * water_murk));
	return 1.0 - under * min(loss * min(water_murk, 1.0), 1.0 - UNDERWATER_LIGHT_FLOOR);
}

// How fast caustic light fades with depth, per meter.
const float CAUSTIC_FALL = 1.0;

// Moving web of bright filaments: two drifting layers of Voronoi borders, warped by slow noise so the
// cells never look like a net.
float caustic_pattern(vec2 p, float t) {
	vec2 warp = vec2(value_noise2(p * 0.6 + vec2(t * 0.11, 1.3)), value_noise2(p * 0.6 + vec2(5.2, -t * 0.09))) - 0.5;
	p += warp * 1.1;
	float a = voronoi_border(p * 0.8 + vec2(t * 0.07, t * 0.04)).x;
	float b = voronoi_border(p * 1.15 + vec2(-t * 0.05, t * 0.08) + 3.7).x;
	float la = 1.0 - smoothstep(0.0, 0.07, a);
	float lb = 1.0 - smoothstep(0.0, 0.07, b);
	return la * la * 0.8 + lb * lb * 0.5;
}

// Caustic light (linear color, add as emission times albedo) at world point `p` with world normal `n`:
// strongest just under the surface and fading fast with depth, plus light reflected onto walls up to 2.5 m above the
// water. Added as emission, it then loses the room's light under water with the rest of the emission (UNDERWATER).
vec3 caustics_at(vec3 p, vec3 n, float t) {
	if (biome_caustic_strength <= 0.0) {
		return vec3(0.0);
	}
	float depth = water_level - p.y;
	if (depth < -2.5 || depth > 12.0) {
		return vec3(0.0);
	}
	vec2 margin = vec2(0.75);
	vec2 inside = step(water_area.xy - margin, p.xz) * step(p.xz, water_area.zw + margin);
	if (inside.x * inside.y == 0.0) {
		return vec3(0.0);
	}
	float under = smoothstep(-0.05, 0.15, depth) * exp(-max(depth, 0.0) * CAUSTIC_FALL);
	float above = (1.0 - smoothstep(0.0, 2.5, -depth)) * step(depth, 0.0) * (1.0 - abs(n.y)) * 0.5;
	// Floors take the pattern from above; walls along their own plane, squashed into wavering bands.
	vec2 q = abs(n.y) > 0.5 ? p.xz : vec2(p.x + p.z, p.y * 2.5);
	return srgb_to_linear(biome_caustic.rgb) * biome_caustic_strength * (under + above) * caustic_pattern(q, t);
}

// How much of the surface at world point `p`, seen from `eye`, the water hides (returned), the color it takes
// instead (linear), and the share of the room's light that still reaches `p`.
float underwater_at(vec3 p, vec3 eye, out vec3 murk_color, out float light) {
	vec3 lo = vec3(water_area.x, water_floor, water_area.y);
	vec3 hi = vec3(water_area.z, water_level, water_area.w);
	vec3 in_box = step(lo - 0.05, p) * step(p, hi + 0.05);
	float depth = max(water_level - p.y, 0.0);
	float inside = in_box.x * in_box.y * in_box.z;

	// The part of the ray from the eye to `p` inside the box (slabs), unless it enters through the top.
	vec3 d = p - eye;
	vec3 safe = d + vec3(1e-5) * (1.0 - abs(sign(d)));
	vec3 t_lo = (lo - eye) / safe;
	vec3 t_hi = (hi - eye) / safe;
	vec3 t_near = min(t_lo, t_hi);
	vec3 t_far = max(t_lo, t_hi);
	float t0 = max(max(t_near.x, t_near.z), max(t_near.y, 0.0));
	float t1 = min(min(t_far.x, t_far.z), min(t_far.y, 1.0));
	bool through_top = eye.y > water_level && t_near.y >= max(t_near.x, t_near.z);
	bool crossed = !(through_top || hi.y <= lo.y || t1 <= t0);
	float path = crossed ? (t1 - t0) * length(d) : 0.0;
	float mid_depth = max(water_level - (eye.y + d.y * 0.5 * (t0 + t1)), 0.0);
	float front_grow = UNDERWATER_FRONT_TOP + (UNDERWATER_FRONT - UNDERWATER_FRONT_TOP) * (1.0 - exp(-depth * UNDERWATER_FRONT_FALL));
	float front = crossed ? front_grow * smoothstep(0.0, 0.05, depth) * water_murk : 0.0;
	// A point beyond the box seen through the water takes the light of the water the view crosses, at its mid-depth.
	light = mix(crossed ? underwater_light(mid_depth) : 1.0, underwater_light(depth), inside);

	vec3 tint = srgb_to_linear(biome_water.rgb);
	vec3 deep = srgb_to_linear(biome_water_deep.rgb);
	float darken = mix(UNDERWATER_SHALLOWEST, UNDERWATER_DEEPEST, 1.0 - exp(-mid_depth * UNDERWATER_DARKEN));
	murk_color = mix(tint, deep, darken);
	float density = (UNDERWATER_MURK + UNDERWATER_MURK_PER_DEPTH * min(mid_depth, UNDERWATER_DEPTH_CAP)) * water_murk;
	float along = UNDERWATER_PATH_MAX * (1.0 - exp(-path * density / UNDERWATER_PATH_MAX));
	return 1.0 - (1.0 - along) * exp(-front);
}

// The waterline: where a surface meets the water's surface, a thin pale line (the meniscus catching the light) over a
// band a little darker just under it, so the surface reads at a glance on every pier, wall and machine that stands in the
// water. Only on faces that water touches: the face's own normal (from the position's derivatives, so a bumped normal
// doesn't break the line) points into the box, so a block's front face on the box's front edge takes none. Its width
// never falls under WATERLINE_PIXELS on screen and its edges are smoothed, so it holds still under TAA
// (docs/art-direction.md#stable-image).
const float WATERLINE_WIDTH = 0.05;
const float WATERLINE_PIXELS = 1.5;
const float WATERLINE_STRENGTH = 1.5;
const float WATERLINE_SHADOW = 0.25;
const float WATERLINE_SHADOW_DEPTH = 0.12;

// The waterline's light at world point `p` seen from `eye` (linear, add to EMISSION), and in `shade` how much darker the
// band just under it is (multiply ALBEDO by 1 - shade).
vec3 waterline_at(vec3 p, vec3 eye, out float shade) {
	vec3 face = normalize(cross(dFdx(p), dFdy(p)));
	face *= sign(dot(face, eye - p));
	vec3 q = p + face * 0.1;
	vec3 lo = vec3(water_area.x, water_floor, water_area.y);
	vec3 hi = vec3(water_area.z, water_level + 1.0, water_area.w);
	vec3 in_box = step(lo, q) * step(q, hi);
	float touches = in_box.x * in_box.y * in_box.z * (1.0 - smoothstep(0.6, 0.9, abs(face.y)));
	float h = p.y - water_level;
	float width = max(WATERLINE_WIDTH, WATERLINE_PIXELS * fwidth(p.y));
	float line = 1.0 - smoothstep(0.0, width, abs(h));
	float under = max(width, WATERLINE_SHADOW_DEPTH);
	shade = touches * WATERLINE_SHADOW * smoothstep(0.0, width, -h) * (1.0 - smoothstep(under, 2.0 * under, -h));
	return srgb_to_linear(biome_caustic.rgb) * WATERLINE_STRENGTH * line * touches;
}

// At the end of fragment(), after ALBEDO and EMISSION (and HAZE): sinks the surface into the water and draws its
// waterline. Leaves `uw_keep`, the share of its own light the surface keeps, for the shader to scale what else it lights
// (SPECULAR, CLEARCOAT, RIM).
#define UNDERWATER \
	vec3 uw_color; \
	float uw_light; \
	vec3 uw_p = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz; \
	float uw_murk = underwater_at(uw_p, CAMERA_POSITION_WORLD, uw_color, uw_light); \
	float uw_shade; \
	vec3 uw_line = waterline_at(uw_p, CAMERA_POSITION_WORLD, uw_shade); \
	float uw_keep = max(uw_light * (1.0 - uw_murk), UNDERWATER_KEEP_MIN) * (1.0 - uw_shade); \
	ALBEDO *= uw_keep; \
	EMISSION = EMISSION * uw_light * (1.0 - uw_murk) + uw_color * min(uw_murk, 1.0 - UNDERWATER_KEEP_MIN) + uw_line;

// UNDERWATER for a surface that also gives a gameplay glow (`glow`, linear, kept out of EMISSION until here): the surface's
// own emission sinks as in UNDERWATER; the glow is veiled by the murk alone and never loses the room's light.
#define UNDERWATER_GLOW(glow) \
	UNDERWATER \
	EMISSION += (glow) * (1.0 - uw_murk);
