// The vignette (scenes/camera/Vignette.tscn): the screen's edges darkened toward the biome's frame color, so the bright
// stage sits in a dark frame (docs/art-direction.md#look). A fixed multiply laid over the 3D view: its factor is a pure
// function of the pixel's place on the screen, never of what was drawn there (docs/art-direction.md#stable-image).
// Strength and color come from the room's BiomeProfile through the vignette globals.
shader_type canvas_item;
render_mode blend_mul, unshaded;

global uniform float vignette;
global uniform vec4 vignette_color;

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
	float v = smoothstep(inner, outer, d) * vignette;
	COLOR = vec4(mix(vec3(1.0), vignette_color.rgb, v), 1.0);
}
