// The glow of the things the pilgrim takes and works (a key, a code, a power-up's crystal, a lever's grip, a door's lock): a lit
// metal or a burning glyph in its kind's color, set per node (`tint`, an instance uniform: ItemKinds, PowerStats), so one material
// serves every kind. Color means gameplay (docs/art-direction.md#look): saturated and bright, the bloom carrying it; the faces
// turned to the eye hold the color, the edges turning away brighten a little toward white, and each flat facet of a crystal
// takes its own value. `glow` lights it (1), spends it (near 0) and flares it (above 1) as the
// thing is used. The glyph pattern burns a code's script into a plate.
shader_type spatial;
render_mode cull_back;

#include "res://assets/shaders/lib/noise.gdshaderinc"

/// The kind's color (sRGB).
instance uniform vec4 tint : source_color = vec4(0.95, 0.78, 0.52, 1.0);
/// 1 lit, 0 spent, above 1 a flare.
instance uniform float glow : hint_range(0.0, 4.0) = 1.0;
uniform float energy : hint_range(0.0, 8.0) = 2.6;
/// How much brighter the edges turning away from the eye glow, and how far they whiten.
uniform float edge : hint_range(0.0, 2.0) = 0.9;
uniform float edge_white : hint_range(0.0, 1.0) = 0.15;
/// Plain, or a code's glyphs burning on dark metal (cells across the surface frame's UV, meters).
uniform int pattern : hint_enum("Plain", "Glyphs") = 0;
uniform float glyph_cell : hint_range(0.02, 0.3) = 0.07;

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
		vec2 cell = floor(UV / glyph_cell);
		vec2 local = fract(UV / glyph_cell);
		float h = hash12(cell);
		float stroke = step(abs(local.x - (0.3 + 0.4 * h)), 0.09) * step(0.15, local.y) * step(local.y, 0.85);
		float bar = step(abs(local.y - (0.25 + 0.5 * fract(h * 7.0))), 0.08) * step(0.2, local.x) * step(local.x, 0.8);
		mask = clamp(stroke + bar, 0.0, 1.0) * step(0.25, h);
	}

	ALBEDO = mix(color * 0.12, color * 0.3, mask);
	ROUGHNESS = 0.3;
	METALLIC = 0.6;
	vec3 hot = mix(color, vec3(1.0), edge_white * rim);
	EMISSION = hot * energy * breathe * lit * mask * facet * (1.0 + edge * rim);
}
