// A light shaft's visible beam (LightShaft, on the strip ShaftBeam builds): light added over the scene, in the biome's shaft
// color, as a soft column with no surface. Across, the chord of a cone of lit air (brightest on its axis, nothing at its rim)
// gathered toward its axis as the light's own falloff says, inside a fainter halo, with a few broad, still bands of brighter air; along, it eases in below the light and fades with
// distance, and over its last meters as it reaches the solid it falls on, so the strip's end never shows as a line; it
// dies out just under a water surface. Only smooth
// math on the mesh (docs/art-direction.md#stable-image): every band is a good part of a meter wide, the only motion a slow breath.
// UV: X across (-1 to 1), Y along (0 at the light, 1 at its end); UV2: X the length in meters, Y how far past the cone the strip
// reaches (the cone's rim at |X| = 1 / spread).
shader_type spatial;
render_mode blend_add, unshaded, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"
#include "res://assets/shaders/lib/water.gdshaderinc"

global uniform vec4 biome_shaft;

/// How strongly the light shows in the air, set per shaft from its light_volumetric_fog_energy (LightShaft): apart from how
/// brightly it lights what it falls on, so a long shaft down a deep room can light the floor and still not draw a wide column.
instance uniform float power : hint_range(0.0, 32.0) = 10.0;
/// The light's angular falloff (its spot_angle_attenuation, set per shaft): over 1, the beam gathers toward its axis.
instance uniform float focus : hint_range(0.1, 8.0) = 1.0;
/// Beam brightness per unit of power.
uniform float strength : hint_range(0.0, 1.0) = 0.03;
/// Brightness of the halo around the cone, against its core.
uniform float halo : hint_range(0.0, 1.0) = 0.12;
/// How fast the beam dims down its length (1 at the light, 1 / (1 + falloff) at its end).
uniform float falloff : hint_range(0.0, 4.0) = 0.8;
/// Meters below the light over which the beam eases in, and over which it fades out where it lands.
uniform float ease_in : hint_range(0.0, 20.0) = 5.0;
uniform float fade_out : hint_range(0.0, 10.0) = 3.0;
/// Meters under a water surface over which the beam dies out: it is lit air, and stops at the water.
uniform float water_fade : hint_range(0.1, 4.0) = 0.35;
/// Depth of the bands across the beam (0 for a plain beam).
uniform float bands : hint_range(0.0, 0.6) = 0.22;
/// Depth and period (seconds) of the slow breath.
uniform float breath : hint_range(0.0, 0.3) = 0.06;
uniform float breath_period : hint_range(2.0, 30.0) = 9.0;

void fragment() {
	float x = UV.x;
	float length_m = UV2.x;
	float spread = UV2.y;
	float meters = UV.y * length_m;

	// Across: the cone's chord (c = 1 at its rim), then a halo that dies out at the strip's edge.
	float c = abs(x) * spread;
	float core = pow(max(1.0 - c * c, 0.0), 1.5 * focus);
	float rim = 1.0 - x * x;
	float glow = exp(-1.6 * c * c) * rim * rim;
	float across = core + halo * glow;

	// Broad bands of brighter air, a few per beam, drifting across by a hair over a minute.
	float drift = TIME * 0.015;
	float banded = 1.0 + bands * sin(x * 5.3 + 1.7 + drift) * sin(x * 2.9 - 0.6 - drift * 0.7);

	// Along: eased in below the light, dimming with distance, faded out over its last meters.
	float along = smoothstep(0.0, ease_in, meters) / (1.0 + falloff * UV.y);
	along *= 1.0 - smoothstep(max(length_m - fade_out, 0.0), length_m, meters);

	// A slow breath, each shaft on its own phase (its place in the room).
	vec3 node = NODE_POSITION_WORLD;
	float pulse = 1.0 + breath * sin(TIME * TAU / breath_period + node.x * 0.37 + node.y * 0.11);

	float light = across * banded * along * pulse * power * strength;
	// The beam is lit air: it dies out just under a water surface, and what shows of it there sinks into the murk (lib/water).
	vec3 world = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz;
	// Over the water across the room, at any depth in front of its back: a beam tilted toward the camera meets the surface
	// in front of the section, where the water is only cut away for the view.
	float over_water = smoothstep(water_area.x - 0.5, water_area.x, world.x) * (1.0 - smoothstep(water_area.z, water_area.z + 0.5, world.x))
			* smoothstep(water_area.y - 0.5, water_area.y, world.z);
	float in_air = 1.0 - over_water * smoothstep(0.0, water_fade, water_level - world.y);
	vec3 uw_color;
	float uw_light;
	float uw_murk = underwater_at(world, CAMERA_POSITION_WORLD, uw_color, uw_light);
	ALBEDO = srgb_to_linear(biome_shaft.rgb) * light * in_air * uw_light * (1.0 - uw_murk);
}
