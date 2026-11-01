// The architecture far behind a room (BackdropArchitecture): cheap, because it covers much of the screen and sits hazed
// and out of focus, where the play plane's rock and metal shaders (bump, cracks, rivets, frost noise) cost the most
// and show the least. A palette color with a little tone per piece, lines drawn from the world (courses of stone or
// panel seams), a cap of rime on what faces up in a frosted layer, and the haze by distance (lib/haze).
shader_type spatial;
render_mode cull_back;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"
#include "res://assets/shaders/lib/haze.gdshaderinc"

global uniform float biome_frost;
global uniform vec4 biome_frost_color;

uniform int slot : hint_enum("Rock", "Metal", "Backdrop", "Backdrop accent", "Foreground") = 2;
/// The lines: 0 courses of stone, 1 panel seams, 2 none (ice).
uniform int lines : hint_enum("Courses", "Panels", "None") = 0;
/// Height of a course or a panel, meters, and the length of a block or a panel along it.
uniform vec2 cell = vec2(1.1, 2.2);
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
	float line = 0.0;
	if (lines == 0) {
		float course = floor(up / cell.x);
		float fx = fract(along / cell.y + hash12(vec2(course, 3.1)));
		float fy = fract(up / cell.x);
		line = max(1.0 - smoothstep(0.0, 0.03, min(fy, 1.0 - fy)), 1.0 - smoothstep(0.0, 0.02, min(fx, 1.0 - fx)));
	} else if (lines == 1) {
		vec2 f = fract(vec2(along / cell.y, up / cell.x));
		vec2 d = min(f, 1.0 - f) * vec2(cell.y, cell.x);
		line = 1.0 - smoothstep(0.0, 0.04, min(d.x, d.y));
	}
	float tone = 0.85 + 0.3 * v_seed + 0.15 * (value_noise2(vec2(along, up) * 0.15) - 0.5);
	vec3 albedo = palette_color(slot) * tone * (1.0 - 0.35 * line);
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
