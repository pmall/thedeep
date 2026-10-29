// Ice: old, clear ice that light gets into. Opaque (cheap, no sorting), so the depth is faked: a dark body
// in the biome's deep-water color that pales toward the rime color at grazing angles and along the edges,
// white fracture planes and trapped bubbles inside, a glossy wet skin. For icicles, ice plugs and sheets.
// The pattern lies in world space (kit pieces) or in the piece's own frame (`local_pattern`, for pieces that
// may move). Like rock, it is never grippable: the physics layer decides that, not the look.
shader_type spatial;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"
#include "res://assets/shaders/lib/surface.gdshaderinc"
#include "res://assets/shaders/lib/frost.gdshaderinc"

global uniform vec4 biome_water_deep;

/// How dark the heart of the ice is (0 clear and pale, 1 black water frozen).
uniform float depth : hint_range(0.0, 1.0) = 0.6;
/// Fracture planes per meter.
uniform float fracture_scale : hint_range(0.2, 6.0) = 1.4;
uniform float fractures : hint_range(0.0, 1.0) = 0.6;
uniform float bubbles : hint_range(0.0, 1.0) = 0.5;
/// Light scattered back out of the ice, so it never goes flat black in shadow.
uniform float glow : hint_range(0.0, 1.0) = 0.12;
/// Albedo multiplier (midground pieces below 1).
uniform float shade : hint_range(0.0, 1.0) = 1.0;
uniform float frost_share : hint_range(0.0, 1.0) = 0.5;

void vertex() {
	SURFACE_VERTEX
}

void fragment() {
	Surface f = SURFACE;
	vec3 p = v_world_pos;
	vec3 world_n = view_to_world(f.normal, INV_VIEW_MATRIX);
	vec3 rime = srgb_to_linear(biome_frost_color.rgb);
	vec3 deep = srgb_to_linear(biome_water_deep.rgb);

	// Seen straight on, you look into the dark; at a grazing angle the skin reflects and scatters pale.
	float facing = clamp(dot(f.normal, normalize(VIEW)), 0.0, 1.0);
	float graze = pow(1.0 - facing, 2.0);
	// A pale rind along the edges, where the ice is thin.
	float rind = 1.0 - smoothstep(0.0, 0.18, f.edge);
	vec3 body = mix(rime * vec3(0.42, 0.52, 0.68), deep * 1.6 + rime * 0.04, depth);
	body = mix(body, rime * 0.75, clamp(graze * 0.8 + rind * 0.5, 0.0, 1.0));
	// Cloudy veils inside.
	float cloud = fbm3(p * 0.8 + v_seed * 7.0, 3);
	body = mix(body, rime * 0.5, smoothstep(0.55, 0.85, cloud) * 0.35);

	// Fracture planes: Voronoi borders on a slant plane through the ice, thin and white.
	vec2 q = vec2(dot(p, vec3(0.8, 0.3, 0.5)), dot(p, vec3(-0.2, 0.9, 0.35))) * fracture_scale;
	vec2 cell = voronoi_border(q + v_seed * 3.0);
	float fracture = (1.0 - smoothstep(0.0, 0.025, cell.x)) * smoothstep(0.4, 0.6, fbm2(q * 0.5, 2)) * fractures;
	// Bubbles: small pale specks in loose trails.
	vec3 bq = p * 9.0;
	float bubble = step(0.93, hash13(floor(bq))) * (1.0 - smoothstep(0.15, 0.35, length(fract(bq) - 0.5)));
	bubble *= smoothstep(0.45, 0.7, fbm3(p * 1.3 + 2.0, 2)) * bubbles;

	vec3 albedo = mix(body, rime, max(fracture * 0.8, bubble * 0.7));
	float rough = mix(0.06, 0.3, fracture);

	Frost fr = frost_at(p, world_n, world_n.y, fracture, f.edge, frost_share);
	albedo = mix(albedo, frost_color(0.0), fr.cover);
	rough = mix(mix(rough, 0.6, fr.cover), 0.04, fr.sparkle);

	ALBEDO = albedo * shade;
	EMISSION = (rime * glow * (0.4 + graze + fracture) + rime * bubble * 0.1) * shade * (1.0 - fr.cover);
	ROUGHNESS = rough;
	METALLIC = 0.0;
	SPECULAR = 0.7;
	CLEARCOAT = 1.0 - fr.cover;
	CLEARCOAT_ROUGHNESS = 0.05;
	RIM = 0.3 * (1.0 - fr.cover);
	RIM_TINT = 0.8;
}
