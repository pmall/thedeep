// Ink outline: a figure's silhouette drawn as a line of constant width on screen, by its back faces pushed out along
// their normals and painted flat (an inverted hull, a geometry pass: MSAA smooths it, and it reads no screen buffer,
// docs/art-direction.md#stable-image). Where one part passes in front of another the line shows too. Used as the next
// pass of a figure's materials (ToonLook).
shader_type spatial;
render_mode unshaded, cull_front, depth_draw_opaque, shadows_disabled;

uniform vec3 ink_color : source_color = vec3(0.012, 0.012, 0.016);
/// The line's width, in pixels of the frame, whatever the distance.
uniform float ink_pixels : hint_range(0.0, 8.0) = 2.0;

void vertex() {
	vec4 clip = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	vec3 normal = (MODELVIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
	vec2 out_dir = (PROJECTION_MATRIX * vec4(normal, 0.0)).xy;
	float len = length(out_dir);
	if (len > 1e-6) {
		// Clip space spans 2 across the viewport; times w so the push survives the perspective divide.
		clip.xy += out_dir / len * ink_pixels * 2.0 / VIEWPORT_SIZE * clip.w;
	}
	POSITION = clip;
}

void fragment() {
	ALBEDO = ink_color;
}
