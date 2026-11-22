// The drawn look, shared by the world's materials and the pilgrim's so the figure and the place it walks in are drawn
// alike: ink along every piece's edges and a still paper grain. Global uniforms (project.godot [shader_globals]), set by
// ToonLook: all 0 and the materials are exactly as without it. Both are fixed to the surface and fade before they get
// finer than a few pixels, so the image stays still (docs/art-direction.md#stable-image).
// Needs lib/noise (value_noise2). Usage, last in fragment() before ALBEDO is set: `albedo = look_drawn(albedo, f.uv, f.edge);`;
// a surface with no edges to ink takes the grain alone: `albedo *= look_paper(uv, look_pixel(uv));`.

// How strong the ink along the edges is, 0 to 1.
global uniform float look_ink;
// The ink line's width, pixels; the pilgrim's outline (ink_outline.gdshader) reads it too.
global uniform float look_ink_pixels;
global uniform vec4 look_ink_color;
// How strong the paper grain is, 0 to 1.
global uniform float look_grain;

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

// The albedo drawn: grained, and inked within `look_ink_pixels` of the piece's edge (`edge`, meters; 1e3 where none).
vec3 look_drawn_at(vec3 albedo, vec2 uv, float edge, float pixel) {
	albedo *= look_paper(uv, pixel);
	float width = look_ink_pixels * pixel;
	// The line wavers a little along its length, as drawn by hand.
	width *= 0.75 + 0.5 * value_noise2(uv / 0.3);
	float ink = 1.0 - smoothstep(width - pixel * 0.5, width + pixel * 0.5, edge);
	return mix(albedo, look_ink_color.rgb, ink * look_ink);
}

// Meters per pixel at `uv` (meters on the surface); a macro, since derivatives are only taken in fragment() itself.
#define look_pixel(uv) (max(length(dFdx(uv)), length(dFdy(uv))) + 1e-5)
#define look_drawn(albedo, uv, edge) look_drawn_at(albedo, uv, edge, look_pixel(uv))
