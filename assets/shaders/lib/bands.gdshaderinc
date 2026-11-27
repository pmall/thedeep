// The drawn look's light for the world (ToonLook): a light() that paints each light in a few flat steps instead of smooth
// shading, as android_toon.gdshader paints the pilgrim, so the place and the figure are lit by the same hand. A face's turn
// to the light is rounded to look_band_steps steps with crisp edges between them, so each face takes one flat value and the
// frame keeps its brightness; the lamp's falloff and the shadow then dim it as they would (a step on the falloff sliced
// flat faces in two); a face turned to any light at all keeps at least look_band_shade of it; one hard highlight on smooth finishes.
// Global uniforms (project.godot [shader_globals]), set by ToonLook.
// Never written into a shader's file: ToonLook appends this include to a twin of each world shader it bands (a shader with
// no light() of its own), so the plain look is untouched. Each term is a smooth step of the light's own values: nothing is
// decided per pixel from noise or time (docs/art-direction.md#stable-image).

// A shader that draws nothing of the look itself (the foreground silhouettes) takes it here, for the marks; its surface
// coordinates are never recorded, so its marks lie flat as one tone.
#ifndef LOOK_INCLUDED
#include "res://assets/shaders/lib/noise.gdshaderinc"
#include "res://assets/shaders/lib/look.gdshaderinc"
#endif

// How many steps of light, and how soft the edge between two is, as a share of a step.
global uniform float look_band_steps;
global uniform float look_band_softness;
// The least light, a share of the light's full, that a face it reaches at all takes.
global uniform float look_band_shade;
// The hard highlight's strength.
global uniform float look_band_gloss;

// `amount` (0 and up) rounded to steps of 1 / look_band_steps, each edge a smooth step `look_band_softness` of a step wide.
float look_band(float amount) {
	float steps = max(look_band_steps, 1.0);
	float x = amount * steps;
	float below = floor(x);
	float soft = clamp(look_band_softness, 0.01, 0.5);
	return (below + smoothstep(0.5 - soft, 0.5 + soft, x - below)) / steps;
}

void light() {
	float turn = clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	float amount = turn * ATTENUATION;
	// Only the face's turn to the light is stepped, so each face takes one flat value; the lamp's falloff and the shadow
	// stay as they are, so no step edge ever slices a flat face in two where a lamp's reach fades across it.
	float banded = max(look_band(turn), look_band_shade * smoothstep(0.005, 0.02, turn)) * ATTENUATION;
	// Or drawn in the look's marks (lib/look): the light's amount read as dots or hatching laid on the surface.
	float drawn = look_mark > 0.0 ? mix(banded, look_marked(amount, look_uv, look_px, 1.0, 1.0), look_mark) : banded;
	DIFFUSE_LIGHT += LIGHT_COLOR / PI * drawn;

	float ndh = dot(NORMAL, normalize(VIEW + LIGHT));
	float at = mix(0.985, 0.9, ROUGHNESS);
	float gloss = smoothstep(at - 0.004, at + 0.004, ndh) * smoothstep(0.3, 0.4, amount);
	// Only a smooth finish shines: on rough stone the highlight would draw stray white lines along every curve.
	SPECULAR_LIGHT += LIGHT_COLOR * gloss * look_band_gloss * (1.0 - smoothstep(0.45, 0.7, ROUGHNESS));
}
