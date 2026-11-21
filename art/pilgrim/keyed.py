"""Clips keyed by hand, for what motion capture doesn't cover: key poses at times, eased into each other, baked like
the capture (mocap.py) into an action.

A pose turns bones (`Turn`) from a neutral stance (`NEUTRAL`: the rest, but the arms hanging at the sides), each in its
parent's frame, so a bone carries its children: pitch tips the bone's top forward (the trunk leans in, a hanging limb
swings back, so an arm swings forward with a negative pitch and an elbow bends with one too), yaw turns it to the body's
left, roll tilts its top to the body's left. They apply roll, then pitch, then yaw. An arm pitched swings in the side
plane, as the game's camera sees it, wherever the chest is turned. A leg may instead be given where its ankle goes (`Pose.feet`, the side plane): the hip and the knee bend to put
it there and the foot stays flat, so a crouch or a lunge keeps its feet on the ground.
"""

import math
from dataclasses import dataclass, field, replace
from typing import Literal, Self

import bpy
import mocap
import skeleton
from mathutils import Quaternion, Vector

type Turn = tuple[float, float, float]
"""Pitch, yaw, roll, in degrees."""

type Ease = Literal['both', 'in', 'out', 'linear']

HANG = 84.0
"""How far the neutral stance brings the arms down from the T-pose, degrees."""


@dataclass(frozen=True)
class Pose:
    """Each bone's turn (a bone left out only follows its parent), the hips moved forward and up from their rest
    (meters), and for a leg ('Left', 'Right') given here, where its ankle goes: forward and up from the root (meters)."""

    turns: dict[str, Turn] = field(default_factory=dict)
    hips: tuple[float, float] = (0.0, 0.0)
    feet: dict[str, tuple[float, float]] = field(default_factory=dict)

    def but(
        self,
        turns: dict[str, Turn] | None = None,
        hips: tuple[float, float] | None = None,
        feet: dict[str, tuple[float, float]] | None = None,
    ) -> Self:
        """This pose with some bones turned otherwise, the hips moved, or the feet placed."""
        return replace(
            self,
            turns={**self.turns, **(turns or {})},
            hips=self.hips if hips is None else hips,
            feet={**self.feet, **(feet or {})},
        )


@dataclass(frozen=True)
class Key:
    """The pose reached at `at` seconds, eased into from the key before."""

    at: float
    pose: Pose
    ease: Ease = 'both'


@dataclass(frozen=True)
class Keyed:
    """A clip keyed by hand: its keys in time order, the first at 0; it holds its last pose at its end, or a loop (named
    "_loop") comes back to its first. A clip whose feet walk travels `travel` meters per second forward on its root, so
    the game can match its rate to the body's speed as it does the run's."""

    name: str
    keys: list[Key]
    travel: float = 0.0


ANKLE = skeleton.ANKLE_HEIGHT
"""An ankle's height over the ground, for a foot planted on it."""


def forward(degrees: float) -> Turn:
    """An upper arm (or a thigh) swung `degrees` forward from hanging (180: straight up; more: behind the head)."""
    return (-degrees, 0.0, 0.0)


def bend(degrees: float) -> Turn:
    """A forearm bent `degrees` at the elbow, the way it folds: toward the front of the upper arm."""
    return (-degrees, 0.0, 0.0)


def trunk(lean: float, twist: float = 0.0) -> dict[str, Turn]:
    """The spine and the chest leaning `lean` degrees forward between them, the chest turned `twist` degrees toward the
    near (right) shoulder going forward."""
    return {'Spine': (lean * 0.5, -twist * 0.4, 0.0), 'Chest': (lean * 0.5, -twist * 0.6, 0.0)}


def turn(t: Turn) -> Quaternion:
    """The rotation of a turn, roll first, then pitch, then yaw."""
    pitch, yaw, roll = (math.radians(a) for a in t)
    return Quaternion((0.0, 0.0, 1.0), yaw) @ Quaternion((1.0, 0.0, 0.0), pitch) @ Quaternion((0.0, 1.0, 0.0), roll)


def _neutral() -> dict[str, Quaternion]:
    """Each bone's turn from its rest in the neutral stance: the arms hanging, everything else at rest."""
    down = {'Left': HANG, 'Right': -HANG}
    return {
        bone.name: next(
            (
                turn((0.0, 0.0, roll))
                for side, roll in down.items()
                if bone.name.startswith(side) and 'Arm' in bone.name
            ),
            next(
                (turn((0.0, 0.0, roll)) for side, roll in down.items() if bone.name == f'{side}Hand'),
                Quaternion(),
            ),
        )
        for bone in skeleton.BONES
    }


NEUTRAL = _neutral()


def _eased(t: float, ease: Ease) -> float:
    match ease:
        case 'both':
            return t * t * (3.0 - 2.0 * t)
        case 'in':
            return t * t
        case 'out':
            return 1.0 - (1.0 - t) * (1.0 - t)
        case 'linear':
            return t


def _leg(hips: Vector, ankle: tuple[float, float], side: str) -> tuple[float, float]:
    """The thigh's and the shin's angles from hanging straight down, forward positive (radians), that put the ankle of
    the leg on `side` at `ankle` (forward, up) with the hips' head at `hips`."""
    upper = skeleton.BY_NAME[f'{side}UpperLeg']
    lower = skeleton.BY_NAME[f'{side}LowerLeg']
    thigh = (Vector(upper.tail) - Vector(upper.head)).length
    shin = (Vector(lower.tail) - Vector(lower.head)).length
    forward, up = ankle[0] - (-hips.y), ankle[1] - hips.z
    reach = min(math.hypot(forward, up), thigh + shin - 1e-4)
    toward = math.atan2(forward, -up)
    at_hip = math.acos(max(-1.0, min(1.0, (thigh * thigh + reach * reach - shin * shin) / (2.0 * thigh * reach))))
    at_knee = math.acos(max(-1.0, min(1.0, (thigh * thigh + shin * shin - reach * reach) / (2.0 * thigh * shin))))
    upper_angle = toward + at_hip
    return upper_angle, upper_angle - (math.pi - at_knee)


def _deltas(pose: Pose) -> tuple[dict[str, Quaternion], Vector]:
    """Each bone's turn from its rest in the world, and the hips' offset (Blender's axes: forward is -Y)."""
    hips = Vector((0.0, -pose.hips[0], pose.hips[1]))
    world: dict[str, Quaternion] = {}
    for bone in skeleton.BONES:
        parent = world[bone.parent] @ NEUTRAL[bone.parent].inverted() if bone.parent else Quaternion()
        world[bone.name] = parent @ turn(pose.turns.get(bone.name, (0.0, 0.0, 0.0))) @ NEUTRAL[bone.name]
    hips_head = Vector(skeleton.BY_NAME['Hips'].head) + hips
    for side, ankle in pose.feet.items():
        thigh, shin = _leg(hips_head, ankle, side)
        world[f'{side}UpperLeg'] = turn((-math.degrees(thigh), 0.0, 0.0))
        world[f'{side}LowerLeg'] = turn((-math.degrees(shin), 0.0, 0.0))
        world[f'{side}Foot'] = Quaternion()
        world[f'{side}Toes'] = Quaternion()
    return world, hips


def _between(a: Pose, b: Pose, t: float) -> Pose:
    """The pose `t` of the way from `a` to `b`, its angles and places blended as written (an arm keyed from 200 to 45
    degrees forward passes over the head and before the body, never the short way behind it)."""

    def mix(x: tuple[float, ...], y: tuple[float, ...]) -> tuple[float, ...]:
        return tuple(p + (q - p) * t for p, q in zip(x, y, strict=True))

    zero = (0.0, 0.0, 0.0)
    names = a.turns.keys() | b.turns.keys()
    turns = {n: mix(a.turns.get(n, zero), b.turns.get(n, zero)) for n in names}
    feet = {side: mix(a.feet[side], b.feet[side]) for side in a.feet.keys() & b.feet.keys()}
    feet.update({side: ankle for side, ankle in (b.feet if t >= 0.5 else a.feet).items() if side not in feet})
    hips = mix(a.hips, b.hips)
    return Pose(
        {n: (v[0], v[1], v[2]) for n, v in turns.items()},
        (hips[0], hips[1]),
        {k: (v[0], v[1]) for k, v in feet.items()},
    )


def bake(rig: bpy.types.Object, clip: Keyed) -> bpy.types.Action:
    """The clip as an action named after it, with a slot for `rig`."""
    rest = mocap.rest_turns(rig)
    keys = clip.keys
    frames = max(1, round(keys[-1].at * mocap.FPS))
    turns: dict[str, list[Quaternion]] = {b.name: [] for b in skeleton.BONES}
    hips: list[Vector] = []
    for k in range(frames + 1):
        seconds = k / mocap.FPS
        after = next((i for i, key in enumerate(keys) if key.at >= seconds - 1e-6), len(keys) - 1)
        before = max(0, after - 1)
        a, b = keys[before], keys[after]
        t = 1.0 if b.at <= a.at else _eased((seconds - a.at) / (b.at - a.at), b.ease)
        deltas, offset = _deltas(_between(a.pose, b.pose, t))
        for name, local in mocap.local_turns(rest, deltas).items():
            turns[name].append(local)
        hips.append(rest['Hips'].inverted() @ offset)
    root = [rest['Root'].inverted() @ Vector((0.0, -clip.travel * k / mocap.FPS, 0.0)) for k in range(frames + 1)]
    return mocap.write_action(rig, clip.name, turns, {'Hips': hips, 'Root': root})
