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
// What the toon light reads of the surface (android_toon.gdshader): where on the part, how rough, and how high on the body.
varying vec2 toon_uv;
varying float toon_rough;
varying float toon_height;
// 1 when the active look bands the world (lib/bands), so the figure shares its painted light steps, pigments and brushwork.
uniform float toon_world_bands : hint_range(0.0, 1.0) = 0.0;
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
// How strong the light drawn out of the black on the lit side is, in the look's marks (lib/look: dots or hatching), as on a
// scratchboard: the world's marks, light on dark (0: none). Drawn in android_toon.gdshader's light().
uniform float toon_scratch : hint_range(0.0, 2.0) = 0.0;
// How far the black takes the drawn look's ink color, so the figure is made of the ink the world's lines are drawn in.
uniform float toon_ink_body : hint_range(0.0, 1.0) = 0.0;

float toon_noise(vec2 uv) {
	return value_noise2(uv / toon_grain_size) - 0.5;
}
#endif

// A white flash when hurt (a machine taking a blow, the pilgrim shimmering in its grace period), 0 to 1.
instance uniform float hurt : hint_range(0.0, 1.0) = 0.0;

void vertex() {
	SURFACE_VERTEX
#ifdef ANDROID_TOON
	toon_height = VERTEX.y;
#endif
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
	vec3 ink_hue = look_ink_color.rgb / max(max(look_ink_color.r, max(look_ink_color.g, look_ink_color.b)), 1e-4);
	vec3 silt = srgb_to_linear(biome_silt.rgb);
	vec3 rock = srgb_to_linear(biome_rock.rgb);
	vec2 dust_uv = f.uv + f.size * 3.7;
	if (toon_world_bands > 0.5) {
		// Painted look: separate sculpted plates from the dark matte undersuit, pigment them in the world's ink and stone
		// tones, and lay figure-scale painterly brushstrokes wide enough to stay readable and stable at gameplay distance.
		float plate = 1.0 - smoothstep(0.3, 0.5, roughness);
		// Mute the stark white mannequin trims into soft warm-paper silver accents so cuffs read without chopping the body.
		vec3 paper = vec3(0.96, 0.92, 0.86);
		vec3 painted_trim = mix(silt, paper, 0.4) * 0.32;
		albedo = mix(albedo, painted_trim, trim * 0.88);
		// Deep velvety ink for the matte undersuit; slightly lifted painted slate-indigo/charcoal for the sculpted plates
		// so the body has painted form and clearly shows light bands and brushwork instead of collapsing to flat black.
		vec3 rock_hue = rock / max(max(rock.r, max(rock.g, rock.b)), 1e-4);
		vec3 shadow_ink = mix(look_ink_color.rgb, rock * 0.08, 0.35);
		vec3 plate_base = mix(shadow_ink, rock * 0.16 + look_ink_color.rgb * 0.9, 0.65);
		vec3 dark_body = mix(shadow_ink, plate_base, plate);
		albedo = mix(albedo, dark_body, toon_ink_body * dark_part * (1.0 - trim));
		vec3 lift_hue = mix(ink_hue, rock_hue, 0.45);
		albedo += toon_lift * mix(0.45, 1.35, plate) * dark_part * (1.0 - trim) * lift_hue;
		// Pale hair takes a gentle warm-paper paint wash, pulled slightly under white so high-exposure biomes still show its
		// painted steps and brushwork instead of clipping flat white, and darkens softly toward the nape.
		float pale_part = (1.0 - dark_part) * (1.0 - trim);
		vec3 hair_tint = mix(vec3(1.0), mix(silt, paper, 0.65), 0.35) * 0.84;
		float nape = 1.0 - smoothstep(1.48, 1.68, toon_height);
		albedo = mix(albedo, albedo * hair_tint * (1.0 - 0.18 * nape), pale_part);
		// Painterly brush strokes at 0.55x architectural scale (~23 x 5.5 cm: >=4 px wide at gameplay distance, zero shimmer).
		float paint_px = look_pixel(dust_uv);
		float dab = look_stroke_dab(dust_uv, paint_px, 0.55) * look_brush;
		albedo *= 1.0 + dab * mix(0.55, 0.85, dark_part);
		// Pigment dabs in dark plates catch a whisper of the world's pale wash/stone tone, like opaque gouache strokes.
		albedo += max(dab, 0.0) * mix(silt, rock, 0.4) * mix(0.025, 0.065, plate) * dark_part * (1.0 - trim);
		// Watercolor wash and boot grounding: deeper ink toward the soles, soft silt bloom along seams and calves.
		float foot_wash = 1.0 - smoothstep(0.0, 0.42, toon_height);
		albedo = mix(albedo, shadow_ink * 0.65, foot_wash * 0.4 * look_wash * dark_part);
		float bloom = value_noise2(dust_uv / 0.12) * 0.6 + value_noise2(dust_uv / 0.05 + 5.0) * 0.4;
		float bloom_aa = fwidth(bloom) + 1e-4;
		float dust = smoothstep(0.66 - bloom_aa, 0.66 + bloom_aa, bloom);
		float tide = 1.0 - smoothstep(0.0, 0.022 + bloom_aa * 1.5, abs(bloom - 0.66));
		float dusted = toon_dust * look_wash * dark_part * (1.0 - trim);
		albedo = mix(albedo, silt * 0.18, dust * 0.45 * dusted);
		albedo *= 1.0 - tide * 0.28 * dusted;
		// Subtle hand-drawn ink hatching in lower/crevice zones (6 cm spacing: stays readable and clean at play scale).
		float shade_hatch = clamp(foot_wash * 0.7 + (1.0 - smoothstep(0.0, 0.06, f.edge)) * 0.35, 0.0, 1.0);
		float hatch = look_hatch_lines(dust_uv, 0.785, 0.06, shade_hatch * 0.4, paint_px);
		albedo = mix(albedo, look_ink_color.rgb * 0.6, hatch * min(look_hatch, 1.0) * 0.6);
		rough = mix(rough, 0.82, 0.5 * look_brush);
	} else {
		albedo = mix(albedo, look_ink_color.rgb, toon_ink_body * dark_part * (1.0 - trim));
		albedo += toon_lift * dark_part * (1.0 - trim) * mix(vec3(1.0), ink_hue, toon_ink_body);
		float dusted = toon_dust * look_wash * dark_part * (1.0 - trim);
		// Dust gathers where a part meets the next (its ends and seams), ragged, with the darker tide line its edge dried to;
		// laid on each part from its own corner shifted by its size.
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
	}
#endif
	// Paper grain only on banded figures (their brushwork is already laid at figure scale above); ink is on the outline pass.
#ifdef ANDROID_TOON
	if (toon_world_bands > 0.5) {
		albedo *= look_paper(f.uv, look_pixel(f.uv));
	} else {
		albedo = look_drawn(albedo, f.uv, 1e3);
	}
#else
	albedo = look_drawn(albedo, f.uv, 1e3);
#endif
	look_mark_at(f.uv);
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
