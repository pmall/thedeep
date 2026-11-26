// The vignette (scenes/camera/Vignette.tscn): the screen's edges darkened toward the biome's frame color, so the bright
// stage sits in a dark frame (docs/art-direction.md#look). A fixed multiply laid over the 3D view: its factor is a pure
// function of the pixel's place on the screen, never of what was drawn there (docs/art-direction.md#stable-image).
// Strength and color come from the room's BiomeProfile through the vignette globals. With the drawn look (ToonLook's
// Frame, the global look_frame) its edge is ragged as charcoal rubbed around a drawing, the ragging fixed to the screen.
shader_type canvas_item;
render_mode blend_mul, unshaded;

#include "res://assets/shaders/lib/noise.gdshaderinc"

global uniform float vignette;
global uniform vec4 vignette_color;
global uniform float look_frame;

/// Where the darkening starts and where it is full, as a share of the half diagonal.
uniform float inner : hint_range(0.0, 1.5) = 0.45;
uniform float outer : hint_range(0.0, 2.0) = 1.15;
/// The center of the clear area, a little above the screen's middle so the bottom rows darken more.
uniform vec2 center = vec2(0.5, 0.48);

void fragment() {
	// Elliptical, following the screen's aspect.
	vec2 aspect = vec2(SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x, 1.0);
	vec2 q = (SCREEN_UV - center) * 2.0;
	float d = length(q * aspect) / length(aspect);
	float v = smoothstep(inner, outer, d);
	if (look_frame > 0.0) {
		// Charcoal: the edge pushed in and out by broad strokes along it, and rubbed in streaks that run along the nearest
		// border (up the sides, across the top and bottom), with a firmer margin near the border.
		vec2 uv = SCREEN_UV * aspect;
		float side = smoothstep(-0.15, 0.15, abs(q.x) * aspect.x / length(aspect) - abs(q.y) / length(aspect));
		float streaks = mix(value_noise2(vec2(uv.x * 3.0, uv.y * 70.0)), value_noise2(vec2(uv.x * 70.0, uv.y * 3.0) + 9.0), side);
		float rag = (value_noise2(uv * vec2(3.0, 5.0)) - 0.5) * 0.22 + (streaks - 0.5) * 0.08;
		float drawn = smoothstep(inner + 0.3, outer, d + rag);
		float margin = smoothstep(0.92, 0.97, d + rag * 0.7);
		v = mix(v, max(drawn, margin), look_frame);
	}
	v *= vignette;
	COLOR = vec4(mix(vec3(1.0), vignette_color.rgb, v), 1.0);
}
