// Water in the room and the biome's water colors. A WaterBody pushes its surface height and area into
// the `water_*` globals (none in the room: the level sits far below, so nothing draws); BiomeProfile
// pushes the colors. Rock and metal include this to draw caustics on surfaces under and just above water.
// Needs noise.gdshaderinc and palette.gdshaderinc first.

global uniform float water_level;
// World X/Z rectangle of the water: x min, z min, x max, z max.
global uniform vec4 water_area;
// What the surface reflects where no geometry is reflected (the haze above the water).
global uniform vec4 biome_water;
// The water's own color with depth (absorption, underwater haze).
global uniform vec4 biome_water_deep;
global uniform vec4 biome_caustic;
global uniform float biome_caustic_strength;

// Moving web of bright filaments: two drifting layers of Voronoi borders, warped by slow noise so the
// cells never look like a net.
float caustic_pattern(vec2 p, float t) {
	vec2 warp = vec2(value_noise2(p * 0.6 + vec2(t * 0.11, 1.3)), value_noise2(p * 0.6 + vec2(5.2, -t * 0.09))) - 0.5;
	p += warp * 1.1;
	float a = voronoi_border(p * 0.8 + vec2(t * 0.07, t * 0.04)).x;
	float b = voronoi_border(p * 1.15 + vec2(-t * 0.05, t * 0.08) + 3.7).x;
	float la = 1.0 - smoothstep(0.0, 0.07, a);
	float lb = 1.0 - smoothstep(0.0, 0.07, b);
	return la * la * 0.8 + lb * lb * 0.5;
}

// Caustic light (linear color, add as emission times albedo) at world point `p` with world normal `n`:
// strongest just under the surface and fading with depth, plus light reflected onto walls up to 2.5 m
// above the water.
vec3 caustics_at(vec3 p, vec3 n, float t) {
	if (biome_caustic_strength <= 0.0) {
		return vec3(0.0);
	}
	float depth = water_level - p.y;
	if (depth < -2.5 || depth > 12.0) {
		return vec3(0.0);
	}
	vec2 margin = vec2(0.75);
	vec2 inside = step(water_area.xy - margin, p.xz) * step(p.xz, water_area.zw + margin);
	if (inside.x * inside.y == 0.0) {
		return vec3(0.0);
	}
	float under = smoothstep(-0.05, 0.15, depth) * exp(-max(depth, 0.0) * 0.3);
	float above = (1.0 - smoothstep(0.0, 2.5, -depth)) * step(depth, 0.0) * (1.0 - abs(n.y)) * 0.5;
	// Floors take the pattern from above; walls along their own plane, squashed into wavering bands.
	vec2 q = abs(n.y) > 0.5 ? p.xz : vec2(p.x + p.z, p.y * 2.5);
	return srgb_to_linear(biome_caustic.rgb) * biome_caustic_strength * (under + above) * caustic_pattern(q, t);
}
