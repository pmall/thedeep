// The fire in a furnace mouth on a wall behind the play plane (WallDressing's MouthGlow, seen through its grate): a dark
// hearth lit from below, a bed of embers glowing along its foot and broad tongues of flame rising from it, dying out before
// the lintel. Its colors are the cool half of heat's ramp (BiomeProfile.MoltenCool to Molten, never white-hot) and it stays
// dim: decor, so the gameplay glows (slag, heat) keep the layer's saturated light (docs/art-direction.md#ambiance). The
// tongues are a baked noise texture (FlameSurface) with mipmaps scrolled up in world meters, never noise computed per pixel
// (docs/art-direction.md#stable-image); the quad's UV runs down from its top. Hazed by its depth like the wall it sits in
// (lib/haze).
shader_type spatial;
render_mode cull_back, shadows_disabled;

#include "res://assets/shaders/lib/palette.gdshaderinc"
#include "res://assets/shaders/lib/haze.gdshaderinc"

/// The flames: the red channel of the baked "flames" surface set (core/surfaces/FlameSurface.cs), seamless, with mipmaps.
uniform sampler2D flames : hint_default_black, filter_linear_mipmap, repeat_enable;
/// One tile of the flames, meters (across, up), and how fast they rise, m/s.
uniform vec2 tile = vec2(1.6, 3.2);
uniform float rise = 0.6;
/// The fire's brightness.
uniform float heat : hint_range(0.0, 8.0) = 1.2;
/// The ember bed's height as a share of the mouth's, and how high the tongues reach (share).
uniform float bed : hint_range(0.0, 0.5) = 0.14;
uniform float reach : hint_range(0.0, 1.0) = 0.8;

varying vec3 v_world_pos;

void vertex() {
	v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float h = 1.0 - UV.y;
	vec2 m = v_world_pos.xy;
	float n1 = texture(flames, vec2(m.x / tile.x, (m.y - TIME * rise) / tile.y)).r;
	float n2 = texture(flames, vec2(m.x / (0.5 * tile.x) + 0.41, (m.y - TIME * rise * 1.6) / (0.5 * tile.y))).r;
	float n = 0.65 * n1 + 0.35 * n2;
	// Tongues thin as they climb and are gone at the reach; the bed glows under them, a little uneven.
	float climb = clamp(h / reach, 0.0, 1.0);
	float tongue = smoothstep(0.42 + 0.2 * climb, 0.62 + 0.2 * climb, n) * (1.0 - climb);
	float embers = exp(-h / max(bed, 1e-3)) * (0.7 + 0.6 * n1);
	float fire = clamp(0.8 * embers + 0.9 * tongue, 0.0, 1.0);
	// The hearth's own dull glow, deepest at the foot, so the bars stand dark against it all the way up.
	float hearth = 0.12 * (1.0 - 0.8 * h);
	vec3 cool = srgb_to_linear(biome_molten_cool.rgb);
	vec3 molten = srgb_to_linear(biome_molten.rgb);
	ALBEDO = vec3(0.02);
	ROUGHNESS = 1.0;
	EMISSION = (mix(cool, molten, fire) * fire + cool * hearth) * heat;
	HAZE
}
