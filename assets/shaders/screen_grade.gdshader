// Full-screen finishing pass over the rendered 3D view (scenes/camera/ScreenGrade.tscn): a vignette that
// darkens the frame's edges toward the biome's frame color. Its strength comes from the biome
// (BiomeProfile → global uniforms), so each depth layer finishes its own way. Color grading
// itself stays in the Environment (adjustments + lift/gain lookup).
shader_type canvas_item;

global uniform float post_vignette;
global uniform vec4 post_vignette_color;

uniform sampler2D screen : hint_screen_texture, filter_nearest;
/// Where the vignette starts and where it is full, as a share of the half diagonal.
uniform float vignette_inner : hint_range(0.0, 1.5) = 0.45;
uniform float vignette_outer : hint_range(0.0, 2.0) = 1.15;

void fragment() {
	vec3 c = texture(screen, SCREEN_UV).rgb;

	// Vignette: elliptical, following the screen's aspect, a little heavier at the bottom.
	vec2 aspect = vec2(SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x, 1.0);
	vec2 q = (SCREEN_UV - vec2(0.5, 0.48)) * 2.0;
	float d = length(q * aspect) / length(aspect);
	float v = smoothstep(vignette_inner, vignette_outer, d) * post_vignette;
	c = mix(c, c * post_vignette_color.rgb, v);

	COLOR = vec4(c, 1.0);
}
