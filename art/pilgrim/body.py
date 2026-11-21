"""The pilgrim's body (docs/art-direction.md#character): a slender android of shells, one or more per bone, each bound
wholly to its bone, so it bends like a machine at its joints and every clip of the skeleton (skeleton.py) carries over.

A shell is lofted along its bone through rings (`Ring`: where along the bone, how deep front to back and how wide), the
rings in between smoothed; its ends are capped or left open where another shell covers them. Its UVs are the surface
frame the project's android shader reads (assets/shaders/lib/surface.gdshaderinc): UV along the shell and around it in
meters, UV2 the shell's size (negative around a closed ring, which has no edge there), so a material's trim draws cuffs
at a shell's ends and the hair's strands run down it. A shell's material is named after the project's material it
stands for (`MATERIALS`, mapped to assets/materials/pilgrim/ by the import).
"""

import math
from dataclasses import dataclass

import bmesh
import bpy
import skeleton
from mathutils import Vector

MATERIALS: dict[str, tuple[tuple[float, float, float], float, float]] = {
    'Shell': ((0.03, 0.03, 0.035), 0.22, 0.15),
    'ShellTrim': ((0.03, 0.03, 0.035), 0.22, 0.15),
    'Suit': ((0.035, 0.035, 0.042), 0.62, 0.0),
    'Joint': ((0.07, 0.07, 0.08), 0.32, 0.7),
    'Hair': ((0.82, 0.83, 0.86), 0.38, 0.2),
    'Visor': ((0.02, 0.02, 0.025), 0.12, 0.3),
}
"""Each material: its color, roughness and metalness in Blender (the game swaps in the project's own)."""

AROUND = 24
"""Vertices around a ring."""

BETWEEN = 3
"""Rings smoothed in between two given ones."""


@dataclass(frozen=True)
class Ring:
    """A cross-section: `at` the share of the bone's length from its head (outside 0..1 past its ends), `front` and
    `side` the half-depth and half-width (meters), `ahead` how far its center sits forward of the bone (meters)."""

    at: float
    front: float
    side: float
    ahead: float = 0.0


@dataclass(frozen=True)
class Shell:
    """A loft along `bone` through `rings`, of `material`, capped at its start and end or not; `arc` the share of the
    way around it covers, from behind its center, (0, 360) all round (an open shell has edges at its sides); `around`
    lays its UV the other way (U around it), for a band whose material draws along it."""

    bone: str
    rings: list[Ring]
    material: str
    caps: tuple[bool, bool] = (True, True)
    arc: tuple[float, float] = (0.0, 360.0)
    around: bool = False


def _both(shells: list[Shell]) -> list[Shell]:
    """The left side's shells, then their mirrors on the right (a bone's own frame mirrors them)."""
    return shells + [
        Shell(s.bone.replace('Left', 'Right'), s.rings, s.material, s.caps, s.arc, s.around) for s in shells
    ]


def _ring(at: float, front: float, side: float, ahead: float = 0.0) -> Ring:
    return Ring(at, front, side, ahead)


SHELLS: list[Shell] = [
    # The trunk: a pelvis and a chest of lacquered shell over a slim matte waist.
    Shell(
        'Hips',
        [
            _ring(-1.0, 0.055, 0.08),
            _ring(-0.6, 0.085, 0.13),
            _ring(0.0, 0.095, 0.148),
            _ring(0.6, 0.09, 0.14),
            _ring(1.05, 0.078, 0.12),
        ],
        'Shell',
    ),
    Shell(
        'Spine',
        [_ring(-0.15, 0.074, 0.112), _ring(0.5, 0.07, 0.104), _ring(1.15, 0.08, 0.118)],
        'Suit',
        caps=(False, False),
    ),
    Shell(
        'Chest',
        [_ring(-0.1, 0.082, 0.12), _ring(0.5, 0.1, 0.138), _ring(1.0, 0.106, 0.148)],
        'ShellTrim',
        caps=(False, False),
    ),
    Shell(
        'UpperChest',
        [_ring(-0.05, 0.104, 0.152), _ring(0.45, 0.1, 0.163), _ring(0.85, 0.078, 0.138), _ring(1.05, 0.048, 0.075)],
        'Shell',
    ),
    Shell(
        'Neck',
        [_ring(-0.25, 0.045, 0.05), _ring(0.5, 0.041, 0.045), _ring(1.1, 0.043, 0.046)],
        'Suit',
        caps=(False, False),
    ),
    # The head: a smooth faceless skull behind a band over the eyes, a silver bob over it.
    Shell(
        'Head',
        [
            _ring(-0.05, 0.03, 0.03),
            _ring(0.08, 0.052, 0.048, 0.008),
            _ring(0.3, 0.078, 0.068, 0.01),
            _ring(0.55, 0.088, 0.077, 0.006),
            _ring(0.8, 0.084, 0.074),
            _ring(0.97, 0.055, 0.05),
        ],
        'Shell',
    ),
    Shell(
        'Head',
        [_ring(0.47, 0.091, 0.08, 0.007), _ring(0.61, 0.091, 0.08, 0.006)],
        'Visor',
        caps=(False, False),
        around=True,
    ),
    Shell(
        'Head',
        [_ring(0.6, 0.097, 0.086, 0.004), _ring(0.78, 0.094, 0.085), _ring(0.95, 0.07, 0.066), _ring(1.06, 0.03, 0.03)],
        'Hair',
        caps=(False, True),
    ),
    Shell(
        'Head',
        [_ring(0.0, 0.085, 0.092, -0.012), _ring(0.3, 0.094, 0.09, -0.006), _ring(0.62, 0.098, 0.088, 0.002)],
        'Hair',
        caps=(False, False),
        arc=(-75.0, 75.0),
    ),
    *_both(
        [
            # The arms: pauldrons, matte upper arms, lacquered forearms cuffed in white (the right one houses the blade).
            Shell(
                'LeftShoulder', [_ring(0.3, 0.035, 0.035), _ring(0.8, 0.058, 0.055), _ring(1.18, 0.044, 0.044)], 'Shell'
            ),
            Shell(
                'LeftUpperArm',
                [_ring(-0.05, 0.044, 0.045), _ring(0.35, 0.042, 0.043), _ring(0.97, 0.034, 0.034)],
                'Suit',
                caps=(False, False),
            ),
            Shell(
                'LeftLowerArm', [_ring(-0.07, 0.037, 0.037), _ring(0.07, 0.037, 0.037)], 'Joint', caps=(False, False)
            ),
            Shell(
                'LeftLowerArm',
                [_ring(0.06, 0.036, 0.037), _ring(0.3, 0.04, 0.041), _ring(0.93, 0.029, 0.033)],
                'ShellTrim',
            ),
            Shell(
                'LeftHand',
                [
                    _ring(-0.15, 0.022, 0.026),
                    _ring(0.4, 0.016, 0.037),
                    _ring(1.0, 0.012, 0.035),
                    _ring(1.55, 0.007, 0.026),
                ],
                'Suit',
                caps=(False, True),
            ),
            # The legs: matte thighs, jointed knees, lacquered boots to a point.
            Shell(
                'LeftUpperLeg',
                [
                    _ring(-0.05, 0.074, 0.078),
                    _ring(0.35, 0.07, 0.074),
                    _ring(0.85, 0.052, 0.053),
                    _ring(1.0, 0.048, 0.049),
                ],
                'Suit',
                caps=(False, False),
            ),
            Shell('LeftLowerLeg', [_ring(-0.04, 0.05, 0.05), _ring(0.07, 0.05, 0.05)], 'Joint', caps=(False, False)),
            Shell(
                'LeftLowerLeg',
                [
                    _ring(0.06, 0.048, 0.047),
                    _ring(0.3, 0.054, 0.048, -0.008),
                    _ring(0.75, 0.037, 0.033),
                    _ring(1.0, 0.033, 0.03),
                ],
                'ShellTrim',
            ),
            Shell(
                'LeftFoot',
                [
                    _ring(-0.3, 0.034, 0.031, -0.012),
                    _ring(0.1, 0.044, 0.034),
                    _ring(0.6, 0.03, 0.037, -0.012),
                    _ring(1.0, 0.022, 0.035, -0.015),
                ],
                'Shell',
            ),
            Shell(
                'LeftToes',
                [_ring(0.0, 0.02, 0.034, -0.004), _ring(0.6, 0.014, 0.027, -0.006), _ring(1.1, 0.006, 0.014, -0.008)],
                'Shell',
            ),
        ]
    ),
]


def _smoothed(rings: list[Ring]) -> list[Ring]:
    """The rings with `BETWEEN` more between each two, on a Catmull-Rom curve through them."""
    if len(rings) < 2:
        return rings
    out: list[Ring] = []
    for i in range(len(rings) - 1):
        p0, p1, p2, p3 = rings[max(i - 1, 0)], rings[i], rings[i + 1], rings[min(i + 2, len(rings) - 1)]
        for k in range(BETWEEN + 1):
            t = k / (BETWEEN + 1)

            def cr(a: float, b: float, c: float, d: float, t: float = t) -> float:
                return 0.5 * (
                    2 * b + (c - a) * t + (2 * a - 5 * b + 4 * c - d) * t * t + (3 * b - a - 3 * c + d) * t**3
                )

            out.append(
                Ring(
                    cr(p0.at, p1.at, p2.at, p3.at),
                    max(1e-3, cr(p0.front, p1.front, p2.front, p3.front)),
                    max(1e-3, cr(p0.side, p1.side, p2.side, p3.side)),
                    cr(p0.ahead, p1.ahead, p2.ahead, p3.ahead),
                )
            )
    out.append(rings[-1])
    return out


def _frame(bone: skeleton.Bone) -> tuple[Vector, Vector, Vector, Vector, float]:
    """The bone's head, its axis, its front (forward, or up for a bone that points forward) and its side, and its length."""
    head, tail = Vector(bone.head), Vector(bone.tail)
    axis = (tail - head).normalized()
    reference = Vector((0.0, -1.0, 0.0)) if abs(axis.y) < 0.9 else Vector((0.0, 0.0, 1.0))
    front = (reference - axis * reference.dot(axis)).normalized()
    side = Vector(
        (axis.y * front.z - axis.z * front.y, axis.z * front.x - axis.x * front.z, axis.x * front.y - axis.y * front.x)
    )
    return head, axis, front, side, (tail - head).length


def material(name: str, color: tuple[float, float, float], roughness: float, metallic: float) -> bpy.types.Material:
    """A plain surface for Blender's renders: its color, roughness and metalness."""
    made = bpy.data.materials.new(name)
    made.use_nodes = True
    tree = made.node_tree
    assert tree is not None
    shader = tree.nodes['Principled BSDF']
    shader.inputs['Base Color'].default_value = (*color, 1.0)  # type: ignore[attr-defined]
    shader.inputs['Roughness'].default_value = roughness  # type: ignore[attr-defined]
    shader.inputs['Metallic'].default_value = metallic  # type: ignore[attr-defined]
    return made


def _loft(bm: bmesh.types.BMesh, shell: Shell, slot: int, layers: tuple[object, object, object], group: int) -> None:
    deform, uv, uv2 = layers
    bone = skeleton.BY_NAME[shell.bone]
    head, axis, front, side, length = _frame(bone)
    rings = _smoothed(shell.rings)
    closed = shell.arc[1] - shell.arc[0] >= 360.0
    count = AROUND if closed else AROUND // 2 + 1
    span = math.radians(shell.arc[1] - shell.arc[0])

    def angle(j: int) -> float:
        return math.radians(shell.arc[0]) + span * j / (AROUND if closed else count - 1)

    girth = sum(span * math.sqrt((r.front * r.front + r.side * r.side) / 2.0) for r in rings) / len(rings)
    first = rings[0].at * length
    total = (rings[-1].at - rings[0].at) * length

    columns: list[list[bmesh.types.BMVert]] = []
    for ring in rings:
        center = head + axis * (ring.at * length) + front * ring.ahead
        column = []
        for j in range(count):
            # Angle 0 is the back: an open shell (the hair behind the head) is centered there.
            a = angle(j)
            point = center - front * (ring.front * math.cos(a)) + side * (ring.side * math.sin(a))
            vert = bm.verts.new(point.to_tuple())
            vert[deform][group] = 1.0  # type: ignore[index]
            column.append(vert)
        columns.append(column)

    def place(vert_uv: tuple[float, float]) -> tuple[float, float]:
        return (vert_uv[1], vert_uv[0]) if shell.around else vert_uv

    size = (total, -girth if closed else girth)
    size2 = (size[1], size[0]) if shell.around else size

    def face(verts: list[bmesh.types.BMVert], uvs: list[tuple[float, float]]) -> None:
        made = bm.faces.new(verts)
        made.material_index = slot
        made.smooth = True
        for loop, coord in zip(made.loops, uvs, strict=True):
            # glTF turns V upside down (1 - v) between Blender and the game: written turned, it arrives as meant.
            at = place(coord)
            loop[uv].uv = (at[0], 1.0 - at[1])
            loop[uv2].uv = (size2[0], 1.0 - size2[1])

    steps = AROUND if closed else count - 1
    for i in range(len(columns) - 1):
        u0 = rings[i].at * length - first
        u1 = rings[i + 1].at * length - first
        for j in range(steps):
            k = (j + 1) % count
            v0 = girth * j / steps
            v1 = girth * (j + 1) / steps
            face(
                [columns[i][j], columns[i + 1][j], columns[i + 1][k], columns[i][k]],
                [(u0, v0), (u1, v0), (u1, v1), (u0, v1)],
            )

    for end, cap in ((0, shell.caps[0]), (len(rings) - 1, shell.caps[1])):
        if not cap or not closed:
            continue
        ring = rings[end]
        outward = -axis if end == 0 else axis
        pole_at = head + axis * (ring.at * length) + front * ring.ahead + outward * (0.35 * min(ring.front, ring.side))
        pole = bm.verts.new(pole_at.to_tuple())
        pole[deform][group] = 1.0  # type: ignore[index]
        u = ring.at * length - first
        u_pole = u + (-1.0 if end == 0 else 1.0) * 0.35 * min(ring.front, ring.side)
        for j in range(steps):
            k = (j + 1) % count
            v0, v1 = girth * j / steps, girth * (j + 1) / steps
            if end == 0:
                face([columns[end][j], columns[end][k], pole], [(u, v0), (u, v1), (u_pole, v1)])
            else:
                face([columns[end][k], columns[end][j], pole], [(u, v1), (u, v0), (u_pole, v0)])


def build(rig: bpy.types.Object) -> bpy.types.Object:
    """The pilgrim's mesh, bound to `rig` by an armature modifier, one vertex group per bone."""
    mesh = bpy.data.meshes.new('Body')
    body = bpy.data.objects.new('Body', mesh)
    scene = bpy.context.scene
    assert scene is not None
    scene.collection.objects.link(body)
    body.parent = rig
    groups = {bone.name: body.vertex_groups.new(name=bone.name).index for bone in skeleton.BONES}
    slots = list(MATERIALS)

    bm = bmesh.new()
    layers = (bm.verts.layers.deform.verify(), bm.loops.layers.uv.new('UVMap'), bm.loops.layers.uv.new('UV2'))
    for shell in SHELLS:
        _loft(bm, shell, slots.index(shell.material), layers, groups[shell.bone])
    bm.normal_update()
    bm.to_mesh(mesh)
    bm.free()

    for name, (color, roughness, metallic) in MATERIALS.items():
        mesh.materials.append(material(name, color, roughness, metallic))

    modifier = body.modifiers.new('Skeleton', 'ARMATURE')
    assert isinstance(modifier, bpy.types.ArmatureModifier)
    modifier.object = rig
    return body
