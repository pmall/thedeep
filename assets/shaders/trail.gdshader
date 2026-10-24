// The pilgrim's dash trail: a band of light left behind, fading with age (vertex alpha) and toward its
// edges. Additive and unshaded; its color is the pilgrim's own glow, not the biome's.
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, shadows_disabled, depth_draw_never;

uniform vec3 color : source_color = vec3(0.86, 0.8, 1.0);
uniform float energy : hint_range(0.0, 8.0) = 1.4;

void fragment() {
	float across = sin(UV.y * PI);
	float a = COLOR.a * across * across;
	ALBEDO = color * energy * a;
}
