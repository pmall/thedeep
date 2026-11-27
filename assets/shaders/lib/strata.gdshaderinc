// Layered strata sampling for rock.gdshader and cracked.gdshader: warped beds cut into faulted columns so the baked
// strata set never repeats along a wall. Needs lib/rock.gdshaderinc included first.

group_uniforms strata;
/// The baked strata set (assets/textures/surfaces/, SurfaceMaps): every strata rock material sets both.
uniform sampler2D strata_normal : hint_normal, repeat_enable, filter_linear_mipmap_anisotropic;
uniform sampler2D strata_surface : hint_default_black, repeat_enable, filter_linear_mipmap_anisotropic;
/// Beds per meter of height: the set is scaled to it (at its own scale it has STRATA_BEDS).
uniform float strata_frequency : hint_range(0.1, 4.0) = 1.5;
/// How far the beds bend up and down over a few meters, meters.
uniform float strata_warp : hint_range(0.0, 2.0) = 0.4;
/// Tone difference between beds (the set's own is 0.2).
uniform float strata_contrast : hint_range(0.0, 0.5) = 0.2;
group_uniforms;

// The baked set's tile (StrataSurface.TileMeters) and its beds per meter (StrataSurface.Beds over it).
const float STRATA_TILE = 4.0;
const float STRATA_BEDS = 1.5;
// Faults: the columns' mean width, in tiles (under one, so a column seldom shows the same part of the tile twice), how far
// their edges stray from even spacing (a share of that width), the lean every edge shares, how far a broad bend of the
// faults reaches (tiles), a finer lean and how far an edge jags (tiles). Columns come in blocks of FAULT_BLOCK: inside a
// block the beds run on, dropping a little at each fault (up to FAULT_THROW tiles); between blocks they break.
const float FAULT_PITCH = 0.85;
const float FAULT_JITTER = 0.3;
const float FAULT_SHEAR = 0.12;
const float FAULT_BEND = 1.3;
const float FAULT_LEAN = 0.2;
const float FAULT_JAG = 0.012;
const float FAULT_BLOCK = 3.0;
const float FAULT_THROW = 0.05;
const float FAULT_JOINT = 0.07;

// Where a fault edge between columns lies (in column widths), `k` the edge's index.
float fault_edge(float k) {
	return k + (hash12(vec2(k, 7.3)) - 0.5) * 2.0 * FAULT_JITTER;
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
