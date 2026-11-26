// The android's surfaces, shared by its shaders: android.gdshader lit as the world is, android_toon.gdshader in flat
// bands (ANDROID_TOON defined before the include). What each finish draws is android.gdshader's header.
// Needs nothing included before it; it includes lib/noise and lib/surface.

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/surface.gdshaderinc"
#include "res://assets/shaders/lib/look.gdshaderinc"

uniform vec3 base_color : source_color = vec3(0.03, 0.03, 0.035);
uniform float roughness : hint_range(0.0, 1.0) = 0.35;
uniform float metallic : hint_range(0.0, 1.0) = 0.1;
uniform float clearcoat : hint_range(0.0, 1.0) = 0.0;
uniform float clearcoat_roughness : hint_range(0.0, 1.0) = 0.2;
/// Light wrapping around the silhouette (fabric, glossy edges).
uniform float rim : hint_range(0.0, 1.0) = 0.0;
uniform float rim_tint : hint_range(0.0, 1.0) = 0.3;
/// A faint cool edge along the silhouette that needs no light (emitted, strongest where the surface turns away from the
/// camera), so a dark body still reads against a dark backdrop; thin, so the body stays black and never looks lamp-lit.
uniform float edge_light : hint_range(0.0, 2.0) = 0.0;
uniform vec3 edge_color : source_color = vec3(0.72, 0.78, 0.9);
/// How tight the edge hugs the silhouette (higher, thinner).
uniform float edge_power : hint_range(1.0, 8.0) = 3.0;
/// How tight it hugs once a pixel covers `edge_far_pixel` meters (a wide shot): looser, so the edge never thins under a
/// pixel and the small figure keeps a faint outline instead of melting into a dark backdrop.
uniform float edge_far_power : hint_range(1.0, 8.0) = 3.0;
/// Meters per pixel on the body where the edge has loosened to `edge_far_power`; it starts loosening at a third of this
/// (just past the play camera's), so at play size the edge is `edge_power`'s.
uniform float edge_far_pixel : hint_range(0.005, 0.2) = 0.04;
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

#ifdef ANDROID_TOON
#include "res://assets/shaders/lib/palette.gdshaderinc"
#include "res://assets/shaders/lib/dirt.gdshaderinc"
// What the toon light reads of the surface (android_toon.gdshader): where on the part, and how rough.
varying vec2 toon_uv;
varying float toon_rough;
// How much the black lifts toward a dark grey, so the drawn figure shows its light and shade as the world's dark pieces do.
uniform float toon_lift : hint_range(0.0, 0.2) = 0.035;
// How much of the world's dust (lib/dirt's wash, look_wash) the dark parts carry: pale blooms of the biome's silt, fixed to
// the parts, as on a pilgrim who walked far.
uniform float toon_dust : hint_range(0.0, 1.0) = 0.8;
// How far the worn chips along the parts' edges reach, meters (with the wash, look_wash).
uniform float toon_chips : hint_range(0.0, 0.03) = 0.009;
// How ragged the bands' edges are (a cosine's worth), and the grain's cell, meters: a still grain fixed to the parts,
// several pixels wide at the play camera, so it never shimmers (docs/art-direction.md#stable-image).
uniform float toon_grain : hint_range(0.0, 0.5) = 0.14;
uniform float toon_grain_size : hint_range(0.005, 0.2) = 0.035;
// How bright the drawn rim is, as a share of the smooth look's edge light.
uniform float toon_edge : hint_range(0.0, 1.0) = 0.1;

float toon_noise(vec2 uv) {
	return value_noise2(uv / toon_grain_size) - 0.5;
}
#endif

// A white flash when hurt (a machine taking a blow, the pilgrim shimmering in its grace period), 0 to 1.
instance uniform float hurt : hint_range(0.0, 1.0) = 0.0;

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

#ifdef ANDROID_TOON
	float dark_part = 1.0 - smoothstep(0.05, 0.2, dot(albedo, vec3(0.2126, 0.7152, 0.0722)));
	albedo += toon_lift * dark_part * (1.0 - trim);
	vec3 silt = srgb_to_linear(biome_silt.rgb);
	float dusted = toon_dust * look_wash * dark_part * (1.0 - trim);
	// Dust gathers where a part meets the next (its ends and seams), ragged, with the darker tide line its edge dried to;
	// laid on each part from its own corner shifted by its size.
	vec2 dust_uv = f.uv + f.size * 3.7;
	float bloom = value_noise2(dust_uv / 0.05) * 0.6 + value_noise2(dust_uv / 0.02 + 5.0) * 0.4;
	bloom += (1.0 - smoothstep(0.0, 0.06, f.edge)) * 0.45;
	float bloom_aa = fwidth(bloom) + 1e-4;
	float dust = smoothstep(0.8 - bloom_aa, 0.8 + bloom_aa, bloom);
	float tide = 1.0 - smoothstep(0.0, 0.015 + bloom_aa * 1.5, abs(bloom - 0.8));
	albedo = mix(albedo, silt * 0.3, dust * 0.55 * dusted);
	albedo = mix(albedo, silt * 0.45, tide * 0.3 * dusted);
	rough = mix(rough, 1.0, dust * dusted);
	// Chips along the parts' edges, where the black wears to the pale primer under it: a ragged band, broken along.
	float ragged = value_noise2(dust_uv / 0.02) * 0.6 + value_noise2(dust_uv / 0.06 + 9.0) * 0.4;
	float chip_width = toon_chips * (ragged - 0.62) * 2.5;
	float chip = (1.0 - smoothstep(chip_width - aa, chip_width + aa, f.edge)) * step(0.0, chip_width);
	albedo = mix(albedo, silt * 0.55, chip * look_wash * dark_part * (1.0 - trim));
#endif
	// Grain only: the ink outline (ink_outline.gdshader) draws the figure's edges, not each part's.
	albedo = look_drawn(albedo, f.uv, 1e3);
	ALBEDO = mix(mix(albedo, glow_color, glow * 0.6), vec3(1.0), hurt * 0.5);
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
	EMISSION = glow_color * glow * glow_energy + vec3(0.95, 0.92, 1.0) * hurt * 0.8;
	if (edge_light > 0.0) {
		float facing = clamp(dot(normalize(NORMAL), normalize(VIEW)), 0.0, 1.0);
		// The size of a pixel at this depth, in meters: how small the figure is on screen.
		float pixel = -VERTEX.z * 2.0 / (PROJECTION_MATRIX[1][1] * VIEWPORT_SIZE.y);
		float tight = mix(edge_power, edge_far_power, smoothstep(edge_far_pixel / 3.0, edge_far_pixel, pixel));
		float edge = pow(1.0 - facing, tight);
#ifdef ANDROID_TOON
		// Drawn: a crisp painted line instead of a glow fading in, too dim for the bloom (its threshold is 0.9) to halo.
		edge = smoothstep(0.3, 0.36, edge + toon_noise(f.uv) * toon_grain) * toon_edge;
#endif
		EMISSION += edge_color * edge_light * edge * (1.0 - trim);
	}
#ifdef ANDROID_TOON
	// Flat paint: no sky in the lacquer, no clearcoat sheen; the toon light's bands and its one highlight carry the shape.
	METALLIC = 0.0;
	SPECULAR = 0.0;
	CLEARCOAT = 0.0;
	toon_uv = f.uv;
	toon_rough = rough;
#endif
}
