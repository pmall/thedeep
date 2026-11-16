// Where a fragment sits on the kit piece it belongs to, for panels, edge wear and grime by height.
// Every kit mesh carries a surface frame instead of texture UVs (scripts/kit/KitMesh.cs, MeshData.cs):
//   UV       position on its patch (a box face, one side of a swept profile), meters
//   UV2      the patch size, meters; negative along an axis that wraps around (a pipe's girth): no edge there
//   TANGENT  direction of +U, with the sign that makes BINORMAL the direction of +V
//   COLOR.r  height above the piece's bottom / 8 m (KitMesh.HeightRange); COLOR.g per-part variation;
//   COLOR.b  cavity: 0 open, 1 deep in a hollow no light reaches (a pipe's mouth, its bore), for the material to darken
// Needs lib/noise (hash13).
// Usage: SURFACE_VERTEX as the body of vertex(), then `Surface f = SURFACE;` in fragment().

// World position of the fragment.
varying vec3 v_world_pos;
// Random value in [0, 1) per instance and part, from the node's position and the part's variation:
// repeated pieces never look identical.
varying flat float v_seed;

struct Surface {
	vec2 uv;        // position on the patch from its corner, meters
	vec2 size;      // the patch size, meters
	float edge;     // distance to the nearest patch edge, meters (1e3 along wrapping axes)
	float height;   // distance above the piece's bottom, meters
	float cavity;   // 0 open, 1 deep in a hollow (MeshData.Cavities)
	vec3 normal;    // view space, interpolated (rounded chamfers)
	vec3 tangent;   // view space, direction of +U
	vec3 bitangent; // view space, direction of +V
};

// Fills the varyings; use it as the body of vertex() (varyings can only be assigned there).
#define SURFACE_VERTEX \
	v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; \
	v_seed = fract(hash13(MODEL_MATRIX[3].xyz * 1.37 + vec3(0.5, 7.3, 2.1)) + COLOR.g * 0.61803);

// Built-ins are only readable in fragment() itself, hence the macro.
#define SURFACE surface_from(UV, UV2, COLOR, NORMAL, TANGENT, BINORMAL)

Surface surface_from(vec2 uv, vec2 uv2, vec4 color, vec3 normal, vec3 tangent, vec3 binormal) {
	Surface f;
	f.uv = uv;
	f.size = abs(uv2);
	vec2 to_edge = min(uv, f.size - uv);
	to_edge = mix(to_edge, vec2(1e3), step(uv2, vec2(0.0)));
	f.edge = max(0.0, min(to_edge.x, to_edge.y));
	f.height = color.r * 8.0;
	f.cavity = color.b;
	f.normal = normalize(normal);
	f.tangent = normalize(tangent);
	f.bitangent = normalize(binormal);
	return f;
}

// Tilts the view-space normal `n` by a height gradient (meters per meter along U and V).
vec3 surface_bump(Surface f, vec3 n, vec2 gradient) {
	return normalize(n - gradient.x * f.tangent - gradient.y * f.bitangent);
}

// The view-space normal of a surface whose baked relief tilts it by `slope` (the normal map's red and green, -1..1: along
// the face and up it).
vec3 surface_relief(Surface f, vec2 slope) {
	vec3 ts = vec3(slope, sqrt(max(0.0, 1.0 - dot(slope, slope))));
	return normalize(f.tangent * ts.x + f.bitangent * ts.y + f.normal * ts.z);
}

// View-space direction to world space.
vec3 view_to_world(vec3 v, mat4 inv_view) {
	return normalize(mat3(inv_view) * v);
}
