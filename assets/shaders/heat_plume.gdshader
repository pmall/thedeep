// The plume of a HeatColumn: a sheet exactly as wide and tall as the column that burns (its QuadMesh is sized from the column's
// region), added over what lies behind it, unshaded: heat is its own light. Soft inside, a bright line at each side so its true
// edges read, white-hot at its foot where it leaves the vent, the biome's accent higher up, and brighter again where it pools
// under the hood or roof that ends it. Wisps rise through it: a baked, seamless noise texture with mipmaps, scrolled up and
// stretched tall (two layers at different speeds), never noise computed per pixel (docs/art-direction.md#stable-image). The
// edge lines are kept a few pixels wide on screen and anti-aliased.
shader_type spatial;
render_mode blend_add, depth_draw_never, cull_disabled, unshaded, shadows_disabled;

#include "res://assets/shaders/lib/palette.gdshaderinc"

/// The column's width and height (meters), set by the HeatColumn so the texture keeps its scale on any column.
instance uniform vec2 size = vec2(2.0, 6.0);
/// Shifts this sheet's wisps from its twin's.
instance uniform float phase = 0.0;
/// How strong the whole plume shows (lower once the pilgrim is plated: it reads as passable).
instance uniform float strength = 1.0;

/// The wisps: seamless noise, baked.
uniform sampler2D wisps : hint_default_black, filter_linear_mipmap, repeat_enable;
/// The size of one tile of the wisps (meters, across and up), and how fast they rise (m/s).
uniform vec2 wisp_tile = vec2(2.5, 5.0);
uniform float rise = 1.1;
/// The overall glow, the soft fill inside, and how much the wisps carve it.
uniform float heat : hint_range(0.0, 8.0) = 0.8;
uniform float fill : hint_range(0.0, 1.0) = 0.1;
uniform float wisp_strength : hint_range(0.0, 2.0) = 0.8;
/// The lines at its sides: width (meters, never under `edge_pixels` on screen) and brightness.
uniform float edge_width : hint_range(0.0, 0.4) = 0.08;
uniform float edge_pixels : hint_range(1.0, 6.0) = 3.0;
uniform float edge_strength : hint_range(0.0, 4.0) = 1.1;
/// How far into the column the sides' glow reaches (meters).
uniform float edge_falloff : hint_range(0.05, 2.0) = 0.35;
/// The white-hot band at its foot, and the pooled heat under its top (meters).
uniform float foot_band : hint_range(0.0, 2.0) = 0.45;
uniform float top_band : hint_range(0.0, 2.0) = 0.5;

void fragment() {
	// Meters from its west side and from its foot (the quad's UV runs down from its top).
	vec2 m = vec2(UV.x, 1.0 - UV.y) * size;
	float side = min(m.x, size.x - m.x);
	float from_top = size.y - m.y;

	vec2 q = vec2(m.x / wisp_tile.x, (m.y - TIME * rise) / wisp_tile.y) + vec2(phase, phase * 0.37);
	float w1 = texture(wisps, q).r;
	float w2 = texture(wisps, q * vec2(1.7, 1.3) + vec2(0.31, -TIME * rise * 0.35 / wisp_tile.y)).r;
	float wisp = smoothstep(0.42, 0.8, w1 * 0.6 + w2 * 0.4);

	float width = max(edge_width, edge_pixels * fwidth(m.x));
	float line = 1.0 - smoothstep(0.0, width, side);
	float sides = exp(-side / edge_falloff);

	float foot = exp(-m.y / foot_band);
	float pooled = exp(-from_top / top_band);
	float body = fill + wisp_strength * wisp * (0.5 + 0.5 * foot) * (0.55 + 0.45 * (1.0 - m.y / size.y));
	float glow = body + sides * 0.3 + line * edge_strength + pooled * 0.3 + foot * 0.5;

	vec3 accent = srgb_to_linear(biome_accent.rgb);
	vec3 white = vec3(1.0, 0.78, 0.45);
	vec3 color = mix(white, accent, smoothstep(0.0, 1.0, m.y));
	ALBEDO = color * glow * heat * strength;
}
