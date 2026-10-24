// Ambient particles (dust, silt, drips, bubbles) on camera-facing quads: the camera always looks down -Z
// and particles never rotate, so a QuadMesh facing +Z needs no billboard. Lit from every side alike
// (they are tiny and translucent), with no ambient light and only a share of the key light: motes vanish
// in the dark and glow inside light shafts and near lamps. `glow` is the only light they keep in the dark.
// Color: the biome's particle tint times the particle's own color (alpha fades from the color ramp).
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, specular_disabled, ambient_light_disabled;

#include "res://assets/shaders/lib/palette.gdshaderinc"

global uniform vec4 biome_particle;

uniform int shape : hint_enum("Mote", "Streak", "Bubble") = 0;
uniform float opacity : hint_range(0.0, 1.0) = 0.6;
/// Light the particle gives off by itself, so it never goes fully black in shadow.
uniform float glow : hint_range(0.0, 4.0) = 0.05;
/// Share of the room's key (directional) light they catch; shafts and lamps light them fully.
uniform float key_light : hint_range(0.0, 1.0) = 0.1;
/// Light multiplier: motes scatter light toward the camera far more than a matte surface would, so they
/// must outshine the lit fog around them.
uniform float light_gain : hint_range(0.0, 8.0) = 5.0;

void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	float a;
	if (shape == 1) {
		// A falling drop drawn as a streak, brightest at its leading (bottom) end.
		a = (1.0 - smoothstep(0.2, 1.0, abs(p.x))) * UV.y * UV.y;
	} else if (shape == 2) {
		// A bubble: a thin bright rim and a small highlight.
		a = smoothstep(0.55, 0.8, r) * (1.0 - smoothstep(0.85, 1.0, r));
		a = max(a, 1.0 - smoothstep(0.0, 0.22, length(p - vec2(-0.35, -0.35))));
	} else {
		a = 1.0 - smoothstep(0.25, 1.0, r);
	}
	vec3 tint = srgb_to_linear(biome_particle.rgb) * COLOR.rgb;
	ALBEDO = tint;
	EMISSION = tint * glow;
	ALPHA = clamp(a * opacity * COLOR.a, 0.0, 1.0);
}

void light() {
	float share = LIGHT_IS_DIRECTIONAL ? key_light : 1.0;
	DIFFUSE_LIGHT += ATTENUATION * LIGHT_COLOR * share * light_gain / PI;
}
