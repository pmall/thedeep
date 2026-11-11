// Water surface (WaterBody), drawn without reading the screen (docs/art-direction.md#stable-image): a premultiplied
// layer over whatever lies under and behind it. Its opacity is the water the view crosses, computed from the body's
// known box (its floor and sides, per-instance uniforms from WaterBody): the longer the path, the more of the deep
// color, so the view sinks into the water with depth. Toward grazing angles (Fresnel) it hides the scene under it and
// takes a faint tint of the biome's water; the reflection itself is the specular: the room pictured once by the
// WaterBody's reflection probe, and highlights from the room's lights. Broad, slow ripples bend the normal, faded out where they would shrink under a few pixels. The colors
// are the biome's (lib/water.gdshaderinc), so the same water follows every layer.
shader_type spatial;
render_mode blend_premul_alpha, depth_draw_always, cull_back, fog_disabled, specular_schlick_ggx;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"
#include "res://assets/shaders/lib/water.gdshaderinc"

/// The body's floor (world Y), set by the WaterBody.
instance uniform float floor_y = -1e4;
/// The body's sides in world X/Z (x min, z min, x max, z max), set by the WaterBody.
instance uniform vec4 bounds = vec4(-1e4, -1e4, 1e4, 1e4);

group_uniforms ripples;
/// Ripple size, meters (across the room; three times longer into the scene, where the view foreshortens them).
uniform float ripple_scale : hint_range(0.5, 8.0) = 3.0;
uniform float ripple_strength : hint_range(0.0, 1.0) = 0.12;
uniform float ripple_speed : hint_range(0.0, 2.0) = 0.18;

group_uniforms look;
/// How fast the view dies with distance through the water (per meter).
uniform float absorption : hint_range(0.0, 4.0) = 0.9;
/// The least the surface hides, looking straight down into shallow water.
uniform float film : hint_range(0.0, 1.0) = 0.12;
/// Reflection share looking straight down; it rises toward grazing angles (Fresnel).
uniform float reflectance : hint_range(0.0, 1.0) = 0.04;
/// How much of the biome's water tint the surface takes where it reflects (under the probe's picture).
uniform float sky_tint : hint_range(0.0, 1.0) = 0.55;
group_uniforms;

varying vec3 v_world_pos;

void vertex() {
	v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float ripple_height(vec2 q, float t) {
	return fbm2(q + vec2(t * 0.6, t * 0.35), 2) + 0.5 * value_noise2(q * 1.7 - vec2(t * 0.4, -t * 0.7));
}

// How far the ray from `p` along `d` runs inside the body's box before it meets the floor or a side.
float path_in_water(vec3 p, vec3 d) {
	vec3 safe = vec3(abs(d.x) < 1e-4 ? 1e-4 : d.x, min(d.y, -1e-4), abs(d.z) < 1e-4 ? 1e-4 : d.z);
	float to_floor = (p.y - floor_y) / -safe.y;
	float to_x = ((safe.x > 0.0 ? bounds.z : bounds.x) - p.x) / safe.x;
	float to_z = ((safe.z > 0.0 ? bounds.w : bounds.y) - p.z) / safe.z;
	return max(min(to_floor, min(to_x, to_z)), 0.0);
}

void fragment() {
	float t = TIME * ripple_speed;
	vec2 q = v_world_pos.xz / vec2(ripple_scale, ripple_scale * 3.0);
	// Ripples smaller than a few pixels would shimmer: fade them by the pattern's size on screen.
	float footprint = length(fwidth(q));
	float strength = ripple_strength * (1.0 - smoothstep(0.08, 0.25, footprint));
	float e = 0.05;
	float h = ripple_height(q, t);
	vec2 gradient = vec2(ripple_height(q + vec2(e, 0.0), t) - h, ripple_height(q + vec2(0.0, e), t) - h) / e;
	vec3 world_normal = normalize(vec3(-gradient.x * strength, 1.0, -gradient.y * strength / 3.0));
	vec3 n = normalize((VIEW_MATRIX * vec4(world_normal, 0.0)).xyz);
	vec3 v = normalize(VERTEX);

	// Through the surface: the water's path along the view sinks what lies under it into the deep color.
	vec3 d = normalize(v_world_pos - CAMERA_POSITION_WORLD);
	float sunk = mix(film, 1.0, 1.0 - exp(-path_in_water(v_world_pos, d) * absorption));
	vec3 deep = srgb_to_linear(biome_water_deep.rgb);

	vec3 reflected = srgb_to_linear(biome_water.rgb) * sky_tint;

	float facing = clamp(dot(n, -v), 0.0, 1.0);
	float fresnel = reflectance + (1.0 - reflectance) * pow(1.0 - facing, 5.0);
	ALBEDO = vec3(0.0);
	// Premultiplied: what the surface adds over the scene, and how much of the scene it hides.
	EMISSION = deep * sunk * (1.0 - fresnel) + reflected * fresnel;
	ALPHA = 1.0 - (1.0 - sunk) * (1.0 - fresnel);
	ROUGHNESS = 0.02;
	SPECULAR = 0.5;
	METALLIC = 0.0;
	NORMAL = n;
}
