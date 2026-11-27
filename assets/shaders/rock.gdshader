// Rock: layered strata and courses of cut stone, silt settled on ledges, chipped edges, grime near the ground, optional
// wetness and puddles. Matte and warm so it never reads as metal (mag-grip). Shared rock finish lives in lib/rock and
// faulted strata sampling in lib/strata; columnar basalt is basalt.gdshader and Drill-breakable cracked rock is cracked.gdshader.
shader_type spatial;

#include "res://assets/shaders/lib/rock.gdshaderinc"
#include "res://assets/shaders/lib/strata.gdshaderinc"

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
	ROCK_FINISH(r, 0.8)
}
