// Metal: the pale, glossy "ceramic-metal" of the art direction. Each patch is cut into panels sized to
// fit it, with seams, a crisp panel bevel and rivets; rust gathers in the creases and along the bottom,
// wet streaks run down from the seams, the coating chips at the edges. `corrosion` turns it into
// rust. Glossy and cool, so it never reads as rock (mag-grip). Pattern on the surface frame (lib/surface),
// so panels fit each box face or pipe and follow its edges.
shader_type spatial;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"
#include "res://assets/shaders/lib/surface.gdshaderinc"
#include "res://assets/shaders/lib/water.gdshaderinc"

uniform int slot : hint_enum("Rock", "Metal", "Backdrop", "Backdrop accent", "Foreground") = 1;

group_uniforms panels;
/// Target panel size in meters (across, up); each face fits a whole number of panels.
uniform vec2 panel_size = vec2(1.7, 2.1);
uniform float seam_width : hint_range(0.0, 0.08) = 0.022;
/// Width of the raised panel bevel next to each seam, meters.
uniform float panel_bevel : hint_range(0.0, 0.2) = 0.05;
uniform float rivet_radius : hint_range(0.0, 0.08) = 0.028;
/// Rivet distance from the panel corner, meters.
uniform float rivet_inset : hint_range(0.0, 0.3) = 0.1;
/// Share of panels with rivets.
uniform float rivet_share : hint_range(0.0, 1.0) = 0.45;

group_uniforms wear;
/// 0 = clean coating, 1 = mostly rust.
uniform float corrosion : hint_range(0.0, 1.0) = 0.25;
/// Width of chipped coating along the edges, meters.
uniform float edge_chip : hint_range(0.0, 0.3) = 0.07;
uniform float streaks : hint_range(0.0, 1.0) = 0.5;
uniform float grime_height : hint_range(0.0, 4.0) = 1.0;
uniform float grime_strength : hint_range(0.0, 1.0) = 0.35;

group_uniforms coating;
uniform float roughness : hint_range(0.0, 1.0) = 0.32;
uniform float metallic : hint_range(0.0, 1.0) = 0.25;
uniform float clearcoat : hint_range(0.0, 1.0) = 0.6;
uniform float rim : hint_range(0.0, 1.0) = 0.25;
/// Albedo multiplier. Below 1 pushes a piece back behind the play plane (midground), so grippable
/// metal on the play plane stays the brightest metal on screen.
uniform float shade : hint_range(0.0, 1.0) = 1.0;
group_uniforms;

struct Panels {
	float height;    // meters, relative
	float seam;      // 1 inside a seam
	float crease;    // 1 close to a seam or rivet: where rust and dirt gather
	float rivet;     // 1 on a rivet head
	float below_top; // meters below the top seam of this panel
	float panel;     // random value per panel
};

Panels panels_at(vec2 uv, vec2 size, float seed, float aa) {
	Panels pn;
	vec2 target = panel_size * (0.8 + 0.4 * seed);
	vec2 count = max(vec2(1.0), round(size / target));
	vec2 cell = size / count;
	vec2 id = clamp(floor(uv / cell), vec2(0.0), count - 1.0);
	vec2 local = uv - id * cell;
	pn.panel = hash12(id + seed * 17.0);

	// Some panels are split in two along their longer side.
	if (pn.panel < 0.3 && max(cell.x, cell.y) > 0.6) {
		if (cell.x >= cell.y) {
			cell.x *= 0.5;
			local.x = mod(local.x, cell.x);
		} else {
			cell.y *= 0.5;
			local.y = mod(local.y, cell.y);
		}
	}

	vec2 to_seam2 = min(local, cell - local);
	float to_seam = min(to_seam2.x, to_seam2.y);
	pn.seam = 1.0 - smoothstep(seam_width * 0.5 - aa, seam_width * 0.5 + aa, to_seam);
	float rise = smoothstep(seam_width * 0.5, seam_width * 0.5 + panel_bevel, to_seam);
	pn.height = rise * 0.012 - pn.seam * 0.008;

	vec2 corner = vec2(local.x < cell.x * 0.5 ? rivet_inset : cell.x - rivet_inset,
			local.y < cell.y * 0.5 ? rivet_inset : cell.y - rivet_inset);
	float d = length(local - corner);
	bool has_rivets = min(cell.x, cell.y) > rivet_inset * 3.0 && fract(pn.panel * 13.7) < rivet_share;
	pn.rivet = has_rivets ? 1.0 - smoothstep(rivet_radius - aa, rivet_radius + aa, d) : 0.0;
	pn.height += has_rivets ? sqrt(max(0.0, rivet_radius * rivet_radius - d * d)) * 0.5 : 0.0;

	pn.crease = max(1.0 - smoothstep(0.0, 0.14, to_seam), has_rivets ? 1.0 - smoothstep(0.0, 0.06, d) : 0.0);
	pn.below_top = cell.y - local.y;
	return pn;
}

void vertex() {
	SURFACE_VERTEX
}

void fragment() {
	Surface f = SURFACE;
	float seed = v_seed;
	vec3 p = v_world_pos;
	bool side = abs(view_to_world(f.normal, INV_VIEW_MATRIX).y) < 0.5;
	float aa = max(fwidth(f.uv.x), fwidth(f.uv.y)) * 0.75;

	Panels pn = panels_at(f.uv, f.size, seed, aa);
	const float e = 0.004;
	float hx = panels_at(f.uv + vec2(e, 0.0), f.size, seed, aa).height;
	float hy = panels_at(f.uv + vec2(0.0, e), f.size, seed, aa).height;
	vec3 n = surface_bump(f, f.normal, vec2(hx - pn.height, hy - pn.height) / e);

	vec3 coat = palette_color(slot) * (0.94 + 0.12 * pn.panel) * (0.96 + 0.08 * seed);
	float blotch = fbm3(p * 0.7 + seed * 9.0, 3);

	// Rust: in creases, near the bottom, and in blotches as corrosion grows.
	float grime = side ? 1.0 - smoothstep(0.0, grime_height, f.height + (blotch - 0.5) * 0.6) : 0.0;
	float rust_field = max(pn.crease * 0.75, grime * 0.7) + (blotch - 0.5) * 0.9 + corrosion * 1.1 - 0.75;
	// Rust also runs down from the creases.
	float drip = side ? smoothstep(0.5, 0.8, fbm2(vec2(f.uv.x * 14.0 + seed * 7.0, f.uv.y * 0.6), 2)) * exp(-pn.below_top / 0.35) : 0.0;
	rust_field = max(rust_field, drip * corrosion - 0.1);
	float rust = smoothstep(0.0, 0.25, rust_field) * (0.35 + 0.65 * corrosion + 0.3 * pn.crease);
	rust = clamp(rust, 0.0, 1.0);
	vec3 rust_color = srgb_to_linear(biome_rust.rgb) * (0.75 + 0.5 * fbm3(p * 6.0, 2));

	// Coating chipped along the edges shows bare, darker metal.
	float chip = 1.0 - smoothstep(0.0, edge_chip * (0.3 + 1.4 * value_noise3(p * 4.0 + seed * 3.0)), f.edge);
	chip = step(0.5, chip);

	// Wet streaks run down from each panel's top seam (sides only).
	float streak = 0.0;
	if (side) {
		float lines = fbm2(vec2(f.uv.x * 9.0 + seed * 31.0, f.uv.y * 0.25), 3);
		streak = smoothstep(0.55, 0.75, lines) * exp(-pn.below_top / 0.7) * streaks;
	}

	vec3 albedo = coat;
	float rough = roughness + (blotch - 0.5) * 0.08;
	float metal = metallic;
	float coat_amount = clearcoat;

	albedo = mix(albedo, coat * 0.32, chip);
	metal = mix(metal, 0.9, chip);
	rough = mix(rough, 0.4, chip);
	coat_amount *= 1.0 - chip;

	albedo *= 1.0 - streak * 0.25;
	rough = mix(rough, 0.12, streak);

	albedo *= 1.0 - grime * grime_strength;
	albedo = mix(albedo, rust_color, rust);
	rough = mix(rough, 0.85, rust);
	metal = mix(metal, 0.0, rust);
	coat_amount *= 1.0 - rust;

	albedo *= 1.0 - pn.seam * 0.65;
	albedo = mix(albedo, coat * 1.08, pn.rivet * (1.0 - rust));

	ALBEDO = albedo * shade;
	EMISSION = ALBEDO * caustics_at(v_world_pos, view_to_world(f.normal, INV_VIEW_MATRIX), TIME);
	ROUGHNESS = clamp(rough, 0.05, 1.0);
	METALLIC = metal;
	CLEARCOAT = coat_amount;
	CLEARCOAT_ROUGHNESS = 0.15;
	RIM = rim * (1.0 - rust);
	RIM_TINT = 0.6;
	AO = 1.0 - pn.seam * 0.7 - pn.crease * 0.15;
	AO_LIGHT_AFFECT = 0.2;
	NORMAL = n;
}
