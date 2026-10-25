// Machine: the native machines of the Deep (docs/long-term.md, objective 1). Their shells take the biome
// palette (pale ceramic-metal near the surface, warmer with depth), worn by rust along their edges; the
// pilgrim's android shader is the opposite, its own colors at every depth. One shader for every machine
// part, read through the parts' surface frame (lib/surface): panel lines across a limb, trim along its
// edges, cleats running along a tread's belt, glyph bands on a prayer wheel, glowing eyes.
// Per machine (instance uniforms, set by Machine from its species and its brain): wear, tone, a seed,
// how roused and how alert it is (eyes go from calm to the hazard color: color means gameplay), and
// how far its treads have rolled.
shader_type spatial;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"
#include "res://assets/shaders/lib/surface.gdshaderinc"

uniform int slot : hint_enum("Rock", "Metal", "Backdrop", "Backdrop accent", "Foreground", "Rust", "Silt", "Accent") = 1;
/// Multiplies the palette color: below 1 for underbodies and joints, above for bright plating.
uniform float tone : hint_range(0.0, 2.0) = 1.0;
uniform float roughness : hint_range(0.0, 1.0) = 0.35;
uniform float metallic : hint_range(0.0, 1.0) = 0.2;
uniform float clearcoat : hint_range(0.0, 1.0) = 0.4;
uniform float rim : hint_range(0.0, 1.0) = 0.2;

group_uniforms detail;
/// Thin grooves across the part every `line_spacing` meters (panel lines); 0 for none.
uniform float line_spacing : hint_range(0.0, 1.0) = 0.0;
/// A pale band along the part's edges, meters; 0 for none.
uniform float trim_width : hint_range(0.0, 0.05) = 0.0;
uniform int trim_slot : hint_enum("Rock", "Metal", "Backdrop", "Backdrop accent", "Foreground", "Rust", "Silt", "Accent") = 6;
uniform float trim_tone : hint_range(0.0, 2.0) = 1.15;
/// How much the machine's wear shows on this part (rust on edges and in grooves).
uniform float wear_response : hint_range(0.0, 1.0) = 1.0;
/// None, cleats along a wrapping belt (a tread), or bands of glyphs around the girth (a prayer wheel).
uniform int pattern : hint_enum("None", "Cleats", "Glyph bands") = 0;
uniform float cleat_spacing : hint_range(0.01, 0.2) = 0.05;

group_uniforms glow;
uniform int glow_pattern : hint_enum("None", "Whole", "Slit", "Glyph bands") = 0;
/// Calm glow: the biome accent mixed toward white by this much.
uniform float calm_whiteness : hint_range(0.0, 1.0) = 0.7;
uniform vec3 hazard_color : source_color = vec3(1.0, 0.24, 0.1);
uniform float glow_energy : hint_range(0.0, 16.0) = 3.0;
/// Slit: its width, meters.
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

void vertex() {
	v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	// Not the kit's position seed: machines move, so their look must not.
	v_seed = fract(machine_seed * 0.754877 + COLOR.g * 0.61803);
}

void fragment() {
	Surface f = surface_from(UV, UV2, COLOR, NORMAL, TANGENT, BINORMAL);
	float aa = max(fwidth(f.uv.x), fwidth(f.uv.y)) * 0.75 + 1e-4;
	float seed = v_seed;

	vec3 albedo = palette_color(slot) * tone * palette_tone * (0.94 + 0.12 * seed);
	float rough = roughness;
	float metal = metallic;
	float coat = clearcoat;
	vec3 n = NORMAL;

	float groove = 0.0;
	if (line_spacing > 0.0) {
		float d = abs(fract(f.uv.x / line_spacing + 0.5 + seed) - 0.5) * line_spacing;
		groove = 1.0 - smoothstep(0.003 - aa, 0.003 + aa, d);
	}

	if (pattern == 1 && UV2.x < 0.0) {
		// Cleats across the belt, rolling with the machine.
		float c = fract((f.uv.x + scroll) / cleat_spacing);
		float bar = smoothstep(0.0, 0.08, c) * (1.0 - smoothstep(0.45, 0.53, c));
		albedo *= 0.55 + 0.45 * bar;
		rough = mix(0.8, rough, bar);
		n = surface_bump(f, n, vec2((smoothstep(0.0, 0.08, c) - smoothstep(0.45, 0.53, c)) * 0.6, 0.0));
	}

	float glyph = 0.0;
	if (pattern == 2 || glow_pattern == 3) {
		// Two rims, and between them a band of generated glyphs around the girth.
		float along = f.uv.x / max(f.size.x, 1e-3);
		float rims = step(abs(along - 0.12), 0.035) + step(abs(along - 0.88), 0.035);
		float cell_v = f.size.y / max(1.0, round(f.size.y / 0.07));
		vec2 cell = vec2(floor(f.uv.y / cell_v), 0.0);
		vec2 local = vec2(fract(f.uv.y / cell_v), (along - 0.22) / 0.56);
		float inside = step(0.0, local.y) * step(local.y, 1.0);
		float h = hash12(cell + seed * 17.0);
		float stroke = step(abs(local.x - 0.5), 0.09) * step(fract(local.y * 3.0 + h), 0.7);
		float bar = step(abs(local.y - (0.2 + 0.6 * h)), 0.07) * step(abs(local.x - 0.5), 0.32);
		glyph = clamp(stroke + bar, 0.0, 1.0) * inside;
		groove = max(groove, rims);
		albedo = mix(albedo, albedo * 0.5, glyph);
	}

	albedo *= 1.0 - 0.6 * groove;
	rough = mix(rough, 0.75, groove);

	float trim = trim_width > 0.0 ? 1.0 - smoothstep(trim_width - aa, trim_width + aa, f.edge) : 0.0;
	albedo = mix(albedo, palette_color(trim_slot) * trim_tone, trim);
	rough = mix(rough, 0.3, trim);

	// Wear: rust eats in from the edges and settles in the grooves, patchy.
	float w = wear * wear_response;
	if (w > 0.0) {
		float patch = fbm2(f.uv * 9.0 + seed * 31.0, 3);
		float edge_rust = 1.0 - smoothstep(0.0, 0.02 + 0.06 * w, f.edge + (patch - 0.5) * 0.04);
		float rust = clamp(max(edge_rust * w * 1.4, groove * w) + smoothstep(1.0 - w * 0.6, 1.0, patch) * 0.8, 0.0, 1.0);
		vec3 rust_color = palette_color(5) * (0.8 + 0.3 * value_noise2(f.uv * 40.0));
		albedo = mix(albedo, rust_color, rust);
		rough = mix(rough, 0.85, rust);
		metal = mix(metal, 0.1, rust);
		coat *= 1.0 - rust;
	}

	vec3 calm = mix(palette_color(7), vec3(1.0), calm_whiteness);
	vec3 glow_color = mix(calm, hazard_color, smoothstep(0.15, 0.85, alert));
	float glow = 0.0;
	if (glow_pattern == 1) {
		glow = 1.0;
	} else if (glow_pattern == 2) {
		float across = abs(f.uv.y - f.size.y * 0.5);
		glow = 1.0 - smoothstep(glow_width * 0.5 - aa, glow_width * 0.5 + aa, across);
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
}
