// Water surface (WaterBody): ripples, the scene under it seen through the water and darkened with depth,
// and the scene above it reflected by a short screen-space march (Godot's SSR skips transparent
// surfaces). Specular highlights come from the room's lights. The colors are the biome's
// (lib/water.gdshaderinc), so the same water follows every layer.
shader_type spatial;
render_mode blend_mix, depth_draw_always, cull_back, fog_disabled, specular_schlick_ggx;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"
#include "res://assets/shaders/lib/water.gdshaderinc"

uniform sampler2D screen : hint_screen_texture, filter_linear_mipmap, repeat_disable;
uniform sampler2D depth_texture : hint_depth_texture, filter_nearest, repeat_disable;

group_uniforms ripples;
/// Ripple size, meters.
uniform float ripple_scale : hint_range(0.1, 8.0) = 2.6;
uniform float ripple_strength : hint_range(0.0, 1.0) = 0.045;
uniform float ripple_speed : hint_range(0.0, 2.0) = 0.18;

group_uniforms look;
/// How fast light dies with depth under the surface (per meter).
uniform float absorption : hint_range(0.0, 4.0) = 0.9;
/// How far the view under the surface is bent by ripples (share of the screen).
uniform float refraction : hint_range(0.0, 0.1) = 0.012;
/// Reflection share looking straight down; it rises toward grazing angles (Fresnel).
uniform float reflectance : hint_range(0.0, 1.0) = 0.04;

group_uniforms reflection;
uniform int march_steps : hint_range(4, 64) = 24;
/// Longest reflected ray, meters.
uniform float march_distance : hint_range(1.0, 80.0) = 30.0;
/// How thick geometry is assumed to be when a ray passes behind it, meters.
uniform float march_thickness : hint_range(0.1, 10.0) = 2.5;
group_uniforms;

varying vec3 v_world_pos;

void vertex() {
	v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float ripple_height(vec2 p, float t) {
	vec2 q = p / ripple_scale;
	return fbm2(q + vec2(t * 0.6, t * 0.35), 3) + 0.5 * value_noise2(q * 2.3 - vec2(t * 0.4, -t * 0.7));
}

vec3 view_from_depth(vec2 uv, float depth, mat4 inv_projection) {
	vec4 v = inv_projection * vec4(uv * 2.0 - 1.0, depth, 1.0);
	return v.xyz / v.w;
}

void fragment() {
	float t = TIME * ripple_speed;
	vec2 p = v_world_pos.xz;
	float e = 0.08;
	float h = ripple_height(p, t);
	vec2 gradient = vec2(ripple_height(p + vec2(e, 0.0), t) - h, ripple_height(p + vec2(0.0, e), t) - h) / e;
	vec3 world_normal = normalize(vec3(-gradient.x * ripple_strength, 1.0, -gradient.y * ripple_strength));
	vec3 n = normalize((VIEW_MATRIX * vec4(world_normal, 0.0)).xyz);
	vec3 v = normalize(VERTEX);

	// Under the surface: the opaque scene, bent by the ripples, sunk into the water's color with depth.
	vec2 bent = SCREEN_UV + n.xz * refraction;
	vec3 below = view_from_depth(bent, texture(depth_texture, bent).r, INV_PROJECTION_MATRIX);
	if (below.z > VERTEX.z) {
		// What lies at the bent spot is in front of the water: look straight through instead.
		bent = SCREEN_UV;
		below = view_from_depth(bent, texture(depth_texture, bent).r, INV_PROJECTION_MATRIX);
	}
	float thickness = max(VERTEX.z - below.z, 0.0);
	vec3 deep = srgb_to_linear(biome_water_deep.rgb);
	vec3 refracted = mix(textureLod(screen, bent, 0.0).rgb, deep, 1.0 - exp(-thickness * absorption));

	// Above: march the reflected ray through the depth buffer.
	vec3 r = normalize(reflect(v, n));
	vec3 reflected = srgb_to_linear(biome_water.rgb);
	float step_length = march_distance / float(march_steps);
	vec3 ray = VERTEX;
	for (int i = 0; i < march_steps; i++) {
		ray += r * step_length * (1.0 + float(i) * 0.08);
		vec4 clip = PROJECTION_MATRIX * vec4(ray, 1.0);
		vec2 uv = clip.xy / clip.w * 0.5 + 0.5;
		if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) {
			break;
		}
		float scene_z = view_from_depth(uv, texture(depth_texture, uv).r, INV_PROJECTION_MATRIX).z;
		float behind = scene_z - ray.z;
		if (behind > 0.0 && behind < march_thickness) {
			// Refine between the last two steps, so reflected edges stay straight.
			vec3 lo = ray - r * step_length * (1.0 + float(i) * 0.08);
			vec3 hi = ray;
			for (int k = 0; k < 5; k++) {
				vec3 mid = 0.5 * (lo + hi);
				vec4 mid_clip = PROJECTION_MATRIX * vec4(mid, 1.0);
				vec2 mid_uv = mid_clip.xy / mid_clip.w * 0.5 + 0.5;
				float mid_z = view_from_depth(mid_uv, texture(depth_texture, mid_uv).r, INV_PROJECTION_MATRIX).z;
				if (mid_z > mid.z) {
					hi = mid;
					uv = mid_uv;
				} else {
					lo = mid;
				}
			}
			// Fade near the screen's edges, where the march runs out of picture.
			vec2 edge = smoothstep(vec2(0.0), vec2(0.08), uv) * smoothstep(vec2(1.0), vec2(0.92), uv);
			reflected = mix(reflected, textureLod(screen, uv, 0.0).rgb, edge.x * edge.y);
			break;
		}
	}

	float facing = clamp(dot(n, -v), 0.0, 1.0);
	float fresnel = reflectance + (1.0 - reflectance) * pow(1.0 - facing, 5.0);
	ALBEDO = vec3(0.0);
	EMISSION = mix(refracted, reflected, fresnel);
	ROUGHNESS = 0.06;
	SPECULAR = 0.5;
	METALLIC = 0.0;
	NORMAL = n;
	ALPHA = 1.0;
}
