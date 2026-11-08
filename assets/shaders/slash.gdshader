shader_type spatial;
// The slash a strike's blade leaves (SlashTrail): the band its edge swept. UV.x runs along it (0 the oldest, 1 the newest),
// UV.y across it from the inner end (0) to the blade's tip (1); COLOR.a is how much of each sample is left (its strength and
// age). A bright edge just inside the tip's path over a pale veil, sharp where it is new; the veil's inner side takes the
// biome's accent.
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, shadows_disabled;

#include "res://assets/shaders/lib/palette.gdshaderinc"

uniform vec3 color : source_color = vec3(0.86, 0.86, 0.92);
/// How the slash's veil fades toward its inner end (higher keeps it closer to the tip).
uniform float edge : hint_range(0.5, 4.0) = 1.6;
/// The veil at its densest, behind the bright edge.
uniform float veil : hint_range(0.0, 1.0) = 0.45;
/// The bright edge just inside the tip's path: how light it is (above 1 it glows).
uniform float energy : hint_range(0.0, 4.0) = 1.8;

void fragment() {
	// Under MSAA a pixel is shaded at its center even where only a sample of it is covered, so the varyings run a little
	// past the band's edges: clamped, or pow() of a negative UV.y is NaN, a black pixel in the frame.
	float y = clamp(UV.y, 0.0, 1.0);
	float life = clamp(COLOR.a, 0.0, 1.0);
	float core = smoothstep(0.72, 0.9, y) * (1.0 - smoothstep(0.96, 1.0, y) * 0.6);
	float across = max(veil * pow(y, edge), core);
	vec3 accent = srgb_to_linear(biome_accent.rgb);
	ALBEDO = mix(mix(accent, vec3(1.0), 0.5), color, smoothstep(0.35, 0.95, y)) * mix(1.0, energy, core);
	ALPHA = clamp(across * life * sqrt(life), 0.0, 1.0);
}
