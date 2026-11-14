// The plume of a HeatColumn: one sheet exactly as wide and tall as the column that burns (its QuadMesh is sized from the
// column's region), unshaded: heat is its own light. Premultiplied: it dims what lies behind it by its coverage and adds its own
// glow, so it keeps its colour on any backdrop (pure addition turns pink on blue stone). Its colour is heat's own ramp, the
// biome's molten colours from dull red through its glow to white-hot (BiomeProfile.Molten), never its decor accent. Tongues of heat rise through it, broad and white-hot at the vent,
// thinning and reddening toward the top. At each side the tongues thin a little over its last few tenths of a meter and a faint
// warmth licks up the edge, broken by the tongues, so the column's true edges read as the flames' own edge, never as a pane of
// glass with a bright frame; the heat that pools under the hood or roof that ends it is broken the same way. The tongues are a
// baked, seamless noise texture with mipmaps, scrolled up, stretched tall and swayed sideways by a slower sample of the same
// texture, never noise computed per pixel (docs/art-direction.md#stable-image). The sides' fade is never narrower than a few
// pixels on screen, so it stays anti-aliased.
shader_type spatial;
render_mode blend_premul_alpha, depth_draw_never, cull_disabled, unshaded, shadows_disabled;

#include "res://assets/shaders/lib/palette.gdshaderinc"

/// The column's width and height (meters), set by the HeatColumn so the texture keeps its scale on any column.
instance uniform vec2 size = vec2(2.0, 6.0);
/// How strong the plume shows (lower once the pilgrim is plated: it reads as passable).
instance uniform float strength = 1.0;

/// The tongues: seamless noise, baked.
uniform sampler2D wisps : hint_default_black, filter_linear_mipmap, repeat_enable;
/// The size of one tile of the tongues (meters, across and up), how fast they rise (m/s) and how far they sway (meters).
uniform vec2 tongue_tile = vec2(1.8, 8.0);
uniform float rise = 1.8;
uniform float sway = 0.3;
/// The glow's brightness and the most the plume hides of what lies behind it.
uniform float heat : hint_range(0.0, 8.0) = 1.4;
uniform float opacity : hint_range(0.0, 1.0) = 0.8;
/// Its sides: how far in the tongues thin and the edge warmth reaches (meters, never under `edge_pixels` on screen), how
/// thin the tongues get at the very edge (share) and the warmth's brightness.
uniform float edge_width : hint_range(0.0, 1.0) = 0.25;
uniform float edge_pixels : hint_range(1.0, 12.0) = 4.0;
uniform float edge_thin : hint_range(0.0, 1.0) = 0.4;
uniform float edge_strength : hint_range(0.0, 4.0) = 0.5;
/// The white-hot core over the vent, and the heat pooled under its top (meters).
uniform float foot_band : hint_range(0.0, 3.0) = 0.5;
uniform float top_band : hint_range(0.0, 2.0) = 0.3;
// Heat's ramp, coolest to hottest, linear.
vec3 ramp(float t) {
	t = clamp(t, 0.0, 1.0);
	vec3 molten = srgb_to_linear(biome_molten.rgb);
	return t < 0.5 ? mix(srgb_to_linear(biome_molten_cool.rgb), molten, t * 2.0)
			: mix(molten, srgb_to_linear(biome_molten_hot.rgb), t * 2.0 - 1.0);
}

void fragment() {
	// Meters from its west side and from its foot (the quad's UV runs down from its top); h is the height's share.
	vec2 m = vec2(UV.x, 1.0 - UV.y) * size;
	float h = m.y / size.y;
	float side = min(m.x, size.x - m.x);

	// Tongues: the noise stretched tall and rising, swayed more the higher they climb, thinning toward the top.
	float bend = texture(wisps, vec2(m.x / 4.0, (m.y - TIME * rise * 0.4) / 9.0) + vec2(0.53, 0.0)).r - 0.5;
	float x = m.x + bend * 2.0 * sway * (0.3 + h);
	float n1 = texture(wisps, vec2(x / tongue_tile.x, (m.y - TIME * rise) / tongue_tile.y)).r;
	float n2 = texture(wisps, vec2(x / (0.55 * tongue_tile.x) + 0.37, (m.y - TIME * rise * 1.4) / (0.6 * tongue_tile.y))).r;
	float n = 0.65 * n1 + 0.35 * n2;
	float thin = mix(0.4, 0.66, h);
	float tongue = smoothstep(thin, thin + 0.16, n) * (1.0 - 0.5 * h);

	// The sides: tongues thinning toward the edge, still reaching it, and a faint warmth licking up it where n1 runs high.
	float width = max(edge_width, edge_pixels * fwidth(m.x));
	float inside = smoothstep(0.0, width, side);
	tongue *= mix(edge_thin, 1.0, inside);
	float licks = smoothstep(0.4, 0.7, n1) * (1.0 - inside);
	float foot = exp(-m.y / foot_band) * mix(0.6, 1.0, inside);
	float pooled = exp(-(size.y - m.y) / top_band) * smoothstep(0.35, 0.7, n2);
	float cool = 1.0 - 0.55 * h;

	float temp = 0.2 + 0.55 * tongue * cool + 0.6 * foot + 0.2 * licks;
	float glow = 0.9 * tongue * cool + 0.8 * foot + edge_strength * licks + 0.2 * pooled;
	float cover = 0.65 * tongue * cool + 0.35 * foot + 0.25 * licks + 0.08 * pooled;

	ALBEDO = ramp(temp) * glow * heat * strength;
	ALPHA = clamp(cover, 0.0, 1.0) * opacity * strength;
}
