// The plume of a HeatColumn: one sheet exactly as wide and tall as the column that burns (its QuadMesh is sized from the
// column's region), unshaded: heat is its own light. Premultiplied: it dims what lies behind it by its coverage and adds its own
// glow, so it keeps its colour on any backdrop (pure addition turns pink on blue stone). Its colour is heat's own ramp, deep red
// through orange to white-hot, never the biome's accent. Tongues of heat rise through it, broad and white-hot at the vent,
// thinning and reddening toward the top; a soft bright rim at each side keeps its true edges legible, and heat pools a little
// under the hood or roof that ends it. The tongues are a baked, seamless noise texture with mipmaps, scrolled up, stretched tall
// and swayed sideways by a slower sample of the same texture, never noise computed per pixel (docs/art-direction.md#stable-image).
// The rims are kept a few pixels wide on screen and anti-aliased.
shader_type spatial;
render_mode blend_premul_alpha, depth_draw_never, cull_disabled, unshaded, shadows_disabled;

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
/// The rims at its sides: width (meters, never under `edge_pixels` on screen) and brightness.
uniform float edge_width : hint_range(0.0, 0.4) = 0.06;
uniform float edge_pixels : hint_range(1.0, 6.0) = 2.5;
uniform float edge_strength : hint_range(0.0, 4.0) = 1.0;
/// The white-hot core over the vent, and the heat pooled under its top (meters).
uniform float foot_band : hint_range(0.0, 3.0) = 0.5;
uniform float top_band : hint_range(0.0, 2.0) = 0.3;
/// Heat's ramp, coolest to hottest.
uniform vec4 dull_red : source_color = vec4(0.5, 0.06, 0.02, 1.0);
uniform vec4 flame : source_color = vec4(1.0, 0.4, 0.08, 1.0);
uniform vec4 white_hot : source_color = vec4(1.0, 0.85, 0.55, 1.0);

vec3 ramp(float t) {
	t = clamp(t, 0.0, 1.0);
	return t < 0.5 ? mix(dull_red.rgb, flame.rgb, t * 2.0) : mix(flame.rgb, white_hot.rgb, t * 2.0 - 1.0);
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

	float width = max(edge_width, edge_pixels * fwidth(m.x));
	float rim = (1.0 - smoothstep(0.0, width, side)) * (0.6 + 0.4 * tongue);
	float halo = exp(-side / 0.3);
	float foot = exp(-m.y / foot_band);
	float pooled = exp(-(size.y - m.y) / top_band);
	float cool = 1.0 - 0.55 * h;

	float temp = 0.2 + 0.55 * tongue * cool + 0.6 * foot + 0.25 * rim;
	float glow = 0.9 * tongue * cool + 0.8 * foot + edge_strength * rim + 0.2 * halo + 0.4 * pooled;
	float cover = 0.65 * tongue * cool + 0.35 * foot + 0.6 * rim + 0.2 * halo + 0.2 * pooled;

	ALBEDO = ramp(temp) * glow * heat * strength;
	ALPHA = clamp(cover, 0.0, 1.0) * opacity * strength;
}
