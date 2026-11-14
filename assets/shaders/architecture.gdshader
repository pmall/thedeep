// The architecture far behind a room (BackdropArchitecture): cheap, because it covers much of the screen and sits hazed
// and out of focus, where the play plane's rock and metal shaders (bump, rivets, baked sets) cost the most
// and show the least. A palette color with a little tone per piece, in a frosted layer the coat of rime every material
// shares (lib/frost, without its relief), then hazed: by distance (lib/haze) for the walls' dressing, by the row's veil
// for the architecture (the instance uniform veil, set per row from BackdropVeil, in biome_backdrop_veil: the layer's
// haze kept darker than its rock). A furnace mouth glows in the layer's molten color (glow), veiled like the rest.
shader_type spatial;
render_mode cull_back;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"
#include "res://assets/shaders/lib/haze.gdshaderinc"
#include "res://assets/shaders/lib/water.gdshaderinc"
#include "res://assets/shaders/lib/frost.gdshaderinc"

uniform int slot : hint_enum("Rock", "Metal", "Backdrop", "Backdrop accent", "Foreground") = 2;
uniform float roughness : hint_range(0.0, 1.0) = 0.85;
uniform float metallic : hint_range(0.0, 1.0) = 0.0;
/// Albedo multiplier.
uniform float shade : hint_range(0.0, 2.0) = 1.0;
/// A glossy rim at grazing angles (ice).
uniform float rim : hint_range(0.0, 1.0) = 0.0;
/// Share of the biome's rime this material takes (lib/frost).
uniform float frost_share : hint_range(0.0, 1.0) = 1.0;
/// How bright its rime is, a share of the biome's: below 1, since everything here stands behind the play plane, which
/// stays the brightest layer (`docs/art-direction.md#midground`).
uniform float frost_value : hint_range(0.0, 1.0) = 0.8;
/// Light of its own in the layer's molten color (a furnace mouth), times this; 0 none.
uniform float glow : hint_range(0.0, 8.0) = 0.0;

global uniform vec4 biome_backdrop_veil;
/// How much of the layer's murk covers this piece, 0–1: set per row of the architecture behind a room; 0 elsewhere.
instance uniform float veil : hint_range(0.0, 1.0) = 0.0;

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
	// Rime, when the layer is frosted: duller than on the play plane (frost_value).
	Frost fr = frost_at(p, n, n, 0.0, 1e3, frost_share);
	albedo = mix(albedo, frost_color(0.0) * frost_value * fr.tone, fr.cover);
	ALBEDO = albedo * shade;
	EMISSION = srgb_to_linear(biome_molten.rgb) * glow;
	ROUGHNESS = mix(roughness, fr.rough, fr.cover);
	METALLIC = metallic * (1.0 - fr.cover);
	RIM = rim;
	RIM_TINT = 0.6;
	HAZE
	ALBEDO *= 1.0 - veil;
	EMISSION = EMISSION * (1.0 - veil) + srgb_to_linear(biome_backdrop_veil.rgb) * veil;
	UNDERWATER
	RIM *= uw_keep;
}
