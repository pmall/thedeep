shader_type spatial;
// The pilgrim's swing: a pale crescent swept from above-behind to below-front. UV.x runs along the arc (0 at
// the start, 1 at the end), UV.y across it (0 inner edge, 1 outer edge). `progress` moves the leading edge
// along the arc; the crescent shows a short trail behind it and fades as the swing ends.
render_mode unshaded, blend_mix, cull_disabled, depth_draw_never, shadows_disabled;

#include "res://assets/shaders/lib/palette.gdshaderinc"

uniform float progress : hint_range(0.0, 1.0) = 0.0;
uniform float strength : hint_range(0.0, 1.0) = 0.0;
uniform vec3 color : source_color = vec3(0.95, 0.94, 1.0);
/// How much of the arc behind the leading edge stays lit.
uniform float trail : hint_range(0.05, 1.0) = 0.8;

void fragment() {
	float lead = progress * (1.0 + trail);
	float behind = lead - UV.x;
	float lit = smoothstep(0.0, 0.04, behind) * (1.0 - smoothstep(0.0, trail, behind));
	float across = smoothstep(0.0, 0.35, UV.y) * (1.0 - smoothstep(0.85, 1.0, UV.y));
	vec3 accent = srgb_to_linear(biome_accent.rgb);
	ALBEDO = mix(mix(accent, vec3(1.0), 0.55), color, smoothstep(0.3, 1.0, UV.y));
	ALPHA = lit * across * strength;
}
