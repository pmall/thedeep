// The makers' script, the one glyph source (glyph.gdshader's faith glyphs, token_glow.gdshader's code script): each glyph is
// strokes between the nodes of a 3x4 grid round a spine, often mirrored, sometimes under a halo. Needs noise.gdshaderinc first.

float segment_distance(vec2 p, vec2 a, vec2 b) {
	vec2 pa = p - a;
	vec2 ba = b - a;
	float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
	return length(pa - ba * h);
}

// Distance to the strokes of glyph `id`, in glyph-height units; p is in [0, 0.6] x [0, 1]; `h` (in [0, 1)) picks its shape.
float glyph_distance(vec2 p, vec2 id, float h) {
	bool mirrored = h < 0.6;
	if (mirrored) {
		p.x = 0.3 - abs(p.x - 0.3);
	}
	// Node grid: 3 columns (x = 0.06, 0.3, 0.54) by 4 rows (y = 0.1 .. 0.9).
	vec2 node_step = vec2(0.24, 0.8 / 3.0);
	vec2 origin = vec2(0.06, 0.1);
	float d = 1e3;
	// The spine keeps every glyph from falling apart.
	d = min(d, segment_distance(p, origin + vec2(1.0, 0.0) * node_step, origin + vec2(1.0, 3.0) * node_step));
	for (int r = 0; r < 4; r++) {
		for (int c = 0; c < 2; c++) {
			vec2 a = origin + vec2(float(c), float(r)) * node_step;
			float k = hash12(id + vec2(float(c) * 7.0 + 1.0, float(r) * 3.0 + 2.0) + h);
			if (k < 0.42) {
				d = min(d, segment_distance(p, a, a + vec2(node_step.x, 0.0)));
			}
			if (r < 3) {
				float v = hash12(id + vec2(float(c) * 5.0 + 11.0, float(r) * 13.0 + 4.0) + h);
				if (v < 0.3) {
					d = min(d, segment_distance(p, a, a + vec2(0.0, node_step.y)));
				} else if (v < 0.42) {
					d = min(d, segment_distance(p, a, a + node_step));
				}
			}
		}
	}
	if (fract(h * 7.31) < 0.3) {
		// A halo over the glyph.
		d = min(d, abs(length(p - vec2(0.3, 0.9)) - 0.12));
	}
	return d;
}
