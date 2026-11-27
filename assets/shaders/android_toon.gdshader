// Android, drawn as a cartoon: the same finishes as android.gdshader (every material's knobs carry over), lit in flat
// bands instead of smooth shading. Each light gives a lit side and a shade cut by a crisp edge, one hard highlight and a
// back-light rim; the edges are ragged by a still grain fixed to the parts (lib/android: toon_grain), like ink on rough
// paper; a look may scratch pale lines into the lit side (toon_scratch). The ambient light and the glows stay as they are; the smooth look's emitted edge becomes a dim crisp line (lib/android). Used by ToonLook, which swaps it in over android.gdshader.
shader_type spatial;

#define ANDROID_TOON
#include "res://assets/shaders/lib/android.gdshaderinc"

group_uniforms toon;
/// Where the lit side turns to shade: the cosine between the surface and the light.
uniform float toon_threshold : hint_range(-1.0, 1.0) = 0.1;
/// How soft that cut and the highlight's edge are, in cosine: wide enough that the turn reads painted, not cel-shaded.
uniform float toon_softness : hint_range(0.0, 0.5) = 0.2;
/// The light left in the shade, as a share of the lit side's.
uniform float toon_shade : hint_range(0.0, 1.0) = 0.42;
/// The hard highlight's strength (it is white on any finish: the light's color, not the surface's).
uniform float toon_gloss : hint_range(0.0, 4.0) = 0.18;
/// The highlight's size on a smooth finish and on a rough one, as the cosine of the half angle where it starts.
uniform float toon_gloss_smooth : hint_range(0.5, 1.0) = 0.97;
uniform float toon_gloss_rough : hint_range(0.5, 1.0) = 0.86;
/// How much of the light's color the highlight keeps (0: white).
uniform float toon_gloss_tint : hint_range(0.0, 1.0) = 0.25;
/// The rim a light behind the body draws along its silhouette.
uniform float toon_rim : hint_range(0.0, 4.0) = 0.5;
/// The brightest a light may paint the figure (its brightest channel): a lamp close by lights it flat, never burns a blot.
uniform float toon_light_cap : hint_range(0.1, 4.0) = 0.8;
group_uniforms;

// The scratches' spacing at the finest, meters; each doubling of the figure's distance doubles it, blended between the
// two nearest so no line pops, and every line stays at least five pixels from the next (docs/art-direction.md#stable-image).
const float SCRATCH_SPACING = 0.035;

// Rings across the part (along its length, as an engraver draws round a limb), each line wider the brighter the light,
// so their weight carries the shading. 0 to 1.
float scratch_lines(float along, float across, float light, float pixel) {
	float level = max(log2(pixel * 5.0 / SCRATCH_SPACING), 0.0);
	float octave = floor(level);
	float lines = 0.0;
	for (int i = 0; i < 2; i++) {
		float spacing = SCRATCH_SPACING * exp2(octave + float(i));
		float t = along / spacing + (value_noise2(vec2(across * 4.0, along * 0.7)) - 0.5) * 0.3;
		float d = abs(fract(t) - 0.5);
		float w = mix(0.02, 0.12, light);
		float aa = pixel / spacing;
		float line = 1.0 - smoothstep(w - aa, w + aa, d);
		lines += line * (i == 0 ? 1.0 - fract(level) : fract(level));
	}
	return lines * step(0.02, light);
}

void light() {
	float rag = toon_noise(toon_uv) * toon_grain;
	vec3 light = LIGHT_COLOR * ATTENUATION;
	float peak = max(light.r, max(light.g, light.b));
	light *= min(1.0, toon_light_cap / max(peak, 1e-4));
	float ndl = dot(NORMAL, LIGHT);
	float lit = smoothstep(toon_threshold - toon_softness, toon_threshold + toon_softness, ndl + rag);
	DIFFUSE_LIGHT += light * mix(toon_shade, 1.0, lit) / PI;
	if (toon_scratch > 0.0) {
		// Only where the light is strong: the scratches draw the light, never the whole body.
		float lighter = smoothstep(0.35, 0.9, clamp(ndl, 0.0, 1.0) * lit);
		// A little across the part as well as along it, so the lines slant as a hand draws them.
		SPECULAR_LIGHT += light * scratch_lines(toon_uv.x + toon_uv.y * 0.5, toon_uv.y, lighter, toon_pixel) * toon_scratch * 0.2;
	}

	float ndh = dot(NORMAL, normalize(VIEW + LIGHT));
	float at = mix(toon_gloss_smooth, toon_gloss_rough, toon_rough);
	float gloss = smoothstep(at - toon_softness * 0.5, at + toon_softness * 0.5, ndh + rag * 0.15) * lit;
	// Shine, not glow: the highlight keeps little of a warm lamp's color, so it never reads as a lit patch.
	vec3 shine = mix(vec3(max(light.r, max(light.g, light.b))), light, toon_gloss_tint);
	SPECULAR_LIGHT += shine * gloss * toon_gloss * (1.0 - 0.6 * toon_rough);

	float behind = smoothstep(-0.1, 0.3, -dot(LIGHT, VIEW));
	float back_rim = smoothstep(0.7, 0.74, 1.0 - dot(NORMAL, VIEW) + rag) * behind * clamp(ndl + 0.5, 0.0, 1.0);
	SPECULAR_LIGHT += light * back_rim * toon_rim;
}
