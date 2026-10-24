// Hashes and noise shared by the material library. No textures: everything is computed.
// Hashes are Dave Hoskins' "hash without sine" (stable on GPUs, fine for coordinates up to ~1e4).

float hash12(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

vec2 hash22(vec2 p) {
	vec3 p3 = fract(vec3(p.xyx) * vec3(0.1031, 0.1030, 0.0973));
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.xx + p3.yz) * p3.zy);
}

float hash13(vec3 p3) {
	p3 = fract(p3 * 0.1031);
	p3 += dot(p3, p3.zyx + 31.32);
	return fract((p3.x + p3.y) * p3.z);
}

// Value noise in [0, 1], smooth (quintic) interpolation.
float value_noise2(vec2 p) {
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
	float a = hash12(i);
	float b = hash12(i + vec2(1.0, 0.0));
	float c = hash12(i + vec2(0.0, 1.0));
	float d = hash12(i + vec2(1.0, 1.0));
	return mix(mix(a, b, u.x), mix(c, d, u.x), u.y);
}

float value_noise3(vec3 p) {
	vec3 i = floor(p);
	vec3 f = fract(p);
	vec3 u = f * f * (3.0 - 2.0 * f);
	float n000 = hash13(i);
	float n100 = hash13(i + vec3(1.0, 0.0, 0.0));
	float n010 = hash13(i + vec3(0.0, 1.0, 0.0));
	float n110 = hash13(i + vec3(1.0, 1.0, 0.0));
	float n001 = hash13(i + vec3(0.0, 0.0, 1.0));
	float n101 = hash13(i + vec3(1.0, 0.0, 1.0));
	float n011 = hash13(i + vec3(0.0, 1.0, 1.0));
	float n111 = hash13(i + vec3(1.0, 1.0, 1.0));
	return mix(
		mix(mix(n000, n100, u.x), mix(n010, n110, u.x), u.y),
		mix(mix(n001, n101, u.x), mix(n011, n111, u.x), u.y), u.z);
}

// Fractal sums in [0, 1]. Each octave is rotated so the lattice never lines up.
float fbm2(vec2 p, int octaves) {
	const mat2 turn = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8));
	float sum = 0.0;
	float amplitude = 0.5;
	float total = 0.0;
	for (int i = 0; i < octaves; i++) {
		sum += amplitude * value_noise2(p);
		total += amplitude;
		p = turn * p * 2.03 + vec2(17.1, 3.7);
		amplitude *= 0.5;
	}
	return sum / total;
}

float fbm3(vec3 p, int octaves) {
	float sum = 0.0;
	float amplitude = 0.5;
	float total = 0.0;
	for (int i = 0; i < octaves; i++) {
		sum += amplitude * value_noise3(p);
		total += amplitude;
		p = p.yzx * 2.03 + vec3(17.1, 3.7, 9.2);
		amplitude *= 0.5;
	}
	return sum / total;
}

// Voronoi cells: x = distance to the nearest cell border (crack lines), y = hash of that cell.
// Two 3x3 passes (Inigo Quilez' border distance); exact enough for cracks.
vec2 voronoi_border(vec2 p) {
	vec2 cell = floor(p);
	vec2 f = fract(p);
	vec2 nearest_cell = vec2(0.0);
	vec2 nearest = vec2(0.0);
	float best = 8.0;
	for (int j = -1; j <= 1; j++) {
		for (int i = -1; i <= 1; i++) {
			vec2 g = vec2(float(i), float(j));
			vec2 r = g + hash22(cell + g) - f;
			float d = dot(r, r);
			if (d < best) {
				best = d;
				nearest = r;
				nearest_cell = g;
			}
		}
	}
	float border = 8.0;
	for (int j = -1; j <= 1; j++) {
		for (int i = -1; i <= 1; i++) {
			vec2 g = nearest_cell + vec2(float(i), float(j));
			vec2 r = g + hash22(cell + g) - f;
			vec2 between = r - nearest;
			if (dot(between, between) > 0.00001) {
				border = min(border, dot(0.5 * (nearest + r), normalize(between)));
			}
		}
	}
	return vec2(border, hash12(cell + nearest_cell));
}
