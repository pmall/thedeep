// Ink outline: a figure's silhouette drawn as a line of constant width on screen, by its back faces pushed out along
// their normals and painted flat (an inverted hull, a geometry pass: MSAA smooths it, and it reads no screen buffer,
// docs/art-direction.md#stable-image). Where one part passes in front of another the line shows too. Used as the next
// pass of a figure's materials (ToonLook).
shader_type spatial;
render_mode unshaded, cull_front, depth_draw_opaque, shadows_disabled;

#include "res://assets/shaders/lib/noise.gdshaderinc"

// The line's color and width in pixels of the frame, whatever the distance: the drawn look's, shared with the world's
// edges (lib/look).
global uniform float look_ink_pixels;
global uniform vec4 look_ink_color;
/// The line's width as a share of the drawn look's: a figure's may be finer than the world's edges.
uniform float width_share : hint_range(0.0, 2.0) = 1.0;

void vertex() {
	vec4 clip = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	vec3 normal = (MODELVIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
	vec2 out_dir = (PROJECTION_MATRIX * vec4(normal, 0.0)).xy;
	float len = length(out_dir);
	if (len > 1e-6) {
		// The line wavers gently along the body, fixed to the part's UVs, matching the hand-drawn ink on world edges.
		float waver = 0.7 + 0.6 * value_noise2(UV / 0.12);
		// Clip space spans 2 across the viewport; times w so the push survives the perspective divide.
		clip.xy += out_dir / len * look_ink_pixels * width_share * waver * 2.0 / VIEWPORT_SIZE * clip.w;
	}
	POSITION = clip;
}

void fragment() {
	ALBEDO = look_ink_color.rgb;
}
