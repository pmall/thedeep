// Rime and hoarfrost from the biome (BiomeProfile.Frost, FrostColor): 0 outside the frozen layer, so nothing changes there;
// in it, a coat of rime lies over every material that frosts. Its fine texture is the baked frost set (FrostSurface; the
// sampling contract is SurfaceMaps), projected on the world's three planes and blended by the face's normal, so it runs on
// across pieces of every material alike: blotches pale and dark, small gaps where it is thin, crusts, hoarfrost fronds, and
// crystals that glint through the specular. Broad masks only say how much rime a place takes: a cap on what faces up and
// feathers out of crevices and along edges, solid where they are full and down to the coat's thickest parts where they
// thin out; and over everything a translucent film, pale where the coat is thick and dark in its gaps, so it mottles the
// surface under it without making it paler as a whole. Include after noise.gdshaderinc and palette.gdshaderinc; each material passes its own share: metal
// takes less, so grippable metal keeps its panels and gloss and never reads as frosted stone.

global uniform float biome_frost;
global uniform vec4 biome_frost_color;
global uniform vec4 biome_frost_shade;
// The baked frost set, global (project.godot sets it) so every material that frosts shares it without a slot of its own.
global uniform sampler2D frost_surface : repeat_enable, filter_linear_mipmap_anisotropic;
global uniform sampler2D frost_normal : repeat_enable, filter_linear_mipmap_anisotropic;

// The set's tile (FrostSurface.TileMeters).
const float FROST_TILE = 4.0;
// The film: the cells per meter of its patches, how opaque it is at least and at most, and its tone against the rime's
// in the coat's gaps and where it is thickest.
const float FROST_FILM_FREQUENCY = 0.35;
const float FROST_FILM_MIN = 0.35;
const float FROST_FILM_MAX = 0.6;
const float FROST_FILM_DARK = 0.24;
const float FROST_FILM_PALE = 0.62;

struct Frost {
	float cover; // 0 bare, 1 under rime
	float solid; // the part of the cover that is solid rime, not the film
	float tone;  // multiplies the rime's color
	float rough; // the rime's roughness
	vec3 bend;   // world-space tilt of the normal by the coat's relief, at full cover
};

// Adds the set on one of the world's planes, weighted by `w`: `uv` meters on it, `dx` `dy` their screen derivatives (so the
// mip holds wherever the call sits), `a` `b` the world axes `uv` runs along.
void frost_sample(vec2 uv, vec2 dx, vec2 dy, vec3 a, vec3 b, float w, inout vec3 surface, inout vec3 bend) {
	vec2 st = vec2(uv.x, -uv.y) / FROST_TILE;
	vec2 sx = vec2(dx.x, -dx.y) / FROST_TILE;
	vec2 sy = vec2(dy.x, -dy.y) / FROST_TILE;
	surface += textureGrad(frost_surface, st, sx, sy).rgb * w;
	vec2 slope = textureGrad(frost_normal, st, sx, sy).rg * 2.0 - 1.0;
	bend += (a * slope.x + b * slope.y) * w;
}

// `p` world position (or the part's own, on a machine), `face_n` the face's own world normal (unbumped), `world_n` the bumped
// one, `cavity` 1 deep in a crevice or seam, `edge` meters to the patch edge, `share` the material's take.
Frost frost_at(vec3 p, vec3 face_n, vec3 world_n, float cavity, float edge, float share) {
	Frost fr;
	fr.cover = 0.0;
	fr.solid = 0.0;
	fr.tone = 1.0;
	fr.rough = 0.66;
	fr.bend = vec3(0.0);
	float amount = biome_frost * share;
	if (amount <= 0.0) {
		return fr;
	}

	// The coat: each of the three planes weighted by how squarely the face looks along its axis.
	vec3 dpdx = dFdx(p);
	vec3 dpdy = dFdy(p);
	vec3 w = pow(abs(face_n), vec3(4.0));
	w /= w.x + w.y + w.z;
	vec3 surface = vec3(0.0);
	vec3 bend = vec3(0.0);
	if (w.z > 0.02) {
		frost_sample(p.xy, dpdx.xy, dpdy.xy, vec3(1.0, 0.0, 0.0), vec3(0.0, 1.0, 0.0), w.z, surface, bend);
	}
	if (w.y > 0.02) {
		frost_sample(p.xz, dpdx.xz, dpdy.xz, vec3(1.0, 0.0, 0.0), vec3(0.0, 0.0, 1.0), w.y, surface, bend);
	}
	if (w.x > 0.02) {
		frost_sample(p.zy, dpdx.zy, dpdy.zy, vec3(0.0, 0.0, 1.0), vec3(0.0, 1.0, 0.0), w.x, surface, bend);
	}
	float thick = surface.g;

	// How much rime the place takes. A cap on what faces up, its border wandering, thinner where the wind scoured it.
	float drift = fbm3(p * vec3(0.9, 2.5, 0.9) + 7.0, 3);
	float up = smoothstep(0.45, 0.85, world_n.y);
	float cap = up * smoothstep(0.3, 0.6, drift + amount * 0.22);
	// The film, thicker and thinner in wide patches.
	float film = mix(FROST_FILM_MIN, FROST_FILM_MAX, smoothstep(0.25, 0.75, value_noise3(p * FROST_FILM_FREQUENCY + 5.3)));
	// Hoarfrost feathering out of crevices and along the edges of side faces, in patches.
	float patches = fbm3(p * 2.2 + 3.1, 3);
	float feather = (1.0 - smoothstep(0.0, 0.08 + 0.12 * patches, edge)) * smoothstep(0.35, 0.6, patches);
	float rime = max(cavity * 0.9, feather) * smoothstep(0.3, 0.7, patches + amount * 0.3);
	// Side faces take less than the faces the rime falls on.
	rime *= mix(0.7, 1.0, smoothstep(-0.2, 0.5, face_n.y));
	// A full cap takes more than the coat's thickest, so it covers even the gaps.
	float take = clamp(max(cap * 1.25, rime * 0.85) * amount, 0.0, 1.25);

	// The coat shows solid where it is thicker than the place leaves bare, and as the film elsewhere.
	float solid = smoothstep(0.0, 0.22, thick + take - 1.0) * smoothstep(0.0, 0.15, take);
	fr.cover = max(solid, film * amount);
	fr.solid = solid;
	float film_tone = mix(FROST_FILM_DARK, FROST_FILM_PALE, smoothstep(0.1, 0.75, thick));
	fr.tone = surface.r * 2.0 * mix(film_tone, 1.0, solid);
	fr.rough = surface.b;
	fr.bend = bend;
	return fr;
}

// The rime's own color, linear: the biome's frost color, turned by its shade in crevices.
vec3 frost_color(float cavity) {
	vec3 c = srgb_to_linear(biome_frost_color.rgb);
	return mix(c, c * srgb_to_linear(biome_frost_shade.rgb), cavity);
}

// The view-space normal `n` tilted by the coat's relief where it lies (`view` the VIEW_MATRIX).
vec3 frost_bump(vec3 n, Frost fr, mat4 view) {
	return normalize(n + mat3(view) * fr.bend * fr.cover);
}
