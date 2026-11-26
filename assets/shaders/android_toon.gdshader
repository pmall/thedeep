// Android, drawn as a cartoon: the same finishes as android.gdshader (every material's knobs carry over), lit in flat
// bands instead of smooth shading. Each light gives a lit side and a shade cut by a crisp edge, one hard highlight and a
// back-light rim; the edges are ragged by a still grain fixed to the parts (lib/android: toon_grain), like ink on rough
// paper. The ambient light and the glows stay as they are; the smooth look's emitted edge becomes a dim crisp line (lib/android). Used by ToonLook, which swaps it in over android.gdshader.
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

void light() {
	float rag = toon_noise(toon_uv) * toon_grain;
	vec3 light = LIGHT_COLOR * ATTENUATION;
	float peak = max(light.r, max(light.g, light.b));
	light *= min(1.0, toon_light_cap / max(peak, 1e-4));
	float ndl = dot(NORMAL, LIGHT);
	float lit = smoothstep(toon_threshold - toon_softness, toon_threshold + toon_softness, ndl + rag);
	DIFFUSE_LIGHT += light * mix(toon_shade, 1.0, lit) / PI;

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
