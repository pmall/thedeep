// A column of heat (HeatZone): what is behind it shimmers, bent by rising waves, and a glow in the biome's accent
// rises from its foot and fades upward. Unshaded and soft at its sides, so it reads as hot air, not as a surface.
shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled, unshaded, fog_disabled;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"

uniform sampler2D screen : hint_screen_texture, filter_linear_mipmap, repeat_disable;

/// How far the view through the heat wavers (share of the screen), and how fast the waves rise.
uniform float waver : hint_range(0.0, 0.03) = 0.006;
uniform float rise : hint_range(0.0, 4.0) = 1.4;
/// The glow at the foot of the column, and how quickly it fades upward (per meter).
uniform float glow : hint_range(0.0, 4.0) = 0.55;
uniform float falloff : hint_range(0.0, 2.0) = 0.35;
/// The column's height (meters), set by the zone.
instance uniform float height = 6.0;

varying vec3 v_world_pos;

void vertex() {
	v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float t = TIME;
	vec2 p = v_world_pos.xy;
	float up = clamp(UV.y, 0.0, 1.0);          // 0 at the top, 1 at the foot
	float above = (1.0 - up) * height;         // meters above the foot
	float side = 1.0 - smoothstep(0.55, 1.0, abs(UV.x * 2.0 - 1.0));
	vec2 wave = vec2(value_noise2(vec2(p.x * 1.7, p.y * 0.9 - t * rise)), value_noise2(vec2(p.x * 1.3 + 4.1, p.y * 1.1 - t * rise * 1.3))) - 0.5;
	float strength = side * (0.35 + 0.65 * exp(-above * 0.15));
	vec3 behind = textureLod(screen, SCREEN_UV + wave * waver * strength, 0.0).rgb;
	vec3 hot = srgb_to_linear(biome_accent.rgb);
	float flicker = 0.8 + 0.2 * value_noise2(vec2(p.x * 0.8, t * 2.0));
	float g = glow * exp(-above * falloff) * pow(up, 1.5) * side * flicker;
	ALBEDO = behind + hot * g;
	ALPHA = side;
}
