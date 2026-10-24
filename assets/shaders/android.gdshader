// Android: the pilgrim's materials, human-built and foreign among the native machines (NieR: dark with
// white highlights). One shader, many finishes: black lacquer or matte suit, white ceramic trim, silver
// hair, a glowing visor slit, lantern windows. It reads the parts' surface frame (lib/surface), so trim
// follows each part's edges: cuffs at the ends of a limb, piping around a coat tail, the rims of a band.
// Its colors are its own, not the biome's: the pilgrim looks the same at every depth.
shader_type spatial;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/surface.gdshaderinc"

uniform vec3 base_color : source_color = vec3(0.03, 0.03, 0.035);
uniform float roughness : hint_range(0.0, 1.0) = 0.35;
uniform float metallic : hint_range(0.0, 1.0) = 0.1;
uniform float clearcoat : hint_range(0.0, 1.0) = 0.0;
uniform float clearcoat_roughness : hint_range(0.0, 1.0) = 0.2;
/// Light wrapping around the silhouette (fabric, glossy edges).
uniform float rim : hint_range(0.0, 1.0) = 0.0;
uniform float rim_tint : hint_range(0.0, 1.0) = 0.3;
/// Stretched highlights along the part (U): hair, brushed metal.
uniform float anisotropy : hint_range(0.0, 1.0) = 0.0;
/// Fine roughness grain in 1/m (fabric weave); 0 for none.
uniform float grain : hint_range(0.0, 400.0) = 0.0;

group_uniforms trim;
uniform vec3 trim_color : source_color = vec3(0.86, 0.87, 0.9);
/// Width of the band along the part's edges, meters; 0 for none.
uniform float trim_width : hint_range(0.0, 0.05) = 0.0;
uniform float trim_roughness : hint_range(0.0, 1.0) = 0.3;
/// Thin grooves across the part every `line_spacing` meters (panel lines); 0 for none.
uniform float line_spacing : hint_range(0.0, 1.0) = 0.0;
uniform float line_width : hint_range(0.0, 0.02) = 0.004;
/// Fine grooves along the part every `strand_spacing` meters across it (hair strands); 0 for none.
uniform float strand_spacing : hint_range(0.0, 0.1) = 0.0;

group_uniforms glow;
uniform int glow_pattern : hint_enum("None", "Whole", "Slit", "Windows") = 0;
uniform vec3 glow_color : source_color = vec3(0.78, 0.58, 1.0);
uniform float glow_energy : hint_range(0.0, 16.0) = 0.0;
/// Slit: its width, meters. Windows: the bars between them, meters.
uniform float glow_width : hint_range(0.0, 0.05) = 0.006;
/// Windows around the girth.
uniform int windows : hint_range(1, 12) = 4;
/// Slow breathing of the glow (0 for steady).
uniform float pulse : hint_range(0.0, 1.0) = 0.0;
group_uniforms;

void vertex() {
	SURFACE_VERTEX
}

void fragment() {
	Surface f = SURFACE;
	float aa = max(fwidth(f.uv.x), fwidth(f.uv.y)) * 0.75 + 1e-4;

	vec3 albedo = base_color;
	float rough = roughness;
	float metal = metallic;
	float coat = clearcoat;

	if (grain > 0.0) {
		rough = clamp(rough + (value_noise2(f.uv * grain) - 0.5) * 0.12, 0.0, 1.0);
	}

	if (line_spacing > 0.0) {
		float d = abs(fract(f.uv.x / line_spacing + 0.5) - 0.5) * line_spacing;
		float line = 1.0 - smoothstep(line_width * 0.5 - aa, line_width * 0.5 + aa, d);
		albedo *= 1.0 - 0.7 * line;
		rough = mix(rough, 0.75, line);
	}

	if (strand_spacing > 0.0) {
		float s = abs(fract(f.uv.y / strand_spacing + 0.5) - 0.5) * 2.0;
		float groove = smoothstep(0.35, 0.0, s) * (0.6 + 0.4 * value_noise2(vec2(f.uv.y / strand_spacing * 3.7, f.uv.x * 6.0)));
		// Fades out where the grooves get finer than a pixel, so they never shimmer.
		float fade = 1.0 - smoothstep(strand_spacing * 0.25, strand_spacing * 0.6, aa);
		albedo *= 1.0 - 0.35 * groove * fade;
		rough = mix(rough, 0.6, groove * fade * 0.5);
	}

	float trim = trim_width > 0.0 ? 1.0 - smoothstep(trim_width - aa, trim_width + aa, f.edge) : 0.0;
	albedo = mix(albedo, trim_color, trim);
	rough = mix(rough, trim_roughness, trim);
	metal = mix(metal, 0.05, trim);

	float glow = 0.0;
	if (glow_pattern == 1) {
		glow = 1.0;
	} else if (glow_pattern == 2) {
		// A slit along the part, centered across it (a visor).
		float across = abs(f.uv.y - f.size.y * 0.5);
		glow = 1.0 - smoothstep(glow_width * 0.5 - aa, glow_width * 0.5 + aa, across);
	} else if (glow_pattern == 3) {
		// Windows around the girth between bars, away from both ends (a lantern's cage).
		float cell = f.size.y / float(windows);
		float local = mod(f.uv.y, cell);
		float between = min(local, cell - local);
		float along = min(f.uv.x, f.size.x - f.uv.x);
		glow = smoothstep(glow_width * 0.5 - aa, glow_width * 0.5 + aa, between)
				* smoothstep(f.size.x * 0.22 - aa, f.size.x * 0.22 + aa, along);
	}
	glow *= 1.0 - pulse * (0.5 + 0.5 * sin(TIME * 1.3));

	ALBEDO = mix(albedo, glow_color, glow * 0.6);
	ROUGHNESS = rough;
	METALLIC = metal;
	CLEARCOAT = coat * (1.0 - trim);
	CLEARCOAT_ROUGHNESS = clearcoat_roughness;
	RIM = rim;
	RIM_TINT = rim_tint;
	if (anisotropy > 0.0) {
		ANISOTROPY = anisotropy;
		ANISOTROPY_FLOW = vec2(1.0, 0.0);
	}
	EMISSION = glow_color * glow * glow_energy;
}
