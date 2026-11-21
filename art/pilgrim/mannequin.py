"""A stand-in body for the skeleton (skeleton.py) while the pilgrim itself is made: rigid tapered segments, one per bone,
each bound wholly to its bone, black with silver joints, so the animation can be judged before the model exists.
"""

from dataclasses import dataclass

import bmesh
import bpy
import skeleton
from mathutils import Matrix, Vector

SHELL, TRIM, GLOW = 0, 1, 2
"""Material slots: the black body, the silver joints, the purple light."""


@dataclass(frozen=True)
class Limb:
    """A tapered segment along a bone, from radius `head` at its head to `tail` at its tail; `depth` flattens it front to
    back (1 round), `inset` shortens it at both ends (meters)."""

    bone: str
    head: float
    tail: float
    depth: float = 1.0
    inset: float = 0.0
    slot: int = SHELL


@dataclass(frozen=True)
class Ball:
    """An ellipsoid bound to `bone`, centered at `at` (meters, rest pose), of radii `size`."""

    bone: str
    at: skeleton.Vec3
    size: skeleton.Vec3
    slot: int = TRIM


def _right(name: str) -> str:
    return name.replace('Left', 'Right')


def _limbs_both_sides(limbs: list[Limb]) -> list[Limb]:
    """The left side's limbs, then their mirrors on the right."""
    return limbs + [Limb(_right(m.bone), m.head, m.tail, m.depth, m.inset, m.slot) for m in limbs]


def _balls_both_sides(balls: list[Ball]) -> list[Ball]:
    """The left side's balls, then their mirrors on the right."""
    return balls + [Ball(_right(b.bone), (-b.at[0], b.at[1], b.at[2]), b.size, b.slot) for b in balls]


LIMBS: list[Limb] = [
    Limb('Spine', 0.095, 0.105, depth=0.8, inset=-0.02),
    Limb('Chest', 0.11, 0.135, depth=0.75),
    Limb('UpperChest', 0.14, 0.12, depth=0.7, inset=-0.01),
    Limb('Neck', 0.038, 0.034, inset=-0.02, slot=TRIM),
    *_limbs_both_sides(
        [
            Limb('LeftShoulder', 0.05, 0.05, depth=0.9, inset=0.02),
            Limb('LeftUpperArm', 0.046, 0.036, inset=0.02),
            Limb('LeftLowerArm', 0.035, 0.026, inset=0.015),
            Limb('LeftHand', 0.028, 0.022, depth=0.5, inset=-0.03),
            Limb('LeftUpperLeg', 0.074, 0.05, inset=0.03),
            Limb('LeftLowerLeg', 0.047, 0.03, inset=0.025),
            Limb('LeftFoot', 0.04, 0.028, depth=0.7, inset=-0.015),
            Limb('LeftToes', 0.026, 0.02, depth=0.7, inset=-0.01),
        ]
    ),
]

BALLS: list[Ball] = [
    Ball('Hips', (0.0, 0.0, 0.97), (0.15, 0.1, 0.1), SHELL),
    Ball('Head', (0.0, -0.01, 1.635), (0.082, 0.098, 0.108), SHELL),
    Ball('Head', (0.0, -0.075, 1.65), (0.05, 0.03, 0.012), GLOW),
    Ball('UpperChest', (0.0, -0.075, 1.36), (0.018, 0.012, 0.018), GLOW),
    *_balls_both_sides(
        [
            Ball('LeftUpperArm', (0.16, 0.0, skeleton.SHOULDER_HEIGHT), (0.05, 0.05, 0.05)),
            Ball('LeftLowerArm', (0.43, 0.0, skeleton.SHOULDER_HEIGHT), (0.034, 0.034, 0.034)),
            Ball('LeftLowerLeg', (0.09, -0.015, 0.52), (0.048, 0.048, 0.048)),
            Ball('LeftFoot', (0.09, 0.0, skeleton.ANKLE_HEIGHT), (0.034, 0.034, 0.034)),
            Ball('LeftUpperLeg', (0.09, 0.0, skeleton.HIP_HEIGHT - 0.02), (0.065, 0.065, 0.065), SHELL),
        ]
    ),
]


def material(
    name: str, color: tuple[float, float, float], roughness: float, metallic: float, glow: float = 0.0
) -> bpy.types.Material:
    """A plain surface: its color, roughness and metalness, and how bright it glows in its own color (0 not at all)."""
    made = bpy.data.materials.new(name)
    made.use_nodes = True
    tree = made.node_tree
    assert tree is not None
    shader = tree.nodes['Principled BSDF']
    shader.inputs['Base Color'].default_value = (*color, 1.0)  # type: ignore[attr-defined]
    shader.inputs['Roughness'].default_value = roughness  # type: ignore[attr-defined]
    shader.inputs['Metallic'].default_value = metallic  # type: ignore[attr-defined]
    if glow > 0.0:
        shader.inputs['Emission Color'].default_value = (*color, 1.0)  # type: ignore[attr-defined]
        shader.inputs['Emission Strength'].default_value = glow  # type: ignore[attr-defined]
    return made


def build(rig: bpy.types.Object) -> bpy.types.Object:
    """The mannequin's mesh, bound to `rig` by an armature modifier, one vertex group per bone."""
    mesh = bpy.data.meshes.new('Pilgrim')
    body = bpy.data.objects.new('Pilgrim', mesh)
    scene = bpy.context.scene
    assert scene is not None
    scene.collection.objects.link(body)
    body.parent = rig
    groups = {bone.name: body.vertex_groups.new(name=bone.name).index for bone in skeleton.BONES}

    bm = bmesh.new()
    deform = bm.verts.layers.deform.verify()

    def bind(geometry: list[bmesh.types.BMVert], bone: str, slot: int) -> None:
        for vert in geometry:
            vert[deform][groups[bone]] = 1.0
            for face in vert.link_faces:
                face.material_index = slot

    for limb in LIMBS:
        bone = skeleton.BY_NAME[limb.bone]
        head, tail = Vector(bone.head), Vector(bone.tail)
        axis = (tail - head).normalized()
        head, tail = head + axis * limb.inset, tail - axis * limb.inset
        length = (tail - head).length
        turn = Vector((0.0, 0.0, 1.0)).rotation_difference(axis).to_matrix().to_4x4()
        place = Matrix.Translation(((head + tail) / 2).to_tuple()) @ turn @ Matrix.Diagonal((1.0, limb.depth, 1.0, 1.0))
        made = bmesh.ops.create_cone(
            bm, cap_ends=True, segments=14, radius1=limb.head, radius2=limb.tail, depth=length, matrix=place
        )
        bind(made['verts'], limb.bone, limb.slot)

    for ball in BALLS:
        place = Matrix.Translation(ball.at) @ Matrix.Diagonal((*ball.size, 1.0))
        made = bmesh.ops.create_uvsphere(bm, u_segments=16, v_segments=10, radius=1.0, matrix=place)
        bind(made['verts'], ball.bone, ball.slot)

    for face in bm.faces:
        face.smooth = True
    bm.to_mesh(mesh)
    bm.free()

    for surface in (
        material('Shell', (0.018, 0.018, 0.02), 0.32, 0.4),
        material('Trim', (0.75, 0.75, 0.78), 0.25, 1.0),
        material('Glow', (0.62, 0.35, 1.0), 0.5, 0.0, glow=4.0),
    ):
        mesh.materials.append(surface)

    modifier = body.modifiers.new('Skeleton', 'ARMATURE')
    assert isinstance(modifier, bpy.types.ArmatureModifier)
    modifier.object = rig
    return body
