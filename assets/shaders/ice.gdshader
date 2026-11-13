// Ice: old, clear ice that light gets into. Opaque (cheap, no sorting), so the depth is faked: a dark body
// in the biome's deep-water color that pales toward the rime color at grazing angles and along the edges,
// a glossy wet skin. For icicles, ice plugs and sheets. What is frozen inside (fracture planes, bubbles,
// veils) and the skin's undulation are a baked set (IceSurface; SurfaceMaps has the sampling contract),
// laid in world meters on each face so it runs on across neighbouring pieces (along its own surface on a
// piece that wraps round). The set shows in patches a few meters across with clear ice between, as baked
// in some regions and mirrored and a little larger in others, so the tile never shows twice along a wall. The shader adds only what is broad: the body, its
// cloudy depths, the rind, rime. Like rock, it is never grippable: the physics layer decides that, not the look.
shader_type spatial;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"
#include "res://assets/shaders/lib/haze.gdshaderinc"
#include "res://assets/shaders/lib/water.gdshaderinc"
#include "res://assets/shaders/lib/surface.gdshaderinc"
#include "res://assets/shaders/lib/frost.gdshaderinc"


/// The baked ice set (assets/textures/surfaces/, SurfaceMaps): every ice material sets both.
uniform sampler2D ice_normal : hint_normal, repeat_enable, filter_linear_mipmap_anisotropic;
uniform sampler2D ice_surface : hint_default_black, repeat_enable, filter_linear_mipmap_anisotropic;
/// How dark the heart of the ice is (0 clear and pale, 1 black water frozen).
uniform float depth : hint_range(0.0, 1.0) = 0.6;
/// How white the fractures, bubbles and veils inside show (0 none).
uniform float inclusions : hint_range(0.0, 1.0) = 0.7;
/// Share of the ice that holds them: the rest is clear.
uniform float inclusion_share : hint_range(0.0, 1.0) = 1.0;
/// Light scattered back out of the ice, so it never goes flat black in shadow.
uniform float glow : hint_range(0.0, 1.0) = 0.12;
/// Albedo multiplier (midground pieces below 1).
uniform float shade : hint_range(0.0, 1.0) = 1.0;
uniform float frost_share : hint_range(0.0, 1.0) = 0.5;
/// How much of the rime's translucent film (lib/frost) lies on the ice, against other materials.
uniform float film_share : hint_range(0.0, 1.0) = 0.5;

// The ice's body seen straight on (BiomeProfile.IceTint).
global uniform vec4 biome_ice;

// The baked set's tile (IceSurface.TileMeters); the mirrored layer's scale against it and where it starts (meters).
const float ICE_TILE = 4.0;
const float MIRROR_SCALE = 1.17;
const vec2 MIRROR_OFFSET = vec2(1.37, 2.71);
// The patches the set shows in (and the regions of each way round): their noise's cells per meter.
const float PATCH_FREQUENCY = 0.22;

// Where the set shows at `q` (meters on the face): 1 in its patches, 0 in the clear ice between.
float patch_at(vec2 q) {
	float threshold = mix(0.72, 0.28, inclusion_share);
	return smoothstep(threshold - 0.08, threshold + 0.08, value_noise2(q * PATCH_FREQUENCY + 3.0));
}

void vertex() {
	SURFACE_VERTEX
}

void fragment() {
	Surface f = SURFACE;
	vec3 p = v_world_pos;
	vec3 t = view_to_world(f.tangent, INV_VIEW_MATRIX);
	vec3 b = view_to_world(f.bitangent, INV_VIEW_MATRIX);
	vec3 rime = srgb_to_linear(biome_frost_color.rgb);
	vec3 deep = srgb_to_linear(biome_water_deep.rgb);

	// The set in meters on the face: in world space, so it runs on across pieces; on a piece that wraps round (an icicle),
	// along its own surface, since the world position would stretch it round the girth.
	bool wraps = UV2.x < 0.0 || UV2.y < 0.0;
	vec2 q = wraps ? f.uv + v_seed * 17.0 : vec2(dot(p, t), dot(p, b));
	// Two ways round, each in its own broad regions, with clear ice in patches across both.
	vec2 uv = vec2(q.x, -q.y) / ICE_TILE;
	vec2 mirrored = vec2(-q.x, -q.y) / (ICE_TILE * MIRROR_SCALE) + MIRROR_OFFSET / ICE_TILE;
	vec3 inside = texture(ice_surface, uv).rgb;
	float second = smoothstep(0.42, 0.58, value_noise2(q * PATCH_FREQUENCY * 0.7 + 41.0));
	float white = mix(
		clamp(inside.r * 2.0 - 1.0, 0.0, 1.0),
		clamp(texture(ice_surface, mirrored).r * 2.0 - 1.0, 0.0, 1.0), second) * patch_at(q) * inclusions;

	// The skin's undulation from the baked normal (tangent space: red along the face, green up it).
	vec2 slope = texture(ice_normal, uv).rg * 2.0 - 1.0;
	vec3 ts = vec3(slope, sqrt(max(0.0, 1.0 - dot(slope, slope))));
	vec3 n = normalize(f.tangent * ts.x + f.bitangent * ts.y + f.normal * ts.z);
	vec3 world_n = view_to_world(n, INV_VIEW_MATRIX);

	// Seen straight on, you look into the dark; at a grazing angle the skin reflects and scatters pale.
	float facing = clamp(dot(f.normal, normalize(VIEW)), 0.0, 1.0);
	float graze = pow(1.0 - facing, 2.0);
	// A pale rind along the edges, where the ice is thin.
	float rind = 1.0 - smoothstep(0.0, 0.18, f.edge);
	vec3 body = mix(rime * srgb_to_linear(biome_ice.rgb), deep * 1.6 + rime * 0.04, depth);
	body = mix(body, rime * 0.75, clamp(graze * 0.8 + rind * 0.5, 0.0, 1.0));
	// Cloudy depths, a meter and more across.
	float cloud = fbm3(p * 0.45 + v_seed * 7.0, 2);
	body = mix(body, rime * 0.5, smoothstep(0.55, 0.85, cloud) * 0.15);

	vec3 albedo = mix(body, rime, white * 0.85);
	float rough = mix(inside.b, 0.25, white);

	// Ice takes the solid rime and little of the film, so its dark body still shows through.
	Frost fr = frost_at(p, view_to_world(f.normal, INV_VIEW_MATRIX), world_n, 0.0, f.edge, frost_share);
	fr.cover = max(fr.solid, fr.cover * film_share);
	albedo = mix(albedo, frost_color(0.0) * fr.tone, fr.cover);
	rough = mix(rough, fr.rough, fr.cover);
	n = frost_bump(n, fr, VIEW_MATRIX);

	ALBEDO = albedo * shade;
	EMISSION = rime * glow * (0.4 + graze + white) * shade * (1.0 - fr.cover);
	ROUGHNESS = rough;
	METALLIC = 0.0;
	SPECULAR = 0.7;
	CLEARCOAT = 1.0 - fr.cover;
	CLEARCOAT_ROUGHNESS = 0.05;
	RIM = 0.3 * (1.0 - fr.cover);
	RIM_TINT = 0.8;
	NORMAL = n;
	HAZE
	UNDERWATER
	SPECULAR *= uw_keep;
	CLEARCOAT *= uw_keep;
	RIM *= uw_keep;
}
