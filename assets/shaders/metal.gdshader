// Metal: the pale, glossy "ceramic-metal" of the art direction. Each patch is cut into panels sized to
// fit it; their seams, bevels, rivets and corner plates come from the baked panel set and the worn
// coating (scratches, dents, smudges, the runs down a wall) from the baked coating set (SurfaceMaps has
// the sampling contract), so the fine detail stays still at any distance. Rust gathers in the creases
// and along the bottom and drips from the seams, wet streaks run down from them, the coating chips at
// the edges. `corrosion` turns it into rust. Glossy and cool, so it never reads as rock (mag-grip).
// Pattern on the surface frame (lib/surface), so panels fit each box face or pipe and follow its edges.
shader_type spatial;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"
#include "res://assets/shaders/lib/haze.gdshaderinc"
#include "res://assets/shaders/lib/surface.gdshaderinc"
#include "res://assets/shaders/lib/water.gdshaderinc"
#include "res://assets/shaders/lib/frost.gdshaderinc"

uniform int slot : hint_enum("Rock", "Metal", "Backdrop", "Backdrop accent", "Foreground") = 1;

group_uniforms panels;
/// Target panel size in meters (across, up); each face fits a whole number of panels.
uniform vec2 panel_size = vec2(1.7, 2.1);
/// Share of panels with rivets.
uniform float rivet_share : hint_range(0.0, 1.0) = 0.45;
/// The baked panel and coating sets (assets/textures/surfaces/, SurfaceMaps): every metal material sets all four.
uniform sampler2D panels_normal : hint_normal, repeat_enable, filter_linear_mipmap_anisotropic;
uniform sampler2D panels_surface : hint_default_black, repeat_enable, filter_linear_mipmap_anisotropic;
uniform sampler2D coating_normal : hint_normal, repeat_enable, filter_linear_mipmap_anisotropic;
uniform sampler2D coating_surface : hint_default_black, repeat_enable, filter_linear_mipmap_anisotropic;

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
/// Share of the biome's rime this material takes (lib/frost): low, so grippable metal keeps its gloss.
uniform float frost_share : hint_range(0.0, 1.0) = 0.45;
group_uniforms;

// The baked panel set's tile (PanelSurface.TileMeters), its panels' side, and how far from a seam their features reach
// (PanelSurface.ReachMeters, with its flat band); the baked coating set's tile (CoatingSurface.TileMeters).
const float PANEL_TILE = 1.0;
const float PANEL_SIDE = 0.5;
const float PANEL_REACH = 0.2;
const float COATING_TILE = 2.0;
// Where rust gathers beside a seam, meters.
const float CREASE = 0.14;

struct Panels {
	vec2 uv;         // where this point of its panel is in the baked panel set
	float crease;    // 1 close to a seam: where rust and dirt gather
	float below_top; // meters below the top seam of this panel
	float panel;     // random value per panel
};

// The panel a point of a face is on, the face cut into panels sized to fit it, and where that point lies in the baked set:
// each panel folded onto one of the set's (PanelSurface), its corners the baked panel's corners and its edges the baked
// panel's middle lines drawn out. Panels too small for corner features take the plain one.
Panels panels_at(vec2 uv, vec2 size, float seed) {
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
	pn.crease = 1.0 - smoothstep(0.0, CREASE, min(to_seam2.x, to_seam2.y));
	pn.below_top = cell.y - local.y;

	// Which baked panel (PanelSurface.PanelVariant: plain, riveted, gusset, inset), in the set's quadrant (i % 2, i / 2).
	int variant = 0;
	if (min(cell.x, cell.y) > PANEL_REACH * 2.0 + 0.04) {
		bool riveted = fract(pn.panel * 13.7) < rivet_share;
		variant = riveted ? (fract(pn.panel * 7.3) < 0.7 ? 1 : 2) : (fract(pn.panel * 5.1) < 0.35 ? 3 : 0);
	}
	vec2 quadrant = vec2(float(variant % 2), float(variant / 2)) * PANEL_SIDE;
	vec2 near = min(local, vec2(PANEL_SIDE * 0.5));
	vec2 far = PANEL_SIDE - min(cell - local, vec2(PANEL_SIDE * 0.5));
	vec2 folded = quadrant + mix(near, far, step(cell * 0.5, local));
	pn.uv = vec2(folded.x, -folded.y) / PANEL_TILE;
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

	// The fine detail comes from the baked sets, their mips picked from the unbroken position on the face (the panels' folded
	// one jumps at every seam).
	Panels pn = panels_at(f.uv, f.size, seed);
	vec2 dx = dFdx(f.uv) * vec2(1.0, -1.0);
	vec2 dy = dFdy(f.uv) * vec2(1.0, -1.0);
	vec4 relief = textureGrad(panels_surface, pn.uv, dx / PANEL_TILE, dy / PANEL_TILE);
	vec2 slope = textureGrad(panels_normal, pn.uv, dx / PANEL_TILE, dy / PANEL_TILE).rg * 2.0 - 1.0;
	float seam = relief.g;
	float rivet = clamp(relief.a * 255.0 / 16.0 - 1.0, 0.0, 1.0);
	vec2 cq = f.uv + vec2(7.31, 3.17) * seed * COATING_TILE;
	vec2 cuv = vec2(cq.x, -cq.y) / COATING_TILE;
	vec4 wear = textureGrad(coating_surface, cuv, dx / COATING_TILE, dy / COATING_TILE);
	slope += textureGrad(coating_normal, cuv, dx / COATING_TILE, dy / COATING_TILE).rg * 2.0 - 1.0;
	slope /= max(1.0, length(slope));
	vec3 ts = vec3(slope, sqrt(max(0.0, 1.0 - dot(slope, slope))));
	vec3 n = normalize(f.tangent * ts.x + f.bitangent * ts.y + f.normal * ts.z);
	float crease = max(pn.crease, seam);

	vec3 coat = palette_color(slot) * (0.94 + 0.12 * pn.panel) * (0.96 + 0.08 * seed);
	float blotch = fbm3(p * 0.7 + seed * 9.0, 3);

	// What runs from a panel's top seam starts past the seam, so it never cuts across it.
	float from_top = smoothstep(0.0, 0.015 + 2.0 * fwidth(f.uv.y), pn.below_top);

	// Rust: in creases, near the bottom, and in blotches as corrosion grows.
	float grime = side ? 1.0 - smoothstep(0.0, grime_height, f.height + (blotch - 0.5) * 0.6) : 0.0;
	float rust_field = max(crease * 0.75, grime * 0.7) + (blotch - 0.5) * 0.9 + corrosion * 1.1 - 0.75;
	// Rust also runs down from the creases: the coating's runs, taken elsewhere in its tile.
	float drip = 0.0;
	if (side) {
		float runs = textureGrad(coating_surface, cuv + vec2(0.37, 0.61), dx / COATING_TILE, dy / COATING_TILE).g;
		drip = runs * exp(-pn.below_top / 0.35) * from_top;
	}
	rust_field = max(rust_field, drip * corrosion - 0.1);
	float rust = smoothstep(0.0, 0.25, rust_field) * (0.35 + 0.65 * corrosion + 0.3 * crease);
	rust = clamp(rust, 0.0, 1.0);
	vec3 rust_color = srgb_to_linear(biome_rust.rgb) * (0.75 + 0.5 * fbm3(p * 6.0, 2));

	// Coating chipped along the edges shows bare, darker metal: a ragged band, its border smoothed over a pixel.
	float ragged = value_noise3(p * 4.0 + seed * 3.0) * 0.75 + value_noise3(p * 11.0 + seed * 5.0) * 0.25;
	float past_chip = f.edge - 0.5 * edge_chip * (0.3 + 1.4 * ragged);
	float chip_aa = fwidth(past_chip) * 0.75 + 1e-4;
	float chip = 1.0 - smoothstep(-chip_aa, chip_aa, past_chip);

	// Wet streaks run down from each panel's top seam (sides only).
	float streak = side ? wear.g * exp(-pn.below_top / 0.7) * from_top * streaks : 0.0;

	vec3 albedo = coat * wear.r * 2.0;
	float rough = roughness + (relief.b - 0.32) + (wear.b - 0.32) + (blotch - 0.5) * 0.08;
	float metal = metallic;
	float coat_amount = clearcoat * (1.0 - 0.6 * rivet);

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

	// The panel's own tone over all of it, so its seams stay dark through the rust; rivet heads stay clean of rust.
	albedo *= relief.r * 2.0;
	albedo = mix(albedo, coat * relief.r * 2.0, rivet * (1.0 - rust));

	// Rime gathers in the seams and on top faces; the panels between stay bare and glossy.
	vec3 world_n = view_to_world(n, INV_VIEW_MATRIX);
	Frost fr = frost_at(p, view_to_world(f.normal, INV_VIEW_MATRIX), world_n, max(seam, pn.crease * 0.6), f.edge, frost_share);
	albedo = mix(albedo, frost_color(crease) * fr.tone, fr.cover);
	rough = mix(rough, fr.rough, fr.cover);
	n = frost_bump(n, fr, VIEW_MATRIX);
	metal = mix(metal, 0.0, fr.cover);
	coat_amount *= 1.0 - fr.cover;

	// Deep in a hollow (a pipe's mouth): dark, whatever the ambient light.
	albedo *= 1.0 - 0.9 * f.cavity;
	ALBEDO = albedo * shade;
	EMISSION = ALBEDO * caustics_at(v_world_pos, view_to_world(f.normal, INV_VIEW_MATRIX), TIME);
	ROUGHNESS = clamp(rough, 0.05, 1.0);
	METALLIC = metal;
	CLEARCOAT = coat_amount;
	CLEARCOAT_ROUGHNESS = 0.15;
	RIM = rim * (1.0 - rust) * (1.0 - fr.cover);
	RIM_TINT = 0.6;
	AO = (1.0 - seam * 0.7 - pn.crease * 0.15) * (1.0 - f.cavity);
	AO_LIGHT_AFFECT = 0.2;
	NORMAL = n;
	HAZE
	UNDERWATER
	CLEARCOAT *= uw_keep;
	RIM *= uw_keep;
}
