#[compute]
#version 450
// The painted pass, step 6 (PaintPass): back to full resolution, in place over the frame.
//
// - The hand: where the paint is read from wobbles by a few pixels (`wobble`), so edges come out hand-cut, not traced.
// - The painted half-res frame, its strokes laid (paint_strokes.glsl), is upsampled guided by the full-res frame (joint
//   bilateral: each of the four nearest painted texels counts as much as its unpainted color and depth look like this
//   pixel), so silhouettes stay crisp.
// - Near the play plane some of the full-res detail is laid back on top (`detail`): the pilgrim and gameplay edges
//   are never smeared.
// - Pigment pools at a stroke's edge: what is darker than its neighborhood darkens a little more (`pooling`).
// - Value bands and a pull toward the layer's pigments: a frame mixed from a few paints (`bands`, `palette pull`).
// - Paint laid unevenly (`density`), settling in the paper's grain (`granulation`), and specks that are the machines'
//   script (`glyph share`).
//
// Everything the hand and the paper add is anchored to the world (it moves with the surfaces, never slides over the
// image) at a size that stays about `grain cell` pixels at any depth: two octaves of the paper texture blended by depth.

#include "paint_params.glslinc"

layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(rgba16f, set = 0, binding = 1) uniform restrict image2D color_image;
layout(set = 0, binding = 2) uniform sampler2D depth_tex;
layout(set = 0, binding = 3) uniform sampler2D half_tex;
layout(set = 0, binding = 4) uniform sampler2D painted_tex;
layout(set = 0, binding = 5) uniform sampler2D tensor_tex;
layout(set = 0, binding = 6) uniform sampler2D paper_tex;

// The size of the finest grain octave, in meters; octaves double from there.
const float GRAIN_BASE = 0.004;
// Paper texels per grain cell.
const float PAPER_TEXELS = 1.0;
// How the slow channels are read, in grain cells per texel: pigment density over a few hundred pixels, the wobble over a
// few dozen.
const float DENSITY_SCALE = 0.5;
const float WOBBLE_SCALE = 3.0;

float segment_distance(vec2 q, vec2 a, vec2 b) {
	vec2 qa = q - a;
	vec2 ba = b - a;
	float h = clamp(dot(qa, ba) / dot(ba, ba), 0.0, 1.0);
	return length(qa - ba * h);
}

// Coverage (0..1) of the machine-script specks in cell space (1 unit = one glyph cell). Each cell holds a glyph or a
// gap: two or three strokes between the nodes of a 2x3 grid, as the faith glyphs are drawn (glyph.gdshader).
float glyph_cover(vec2 q, float aa) {
	vec2 id = floor(q);
	vec2 f = fract(q) - vec2(0.5);
	float h = hash12(id);
	if (h < 0.55) {
		return 0.0;
	}

	float d = 1e3;
	for (int s = 0; s < 3; s++) {
		if (s == 2 && h < 0.8) {
			break;
		}
		float r1 = hash12(id + vec2(17.0 * float(s), 3.1));
		float r2 = hash12(id + vec2(5.3, 11.0 * float(s)));
		float r3 = hash12(id + vec2(7.7 * float(s), 29.0));
		vec2 a = vec2(r1 < 0.5 ? -0.15 : 0.15, (floor(r2 * 3.0) - 1.0) * 0.25);
		vec2 b = r3 < 0.33 ? vec2(-a.x, a.y)
			: (r3 < 0.66 ? vec2(a.x, clamp(a.y + (r2 < 0.5 ? 0.25 : -0.25), -0.25, 0.25))
			: vec2(-a.x, clamp(a.y + 0.25, -0.25, 0.25)));
		d = min(d, segment_distance(f, a, b));
	}

	const float WIDTH = 0.05;
	return 1.0 - smoothstep(WIDTH - aa, WIDTH + aa, d);
}

vec4 paper_at(vec2 q0, vec2 q1, float blend) {
	return mix(textureLod(paper_tex, q0 * (PAPER_TEXELS / 512.0), 0.0), textureLod(paper_tex, q1 * (PAPER_TEXELS / 512.0), 0.0), blend);
}

void main() {
	ivec2 px = ivec2(gl_GlobalInvocationID.xy);
	ivec2 fs = ivec2(p.size.xy);
	if (px.x >= fs.x || px.y >= fs.y) {
		return;
	}

	vec3 c0 = imageLoad(color_image, px).rgb;
	vec3 x0 = to_pigment(c0);
	float z = view_depth(texelFetch(depth_tex, px, 0).r);
	vec2 uv = (vec2(px) + 0.5) / p.size.xy;

	// World-anchored paper coordinates, two octaves blended by depth so a grain cell stays about `grain cell` pixels.
	vec3 world = world_at(uv, z);
	vec2 anchor = world_anchor(world);
	float cell = p.pigment.z * pixel_meters(z);
	float level = log2(max(cell / GRAIN_BASE, 1.0));
	float lo = floor(level);
	float blend = level - lo;
	float size0 = GRAIN_BASE * exp2(lo);
	vec2 q0 = anchor / size0;
	vec2 q1 = q0 * 0.5;
	vec4 slow = paper_at(q0 * DENSITY_SCALE, q1 * DENSITY_SCALE, blend);

	// The hand's wobble, in half-res pixels.
	vec2 wobble = (paper_at(q0 * WOBBLE_SCALE, q1 * WOBBLE_SCALE, blend).gb - 0.5) * 2.0 * p.brush2.w * 0.5;

	// Joint bilateral upsample of the painted frame, read where the hand wobbled to.
	ivec2 hs = ivec2(p.size.zw);
	vec2 hpos = (vec2(px) + 0.5) * 0.5 - 0.5 + wobble;
	ivec2 base = ivec2(floor(hpos));
	vec2 fr = hpos - vec2(base);
	vec3 painted = vec3(0.0);
	vec3 unpainted = vec3(0.0);
	float wsum = 0.0;
	for (int j = 0; j < 2; j++) {
		for (int i = 0; i < 2; i++) {
			ivec2 q = clamp(base + ivec2(i, j), ivec2(0), hs - 1);
			vec4 hc = texelFetch(half_tex, q, 0);
			vec3 pc = texelFetch(painted_tex, q, 0).rgb;
			float wb = (i == 0 ? 1.0 - fr.x : fr.x) * (j == 0 ? 1.0 - fr.y : fr.y);
			vec3 dc = hc.rgb - x0;
			float wc = exp(-dot(dc, dc) * 60.0);
			float wd = exp(-abs(hc.a - z) / (0.04 * z + 0.05));
			float w = wb * (mix(1.0, wc * wd, 1.0 - min(length(wobble), 1.0))) + 1e-5 * wb;
			painted += pc * w;
			unpainted += hc.rgb * w;
			wsum += w;
		}
	}
	painted /= wsum;
	unpainted /= wsum;

	float plane = p.cam_right.w;
	float near_plane = 1.0 - smoothstep(plane + 1.5, plane + 8.0, z);

	vec3 x = painted;

	// The full-res detail the half-res brush lost, laid back near the play plane.
	x += (x0 - unpainted) * p.brush2.z * near_plane;

	// Pigment pooling: what is darker than its neighborhood (the blurred luminance in the tensor's w) darkens more.
	vec2 wobble_uv = wobble / p.size.zw;
	vec4 tensor = textureLod(tensor_tex, uv + wobble_uv, 0.0);
	float lum = dot(x, LUMA);
	float rim = clamp((tensor.w - lum) * 6.0, 0.0, 1.0);
	x *= 1.0 - p.pigment.x * rim;

	// Value bands: gouache is laid in a few values. In perceived lightness (not light), each band pulled toward its
	// middle by a soft staircase that keeps the average, so gradients turn into flat areas with soft edges and darks
	// keep their value. The bands' edges wander with the pigment density, as no hand lays them straight.
	float bands = max(p.tone.x, 1.0);
	lum = dot(x, LUMA);
	if (p.tone.y > 0.0 && lum > 1e-4) {
		float v = pow(lum, 1.0 / 2.2);
		float fb = v * bands + (slow.g - 0.5) * 0.4;
		float ib = floor(fb);
		float fr = fb - ib;
		float t = 2.0 * fr - 1.0;
		float stepped = ib + 0.5 + 0.5 * sign(t) * pow(abs(t), 3.0);
		float banded = max(stepped - (slow.g - 0.5) * 0.4, 0.0) / bands;
		x *= mix(1.0, pow(banded, 2.2) / lum, p.tone.y);
	}

	// Palette pull: each color's hue toward the nearest of the layer's pigments, its luminance kept.
	if (p.tone.z > 0.0) {
		float total = x.r + x.g + x.b;
		float value = dot(x, LUMA);
		if (total > 1e-4) {
			vec3 chroma = x / total;
			vec3 best = chroma;
			float best_d = 1e3;
			for (int k = 0; k < 6; k++) {
				if (p.palette[k].w <= 0.0) {
					continue;
				}
				vec3 pc = p.palette[k].rgb / max(p.palette[k].r + p.palette[k].g + p.palette[k].b, 1e-4);
				float d = distance(pc, chroma);
				if (d < best_d) {
					best_d = d;
					best = pc;
				}
			}
			x = mix(x, best * (value / max(dot(best, LUMA), 1e-4)), p.tone.z);
		}
	}

	// Where the paper shows most: flat areas and midtones, less on edges, deep shadow or light.
	float edge = sqrt(max(tensor.x + tensor.z, 0.0));
	float flat_ = 1.0 - smoothstep(0.015, 0.08, edge);
	lum = clamp(dot(x, LUMA), 0.0, 1.0);
	float mid = clamp(4.0 * lum * (1.0 - lum) * 1.3, 0.0, 1.0);

	// Paint laid unevenly: slow density variation.
	x *= 1.0 + p.hand.x * (slow.g - 0.5) * 1.2;

	vec4 fine = paper_at(q0, q1, blend);
	if (p.pigment.y > 0.0) {
		// Pigment settles in the valleys and leaves the paper's tooth lighter: no darker on the whole.
		x *= 1.0 + p.pigment.y * (fine.r - 0.5) * 1.4 * mix(0.4, 1.0, flat_) * mid;
	}

	if (p.pigment.w > 0.0) {
		// A glyph is 4 grain cells wide, so the script reads as specks, not text.
		const float GLYPH_CELLS = 4.0;
		float aa = 1.0 / (p.pigment.z * GLYPH_CELLS);
		float gl = mix(glyph_cover(q0 / GLYPH_CELLS, aa * 0.7), glyph_cover(q1 / GLYPH_CELLS, aa * 1.4), blend);
		x *= 1.0 - p.pigment.w * gl * flat_ * mid;
	}

	vec3 c = from_pigment(clamp(x, vec3(0.0), vec3(0.999)));
	c = mix(c0, c, p.brush2.y);
	imageStore(color_image, px, vec4(c, 1.0));
}
