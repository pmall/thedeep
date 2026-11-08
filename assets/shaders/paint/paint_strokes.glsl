#[compute]
#version 450
// The painted pass, step 5 (PaintPass): strokes laid over the brushed frame, at half resolution (a stroke is several
// pixels wide, so the half-res frame holds it, at a quarter of the cost; the composite upsamples it guided by the full
// frame, so its edges against shapes stay crisp).
//
// Dabs of paint laid on a grid anchored to the world, each an ellipse oriented along the form (the tensor at its
// center), in the color under its center, a little varied, streaked by its bristles inside and dry at its ends. A
// stroke only covers pixels that look like its own color and depth, so strokes fill shapes and never cross an edge
// (`strokes`, `stroke size`, `stroke jitter`). In the mechanical hand they snap to 0/45/90 degrees. Cells are
// `stroke size` screen pixels on the play plane and grow with depth (`stroke growth`), so the far is laid in broader
// paint; where the depth crosses from one octave of cells to the next, both, faded.

#include "paint_params.glslinc"

layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(set = 0, binding = 1) uniform sampler2D painted_tex;
layout(set = 0, binding = 2) uniform sampler2D tensor_tex;
layout(set = 0, binding = 3) uniform sampler2D paper_tex;
layout(rgba16f, set = 0, binding = 4) uniform restrict writeonly image2D stroked_out;

// The size of the finest stroke cell octave, in meters.
const float STROKE_BASE = 0.02;
// A stroke's soft edge, in screen pixels: under a pixel of this half-res frame, as the upsample softens it again.
const float EDGE_PIXELS = 1.5;

// The stroke color (rgb) and its coverage (a) at this pixel for one octave of cells: `q` is the pixel in stroke cells,
// `px_per_cell` how many screen pixels a cell spans, `base` the pixel's own paint.
vec4 stroke_at(vec2 q, float px_per_cell, vec2 uv, vec3 base, float z) {
	vec2 cid = floor(q);
	vec4 best = vec4(base, 0.0);
	float best_rank = -1.0;
	float jitter = p.hand.w;
	float mechanical = p.brush2.x;
	// A stroke reaches LENGTH cells from its center, plus its soft edge.
	const float LENGTH = 0.8;
	const float WIDTH = 0.3;
	float aa = EDGE_PIXELS / (px_per_cell * WIDTH);
	float reach = LENGTH * (1.0 + aa);
	float reach2 = reach * reach;
	// Only the highest-ranked stroke that covers this pixel is drawn, so a cell that ranks no higher than the best found
	// so far is skipped before any texture is read, and each test runs from the cheapest reject to the costliest.
	for (int dy = -1; dy <= 1; dy++) {
		for (int dx = -1; dx <= 1; dx++) {
			vec2 id = cid + vec2(dx, dy);
			vec4 h = hash42(id);
			float h0 = h.x;
			float h1 = h.y;
			float h2 = h.z;
			float rank = h.w;
			if (rank <= best_rank) {
				continue;
			}
			vec2 center = id + 0.5 + (vec2(h0, h1) - 0.5) * 0.7;
			vec2 rel = q - center; // in cells, world axes (y up)
			if (dot(rel, rel) > reach2) {
				continue;
			}

			// The stroke's center on screen: cells to pixels, world y up to screen y down.
			vec2 center_uv = uv - vec2(rel.x, -rel.y) * px_per_cell / p.size.xy;
			vec4 under = textureLod(painted_tex, center_uv, 0.0);
			// A stroke stays inside its shape: it covers only what looks like its own paint and depth.
			vec3 dc = under.rgb - base;
			float same = exp(-dot(dc, dc) * 90.0) * exp(-abs(under.a - z) / (0.03 * z + 0.05));
			if (same <= 0.08) {
				continue;
			}

			vec4 t = textureLod(tensor_tex, center_uv, 0.0);
			float e = t.x;
			float f = t.y;
			float g = t.z;
			float root = sqrt((e - g) * (e - g) + 4.0 * f * f);
			float l1 = 0.5 * (e + g + root);
			float l2 = 0.5 * (e + g - root);
			float aniso = (l1 + l2 > 1e-8) ? (l1 - l2) / (l1 + l2) : 0.0;
			// Along the edge (the tensor's minor eigenvector), in world axes; where the form gives no direction, the hand's.
			vec2 tangent = vec2(l1 - e, -f);
			vec2 along = dot(tangent, tangent) > 1e-12 ? normalize(vec2(tangent.x, -tangent.y)) : vec2(1.0, 0.0);
			float hand_angle = (h2 - 0.5) * 1.2 + 0.35;
			vec2 hand_dir = vec2(cos(hand_angle), sin(hand_angle));
			along = normalize(mix(hand_dir, along * sign(dot(along, hand_dir) + 1e-4), smoothstep(0.15, 0.6, aniso)));
			if (mechanical > 0.0) {
				float an = atan(along.y, along.x);
				float snapped = round(an / 0.78539816) * 0.78539816;
				an = mix(an, snapped, mechanical);
				along = vec2(cos(an), sin(an));
			}
			vec2 across = vec2(-along.y, along.x);
			vec2 local = vec2(dot(rel, along), dot(rel, across));

			// An ellipse 2 LENGTH long and 2 WIDTH wide, tapered toward its ends.
			float u = local.x / LENGTH;
			float w = WIDTH * (1.0 - 0.35 * u * u);
			float d = length(vec2(u, local.y / max(w, 1e-3)));
			float cover = (1.0 - smoothstep(1.0 - aa, 1.0 + aa, d)) * same;
			if (cover <= 0.08) {
				continue;
			}

			// Bristles: streaks along the stroke, and a dry end where they break up.
			float bristle = textureLod(paper_tex, vec2(local.y * 3.0 + rank * 37.0, local.x * 0.25 + h0 * 11.0) / 8.0, 0.0).a;
			float dry = smoothstep(0.2, 1.0, abs(u)) * 0.9;
			cover *= clamp(1.0 - dry * (1.0 - smoothstep(0.35, 0.65, bristle)) * 1.6, 0.0, 1.0);
			if (cover > 0.08) {
				best_rank = rank;
				// No two loads of a brush are the same: value and a little hue vary per stroke.
				vec3 color = under.rgb;
				color *= 1.0 + (h2 - 0.5) * 0.3 * jitter;
				color *= 1.0 + (vec3(h0, h1, h0) - 0.5) * vec3(0.03, 0.0, -0.03) * jitter;
				color *= 1.0 + (bristle - 0.5) * 0.15;
				// Paint gathers at the stroke's edge.
				color *= 1.0 - 0.12 * jitter * smoothstep(0.55, 1.0, d);
				best = vec4(color, cover);
			}
		}
	}
	return best;
}

void main() {
	ivec2 hp = ivec2(gl_GlobalInvocationID.xy);
	if (hp.x >= int(p.size.z) || hp.y >= int(p.size.w)) {
		return;
	}

	vec4 here = texelFetch(painted_tex, hp, 0);
	vec3 x = here.rgb;
	float z = here.a;
	if (p.hand.z > 0.0) {
		vec2 uv = (vec2(hp) + 0.5) / p.size.zw;
		vec2 anchor = world_anchor(world_at(uv, z));
		float stroke_px = p.hand.y * mix(1.0, p.tone.w, far_share(z));
		float stroke_m = stroke_px * pixel_meters(z);
		float slevel = log2(max(stroke_m / STROKE_BASE, 1.0));
		float slo = floor(slevel);
		float sblend = slevel - slo;
		vec2 sq = anchor / (STROKE_BASE * exp2(slo));
		float px_per_cell = stroke_px * exp2(-sblend);
		vec4 s0 = stroke_at(sq, px_per_cell, uv, x, z);
		float fade = smoothstep(0.7, 1.0, sblend);
		if (fade > 0.0) {
			vec4 s1 = stroke_at(sq * 0.5, px_per_cell * 2.0, uv, x, z);
			s0 = mix(s0, s1, fade);
		}
		x = mix(x, s0.rgb, s0.a * p.hand.z);
	}

	imageStore(stroked_out, hp, vec4(x, z));
}
