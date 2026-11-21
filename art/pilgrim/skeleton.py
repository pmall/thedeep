"""The pilgrim's skeleton: its bones, named as Godot's humanoid profile names them, and the armature built from them.

The rest pose is a T-pose facing -Y (Blender's front), the left side at +X, Z up, in meters, feet on the ground at the
origin. Motion capture is retargeted onto it from its own T-pose (mocap.py), and the meshes are bound to it rigidly
(body.py). Its proportions are a slender human android a little taller than the body the game collides with
(resources/player_tuning.tres: BodyHeight).
"""

import math
from dataclasses import dataclass

import bpy
from mathutils import Vector

type Vec3 = tuple[float, float, float]


@dataclass(frozen=True)
class Bone:
    """One bone: its parent (none for the root), where it starts and where it points, in meters."""

    name: str
    parent: str | None
    head: Vec3
    tail: Vec3


HIP_HEIGHT = 0.96
"""Height of the hip joints and of the Hips bone's head at rest, in meters."""

ANKLE_HEIGHT = 0.085
"""Height of the ankles at rest, in meters."""

SHOULDER_HEIGHT = 1.41
"""Height of the arms' line in the T-pose, in meters."""


def _side(name: str, parent: str, head: Vec3, tail: Vec3) -> list[Bone]:
    """The bone on the left (+X) and its mirror on the right."""
    mirror = (-head[0], head[1], head[2]), (-tail[0], tail[1], tail[2])
    right_parent = parent.replace('Left', 'Right')
    return [Bone('Left' + name, parent, head, tail), Bone('Right' + name, right_parent, *mirror)]


BONES: list[Bone] = [
    Bone('Root', None, (0.0, 0.0, 0.0), (0.0, -0.15, 0.0)),
    Bone('Hips', 'Root', (0.0, 0.0, HIP_HEIGHT), (0.0, 0.0, 1.06)),
    Bone('Spine', 'Hips', (0.0, 0.0, 1.06), (0.0, 0.0, 1.18)),
    Bone('Chest', 'Spine', (0.0, 0.0, 1.18), (0.0, 0.0, 1.30)),
    Bone('UpperChest', 'Chest', (0.0, 0.0, 1.30), (0.0, 0.0, 1.42)),
    Bone('Neck', 'UpperChest', (0.0, 0.0, 1.42), (0.0, 0.0, 1.53)),
    Bone('Head', 'Neck', (0.0, 0.0, 1.53), (0.0, 0.0, 1.74)),
    *_side('Shoulder', 'UpperChest', (0.03, 0.0, 1.39), (0.16, 0.0, SHOULDER_HEIGHT)),
    *_side('UpperArm', 'LeftShoulder', (0.16, 0.0, SHOULDER_HEIGHT), (0.43, 0.0, SHOULDER_HEIGHT)),
    *_side('LowerArm', 'LeftUpperArm', (0.43, 0.0, SHOULDER_HEIGHT), (0.68, 0.0, SHOULDER_HEIGHT)),
    *_side('Hand', 'LeftLowerArm', (0.68, 0.0, SHOULDER_HEIGHT), (0.77, 0.0, SHOULDER_HEIGHT)),
    # The knee sits a little forward so a leg solved by IK always bends the right way.
    *_side('UpperLeg', 'Hips', (0.09, 0.0, HIP_HEIGHT), (0.09, -0.015, 0.52)),
    *_side('LowerLeg', 'LeftUpperLeg', (0.09, -0.015, 0.52), (0.09, 0.0, ANKLE_HEIGHT)),
    *_side('Foot', 'LeftLowerLeg', (0.09, 0.0, ANKLE_HEIGHT), (0.09, -0.13, 0.025)),
    *_side('Toes', 'LeftFoot', (0.09, -0.13, 0.025), (0.09, -0.20, 0.025)),
]


def _lock(name: str, around: float) -> list[Bone]:
    """A lock of hair hanging from the crown to the nape, `around` degrees from straight behind toward the left: two bones
    the game swings on springs (its SpringBoneSimulator3D), so the hair trails the head's moves."""
    a = math.radians(around)
    x, y = math.sin(a), math.cos(a)
    crown = (0.085 * x, 0.085 * y - 0.005, 1.67)
    middle = (0.098 * x, 0.098 * y, 1.6)
    tip = (0.1 * x, 0.104 * y, 1.52)
    return [Bone(f'Hair{name}1', 'Head', crown, middle), Bone(f'Hair{name}2', f'Hair{name}1', middle, tip)]


LOCKS: dict[str, float] = {'Back': 0.0, 'Left': 52.0, 'Right': -52.0}
"""The locks of the hair, each its angle around the head from straight behind."""

BONES += [bone for name, around in LOCKS.items() for bone in _lock(name, around)]

BY_NAME: dict[str, Bone] = {bone.name: bone for bone in BONES}


def build(name: str) -> bpy.types.Object:
    """The armature object at the origin, in its rest pose, linked to the scene and active."""
    data = bpy.data.armatures.new(name)
    rig = bpy.data.objects.new(name, data)
    scene = bpy.context.scene
    assert scene is not None
    scene.collection.objects.link(rig)
    view_layer = bpy.context.view_layer
    assert view_layer is not None
    view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    for bone in BONES:
        edit = data.edit_bones.new(bone.name)
        edit.head = Vector(bone.head)
        edit.tail = Vector(bone.tail)
        edit.roll = 0.0
        if bone.parent is not None:
            edit.parent = data.edit_bones[bone.parent]
            edit.use_connect = False
    bpy.ops.object.mode_set(mode='OBJECT')
    return rig
