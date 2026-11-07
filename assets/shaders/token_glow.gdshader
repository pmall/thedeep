// The glow of the things the pilgrim takes and works (a key, a code, a power-up's crystal, a lever's grip, a door's lock): a lit
// metal or a burning glyph in its kind's color, set per node (`tint`, an instance uniform: ItemKinds, PowerStats), so one material
// serves every kind. Color means gameplay (docs/art-direction.md#look): saturated and bright, the bloom carrying it; the light
// is pushed past the kind's saturation (`chroma`) so it keeps its hue through the tonemapper; the faces turned to the eye hold the
// color, the edges turning away brighten and pale a little, and each flat facet of a crystal
// takes its own value. `glow` lights it (1), spends it (near 0) and flares it (above 1) as the
// thing is used. The glyph pattern burns a code's script, the makers' glyphs, into a plate.
shader_type spatial;
render_mode cull_back;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/glyph.gdshaderinc"

/// The kind's color (sRGB).
instance uniform vec4 tint : source_color = vec4(0.95, 0.78, 0.52, 1.0);
/// 1 lit, 0 spent, above 1 a flare.
instance uniform float glow : hint_range(0.0, 4.0) = 1.0;
uniform float energy : hint_range(0.0, 8.0) = 1.8;
/// How much brighter the edges turning away from the eye glow, and how far they pale.
uniform float edge : hint_range(0.0, 2.0) = 0.9;
uniform float edge_white : hint_range(0.0, 1.0) = 0.08;
/// How far the light's color is pushed past the kind's own saturation, at the same luminance: the rooms' tonemapper (AgX) whitens
/// bright light, so without it a key's gold reads cream and an azure pale.
uniform float chroma : hint_range(1.0, 3.0) = 2.0;
/// Plain, or a code's script burning on dark metal: the makers' glyphs (lib/glyph.gdshaderinc) in lines across the surface frame's
/// UV (meters), `glyph_height` tall, a share of them left out as the gaps between words.
uniform int pattern : hint_enum("Plain", "Glyphs") = 0;
uniform float glyph_height : hint_range(0.03, 0.3) = 0.1;
uniform float glyph_stroke : hint_range(0.04, 0.25) = 0.13;
uniform float glyph_gaps : hint_range(0.0, 0.9) = 0.2;

void fragment() {
	float breathe = 0.88 + 0.12 * sin(TIME * 1.3);
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	float rim = pow(1.0 - facing, 2.0);
	// Each flat facet takes its own value from its normal, so a crystal reads cut, not as a blob.
	float facet = 0.82 + 0.18 * dot(NORMAL, normalize(vec3(0.4, 0.8, 0.45)));
	vec3 color = tint.rgb;
	float lit = clamp(glow, 0.0, 4.0);
	float mask = 1.0;
	if (pattern == 1) {
		vec2 cell_size = vec2(0.72, 1.3) * glyph_height;
		vec2 q = UV / cell_size;
		vec2 id = floor(q);
		// Glyph-height units, upright as the eye reads them (UV's v runs down the face).
		vec2 p = vec2(fract(q.x), 1.0 - fract(q.y)) * vec2(0.72, 1.3) - vec2(0.06, 0.15);
		float d = glyph_distance(p, id, hash12(id * 1.7 + 0.37));
		float aa = fwidth(d) * 0.75;
		mask = (1.0 - smoothstep(glyph_stroke * 0.5 - aa, glyph_stroke * 0.5 + aa, d)) * step(glyph_gaps, hash12(id * 3.1 + 5.0));
	}

	ALBEDO = mix(color * 0.12, color * 0.3, mask);
	ROUGHNESS = 0.3;
	METALLIC = 0.6;
	float luma = dot(color, vec3(0.2126, 0.7152, 0.0722));
	vec3 vivid = max(mix(vec3(luma), color, chroma), vec3(0.0));
	vec3 hot = mix(vivid, vec3(luma), edge_white * rim);
	EMISSION = hot * energy * breathe * lit * mask * facet * (1.0 + edge * rim);
}
