// The drawn look's light for the world (ToonLook): a light() that paints each light in a few flat steps instead of smooth
// shading, as android_toon.gdshader paints the pilgrim, so the place and the figure are lit by the same hand. The light a
// face takes (its turn to the light, the lamp's falloff, the shadow: what the smooth look would give it) is rounded to
// look_band_steps steps with crisp edges between them, so the frame keeps its brightness while a lamp throws rings of flat
// light and a shadow ends on a line; a face any light reaches keeps at least look_band_shade of it; one hard highlight on smooth finishes.
// Global uniforms (project.godot [shader_globals]), set by ToonLook.
// Never written into a shader's file: ToonLook appends this include to a twin of each world shader it bands (a shader with
// no light() of its own), so the plain look is untouched. Each term is a smooth step of the light's own values: nothing is
// decided per pixel from noise or time (docs/art-direction.md#stable-image).

// A shader that draws nothing of the look itself (the foreground silhouettes) takes it here, for the bands and marks; its
// surface coordinates are never recorded, so its marks lie flat as one tone.
#ifndef LOOK_INCLUDED
#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/look.gdshaderinc"
#endif

void light() {
	float amount = clamp(dot(NORMAL, LIGHT), 0.0, 1.0) * ATTENUATION;
	float drawn = look_band_diffuse(amount, look_band_shade, look_uv, look_px, 1.0, 1.0);
	DIFFUSE_LIGHT += LIGHT_COLOR / PI * drawn;

	float ndh = dot(NORMAL, normalize(VIEW + LIGHT));
	// Only a smooth finish shines: on rough stone the highlight would draw stray white lines along every curve.
	SPECULAR_LIGHT += LIGHT_COLOR * look_band_highlight(ndh, ROUGHNESS, amount) * look_band_gloss;
}
