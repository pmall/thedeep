// The glow of an item and of the lock it opens (a key, a code: ItemKinds gives the color): a lit metal or a burning glyph, its tint
// set per node (`tint`, an instance uniform: ItemPickup, LockedDoor), so one material serves every kind. Color means gameplay
// (docs/art-direction.md#look): bright and saturated, like the relics' and anchors' glow.
shader_type spatial;
render_mode cull_back;

/// The item kind's color (sRGB); dimmed by the door once it is open.
instance uniform vec4 tint : source_color = vec4(0.95, 0.78, 0.52, 1.0);
uniform float energy : hint_range(0.0, 8.0) = 2.2;

void fragment() {
	float breathe = 0.85 + 0.15 * sin(TIME * 1.3);
	ALBEDO = tint.rgb * 0.35;
	ROUGHNESS = 0.35;
	METALLIC = 0.6;
	EMISSION = tint.rgb * energy * breathe;
}
