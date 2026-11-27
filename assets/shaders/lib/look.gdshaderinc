// The drawn look, shared by the world's materials and the pilgrim's so the figure and the place it walks in are drawn
// alike: ink along every piece's edges, a still paper grain, brush strokes through the paint, and the marks (dots or
// hatching) the light may be drawn in. Global uniforms (project.godot [shader_globals]), set by
// ToonLook: all 0 and the materials are exactly as without it. Both are fixed to the surface and fade before they get
// finer than a few pixels, so the image stays still (docs/art-direction.md#stable-image).
// Needs lib/noise (value_noise2). Usage, last in fragment() before ALBEDO is set: `albedo = look_drawn(albedo, f.uv, f.edge);`
// and `look_mark_at(f.uv);` so the light can draw its marks (lib/bands);
// a surface with no edges to ink takes the grain alone: `albedo *= look_paper(uv, look_pixel(uv));`.

#define LOOK_INCLUDED

// How strong the ink along the edges is, 0 to 1.
global uniform float look_ink;
// The ink line's width, pixels; the pilgrim's outline (ink_outline.gdshader) reads it too.
global uniform float look_ink_pixels;
global uniform vec4 look_ink_color;
// How strong the paper grain is, 0 to 1.
global uniform float look_grain;
// How strong the brush strokes laid through every surface's paint are, 0 to 1.
global uniform float look_brush;
// How much of the light is drawn as marks instead of shaded (0: none, 1: all), which marks (0: halftone dots, 1: hatching
// that crosses itself in the darks), their cell at the finest, meters, and how far the light is pushed toward full before
// it is drawn (above 1, faces in plain light come out clean and only the shade and shadows carry marks).
global uniform float look_mark;
global uniform float look_mark_kind;
global uniform float look_mark_cell;
global uniform float look_mark_gain;
// The flat painted light bands shared by the world (lib/bands) and the pilgrim (android_toon.gdshader): how many steps of
// light, how soft the edge between two steps is (as a share of a step), the least light a reached face keeps, and the hard
// highlight's strength on smooth finishes.
global uniform float look_band_steps;
global uniform float look_band_softness;
global uniform float look_band_shade;
global uniform float look_band_gloss;

// Where on the surface the marks are laid (meters) and how many meters a pixel covers there: written in fragment() by
// look_mark_at, read by the light (lib/bands, android_toon.gdshader).
varying vec2 look_uv;
varying float look_px;

// The grain's two cells, meters: the coarse one carries it at the play camera, the fine one close up.
const float LOOK_GRAIN_COARSE = 0.045;
const float LOOK_GRAIN_FINE = 0.014;

// The paper grain: a factor around 1 for the albedo, at `uv` (meters on the surface), each cell faded out before it gets
// under three pixels (`pixel`: meters per pixel).
float look_paper(vec2 uv, float pixel) {
	float coarse = (value_noise2(uv / LOOK_GRAIN_COARSE) - 0.5) * (1.0 - smoothstep(LOOK_GRAIN_COARSE / 4.0, LOOK_GRAIN_COARSE / 2.5, pixel));
	float fine = (value_noise2(uv / LOOK_GRAIN_FINE + 17.0) - 0.5) * (1.0 - smoothstep(LOOK_GRAIN_FINE / 4.0, LOOK_GRAIN_FINE / 2.5, pixel));
	return 1.0 + (coarse * 0.8 + fine * 0.5) * look_grain;
}

// Brush strokes through the paint: patches of short strokes, each patch laid in its own direction, each stroke a flat
// value with ragged ends, rows of them offset like a painter's dabs. `scale` scales the stroke size (1 for architecture,
// smaller for a figure's limbs), returning a signed dab value in [-0.5, 0.5], faded out before a stroke gets under four
// pixels wide.
float look_stroke_layer(vec2 uv, float angle, float layer, float scale) {
	vec2 stroke = vec2(0.42, 0.1) * scale;
	vec2 q = vec2(cos(angle) * uv.x + sin(angle) * uv.y, -sin(angle) * uv.x + cos(angle) * uv.y) / stroke;
	float row = floor(q.y);
	// Each row's strokes start at their own offset, and each stroke's ends waver across its width.
	float x = q.x + hash12(vec2(row, layer)) * 7.0 + (value_noise2(vec2(q.y * 2.5, row * 1.3 + layer)) - 0.5) * 0.7;
	return hash12(vec2(floor(x), row + layer * 31.0));
}

float look_stroke_dab(vec2 uv, float pixel, float scale) {
	float inv = 1.0 / max(scale, 0.05);
	float drift = (value_noise2(uv * 0.15 * inv + 2.0) - 0.5) * 0.8;
	float one = look_stroke_layer(uv, 0.45 + drift, 0.0, scale);
	float two = look_stroke_layer(uv, -0.6 + drift, 1.0, scale);
	// Patches about a meter across (scaled with the strokes), each in one direction, meeting on a crisp edge.
	float patch_noise = value_noise2(uv * 0.9 * inv + 6.0);
	float edge = fwidth(patch_noise) + 1e-4;
	float value = mix(one, two, smoothstep(0.5 - edge, 0.5 + edge, patch_noise));
	float fade = 1.0 - smoothstep(0.1 * scale / 6.0, 0.1 * scale / 4.0, pixel);
	return (floor(value * 4.0) / 3.0 - 0.5) * fade;
}

float look_strokes(vec2 uv, float pixel) {
	return 1.0 + look_stroke_dab(uv, pixel, 1.0) * 0.5 * look_brush;
}

// How much of a cell is lit for a light of `tone` (0–1), in one of the look's marks, at `uv` (meters) with cells
// `cell` meters wide: halftone dots growing from each cell's middle, or lines thinning as the light grows, a second family
// across them in the darks (`crossed`: 1 for all of it, 0 for none).
float look_mark_cover(float tone, vec2 uv, float cell, float pixel, float crossed) {
	float aa = pixel / cell * 1.5 + 1e-3;
	if (look_mark_kind < 0.5) {
		// A grid turned 45 degrees, as a printer's screen.
		vec2 q = vec2(uv.x + uv.y, uv.y - uv.x) * 0.7071 / cell;
		// Each dot spreads a little more or less, as ink does on paper.
		float d = length(fract(q) - 0.5) / 0.7071 + (value_noise2(uv * 3.0) - 0.5) * 0.18;
		return smoothstep(d - aa, d + aa, tone);
	}
	// The strokes' direction drifts slowly over the surface, as a hand turns, instead of one ruled angle everywhere.
	float angle = 0.785 + (value_noise2(uv * 0.07 + 3.0) - 0.5) * 0.6;
	vec2 along = vec2(cos(angle), sin(angle));
	vec2 across = vec2(-along.y, along.x);
	float wobble = (value_noise2(uv * 2.0) - 0.5) * 0.3;
	float a = abs(fract(dot(uv, across) / cell + wobble) - 0.5) * 2.0;
	float b = abs(fract(dot(uv, along) / cell - wobble) - 0.5) * 2.0;
	float first = smoothstep(1.0 - tone - aa, 1.0 - tone + aa, a);
	float second = smoothstep((0.5 - tone) * 2.0 - aa, (0.5 - tone) * 2.0 + aa, b);
	return first * mix(1.0, second, crossed);
}

// A light of `amount` (what smooth shading would give, 0 and up) drawn as the look's marks at `uv` (meters), `pixel`
// meters a pixel, the cells `scale` times the look's (a figure's limbs take finer ones), the hatching crossed in the darks
// by `crossed`: the share of the light the point takes. Cells grow with distance in doublings, blended between the two
// nearest, so they never get under seven pixels and none pops (docs/art-direction.md#stable-image).
float look_marked(float amount, vec2 uv, float pixel, float scale, float crossed) {
	float gain = max(look_mark_gain, 1.0);
	float tone = clamp(amount * gain, 0.0, 1.0);
	float cell = max(look_mark_cell * scale, 0.005);
	float level = max(log2(pixel * 7.0 / cell), 0.0);
	float octave = floor(level);
	float near = look_mark_cover(tone, uv, cell * exp2(octave), pixel, crossed);
	float far = look_mark_cover(tone, uv, cell * exp2(octave + 1.0), pixel, crossed);
	return mix(near, far, fract(level)) / gain;
}

// `amount` (0 and up) rounded to steps of 1 / look_band_steps, each edge a smooth step `look_band_softness` of a step wide.
float look_band(float amount) {
	float steps = max(look_band_steps, 1.0);
	float x = amount * steps;
	float below = floor(x);
	float soft = clamp(look_band_softness, 0.01, 0.5);
	return (below + smoothstep(0.5 - soft, 0.5 + soft, x - below)) / steps;
}

// One light's banded diffuse share (0 and up), shared by the world and the figure so both step to the same light.
float look_band_diffuse(float amount, float shade, vec2 uv, float pixel, float mark_scale, float crossed) {
	float reached = smoothstep(0.005, 0.02, amount);
	float banded = max(look_band(amount), shade * reached);
	return look_mark > 0.0 ? mix(banded, look_marked(amount, uv, pixel, mark_scale, crossed), look_mark) : banded;
}

// One light's hard painted highlight on smooth finishes (0 on rough stone/cloth).
float look_band_highlight(float ndh, float rough, float amount) {
	float at = mix(0.985, 0.9, rough);
	float gloss = smoothstep(at - 0.004, at + 0.004, ndh) * smoothstep(0.3, 0.4, amount);
	return gloss * (1.0 - smoothstep(0.45, 0.7, rough));
}

// Records where the marks are laid, for the light; a statement for fragment(), before ALBEDO is set.
#define look_mark_at(uv) look_uv = uv; look_px = look_pixel(uv)

// The albedo drawn: grained, and inked within `look_ink_pixels` of the piece's edge (`edge`, meters; 1e3 where none).
vec3 look_drawn_at(vec3 albedo, vec2 uv, float edge, float pixel) {
	albedo *= look_paper(uv, pixel);
	albedo *= look_strokes(uv, pixel);
	float width = look_ink_pixels * pixel;
	// The line wavers a little along its length, as drawn by hand.
	width *= 0.75 + 0.5 * value_noise2(uv / 0.3);
	float ink = 1.0 - smoothstep(width - pixel * 0.5, width + pixel * 0.5, edge);
	return mix(albedo, look_ink_color.rgb, ink * look_ink);
}

// Meters per pixel at `uv` (meters on the surface); a macro, since derivatives are only taken in fragment() itself.
#define look_pixel(uv) (max(length(dFdx(uv)), length(dFdy(uv))) + 1e-5)
#define look_drawn(albedo, uv, edge) look_drawn_at(albedo, uv, edge, look_pixel(uv))
