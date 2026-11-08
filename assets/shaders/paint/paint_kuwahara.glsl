#[compute]
#version 450
// The painted pass, step 4 (PaintPass): the brush. An anisotropic Kuwahara filter with polynomial sector weights
// (Kyprianidis et al.): around each pixel, an ellipse stretched along the local edge direction (the smoothed structure
// tensor) is cut into eight sectors; the sectors with the least color variance win, so flat areas become flat
// opaque paint while edges stay sharp, with soft brush-like borders that follow the form. In pigment space, at half
// resolution.
//
// The brush is small on the play plane and wider with depth (PaintStyle radii), so the backdrop is simplified and the
// play plane keeps its detail; it also softens with depth. A wide brush samples on a stride (bilinear taps), so its cost stays that of a small one.
// `mechanical` turns the strokes toward 0/45/90 degrees and stretches them: a machine's
// hand instead of a pilgrim's.

#include "paint_params.glslinc"

layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(set = 0, binding = 1) uniform sampler2D half_tex;
layout(set = 0, binding = 2) uniform sampler2D tensor_tex;
layout(rgba16f, set = 0, binding = 3) uniform restrict writeonly image2D painted_out;

const int N = 8;
const float PI = 3.14159265;
// The zero crossing of the sector weights and the sector overlap (the paper's defaults).
const float ZERO_CROSSING = 0.58;
// The most taps the brush takes each way from its center.
const int MAX_REACH = 3;
// The sharpness deep in the haze.
const float FAR_SHARPNESS = 1.5;
// Variance hardness: how strongly a calm sector wins over a busy one.
const float HARDNESS = 8.0;

void main() {
	ivec2 hp = ivec2(gl_GlobalInvocationID.xy);
	ivec2 hs = ivec2(p.size.zw);
	if (hp.x >= hs.x || hp.y >= hs.y) {
		return;
	}

	vec4 center = texelFetch(half_tex, hp, 0);
	float z = center.a;

	// The brush grows from the play plane into the haze.
	float far_t = far_share(z);
	float radius = clamp(mix(p.brush.x, p.brush.y, far_t), 0.5, 16.0);
	if (isnan(radius)) {
		radius = 0.5;
	}
	// Far away the calm sectors win less sharply: edges soften into the haze, as a painter loses them.
	float q = mix(p.brush.z, FAR_SHARPNESS, far_t);
	float alpha = p.brush.w;
	float mechanical = p.brush2.x;

	// Direction and anisotropy from the tensor's eigenvectors.
	vec3 t = texelFetch(tensor_tex, hp, 0).xyz;
	float e = t.x;
	float f = t.y;
	float g = t.z;
	float root = sqrt((e - g) * (e - g) + 4.0 * f * f);
	float l1 = 0.5 * (e + g + root);
	float l2 = 0.5 * (e + g - root);
	vec2 v = vec2(l1 - e, -f);
	vec2 dir = dot(v, v) > 1e-12 ? normalize(v) : vec2(0.0, 1.0);
	float phi = -atan(dir.y, dir.x);
	float aniso = (l1 + l2 > 1e-8) ? clamp((l1 - l2) / (l1 + l2), 0.0, 1.0) : 0.0;

	if (mechanical > 0.0) {
		float step_ = PI * 0.25;
		float snapped = round(phi / step_) * step_;
		phi = mix(phi, snapped, mechanical);
		aniso = mix(aniso, max(aniso, 0.85), mechanical);
	}

	float a = radius * clamp((alpha + aniso) / alpha, 0.1, 2.0);
	float b = radius * clamp(alpha / (alpha + aniso), 0.1, 2.0);
	float cos_phi = cos(phi);
	float sin_phi = sin(phi);
	mat2 R = mat2(cos_phi, -sin_phi, sin_phi, cos_phi);
	mat2 S = mat2(0.5 / a, 0.0, 0.0, 0.5 / b);
	mat2 SR = S * R;

	// About 1.5 taps per radius on each side (3 along a stretched stroke), never more than MAX_REACH.
	float stride = max(1.0, radius / 1.5);
	// Never more than MAX_REACH taps each way, whatever the inputs: the cost stays bounded.
	int max_x = clamp(int(sqrt(a * a * cos_phi * cos_phi + b * b * sin_phi * sin_phi) / stride), 0, MAX_REACH);
	int max_y = clamp(int(sqrt(a * a * sin_phi * sin_phi + b * b * cos_phi * cos_phi) / stride), 0, MAX_REACH);
	vec2 inv_size = 1.0 / p.size.zw;
	vec2 center_uv = (vec2(hp) + 0.5) * inv_size;

	float zeta = 2.0 / radius;
	float sin_zero = sin(ZERO_CROSSING);
	float eta = (zeta + cos(ZERO_CROSSING)) / (sin_zero * sin_zero);

	// Per sector: the weighted sum of colors and of weights, and the weighted sum of squared colors over all channels
	// (only the variance's sum over channels is used, so one float holds it: fewer registers, a faster brush).
	vec4 m[N];
	float s[N];
	for (int k = 0; k < N; k++) {
		m[k] = vec4(0.0);
		s[k] = 0.0;
	}

	for (int y = -max_y; y <= max_y; y++) {
		for (int x = -max_x; x <= max_x; x++) {
			vec2 offset = vec2(x, y) * stride;
			vec2 vv = SR * offset;
			if (dot(vv, vv) > 0.25) {
				continue;
			}

			vec3 c = textureLod(half_tex, center_uv + offset * inv_size, 0.0).rgb;
			float cc = dot(c, c);
			float w[N];
			float sum = 0.0;

			float vxx = zeta - eta * vv.x * vv.x;
			float vyy = zeta - eta * vv.y * vv.y;
			float zz = max(0.0, vv.y + vxx); w[0] = zz * zz; sum += w[0];
			zz = max(0.0, -vv.x + vyy); w[2] = zz * zz; sum += w[2];
			zz = max(0.0, -vv.y + vxx); w[4] = zz * zz; sum += w[4];
			zz = max(0.0, vv.x + vyy); w[6] = zz * zz; sum += w[6];

			vec2 vr = 0.70710678 * vec2(vv.x - vv.y, vv.x + vv.y);
			vxx = zeta - eta * vr.x * vr.x;
			vyy = zeta - eta * vr.y * vr.y;
			zz = max(0.0, vr.y + vxx); w[1] = zz * zz; sum += w[1];
			zz = max(0.0, -vr.x + vyy); w[3] = zz * zz; sum += w[3];
			zz = max(0.0, -vr.y + vxx); w[5] = zz * zz; sum += w[5];
			zz = max(0.0, vr.x + vyy); w[7] = zz * zz; sum += w[7];

			float gauss = exp(-3.125 * dot(vv, vv)) / max(sum, 1e-6);
			for (int k = 0; k < N; k++) {
				float wk = w[k] * gauss;
				m[k] += vec4(c * wk, wk);
				s[k] += cc * wk;
			}
		}
	}

	vec4 result = vec4(0.0);
	for (int k = 0; k < N; k++) {
		if (m[k].w <= 1e-6) {
			continue;
		}
		vec3 mean = m[k].rgb / m[k].w;
		float sigma2 = abs(s[k] / m[k].w - dot(mean, mean));
		float w = 1.0 / (1.0 + pow(HARDNESS * 1000.0 * sigma2, 0.5 * q));
		result += vec4(mean * w, w);
	}

	vec3 painted = result.w > 1e-6 ? result.rgb / result.w : center.rgb;
	imageStore(painted_out, hp, vec4(painted, z));
}
