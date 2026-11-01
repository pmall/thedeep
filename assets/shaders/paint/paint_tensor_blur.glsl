#[compute]
#version 450
// The painted pass, step 3 (PaintPass): one direction of a separable Gaussian over the structure tensor, run twice,
// so the brush direction turns smoothly instead of following every pixel.

#include "paint_params.glslinc"

layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(set = 0, binding = 1) uniform sampler2D tensor_in;
layout(rgba16f, set = 0, binding = 2) uniform restrict writeonly image2D tensor_out;

layout(push_constant, std430) uniform BlurStep {
	vec2 direction; // (1, 0) or (0, 1)
	vec2 unused;
} blur_step;

const int RADIUS = 6;
const float SIGMA = 2.5;

void main() {
	ivec2 hp = ivec2(gl_GlobalInvocationID.xy);
	ivec2 hs = ivec2(p.size.zw);
	if (hp.x >= hs.x || hp.y >= hs.y) {
		return;
	}

	ivec2 step_ = ivec2(blur_step.direction);
	vec4 sum = vec4(0.0);
	float wsum = 0.0;
	for (int k = -RADIUS; k <= RADIUS; k++) {
		float w = exp(-float(k * k) / (2.0 * SIGMA * SIGMA));
		sum += texelFetch(tensor_in, clamp(hp + step_ * k, ivec2(0), hs - 1), 0) * w;
		wsum += w;
	}

	imageStore(tensor_out, hp, sum / wsum);
}
