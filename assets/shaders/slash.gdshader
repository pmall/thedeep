shader_type spatial;
// The slash a strike's blade leaves (SlashTrail): the band its edge swept. UV.x runs along it (0 the oldest, 1 the newest),
// UV.y across it from the inner end (0) to the blade's tip (1); COLOR.a is how much of each sample is left (its strength and
// age). Pale and brightest at the tip, sharp where it is new; its inner side takes the biome's accent.
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, shadows_disabled;

#include "res://assets/shaders/lib/palette.gdshaderinc"

uniform vec3 color : source_color = vec3(0.86, 0.86, 0.92);
/// How the slash fades toward its inner end (higher keeps only a thin edge near the tip).
uniform float edge : hint_range(0.5, 4.0) = 2.8;
/// The slash at its brightest (its newest edge).
uniform float opacity : hint_range(0.0, 1.0) = 0.8;

void fragment() {
	float life = clamp(COLOR.a, 0.0, 1.0);
	float across = pow(UV.y, edge) * (1.0 - smoothstep(0.96, 1.0, UV.y) * 0.4);
	vec3 accent = srgb_to_linear(biome_accent.rgb);
	ALBEDO = mix(mix(accent, vec3(1.0), 0.5), color, smoothstep(0.35, 0.95, UV.y));
	ALPHA = opacity * across * life * life;
}
