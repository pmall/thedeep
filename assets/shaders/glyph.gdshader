// Emissive "faith" glyphs: pilgrim markings, generated. Put it on a flat mesh (a QuadMesh facing +Z)
// laid just in front of a surface; the mesh is filled with a grid of glyphs `glyph_height` tall, each
// built from strokes between the nodes of a 3x4 grid (often mirrored, sometimes under a halo).
// They breathe slowly. Color: the biome accent, so they follow the layer.
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, shadows_disabled, depth_draw_never;

#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/palette.gdshaderinc"

/// Glyph height in meters; the grid of glyphs fills the mesh.
uniform float glyph_height : hint_range(0.1, 2.0) = 0.45;
/// Stroke width as a share of the glyph height.
uniform float stroke : hint_range(0.01, 0.2) = 0.075;
uniform float intensity : hint_range(0.0, 8.0) = 2.2;
/// Faint glow around the strokes, as a share of the glyph height.
uniform float glow : hint_range(0.0, 0.5) = 0.12;
uniform float pulse_speed : hint_range(0.0, 2.0) = 0.35;
/// Share of grid cells left empty (word gaps).
uniform float gaps : hint_range(0.0, 0.9) = 0.15;
/// Mix toward white: keeps the accent from saturating decor (color means gameplay).
uniform float whiten : hint_range(0.0, 1.0) = 0.35;
uniform int seed = 0;

varying vec2 v_local;
varying flat float v_seed;

float segment_distance(vec2 p, vec2 a, vec2 b) {
	vec2 pa = p - a;
	vec2 ba = b - a;
	float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
	return length(pa - ba * h);
}

// Distance to the strokes of glyph `id`, in glyph-height units; p is in [0, 0.6] x [0, 1].
float glyph_distance(vec2 p, vec2 id) {
	float h = hash12(id * 1.7 + v_seed * 13.0 + float(seed));
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

void vertex() {
	v_local = VERTEX.xy;
	vec3 o = MODEL_MATRIX[3].xyz;
	v_seed = fract(sin(dot(o, vec3(12.9898, 78.233, 37.719))) * 43758.5453);
}

void fragment() {
	vec2 cell_size = vec2(0.72, 1.3) * glyph_height;
	vec2 q = v_local / cell_size;
	vec2 id = floor(q);
	vec2 in_cell = fract(q) * cell_size / glyph_height; // [0, 0.72] x [0, 1.3]
	vec2 p = in_cell - vec2(0.06, 0.15);

	float light = 0.0;
	if (hash12(id * 3.1 + v_seed * 5.0 + float(seed)) >= gaps) {
		float d = glyph_distance(p, id);
		float aa = fwidth(d) * 0.75;
		float core = 1.0 - smoothstep(stroke * 0.5 - aa, stroke * 0.5 + aa, d);
		float halo = glow > 0.0 ? exp(-max(d - stroke * 0.5, 0.0) / glow * 3.0) * 0.35 : 0.0;
		float phase = hash12(id + 0.5) * 6.2832;
		float breath = 0.75 + 0.25 * sin(TIME * pulse_speed * 6.2832 * 0.25 + phase);
		light = (core + halo) * breath;
	}

	vec3 color = mix(srgb_to_linear(biome_accent.rgb), vec3(1.0), whiten);
	ALBEDO = color * light * intensity;
	ALPHA = clamp(light, 0.0, 1.0);
}
