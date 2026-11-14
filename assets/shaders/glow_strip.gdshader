// A strip of light set in old machinery (a slit in a core wall): the biome's accent, mixed a little toward white at its
// middle line, breathing slowly with a flicker running along it. Unshaded, so its light is its albedo (an unshaded material
// draws no emission); decor, so kept dim beside gameplay glows.
shader_type spatial;
render_mode unshaded, cull_back, shadows_disabled;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"

uniform float energy : hint_range(0.0, 8.0) = 1.6;

varying vec3 v_world_pos;

void vertex() {
	v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float across = abs(UV.y - 0.5) * 2.0;
	float core = 1.0 - smoothstep(0.0, 1.0, across);
	float run = 0.75 + 0.25 * value_noise2(vec2(v_world_pos.x * 0.8 - TIME * 0.6, v_world_pos.y));
	float breathe = 0.85 + 0.15 * sin(TIME * 0.7 + v_world_pos.x * 0.3);
	vec3 accent = srgb_to_linear(biome_accent.rgb);
	ALBEDO = mix(accent, vec3(1.0, 0.9, 0.8), core * 0.35) * energy * core * run * breathe;
}
