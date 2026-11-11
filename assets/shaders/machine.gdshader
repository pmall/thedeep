// Machine: the native machines of the Deep (docs/long-term.md#machines-distinct). Their shells take the biome
// palette (pale ceramic-metal near the surface, warmer with depth), worn by rust along their edges; the
// pilgrim's android shader is the opposite, its own colors at every depth. One shader for every machine
// part, read through the parts' surface frame (lib/surface): panel lines across a limb, trim along its
// edges, cleats running along a tread's belt, glyph bands on a prayer wheel, glowing eyes.
// What is finer than a few pixels at the gameplay camera comes from the baked sets (SurfaceMaps has the
// sampling contract): the panel lines are the panel set's seam, the glyph bands the glyph set; what is
// drawn here is held to a few pixels wide on screen or fades to its average (docs/art-direction.md#stable-image).
// Per machine (instance uniforms, set by Machine from its species and its brain): wear, tone, a seed,
// how roused and how alert it is (eyes go from calm to the hazard color: color means gameplay), and
// how far its treads have rolled.
shader_type spatial;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"
#include "res://assets/shaders/lib/water.gdshaderinc"
#include "res://assets/shaders/lib/surface.gdshaderinc"
#include "res://assets/shaders/lib/frost.gdshaderinc"

// The part's own position: rime is laid on in the machine's frame, so it rides along when it moves.
varying vec3 v_local_pos;

uniform int slot : hint_enum("Rock", "Metal", "Backdrop", "Backdrop accent", "Foreground", "Rust", "Silt", "Accent") = 1;
/// Multiplies the palette color: below 1 for underbodies and joints, above for bright plating.
uniform float tone : hint_range(0.0, 2.0) = 1.0;
uniform float roughness : hint_range(0.0, 1.0) = 0.35;
uniform float metallic : hint_range(0.0, 1.0) = 0.2;
uniform float clearcoat : hint_range(0.0, 1.0) = 0.4;
uniform float rim : hint_range(0.0, 1.0) = 0.2;

group_uniforms detail;
/// Seams across the part every `line_spacing` meters (panel lines, up to half a meter apart); 0 for none.
uniform float line_spacing : hint_range(0.0, 1.0) = 0.0;
/// A pale band along the part's edges, meters (at least a few pixels wide on screen); 0 for none.
uniform float trim_width : hint_range(0.0, 0.05) = 0.0;
uniform int trim_slot : hint_enum("Rock", "Metal", "Backdrop", "Backdrop accent", "Foreground", "Rust", "Silt", "Accent") = 6;
uniform float trim_tone : hint_range(0.0, 2.0) = 1.15;
/// How much the machine's wear shows on this part (rust on edges and in grooves).
uniform float wear_response : hint_range(0.0, 1.0) = 1.0;
/// None, cleats along a wrapping belt (a tread), or bands of glyphs around the girth (a prayer wheel).
uniform int pattern : hint_enum("None", "Cleats", "Glyph bands") = 0;
uniform float cleat_spacing : hint_range(0.01, 0.2) = 0.05;
/// The baked sets (assets/textures/surfaces/): a material with panel lines sets the panel pair, one with glyph bands the
/// glyph pair; the others leave them unset.
uniform sampler2D panels_normal : hint_normal, repeat_enable, filter_linear_mipmap_anisotropic;
uniform sampler2D panels_surface : hint_default_black, repeat_enable, filter_linear_mipmap_anisotropic;
uniform sampler2D glyphs_normal : hint_normal, repeat_enable, filter_linear_mipmap_anisotropic;
uniform sampler2D glyphs_surface : hint_default_black, repeat_enable, filter_linear_mipmap_anisotropic;

group_uniforms glow;
uniform int glow_pattern : hint_enum("None", "Whole", "Slit", "Glyph bands") = 0;
/// Calm glow: the biome accent mixed toward white by this much.
uniform float calm_whiteness : hint_range(0.0, 1.0) = 0.7;
uniform vec3 hazard_color : source_color = vec3(1.0, 0.24, 0.1);
uniform float glow_energy : hint_range(0.0, 16.0) = 3.0;
/// Slit: its width, meters; drawn at least a few pixels wide on screen, its light spread over them.
uniform float glow_width : hint_range(0.0, 0.05) = 0.01;
group_uniforms;

instance uniform float wear : hint_range(0.0, 1.0) = 0.3;
instance uniform float palette_tone : hint_range(0.0, 2.0) = 1.0;
instance uniform float machine_seed = 0.0;
instance uniform float alert : hint_range(0.0, 1.0) = 0.0;
instance uniform float rouse : hint_range(0.0, 1.0) = 1.0;
instance uniform float scroll = 0.0;
// A white flash when hurt (a machine taking a blow, the pilgrim shimmering in its grace period), 0 to 1.
instance uniform float hurt : hint_range(0.0, 1.0) = 0.0;

// The baked panel set's tile and its panels' side (PanelSurface: TileMeters, PanelMeters); the glyph set's tile, its cells'
// width, its bands' height and how many of each (GlyphBandSurface).
const float PANEL_TILE = 1.0;
const float PANEL_SIDE = 0.5;
// A machine's plating is finer than a wall's: the panel set is laid at half its size, its seams half as wide.
const float PANEL_SCALE = 2.0;
const float GLYPH_TILE = 0.64;
const float GLYPH_CELL = 0.08;
const float GLYPH_BAND = 0.16;
const float GLYPH_COLUMNS = 8.0;
const float GLYPH_ROWS = 4.0;
// Where the glyph band lies along the part, as a share of its length.
const vec2 GLYPH_SPAN = vec2(0.06, 0.94);
// The fewest pixels a drawn shape spans on screen.
const float MIN_PIXELS = 2.5;

void vertex() {
	v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_local_pos = VERTEX;
	// Not the kit's position seed: machines move, so their look must not.
	v_seed = fract(machine_seed * 0.754877 + COLOR.g * 0.61803);
}

void fragment() {
	Surface f = surface_from(UV, UV2, COLOR, NORMAL, TANGENT, BINORMAL);
	// A pixel's footprint on the part, meters.
	float pixel = max(fwidth(f.uv.x), fwidth(f.uv.y)) + 1e-5;
	float aa = pixel * 0.75;
	float seed = v_seed;

	vec3 albedo = palette_color(slot) * tone * palette_tone * (0.94 + 0.12 * seed);
	float rough = roughness;
	float metal = metallic;
	float coat = clearcoat;
	vec3 n = NORMAL;

	// The baked sets are sampled with the gradients of the unbroken position on the part, as metal does: the folded one jumps
	// at every seam and every glyph cell.
	vec2 duv_x = dFdx(f.uv);
	vec2 duv_y = dFdy(f.uv);

	float groove = 0.0;
	if (line_spacing > 0.0) {
		// Panel lines: each strip between two lines folded onto the baked plain panel across its middle, its seams the panel's.
		float s = line_spacing * PANEL_SCALE;
		float x = fract(f.uv.x / line_spacing + seed) * s;
		float near = min(x, PANEL_SIDE * 0.5);
		float far = PANEL_SIDE - min(s - x, PANEL_SIDE * 0.5);
		vec2 puv = vec2(x < s * 0.5 ? near : far, -PANEL_SIDE * 0.5) / PANEL_TILE;
		vec2 gx = duv_x * vec2(1.0, -1.0) * PANEL_SCALE / PANEL_TILE;
		vec2 gy = duv_y * vec2(1.0, -1.0) * PANEL_SCALE / PANEL_TILE;
		vec4 relief = textureGrad(panels_surface, puv, gx, gy);
		vec2 slope = textureGrad(panels_normal, puv, gx, gy).rg * 2.0 - 1.0;
		vec3 ts = vec3(slope, sqrt(max(0.0, 1.0 - dot(slope, slope))));
		n = normalize(f.tangent * ts.x + f.bitangent * ts.y + f.normal * ts.z);
		groove = relief.g;
		albedo *= relief.r * 2.0;
	}

	if (pattern == 1 && UV2.x < 0.0) {
		// Cleats across the belt, rolling with the machine: a bar over half of each cleat, its edges spread over a pixel at
		// least, and the whole fading to its average where the cleats close up to a few pixels.
		float t = (f.uv.x + scroll) / cleat_spacing;
		float c = abs(fract(t) - 0.5);
		float w = clamp(fwidth(t), 0.06, 0.2);
		float bar = 1.0 - smoothstep(0.235 - w, 0.235 + w, c);
		float edges = (1.0 - smoothstep(0.0, w, abs(c - 0.235))) * sign(0.5 - fract(t));
		float fade = smoothstep(0.12, 0.3, fwidth(t));
		bar = mix(bar, 0.47, fade);
		albedo *= 0.4 + 0.6 * bar;
		rough = mix(0.8, rough, bar);
		n = surface_bump(f, n, vec2(edges * 0.6 * (1.0 - fade), 0.0));
	}

	float glyph = 0.0;
	if (pattern == 2 || glow_pattern == 3) {
		// A band of glyphs round the girth between two rims (the glyph set): a whole number of cells round it, each folded onto
		// one of a row's, the row and where it starts picked per machine.
		float along = f.uv.x / max(f.size.x, 1e-3);
		float band = (along - GLYPH_SPAN.x) / (GLYPH_SPAN.y - GLYPH_SPAN.x);
		if (band > 0.0 && band < 1.0) {
			float cell_v = f.size.y / max(1.0, round(f.size.y / GLYPH_CELL));
			float g = f.uv.y / cell_v;
			float column = mod(floor(g) + floor(seed * GLYPH_COLUMNS), GLYPH_COLUMNS);
			float row = floor(fract(seed * 7.31) * GLYPH_ROWS);
			vec2 tex = vec2((column + fract(g)) * GLYPH_CELL, (row + band) * GLYPH_BAND);
			vec2 guv = vec2(tex.x, -tex.y) / GLYPH_TILE;
			// Along the set runs round the girth (+V on the part), up it along the part (+U).
			vec2 scale = vec2(GLYPH_CELL / cell_v, GLYPH_BAND / ((GLYPH_SPAN.y - GLYPH_SPAN.x) * max(f.size.x, 1e-3)));
			vec2 gx = vec2(duv_x.y * scale.x, -duv_x.x * scale.y) / GLYPH_TILE;
			vec2 gy = vec2(duv_y.y * scale.x, -duv_y.x * scale.y) / GLYPH_TILE;
			vec4 relief = textureGrad(glyphs_surface, guv, gx, gy);
			vec2 slope = textureGrad(glyphs_normal, guv, gx, gy).rg * 2.0 - 1.0;
			vec3 ts = vec3(slope, sqrt(max(0.0, 1.0 - dot(slope, slope))));
			n = normalize(f.bitangent * ts.x + f.tangent * ts.y + f.normal * ts.z);
			glyph = clamp(relief.a * 255.0 / 16.0 - 1.0, 0.0, 1.0);
			groove = max(groove, relief.g);
			albedo *= relief.r * 2.0;
			rough = relief.b;
		}
	}

	rough = mix(rough, 0.75, groove * 0.5);

	// The trim, held to a few pixels wide on screen (up to three times its own width).
	float trim_w = trim_width > 0.0 ? clamp(MIN_PIXELS * pixel, trim_width, trim_width * 3.0) : 0.0;
	float trim = trim_width > 0.0 ? 1.0 - smoothstep(trim_w - aa, trim_w + aa, f.edge) : 0.0;
	albedo = mix(albedo, palette_color(trim_slot) * trim_tone, trim);
	rough = mix(rough, 0.3, trim);

	// Wear: rust eats in from the edges and settles in the grooves, patchy, its patches and its tone a few centimeters across
	// at the finest.
	float w = wear * wear_response;
	if (w > 0.0) {
		float patch = fbm2(f.uv * 9.0 + seed * 31.0, 2);
		float edge_rust = 1.0 - smoothstep(0.0, 0.02 + 0.06 * w, f.edge + (patch - 0.5) * 0.04);
		float rust = clamp(max(edge_rust * w * 1.4, groove * w) + smoothstep(1.0 - w * 0.6, 1.0, patch) * 0.8, 0.0, 1.0);
		vec3 rust_color = palette_color(5) * (0.8 + 0.3 * value_noise2(f.uv * 8.0 + seed * 5.0));
		albedo = mix(albedo, rust_color, rust);
		rough = mix(rough, 0.85, rust);
		metal = mix(metal, 0.1, rust);
		coat *= 1.0 - rust;
	}

	// In the cold, a dormant machine gathers rime (lib/frost); it falls away as the machine rouses. Its pattern runs a little
	// finer than on the rooms' stone, to fit a machine's parts, but never under a few pixels.
	if (biome_frost > 0.0 && rouse < 1.0) {
		vec3 world_n = view_to_world(n, INV_VIEW_MATRIX);
		Frost fr = frost_at(v_local_pos * 2.0 + machine_seed * 13.0, view_to_world(f.normal, INV_VIEW_MATRIX), world_n, groove, f.edge, 0.8 * (1.0 - rouse));
		albedo = mix(albedo, frost_color(groove) * fr.tone, fr.cover);
		rough = mix(rough, fr.rough, fr.cover);
		n = frost_bump(n, fr, VIEW_MATRIX);
		metal = mix(metal, 0.0, fr.cover);
		coat *= 1.0 - fr.cover;
	}

	vec3 calm = mix(palette_color(7), vec3(1.0), calm_whiteness);
	vec3 glow_color = mix(calm, hazard_color, smoothstep(0.15, 0.85, alert));
	float glow = 0.0;
	if (glow_pattern == 1) {
		glow = 1.0;
	} else if (glow_pattern == 2) {
		// A slit narrower than a few pixels is drawn that wide, soft across, and dimmed so it gives the same light.
		float half_w = max(glow_width * 0.5, MIN_PIXELS * 0.5 * pixel);
		float across = abs(f.uv.y - f.size.y * 0.5);
		glow = (1.0 - smoothstep(half_w * 0.3, half_w + aa, across)) * min(1.0, glow_width * 0.5 / half_w);
	} else if (glow_pattern == 3) {
		glow = glyph;
	}
	// Dormant machines barely glow; alert ones glow hotter.
	glow *= (0.08 + 0.92 * rouse) * (1.0 + 0.6 * alert);

	ALBEDO = mix(mix(albedo, glow_color, min(glow, 1.0) * 0.6), vec3(1.0), hurt * 0.35);
	NORMAL = n;
	ROUGHNESS = rough;
	METALLIC = metal;
	CLEARCOAT = coat;
	CLEARCOAT_ROUGHNESS = 0.2;
	RIM = rim;
	RIM_TINT = 0.4;
	EMISSION = glow_color * glow * glow_energy + vec3(0.95, 0.92, 1.0) * hurt * 0.9;
	UNDERWATER
	CLEARCOAT *= uw_keep;
	RIM *= uw_keep;
}
