// The architecture far behind a room (BackdropArchitecture): cheap, because it covers much of the screen and sits hazed
// and out of focus, where the play plane's rock and metal shaders (bump, rivets, frost noise) cost the most
// and show the least. A palette color with a little tone per piece, a cap of rime on what faces up in a frosted layer, and the haze by distance (lib/haze).
shader_type spatial;
render_mode cull_back;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"
#include "res://assets/shaders/lib/haze.gdshaderinc"

global uniform float biome_frost;
global uniform vec4 biome_frost_color;

uniform int slot : hint_enum("Rock", "Metal", "Backdrop", "Backdrop accent", "Foreground") = 2;
uniform float roughness : hint_range(0.0, 1.0) = 0.85;
uniform float metallic : hint_range(0.0, 1.0) = 0.0;
/// Albedo multiplier.
uniform float shade : hint_range(0.0, 2.0) = 1.0;
/// A glossy rim at grazing angles (ice).
uniform float rim : hint_range(0.0, 1.0) = 0.0;

varying vec3 v_world_pos;
varying vec3 v_world_normal;
varying float v_seed;

void vertex() {
	v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	v_seed = hash12(MODEL_MATRIX[3].xy * 0.37 + MODEL_MATRIX[3].z);
}

void fragment() {
	vec3 p = v_world_pos;
	vec3 n = v_world_normal;
	// Along the face: X on faces toward the camera, Z on faces sideways.
	float along = abs(n.x) > 0.6 ? p.z : p.x;
	float up = abs(n.y) > 0.6 ? p.z : p.y;
	float tone = 0.85 + 0.3 * v_seed + 0.15 * (value_noise2(vec2(along, up) * 0.15) - 0.5);
	vec3 albedo = palette_color(slot) * tone;
	// Rime on what faces up, when the layer is frosted.
	float frost = biome_frost * smoothstep(0.5, 0.85, n.y);
	albedo = mix(albedo, srgb_to_linear(biome_frost_color.rgb) * 0.8, frost);
	ALBEDO = albedo * shade;
	EMISSION = vec3(0.0);
	ROUGHNESS = mix(roughness, 0.6, frost);
	METALLIC = metallic * (1.0 - frost);
	RIM = rim;
	RIM_TINT = 0.6;
	HAZE
}
