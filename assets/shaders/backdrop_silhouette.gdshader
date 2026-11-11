// Far backdrop: a procedural skyline on a large quad facing +Z, standing far behind the play plane
// (its origin is the skyline's ground line; offset the QuadMesh up by half its height).
// Style 0, a drowned cathedral: bays between buttresses, tall pointed windows with a mullion and a
// rose, pinnacles on the buttresses and now and then a spire. Style 1, ancient server halls: towers of
// uneven height with sparse lit slits. Style 2, a foundry: smokestacks of uneven girth, their mouths glowing, smoke
// rising off them, over sawtooth-roofed sheds. Style 3, ancient machinery: domes, great rings standing on edge, masts.
// Style 4, ice crags: jagged frozen peaks with archive towers sunk in them. Openings show light from beyond (biome shaft color); the solid
// parts take the biome backdrop color sunk into the biome haze by `haze`, plus mist rising from below.
// Distance fog is done here (fog_disabled), so each plane keeps a readable value however far it stands.
// Above the skyline the quad is transparent, so planes layer with parallax.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_back, fog_disabled, shadows_disabled;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"
#include "res://assets/shaders/lib/water.gdshaderinc"

global uniform vec4 biome_haze;
global uniform vec4 biome_shaft;

uniform int style : hint_enum("Cathedral", "Server halls", "Foundry", "Machinery", "Crags") = 0;
uniform int seed = 0;
/// Width of one bay (cathedral) or tower (halls), meters.
uniform float bay_width : hint_range(1.0, 30.0) = 9.0;
/// Skyline height above the quad's bottom edge, meters, and its variation between bays.
uniform float height : hint_range(1.0, 100.0) = 30.0;
uniform float height_variation : hint_range(0.0, 1.0) = 0.3;
/// Share of the biome haze mixed into the silhouette (farther planes get more).
uniform float haze : hint_range(0.0, 1.0) = 0.5;
/// Mist rising from below: how high it reaches, meters, and how thick it is at the bottom.
uniform float mist_height : hint_range(0.0, 60.0) = 14.0;
uniform float mist : hint_range(0.0, 1.0) = 0.6;
/// Light through the openings.
uniform float window_light : hint_range(0.0, 4.0) = 0.6;
/// Share of the bays whose windows are lit.
uniform float lit_share : hint_range(0.0, 1.0) = 0.35;

varying vec2 v_local;

void vertex() {
	v_local = VERTEX.xy;
}

// Every edge is spread over a pixel, and a feature narrower than `MIN_PX` pixels is drawn that wide and fainter, so a skyline
// 70-90 m away holds still under a creeping camera (docs/art-direction.md#stable-image). `px` is the pixel's footprint in meters.
const float MIN_PX = 2.0;

// Coverage of `x < edge`, x and edge in meters.
float below(float x, float edge, float px) {
	return clamp((edge - x) / px + 0.5, 0.0, 1.0);
}

// Coverage of a band `half_width` either side of its center line, `d` meters off it.
float band(float d, float half_width, float px) {
	float drawn = max(half_width, MIN_PX * 0.5 * px);
	return below(d, drawn, px) * min(half_width / drawn, 1.0);
}

// Coverage of `f < 0` for an edge that is not in meters (a slope, a sum of shapes), from f's own rate across the pixel.
// Call it only where every pixel of the quad runs it (derivatives).
float under(float f) {
	return clamp(0.5 - f / max(fwidth(f), 1e-4), 0.0, 1.0);
}

// Inside a lancet (pointed) window of half width `hw`, standing on y = 0, springing at `spring`.
float lancet(vec2 p, float hw, float spring, float px) {
	float x = abs(p.x);
	float r = hw * 1.6;
	float jamb = below(x, hw, px) * step(p.y, spring);
	float arch = below(length(vec2(x - (hw - r), p.y - spring)), r, px) * step(spring, p.y);
	return max(jamb, arch) * below(-p.y, 0.0, px);
}

// 1 where solid, 0 in the sky; `opening` 1 in a window or slit.
void cathedral(vec2 p, float px, out float solid, out float opening, out float lit) {
	float s = float(seed);
	float bay = floor(p.x / bay_width);
	float fx = (fract(p.x / bay_width) - 0.5) * bay_width;
	float h = height * (1.0 - height_variation * hash12(vec2(bay, s)));
	float roof = h;
	// Pinnacles on the buttresses (bay edges) and a spire on some bays.
	float edge = 0.5 * bay_width - abs(fx);
	float pinnacle = max(0.0, 1.0 - edge / 0.45) * bay_width * 0.28;
	float spire_h = hash12(vec2(bay, s + 7.0)) > 0.78 ? height * 0.55 : 0.0;
	float spire = max(0.0, 1.0 - abs(fx) / 1.4) * spire_h;
	roof += max(pinnacle, spire);
	solid = under(p.y - roof);

	float hw = bay_width * 0.2;
	float sill = h * 0.18;
	float spring = h * 0.42;
	vec2 w = vec2(fx, p.y - sill);
	opening = lancet(w, hw, spring, px);
	// Tracery: a mullion and a rose in the head.
	float apex = spring + sqrt(max((hw * 1.6) * (hw * 1.6) - (hw * 0.6) * (hw * 0.6), 0.0));
	opening *= 1.0 - band(abs(fx), 0.1, px);
	float rose = abs(length(vec2(fx, p.y - sill - spring - hw * 0.55)) - hw * 0.42);
	opening *= 1.0 - band(rose, 0.08, px);
	opening *= below(p.y, sill + apex, px);
	lit = step(1.0 - lit_share, hash12(vec2(bay * 1.3, s + 3.0)));
}

void halls(vec2 p, float px, out float solid, out float opening, out float lit) {
	float s = float(seed);
	float tower = floor(p.x / bay_width);
	float fx = fract(p.x / bay_width) * bay_width;
	float gap = 0.25 + 0.6 * hash12(vec2(tower, s + 1.0));
	float h = height * (1.0 - height_variation * hash12(vec2(tower, s)));
	// The tower stands centered in its cell, half its gap either side, so both its sides fall inside the cell.
	float across = abs(fx - bay_width * 0.5);
	solid = below(p.y, h, px) * below(across, (bay_width - gap) * 0.5, px);
	// A walkway band joins the towers low down.
	solid = max(solid, band(abs(p.y - height * 0.22), 0.35, px));
	// Long lit bands: rows of rack bays still running, a few per tower.
	float row = floor(p.y / 1.6);
	float rack = band(abs(fract(p.y / 1.6) - 0.5) * 1.6, 0.1, px) * below(across, (bay_width - gap) * 0.5 - 0.4, px);
	opening = rack * step(0.82, hash12(vec2(row, tower * 7.0 + s))) * below(p.y, h - 1.0, px) * below(1.0, p.y, px);
	lit = 1.0;
}

// Smokestacks over sheds; `glow` 1 at a stack's mouth, `smoke` the plume's density above it.
void foundry(vec2 p, float px, out float solid, out float opening, out float lit, out float smoke) {
	float s = float(seed);
	float cell = floor(p.x / bay_width);
	float fx = (fract(p.x / bay_width) - 0.5) * bay_width;
	// Sheds: a sawtooth roof along the ground.
	float shed_h = height * 0.32;
	float tooth = fract(p.x / (bay_width * 0.5));
	float shed = under(p.y - shed_h - tooth * height * 0.07);
	// One stack per cell, of uneven girth and height, a little off center.
	float girth = bay_width * (0.07 + 0.08 * hash12(vec2(cell, s + 2.0)));
	float off = (hash12(vec2(cell, s + 5.0)) - 0.5) * bay_width * 0.4;
	float h = height * (1.0 - height_variation * hash12(vec2(cell, s)));
	float x = abs(fx - off);
	float taper = girth * (1.0 - 0.15 * p.y / max(h, 1.0));
	float iron = band(abs(fract(p.y / 4.0) - 0.5) * 4.0, 0.16, px) * 0.25; // the stacks' iron bands
	float stack = below(x, taper + iron, px) * below(p.y, h, px);
	solid = max(shed, stack);
	// Windows along the sheds, and the furnace light at each mouth.
	float win = band(abs(fract(p.x / 1.8) - 0.5) * 1.8, 0.32, px) * band(abs(p.y - shed_h * 0.55), shed_h * 0.12, px);
	float mouth = below(x, taper, px) * smoothstep(h - 1.6, h, p.y) * below(p.y, h, px);
	opening = max(win * (1.0 - stack), mouth);
	lit = max(mouth, step(1.0 - lit_share, hash12(vec2(floor(p.x / 1.8), s + 3.0))));
	// Smoke: a widening, drifting plume above the mouth.
	float rise = max(p.y - h, 0.0);
	float drift = rise * 0.25;
	float width = taper + rise * 0.28;
	float plume = (1.0 - smoothstep(width * 0.4, width, abs(fx - off - drift))) * below(h, p.y, px);
	plume *= (1.0 - smoothstep(0.0, height * 0.7, rise));
	plume *= 0.35 + 0.65 * fbm2(vec2(p.x * 0.12 - TIME * 0.05, p.y * 0.08 - TIME * 0.12), 4);
	smoke = plume * step(0.35, hash12(vec2(cell, s + 9.0)));
}

// Domes, rings on edge and masts: the machinery older than the foundry. Each cell's three shapes are all worked out and its kind
// picks one, so the edges' derivatives run on every pixel.
void machinery(vec2 p, float px, out float solid, out float opening, out float lit) {
	float s = float(seed);
	float cell = floor(p.x / bay_width);
	float fx = (fract(p.x / bay_width) - 0.5) * bay_width;
	float h = height * (1.0 - height_variation * hash12(vec2(cell, s)));
	float kind = hash12(vec2(cell, s + 4.0));
	float base = below(p.y, height * 0.22, px); // the base all of it stands on

	// A dome on a drum, a lit band round its drum.
	float r_dome = bay_width * 0.42;
	float drum = h * 0.45;
	float dome = max(below(abs(fx), r_dome, px) * below(p.y, drum, px),
			under(length(vec2(fx, max(p.y - drum, 0.0) * 1.2)) - r_dome) * step(drum, p.y));
	float dome_band = below(abs(fx), r_dome * 0.9, px) * band(abs(p.y - drum * 0.8), 0.25, px);

	// A ring standing on edge, six spokes of even width to its hub.
	float r = bay_width * 0.48;
	vec2 c = vec2(fx, p.y - (height * 0.22 + r));
	float d = length(c);
	float rim = band(abs(d - r * 0.92), r * 0.08, px);
	float a = atan(c.y, c.x);
	float off = (fract(a / 6.2831853 * 6.0 + hash12(vec2(cell, s))) - 0.5) * 6.2831853 / 6.0;
	float spokes = band(d * abs(sin(off)), r * 0.035, px) * below(d, r, px);
	float ring = max(rim, max(spokes, below(d, r * 0.12, px)));
	float hub = below(d, r * 0.06, px);

	// A mast with crossbars.
	float bars = band(abs(fract(p.y / 3.0) - 0.5) * 3.0, 0.15, px) * below(abs(fx), 1.2, px) * below(p.y, h, px);
	float mast = max(below(abs(fx), 0.35, px) * below(p.y, h * 1.2, px), bars);
	float lamp = below(abs(fx), 0.3, px) * band(abs(p.y - h * 1.2 + 0.4), 0.4, px);

	bool is_dome = kind < 0.4;
	bool is_ring = !is_dome && kind < 0.75;
	solid = max(base, is_dome ? dome : (is_ring ? ring : mast));
	opening = is_dome ? dome_band : (is_ring ? hub : lamp);
	lit = 1.0;
}

// Jagged frozen peaks, archive towers sunk in them.
void crags(vec2 p, float px, out float solid, out float opening, out float lit) {
	float s = float(seed);
	float ridge = height * (0.55 + 0.25 * fbm2(vec2(p.x * 0.025, s), 3));
	// Sharp facets: triangle waves of a few periods.
	ridge += height * 0.18 * abs(fract(p.x / (bay_width * 1.7) + s * 0.13) - 0.5) * 2.0;
	ridge += height * 0.08 * abs(fract(p.x / (bay_width * 0.6)) - 0.5) * 2.0;
	solid = under(p.y - ridge);
	// Towers standing out of the ice, slits lit here and there.
	float tower = floor(p.x / bay_width);
	float tx = fract(p.x / bay_width) * bay_width;
	float th = ridge * (0.5 + 0.6 * hash12(vec2(tower, s + 1.0)));
	float t = below(bay_width * 0.35, tx, px) * below(tx, bay_width * 0.55, px) * under(p.y - th)
			* step(0.5, hash12(vec2(tower, s + 2.0)));
	solid = max(solid, t);
	float slit = band(abs(fract(p.y / 2.2) - 0.5) * 2.2, 0.154, px) * below(bay_width * 0.39, tx, px) * below(tx, bay_width * 0.51, px);
	opening = t * slit * step(0.7, hash12(vec2(floor(p.y / 2.2), tower + s)));
	lit = 1.0;
}

void fragment() {
	// Meters on the quad, y = 0 at the origin: give the QuadMesh a center_offset so it stands on it.
	vec2 p = v_local;
	float px = max(max(length(dFdx(p)), length(dFdy(p))), 1e-4);

	float solid;
	float opening;
	float lit;
	float smoke = 0.0;
	if (style == 1) {
		halls(p, px, solid, opening, lit);
	} else if (style == 2) {
		foundry(p, px, solid, opening, lit, smoke);
	} else if (style == 3) {
		machinery(p, px, solid, opening, lit);
	} else if (style == 4) {
		crags(p, px, solid, opening, lit);
	} else {
		cathedral(p, px, solid, opening, lit);
	}

	vec3 haze_color = srgb_to_linear(biome_haze.rgb);
	vec3 body = mix(srgb_to_linear(biome_backdrop.rgb), haze_color, haze);
	float m = (1.0 - smoothstep(0.0, mist_height, p.y)) * mist;
	body = mix(body, haze_color * 1.15, m);
	vec3 light = srgb_to_linear(biome_shaft.rgb) * window_light * lit;
	vec3 color = mix(body, mix(body, light, 1.0 - m * 0.7), opening * lit);
	// Smoke is the haze thickened: it veils what stands behind and fades into the sky.
	color = mix(color, haze_color * 0.9, smoke * (1.0 - solid));
	// Seen through a room's water, it sinks into it (lib/water).
	vec3 uw_color;
	float uw_light;
	float uw_murk = underwater_at((INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz, CAMERA_POSITION_WORLD, uw_color, uw_light);
	ALBEDO = mix(color * uw_light, uw_color, uw_murk);
	ALPHA = max(solid, smoke * 0.7);
}
