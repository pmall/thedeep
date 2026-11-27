// Cracked rock (the Drill bores through it): layered strata and masonry from lib/rock and lib/strata, overlaid with a
// baked web of deep fissures (CracksSurface), mirrored across broad regions so the web never repeats along a wall.
shader_type spatial;

#include "res://assets/shaders/lib/rock.gdshaderinc"
#include "res://assets/shaders/lib/strata.gdshaderinc"

group_uniforms cracks;
/// How strongly the baked web of deep fissures shows over the whole face, so cracked rock reads apart from sound rock.
uniform float fracture : hint_range(0.0, 1.0) = 1.0;
uniform sampler2D cracks_normal : hint_normal, repeat_enable, filter_linear_mipmap_anisotropic;
uniform sampler2D cracks_surface : hint_default_white, repeat_enable, filter_linear_mipmap_anisotropic;
group_uniforms;

// The baked cracks set's tile (CracksSurface.TileMeters); the mirrored layer's scale against it and where it starts (meters);
// the cells per meter of the regions each way round shows in.
const float CRACKS_TILE = 4.0;
const float CRACKS_MIRROR_SCALE = 1.13;
const vec2 CRACKS_MIRROR_OFFSET = vec2(1.71, 0.53);
const float CRACKS_REGION_FREQUENCY = 0.16;

void fragment() {
	Surface f = SURFACE;
	vec3 t = view_to_world(f.tangent, INV_VIEW_MATRIX);
	vec3 b = view_to_world(f.bitangent, INV_VIEW_MATRIX);
	vec3 p = v_world_pos;
	float seed = v_seed;
	vec3 face_world_n = view_to_world(f.normal, INV_VIEW_MATRIX);
	float up = abs(face_world_n.y);
	vec2 q = vec2(dot(p, t), dot(p, b));
	float warp = fbm3(p * 0.3 + 11.0, 3);
	Rock r = rock_overlay(strata_at(q, warp, up), q, p.y, warp, up);
	if (fracture > 0.0) {
		vec2 uv = vec2(q.x, -q.y) / CRACKS_TILE;
		vec2 mirrored = vec2(-q.x, -q.y) / (CRACKS_TILE * CRACKS_MIRROR_SCALE) + CRACKS_MIRROR_OFFSET / CRACKS_TILE;
		float second = smoothstep(0.45, 0.55, value_noise2(q * CRACKS_REGION_FREQUENCY + 23.0));
		vec3 crack = mix(texture(cracks_surface, uv).rgb, texture(cracks_surface, mirrored).rgb, second);
		r.tone *= mix(1.0, crack.r * 2.0, fracture);
		r.cavity = max(r.cavity, crack.g * fracture * (detail ? 1.0 : 0.5));
		r.rough = mix(r.rough, crack.b, fracture);
		if (detail) {
			vec2 slope = mix(texture(cracks_normal, uv).rg * 2.0 - 1.0, (texture(cracks_normal, mirrored).rg * 2.0 - 1.0) * vec2(-1.0, 1.0), second);
			r.slope = r.slope * (1.0 - 0.5 * fracture) + slope * fracture;
		}
	}
	ROCK_FINISH(r, 0.8)
}
