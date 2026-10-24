// Where a fragment sits on the box it belongs to, for edge wear, bevels, panels and grime by height.
// The box size comes from the `box_size` instance uniform (Block and BackdropScatter set it); the mesh
// may be sized (Block) or a unit box scaled by its node (BackdropScatter). Faces are the box's six
// axis-aligned planes, so each face gets an exact 2D frame and no UVs are needed.
// Usage: BOX_VERTEX as the body of vertex(), then box_face() in fragment().

instance uniform vec3 box_size = vec3(0.0);

// Position from the box's lower corner, in meters along the box axes (0..box_size).
varying vec3 v_box_pos;
// Box-space normal (its dominant axis picks the face).
varying vec3 v_box_normal;
varying vec3 v_world_pos;
// Per-instance random value in [0, 1), from the node's position: repeated pieces never look identical.
varying flat float v_seed;

struct BoxFace {
	vec2 uv;        // position on the face from its corner, meters
	vec2 size;      // the face's size, meters
	vec3 normal;    // outward face axis, box space
	vec3 tangent;   // box-space direction in which uv.x grows
	vec3 bitangent; // box-space direction in which uv.y grows
	float edge;     // distance to the nearest box edge, meters (1e3 when box_size is unset)
	float height;   // distance above the box's bottom, meters
};

float box_hash(vec3 p) {
	p = fract(p * 0.1031);
	p += dot(p, p.zyx + 31.32);
	return fract((p.x + p.y) * p.z);
}

vec3 box_scale(mat4 model) {
	return vec3(length(model[0].xyz), length(model[1].xyz), length(model[2].xyz));
}

// Fills the varyings; use it as the body of vertex() (varyings can only be assigned there).
#define BOX_VERTEX \
	v_box_pos = VERTEX * box_scale(MODEL_MATRIX) + box_size * 0.5; \
	v_box_normal = NORMAL; \
	v_world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz; \
	v_seed = box_hash(MODEL_MATRIX[3].xyz * 1.37 + vec3(0.5, 7.3, 2.1));

BoxFace box_face() {
	vec3 a = abs(v_box_normal);
	BoxFace f;
	if (a.x > a.y && a.x > a.z) {
		f.uv = v_box_pos.zy;
		f.size = box_size.zy;
		f.normal = vec3(sign(v_box_normal.x), 0.0, 0.0);
		f.tangent = vec3(0.0, 0.0, 1.0);
		f.bitangent = vec3(0.0, 1.0, 0.0);
	} else if (a.y > a.z) {
		f.uv = v_box_pos.xz;
		f.size = box_size.xz;
		f.normal = vec3(0.0, sign(v_box_normal.y), 0.0);
		f.tangent = vec3(1.0, 0.0, 0.0);
		f.bitangent = vec3(0.0, 0.0, 1.0);
	} else {
		f.uv = v_box_pos.xy;
		f.size = box_size.xy;
		f.normal = vec3(0.0, 0.0, sign(v_box_normal.z));
		f.tangent = vec3(1.0, 0.0, 0.0);
		f.bitangent = vec3(0.0, 1.0, 0.0);
	}
	bool known = box_size.x > 0.0;
	vec2 to_edge = min(f.uv, f.size - f.uv);
	f.edge = known ? min(to_edge.x, to_edge.y) : 1e3;
	f.height = known ? v_box_pos.y : 1e3;
	return f;
}

// Box-space normal of the box with its edges rounded by `radius` meters (the gradient of a rounded
// box, read on the flat box): a fake bevel that catches rim light, with no extra geometry.
vec3 box_bevel_normal(BoxFace f, float radius) {
	if (box_size.x <= 0.0) {
		return f.normal;
	}
	vec3 half_size = box_size * 0.5;
	radius = min(radius, 0.45 * min(half_size.x, min(half_size.y, half_size.z)));
	vec3 p = v_box_pos - half_size;
	vec3 d = p - clamp(p, -half_size + radius, half_size - radius);
	return length(d) > 1e-6 ? normalize(d) : f.normal;
}

// Tilts a box-space normal by a height gradient (meters per meter along the face's tangent frame).
vec3 box_bump(BoxFace f, vec3 n, vec2 gradient) {
	return normalize(n - gradient.x * f.tangent - gradient.y * f.bitangent);
}

// Box-space direction to view space (NORMAL in fragment()).
vec3 box_to_view(vec3 n, mat4 model, mat4 view) {
	mat3 rotation = mat3(normalize(model[0].xyz), normalize(model[1].xyz), normalize(model[2].xyz));
	return normalize(mat3(view) * (rotation * n));
}

// Box-space direction to world space.
vec3 box_to_world(vec3 n, mat4 model) {
	mat3 rotation = mat3(normalize(model[0].xyz), normalize(model[1].xyz), normalize(model[2].xyz));
	return normalize(rotation * n);
}
