// Rime and hoarfrost from the biome (BiomeProfile.Frost, FrostColor): 0 in the flooded layer, so nothing
// changes there; in the frozen layer it settles on whatever faces up and feathers out of crevices and edges.
// Rock and metal include it (after noise.gdshaderinc and palette.gdshaderinc) and pass their own share:
// metal takes less, so grippable metal keeps its panels and gloss and never reads as frosted stone.

global uniform float biome_frost;
global uniform vec4 biome_frost_color;

struct Frost {
	float cover;   // 0 bare, 1 under rime
	float sparkle; // 1 on a glinting ice crystal
};

// `p` world position, `world_n` the bumped world normal, `face_up` the face's own world normal Y,
// `cavity` 1 deep in a crevice or seam, `edge` meters to the patch edge, `share` the material's take.
Frost frost_at(vec3 p, vec3 world_n, float face_up, float cavity, float edge, float share) {
	Frost fr;
	fr.cover = 0.0;
	fr.sparkle = 0.0;
	float amount = biome_frost * share;
	if (amount <= 0.0) {
		return fr;
	}

	// A cap of rime on up-facing surfaces, its border wandering, thinner where the wind scoured it.
	float drift = fbm3(p * vec3(0.9, 2.5, 0.9) + 7.0, 3);
	float up = smoothstep(0.45, 0.85, world_n.y);
	float cap = up * smoothstep(0.42, 0.56, drift + amount * 0.22);
	// Where it is thin, the stone shows through as a grey dusting rather than a clean border.
	cap = max(cap, up * 0.45 * smoothstep(0.2, 0.42, drift + amount * 0.22));
	// Hoarfrost feathering out of crevices and along the edges of side faces, in patches.
	float patches = fbm3(p * 2.2 + 3.1, 3);
	float feather = (1.0 - smoothstep(0.0, 0.08 + 0.12 * patches, edge)) * smoothstep(0.35, 0.6, patches);
	float rime = max(cavity * 0.9, feather) * smoothstep(0.3, 0.7, patches + amount * 0.3);
	// Side faces take less than the faces the rime falls on.
	rime *= mix(0.7, 1.0, smoothstep(-0.2, 0.5, face_up));
	fr.cover = clamp(max(cap, rime * 0.8) * amount, 0.0, 1.0);

	// Crystals: a sparse grid of glints, only where rime lies.
	vec3 cell = floor(p * 55.0);
	float glint = step(0.985, fract(sin(dot(cell, vec3(12.9898, 78.233, 37.719))) * 43758.5453));
	fr.sparkle = glint * smoothstep(0.4, 0.8, fr.cover);
	return fr;
}

// The rime's own color, linear: the biome's frost color, a touch bluer in the shade of crevices.
vec3 frost_color(float cavity) {
	vec3 c = srgb_to_linear(biome_frost_color.rgb);
	return mix(c, c * vec3(0.82, 0.9, 1.0), cavity);
}
