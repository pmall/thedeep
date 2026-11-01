#[compute]
#version 450
// The painted pass's paper (PaintPass), made once when the pass starts: a tileable texture the composite reads instead of
// computing noise per pixel. r: the paper's fine grain, with a few fibers; g, b: two slow noises (pigment density and
// the hand's wobble); a: another fine noise, that the bristle streaks along the stroke.

layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(rgba8, set = 0, binding = 0) uniform restrict writeonly image2D paper_out;

const int SIZE = 512;

float hash12(vec2 q) {
	vec3 q3 = fract(vec3(q.xyx) * 0.1031);
	q3 += dot(q3, q3.yzx + 33.33);
	return fract((q3.x + q3.y) * q3.z);
}

// Value noise tiling with `period` cells on each axis.
float tiled_noise(vec2 q, vec2 period, float seed) {
	vec2 i = floor(q);
	vec2 f = fract(q);
	vec2 u = f * f * (3.0 - 2.0 * f);
	float a = hash12(mod(i, period) + seed);
	float b = hash12(mod(i + vec2(1.0, 0.0), period) + seed);
	float c = hash12(mod(i + vec2(0.0, 1.0), period) + seed);
	float d = hash12(mod(i + vec2(1.0, 1.0), period) + seed);
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

// Fractal noise over `octaves`, tiling over the texture (cells = cells per texture at the first octave).
float tiled_fbm(vec2 uv, float cells, int octaves, float seed) {
	float sum = 0.0;
	float amp = 0.5;
	float norm = 0.0;
	for (int o = 0; o < octaves; o++) {
		sum += tiled_noise(uv * cells, vec2(cells), seed + float(o) * 17.0) * amp;
		norm += amp;
		cells *= 2.0;
		amp *= 0.5;
	}
	return sum / norm;
}

void main() {
	ivec2 px = ivec2(gl_GlobalInvocationID.xy);
	if (px.x >= SIZE || px.y >= SIZE) {
		return;
	}

	vec2 uv = (vec2(px) + 0.5) / float(SIZE);

	// Grain: fine fractal noise, one cell per texel at its finest, plus sparse fibers (thin dark streaks).
	float grain = tiled_fbm(uv, 64.0, 4, 1.0);
	float fiber_field = tiled_fbm(uv, 16.0, 2, 9.0);
	float fibers = smoothstep(0.02, 0.0, abs(fiber_field - 0.5)) * 0.35;
	float r = clamp(grain - fibers, 0.0, 1.0);

	// Slow noises: pigment density and the wobble.
	float g = tiled_fbm(uv, 8.0, 3, 31.0);
	float b = tiled_fbm(uv, 8.0, 3, 57.0);

	float a = tiled_fbm(uv, 128.0, 2, 83.0);

	imageStore(paper_out, px, vec4(r, g, b, a));
}
