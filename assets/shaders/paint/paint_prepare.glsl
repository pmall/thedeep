#[compute]
#version 450
// The painted pass, step 1 (PaintPass): the frame at half resolution, in pigment space (paint_params.glslinc), with the
// view depth of its nearest quarter in alpha (thin near things keep their depth for the upsample).

#include "paint_params.glslinc"

layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(set = 0, binding = 1) uniform sampler2D color_tex;
layout(set = 0, binding = 2) uniform sampler2D depth_tex;
layout(rgba16f, set = 0, binding = 3) uniform restrict writeonly image2D half_out;

void main() {
	ivec2 hp = ivec2(gl_GlobalInvocationID.xy);
	ivec2 hs = ivec2(p.size.zw);
	if (hp.x >= hs.x || hp.y >= hs.y) {
		return;
	}

	// The linear sampler between four full-res pixels averages them.
	vec2 uv = (vec2(hp) + 0.5) / p.size.zw;
	vec3 c = textureLod(color_tex, uv, 0.0).rgb;

	ivec2 fp = hp * 2;
	ivec2 fmax = ivec2(p.size.xy) - 1;
	float d = max(
		max(texelFetch(depth_tex, min(fp, fmax), 0).r, texelFetch(depth_tex, min(fp + ivec2(1, 0), fmax), 0).r),
		max(texelFetch(depth_tex, min(fp + ivec2(0, 1), fmax), 0).r, texelFetch(depth_tex, min(fp + ivec2(1, 1), fmax), 0).r));

	imageStore(half_out, hp, vec4(to_pigment(c), view_depth(d)));
}
