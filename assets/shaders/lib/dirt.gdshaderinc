// The drawn look's dirt (ToonLook): what makes a drawn place lived in, layered over a surface's own finish. Each part has
// its own global uniform, all 0 without the look, so each can be kept, tuned or dropped alone:
//   look_wash   watercolor blooms: broad stains of the biome's silt, each with the darker tide line its pigment dried to
//   look_drips  streaks run down a side face from its top edge, each its own length
//   look_hatch  ink hatching where a surface is dark or dirty: its underside, its foot, its drips and stains, its hollows;
//               denser and doubled the darker it is, as an etcher shades
// All of it is fixed to the surface and in meters, each line several pixels wide at the play camera and faded out before
// it gets finer (docs/art-direction.md#stable-image).
// Needs lib/noise, lib/palette and lib/look; look_dirt reads a Surface (lib/surface). Usage, in fragment() after the finish and before look_drawn:
//   LookDirt d = look_dirt(albedo, f, v_world_pos); albedo = d.albedo; rough = mix(rough, 1.0, d.matte);
// A surface with no surface frame (the architecture behind a room) takes the wash alone: `albedo = look_wash_at(albedo, p, stain);`.

global uniform float look_wash;
global uniform float look_drips;
global uniform float look_hatch;

// The hatching's line spacing, meters: 9 px at the play camera; faded out once it is under 4.
const float LOOK_HATCH_SPACING = 0.1;

// How much of the dirt a shader takes, 0 to 1: defined before the include to take less (pale metal keeps most of its
// read for the grip: docs/art-direction.md#midground).
#ifndef LOOK_DIRT_SHARE
#define LOOK_DIRT_SHARE 1.0
#endif

struct LookDirt {
	vec3 albedo;
	// How much of the surface is now dirt, which is matte, 0 to 1.
	float matte;
};

// Where a fragment of a side face sits under its patch's top edge: x meters across the face, y meters below the top;
// y < 0 where the patch has no top (a floor, a ceiling, a face lying sideways).
vec2 look_under_top(vec2 uv, vec2 size, vec3 tangent, vec3 bitangent, mat4 inv_view) {
	vec3 b = normalize(mat3(inv_view) * bitangent);
	vec3 t = normalize(mat3(inv_view) * tangent);
	if (b.y > 0.7) {
		return vec2(uv.x, size.y - uv.y);
	}
	if (b.y < -0.7) {
		return vec2(uv.x, uv.y);
	}
	if (t.y > 0.7) {
		return vec2(uv.y, size.x - uv.x);
	}
	if (t.y < -0.7) {
		return vec2(uv.y, uv.x);
	}
	return vec2(0.0, -1.0);
}

// One family of hatch lines across `uv` (meters) at `angle`, `spacing` meters apart, `width` their share of it (0 none):
// broken into strokes that each waver a little, as drawn by hand, and faded out where the spacing gets under four pixels.
float look_hatch_lines(vec2 uv, float angle, float spacing, float width, float pixel) {
	vec2 dir = vec2(cos(angle), sin(angle));
	float across = dot(uv, vec2(-dir.y, dir.x)) / spacing;
	float along = dot(uv, dir) / spacing;
	float row = floor(across);
	across += (value_noise2(vec2(along * 0.22, row * 3.7)) - 0.5) * 0.35;
	float d = abs(fract(across) - 0.5);
	float w = width * 0.5;
	float aa = pixel / spacing;
	float line = 1.0 - smoothstep(w - aa, w + aa, d);
	// Strokes of a few spacings each, with gaps between them.
	float stroke = smoothstep(0.3, 0.42, value_noise2(vec2(along * 0.3, row * 5.1 + 2.0)));
	float fade = 1.0 - smoothstep(spacing / 6.0, spacing / 4.0, pixel);
	return line * stroke * step(0.02, width) * fade;
}

// The watercolor wash over `albedo` at `p` (world meters): where a broad noise passes a threshold a stain sits, a little
// darker and toward the silt's hue, its border a darker tide line, as wet pigment dries to its rim; beneath the stains a
// slow swell of value over the meters. Returns the washed albedo; `stain` says where the stains are.
vec3 look_wash_at(vec3 albedo, vec3 p, out float stain) {
	vec3 silt = srgb_to_linear(biome_silt.rgb);
	float value = dot(albedo, vec3(0.2126, 0.7152, 0.0722));
	// Stretched up and down, as damp runs: a stain is taller than wide.
	vec3 q = p * vec3(1.0, 0.45, 1.0);
	float bloom = value_noise3(q * 0.5 + 3.1) * 0.6 + value_noise3(q * 1.6 + 7.7) * 0.4;
	float bloom_aa = fwidth(bloom) + 1e-4;
	stain = smoothstep(0.6 - bloom_aa, 0.6 + bloom_aa, bloom);
	// Each stain its own strength; inside, the pigment gathers toward the rim and dries there to a thin tide line.
	float strength = look_wash * (0.35 + 0.65 * value_noise3(p * 0.3 + 20.0));
	float rimward = stain * (1.0 - smoothstep(0.0, 0.08, bloom - 0.6));
	float tide = 1.0 - smoothstep(0.0, 0.01 + bloom_aa * 1.5, abs(bloom - 0.6));
	vec3 stained = mix(albedo, silt * value * 1.2, 0.3) * 0.94;
	albedo = mix(albedo, stained, stain * strength);
	albedo *= 1.0 - (rimward * 0.12 + tide * 0.3) * strength;
	albedo *= 1.0 + (value_noise3(p * 0.21 + 11.0) - 0.5) * 0.45 * look_wash;
	return albedo;
}

LookDirt look_dirt_at(vec3 albedo, vec3 p, vec3 n, vec2 uv, vec2 under_top, float height, float cavity, float pixel) {
	LookDirt d;
	vec3 clean = albedo;
	vec3 silt = srgb_to_linear(biome_silt.rgb);
	float stain;
	albedo = look_wash_at(albedo, p, stain);

	// Drips from the top edge: columns of a few centimeters, about half of them streaked, each streak its own length.
	float drip = 0.0;
	if (under_top.y >= 0.0) {
		float x = under_top.x / 0.09;
		float column = floor(x);
		float pick = hash12(vec2(column, 3.3));
		float len = mix(0.25, 2.4, pick * pick);
		float width = 0.25 + 0.35 * value_noise2(vec2(column * 1.7, under_top.y * 1.6));
		float across = abs(fract(x) - 0.5) * 2.0;
		float aa = pixel / 0.09 * 2.0;
		drip = (1.0 - smoothstep(width - aa, width + aa, across)) * step(0.45, hash12(vec2(column, 9.1)));
		drip *= 1.0 - smoothstep(len * 0.3, len, under_top.y);
		drip *= 1.0 - smoothstep(0.09 / 4.0, 0.09 / 2.0, pixel);
	}
	albedo = mix(albedo, albedo * mix(vec3(1.0), silt * 1.6, 0.4) * 0.55, drip * look_drips);

	// The hatching follows where a surface is always dark: its underside, its foot (the first half meter of a wall), its
	// drips, its hollows.
	float below = smoothstep(-0.3, -0.7, n.y);
	// Only a wall has a foot: a face under 1.5 m tall (a slab's front) has none.
	float tall = under_top.y >= 0.0 ? smoothstep(1.4, 1.6, height + under_top.y) : 1.0;
	float foot = abs(n.y) < 0.5 ? (1.0 - smoothstep(0.0, 0.6, height)) * tall : 0.0;
	float dark = max(max(below * 0.8, foot * 0.7), max(drip * 0.5, cavity));
	dark = clamp(dark + (value_noise3(p * 1.7) - 0.5) * 0.5, 0.0, 1.0);
	float hatch = look_hatch_lines(uv, 0.785, LOOK_HATCH_SPACING, dark * 0.4, pixel);
	hatch = max(hatch, look_hatch_lines(uv, -0.785, LOOK_HATCH_SPACING, (dark - 0.6) * 0.8, pixel));
	albedo = mix(albedo, look_ink_color.rgb, hatch * look_hatch * 0.75);

	d.albedo = mix(clean, albedo, LOOK_DIRT_SHARE);
	d.matte = max(stain * look_wash * 0.5, drip * look_drips * 0.6) * LOOK_DIRT_SHARE;
	return d;
}

// Built-ins are only readable in fragment() itself, hence the macro.
#define look_dirt(albedo, f, p) look_dirt_at(albedo, p, view_to_world(f.normal, INV_VIEW_MATRIX), f.uv, look_under_top(f.uv, f.size, f.tangent, f.bitangent, INV_VIEW_MATRIX), f.height, f.cavity, look_pixel(f.uv))
