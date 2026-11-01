#[compute]
#version 450
// The painted pass, step 2 (PaintPass): the structure tensor of the half-res frame (Sobel per channel), the local
// direction of its edges that the brush follows. Out: (E, F, G) = (gx.gx, gx.gy, gy.gy), and the pixel's luminance (once
// blurred, the neighborhood's: where paint pools).

#include "paint_params.glslinc"

layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(set = 0, binding = 1) uniform sampler2D half_tex;
layout(rgba16f, set = 0, binding = 2) uniform restrict writeonly image2D tensor_out;

vec3 at(ivec2 q) {
	return texelFetch(half_tex, clamp(q, ivec2(0), ivec2(p.size.zw) - 1), 0).rgb;
}

void main() {
	ivec2 hp = ivec2(gl_GlobalInvocationID.xy);
	if (hp.x >= int(p.size.z) || hp.y >= int(p.size.w)) {
		return;
	}

	vec3 a = at(hp + ivec2(-1, -1));
	vec3 b = at(hp + ivec2(0, -1));
	vec3 c = at(hp + ivec2(1, -1));
	vec3 d = at(hp + ivec2(-1, 0));
	vec3 f = at(hp + ivec2(1, 0));
	vec3 g = at(hp + ivec2(-1, 1));
	vec3 h = at(hp + ivec2(0, 1));
	vec3 i = at(hp + ivec2(1, 1));

	vec3 gx = (c + 2.0 * f + i - a - 2.0 * d - g) * 0.25;
	vec3 gy = (g + 2.0 * h + i - a - 2.0 * b - c) * 0.25;
	vec3 e = at(hp);
	imageStore(tensor_out, hp, vec4(dot(gx, gx), dot(gx, gy), dot(gy, gy), dot(e, LUMA)));
}
