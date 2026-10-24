// Far backdrop: a procedural skyline on a large quad facing +Z, standing far behind the play plane
// (its origin is the skyline's ground line; offset the QuadMesh up by half its height).
// Style 0, a drowned cathedral: bays between buttresses, tall pointed windows with a mullion and a
// rose, pinnacles on the buttresses and now and then a spire. Style 1, ancient server halls: towers of
// uneven height with sparse lit slits. Openings show light from beyond (biome shaft color); the solid
// parts take the biome backdrop color sunk into the biome haze by `haze`, plus mist rising from below.
// Distance fog is done here (fog_disabled), so each plane keeps a readable value however far it stands.
// Above the skyline the quad is transparent, so planes layer with parallax.
shader_type spatial;
render_mode unshaded, blend_mix, depth_draw_never, cull_back, fog_disabled, shadows_disabled;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"

global uniform vec4 biome_haze;
global uniform vec4 biome_shaft;

uniform int style : hint_enum("Cathedral", "Server halls") = 0;
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

// Inside a lancet (pointed) window of half width `hw`, standing on y = 0, springing at `spring`.
float lancet(vec2 p, float hw, float spring) {
	if (p.y < 0.0) {
		return 0.0;
	}
	float x = abs(p.x);
	if (p.y < spring) {
		return step(x, hw);
	}
	float r = hw * 1.6;
	return step(length(vec2(x - (hw - r), p.y - spring)), r);
}

// 1 where solid, 0 in the sky; `opening` 1 in a window or slit.
void cathedral(vec2 p, out float solid, out float opening, out float lit) {
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
	solid = step(p.y, roof);

	float hw = bay_width * 0.2;
	float sill = h * 0.18;
	float spring = h * 0.42;
	vec2 w = vec2(fx, p.y - sill);
	opening = lancet(w, hw, spring);
	// Tracery: a mullion and a rose in the head.
	float apex = spring + sqrt(max((hw * 1.6) * (hw * 1.6) - (hw * 0.6) * (hw * 0.6), 0.0));
	opening *= step(0.1, abs(fx));
	float rose = abs(length(vec2(fx, p.y - sill - spring - hw * 0.55)) - hw * 0.42);
	opening *= step(0.08, rose);
	opening *= step(p.y, sill + apex);
	lit = step(1.0 - lit_share, hash12(vec2(bay * 1.3, s + 3.0)));
}

void halls(vec2 p, out float solid, out float opening, out float lit) {
	float s = float(seed);
	float tower = floor(p.x / bay_width);
	float fx = fract(p.x / bay_width) * bay_width;
	float gap = 0.25 + 0.6 * hash12(vec2(tower, s + 1.0));
	float h = height * (1.0 - height_variation * hash12(vec2(tower, s)));
	solid = step(p.y, h) * step(gap, fx);
	// A walkway band joins the towers low down.
	solid = max(solid, step(abs(p.y - height * 0.22), 0.35));
	// Long lit bands: rows of rack bays still running, a few per tower.
	float row = floor(p.y / 1.6);
	float band = step(abs(fract(p.y / 1.6) - 0.5), 0.06) * step(gap + 0.4, fx) * step(fx, bay_width - 0.4);
	opening = band * step(0.82, hash12(vec2(row, tower * 7.0 + s))) * step(p.y, h - 1.0) * step(1.0, p.y);
	lit = 1.0;
}

void fragment() {
	// Meters on the quad, y = 0 at the origin: give the QuadMesh a center_offset so it stands on it.
	vec2 p = v_local;

	float solid;
	float opening;
	float lit;
	if (style == 1) {
		halls(p, solid, opening, lit);
	} else {
		cathedral(p, solid, opening, lit);
	}

	vec3 haze_color = srgb_to_linear(biome_haze.rgb);
	vec3 body = mix(srgb_to_linear(biome_backdrop.rgb), haze_color, haze);
	float m = (1.0 - smoothstep(0.0, mist_height, p.y)) * mist;
	body = mix(body, haze_color * 1.15, m);
	vec3 light = srgb_to_linear(biome_shaft.rgb) * window_light * lit;
	vec3 color = mix(body, mix(body, light, 1.0 - m * 0.7), opening * lit);
	ALBEDO = color;
	ALPHA = solid;
}
