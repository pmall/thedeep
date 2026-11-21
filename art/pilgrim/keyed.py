"""Clips keyed by hand, for what motion capture doesn't give: key poses at times, splined into each other, baked like
the capture (mocap.py) into an action.

A pose turns bones (`Turn`) from a neutral stance (`NEUTRAL`: the rest, but the arms hanging at the sides), each in its
parent's frame, so a bone carries its children: pitch tips the bone's top forward (the trunk leans in, a hanging limb
swings back, so an arm swings forward with a negative pitch and an elbow bends with one too), yaw turns it to the body's
left, roll tilts its top to the body's left. They apply roll, then pitch, then yaw. An arm pitched swings in the side
plane, as the game's camera sees it, wherever the chest is turned; a bone's yaw turns about its parent's length, so the
spine's yaw rolls a swimmer lying on the water. A leg may instead be given where its ankle goes (`Pose.feet`, the side
plane) and how its sole tips (`Pose.soles`): the hip and the knee bend to put it there, so a crouch or a lunge keeps its
feet on the ground. The whole body may flip (a somersault) and roll (about its length), about its middle (`Pose.body`).

Between keys every number moves on a spline that is clamped at a key where it turns back (`Key.ease`: 'smooth', the
default; 'stop' arrives still, 'linear' at an even pace), so a move flows through its keys instead of stopping at each.
Each bone then drags behind its own keys by its share of `LAG` (the head after the chest, the forearm after the arm, the
hand last), scaled by the clip's `drag`: the overlap that makes a body move loose instead of in one block.
"""

import math
from collections.abc import Callable
from dataclasses import dataclass, field, replace
from typing import Literal, Self

import bpy
import mocap
import skeleton
from mathutils import Quaternion, Vector

type Turn = tuple[float, float, float]
"""Pitch, yaw, roll, in degrees."""

type Place = tuple[float, float]
"""Forward and up, in meters."""

type Ease = Literal['smooth', 'stop', 'linear']

HANG = 84.0
"""How far the neutral stance brings the arms down from the T-pose, degrees."""

PIVOT = 0.14
"""How far above the hips' head the body's middle is, which a flip or a roll turns about, meters."""

LAG: dict[str, float] = {
    'Chest': 0.01,
    'UpperChest': 0.018,
    'Neck': 0.03,
    'Head': 0.05,
    **{
        f'{side}{bone}': lag
        for side in ('Left', 'Right')
        for bone, lag in (('Shoulder', 0.015), ('UpperArm', 0.012), ('LowerArm', 0.035), ('Hand', 0.06), ('Toes', 0.03))
    },
}
"""How long each bone trails its own keys at a drag of 1, seconds (the others follow theirs at once)."""


@dataclass(frozen=True)
class Pose:
    """Each bone's turn (a bone left out only follows its parent), the hips moved forward and up from their rest, for
    a leg ('Left', 'Right') given here where its ankle goes (forward and up from the root) and its sole's tip (degrees,
    toes down), and the whole body's flip (forward) and roll (degrees) about its middle."""

    turns: dict[str, Turn] = field(default_factory=dict)
    hips: Place = (0.0, 0.0)
    feet: dict[str, Place] = field(default_factory=dict)
    soles: dict[str, float] = field(default_factory=dict)
    body: tuple[float, float] = (0.0, 0.0)

    def but(
        self,
        turns: dict[str, Turn] | None = None,
        hips: Place | None = None,
        feet: dict[str, Place] | None = None,
        soles: dict[str, float] | None = None,
        body: tuple[float, float] | None = None,
    ) -> Self:
        """This pose with some bones turned otherwise, the hips moved, the feet placed or the body turned."""
        return replace(
            self,
            turns={**self.turns, **(turns or {})},
            hips=self.hips if hips is None else hips,
            feet={**self.feet, **(feet or {})},
            soles={**self.soles, **(soles or {})},
            body=self.body if body is None else body,
        )


@dataclass(frozen=True)
class Key:
    """The pose reached at `at` seconds, and how the move arrives there."""

    at: float
    pose: Pose
    ease: Ease = 'smooth'


@dataclass(frozen=True)
class Keyed:
    """A clip keyed by hand: its keys in time order, the first at 0; it holds its last pose at its end, or a loop (named
    "_loop", its last key its first pose again) comes back to its first. A clip whose feet walk travels `travel` meters
    per second forward on its root, so the game can match its rate to the body's speed as it does the run's. `drag`
    scales how far the bones trail (`LAG`): more for a clip the game plays faster than it is keyed."""

    name: str
    keys: list[Key]
    travel: float = 0.0
    drag: float = 1.0

    @property
    def looping(self) -> bool:
        return self.name.endswith('_loop')


ANKLE = skeleton.ANKLE_HEIGHT
"""An ankle's height over the ground, for a foot planted on it."""

SIDES = ('Left', 'Right')


def forward(degrees: float) -> Turn:
    """An upper arm (or a thigh) swung `degrees` forward from hanging (180: straight up; more: behind the head)."""
    return (-degrees, 0.0, 0.0)


def bend(degrees: float) -> Turn:
    """A forearm bent `degrees` at the elbow, the way it folds: toward the front of the upper arm."""
    return (-degrees, 0.0, 0.0)


def knee(degrees: float) -> Turn:
    """A shin bent `degrees` at the knee, the way it folds: behind the thigh."""
    return (degrees, 0.0, 0.0)


def trunk(lean: float, twist: float = 0.0) -> dict[str, Turn]:
    """The spine and the chest leaning `lean` degrees forward between them, the chest turned `twist` degrees toward the
    near (right) shoulder going forward."""
    return {'Spine': (lean * 0.5, -twist * 0.4, 0.0), 'Chest': (lean * 0.5, -twist * 0.6, 0.0)}


def cycle(
    name: str, length: float, travel: float, pose_at: Callable[[float], Pose], samples: int = 12, drag: float = 1.0
) -> Keyed:
    """A loop of `length` seconds sampled from `pose_at(phase)` (phase 0 to 1, its end the same pose as its start, an
    angle that winds all the way round wound on), the samples joined evenly."""
    keys = [Key(length * k / samples, pose_at(k / samples), 'linear') for k in range(samples + 1)]
    return Keyed(name, keys, travel, drag)


def turn(t: Turn) -> Quaternion:
    """The rotation of a turn, roll first, then pitch, then yaw."""
    pitch, yaw, roll = (math.radians(a) for a in t)
    return Quaternion((0.0, 0.0, 1.0), yaw) @ Quaternion((1.0, 0.0, 0.0), pitch) @ Quaternion((0.0, 1.0, 0.0), roll)


def _as_turn(q: Quaternion) -> Turn:
    """The turn of a rotation (the inverse of `turn`)."""
    e = q.to_euler('YXZ')
    return (math.degrees(e.x), math.degrees(e.z), math.degrees(e.y))


def _neutral() -> dict[str, Quaternion]:
    """Each bone's turn from its rest in the neutral stance: the arms hanging, everything else at rest."""
    down = {'Left': HANG, 'Right': -HANG}
    neutral: dict[str, Quaternion] = {}
    for bone in skeleton.BONES:
        side = next((s for s in SIDES if bone.name.startswith(s)), None)
        hanging = side is not None and ('Arm' in bone.name or bone.name == f'{side}Hand')
        neutral[bone.name] = turn((0.0, 0.0, down[side])) if side is not None and hanging else Quaternion()
    return neutral


NEUTRAL = _neutral()


def _leg(hips: Vector, ankle: Place, side: str) -> tuple[float, float]:
    """The thigh's and the shin's angles from hanging straight down, forward positive (radians), that put the ankle of
    the leg on `side` at `ankle` (forward, up) with the hips' head at `hips`."""
    upper = skeleton.BY_NAME[f'{side}UpperLeg']
    lower = skeleton.BY_NAME[f'{side}LowerLeg']
    thigh = (Vector(upper.tail) - Vector(upper.head)).length
    shin = (Vector(lower.tail) - Vector(lower.head)).length
    ahead, up = ankle[0] - (-hips.y), ankle[1] - hips.z
    reach = min(math.hypot(ahead, up), thigh + shin - 1e-4)
    toward = math.atan2(ahead, -up)
    at_hip = math.acos(max(-1.0, min(1.0, (thigh * thigh + reach * reach - shin * shin) / (2.0 * thigh * reach))))
    at_knee = math.acos(max(-1.0, min(1.0, (thigh * thigh + shin * shin - reach * reach) / (2.0 * thigh * shin))))
    upper_angle = toward + at_hip
    return upper_angle, upper_angle - (math.pi - at_knee)


def _hips_head(pose: Pose) -> Vector:
    return Vector(skeleton.BY_NAME['Hips'].head) + Vector((0.0, -pose.hips[0], pose.hips[1]))


def _placed_legs(pose: Pose) -> dict[str, Quaternion]:
    """For each leg the pose places, its thigh's, shin's and foot's turns in the world."""
    world: dict[str, Quaternion] = {}
    for side, ankle in pose.feet.items():
        thigh, shin = _leg(_hips_head(pose), ankle, side)
        world[f'{side}UpperLeg'] = turn((-math.degrees(thigh), 0.0, 0.0))
        world[f'{side}LowerLeg'] = turn((-math.degrees(shin), 0.0, 0.0))
        sole = pose.soles.get(side, 0.0)
        world[f'{side}Foot'] = turn((sole, 0.0, 0.0))
        # On the ground, a heel lifted leaves the toes flat on it; in the air they point with the foot.
        grounded = ankle[1] < ANKLE + 0.1
        world[f'{side}Toes'] = turn((min(sole, 0.0) if grounded else sole, 0.0, 0.0))
    return world


def _unplaced(pose: Pose, sides: set[str]) -> Pose:
    """The pose with the legs of `sides` given by their turns instead of where their ankles go (the same pose)."""
    if not sides & pose.feet.keys():
        return pose
    hips = turn(pose.turns.get('Hips', (0.0, 0.0, 0.0)))
    placed = _placed_legs(pose)
    turns = dict(pose.turns)
    for side in sides & pose.feet.keys():
        chain = [f'{side}UpperLeg', f'{side}LowerLeg', f'{side}Foot', f'{side}Toes']
        parent = hips
        for bone in chain:
            turns[bone] = _as_turn(parent.inverted() @ placed[bone])
            parent = placed[bone]
    keep = {s: a for s, a in pose.feet.items() if s not in sides}
    return replace(pose, turns=turns, feet=keep, soles={s: a for s, a in pose.soles.items() if s in keep})


def _deltas(pose: Pose) -> tuple[dict[str, Quaternion], Vector]:
    """Each bone's turn from its rest in the world, and the hips' offset (Blender's axes: forward is -Y)."""
    flip, roll = (math.radians(a) for a in pose.body)
    spin = Quaternion((0.0, 1.0, 0.0), roll) @ Quaternion((1.0, 0.0, 0.0), flip)
    head = _hips_head(pose)
    middle = head + Vector((0.0, 0.0, PIVOT))
    offset = Vector((0.0, -pose.hips[0], pose.hips[1])) + (middle + spin @ (head - middle)) - head
    world: dict[str, Quaternion] = {}
    for bone in skeleton.BONES:
        parent = world[bone.parent] @ NEUTRAL[bone.parent].inverted() if bone.parent else Quaternion()
        world[bone.name] = parent @ turn(pose.turns.get(bone.name, (0.0, 0.0, 0.0))) @ NEUTRAL[bone.name]
        if bone.name == 'Hips':
            world[bone.name] = spin @ world[bone.name]
    world.update(_placed_legs(pose))
    return world, offset


class _Channels:
    """A clip's keys as rows of numbers (every turn, the hips, the placed feet, the body), splined in time."""

    def __init__(self, clip: Keyed) -> None:
        self.loop = clip.looping
        self.length = clip.keys[-1].at
        self.placed = sorted(s for s in SIDES if all(s in k.pose.feet for k in clip.keys))
        poses = [_unplaced(k.pose, set(SIDES) - set(self.placed)) for k in clip.keys]
        self.bones = sorted({b for p in poses for b in p.turns})
        self.times = [k.at for k in clip.keys]
        self.eases = [k.ease for k in clip.keys]
        self.rows = [self._row(p) for p in poses]
        self.slopes = [self._slope(i) for i in range(len(self.rows))]

    def _row(self, pose: Pose) -> list[float]:
        row: list[float] = []
        for bone in self.bones:
            row += pose.turns.get(bone, (0.0, 0.0, 0.0))
        row += pose.hips
        for side in self.placed:
            row += (*pose.feet[side], pose.soles.get(side, 0.0))
        row += pose.body
        return row

    def pose(self, row: list[float]) -> Pose:
        turns = {b: (row[3 * i], row[3 * i + 1], row[3 * i + 2]) for i, b in enumerate(self.bones)}
        at = 3 * len(self.bones)
        hips = (row[at], row[at + 1])
        at += 2
        feet: dict[str, Place] = {}
        soles: dict[str, float] = {}
        for side in self.placed:
            feet[side] = (row[at], row[at + 1])
            soles[side] = row[at + 2]
            at += 3
        return Pose(turns, hips, feet, soles, (row[at], row[at + 1]))

    def _slope(self, i: int) -> list[float]:
        """The rows' rate of change at key `i`: none at a key that stops, at an end of a one-shot, or for a number
        that turns back there; else through its neighbors, clamped so the spline never overshoots them."""
        n = len(self.rows)
        width = len(self.rows[0])
        if self.eases[i] == 'stop' or n < 3 and not self.loop:
            return [0.0] * width
        if i in (0, n - 1):
            if not self.loop:
                return [0.0] * width
            before, after = n - 2, 1
            dt_before, dt_after = self.times[n - 1] - self.times[n - 2], self.times[1] - self.times[0]
        else:
            before, after = i - 1, i + 1
            dt_before, dt_after = self.times[i] - self.times[i - 1], self.times[i + 1] - self.times[i]
        here = self.rows[i]
        slope: list[float] = []
        for c in range(width):
            back = (here[c] - self.rows[before][c]) / max(dt_before, 1e-6)
            ahead = (self.rows[after][c] - here[c]) / max(dt_after, 1e-6)
            if back * ahead <= 0.0:
                slope.append(0.0)
                continue
            m = (back * dt_after + ahead * dt_before) / max(dt_before + dt_after, 1e-6)
            bound = 3.0 * min(abs(back), abs(ahead))
            slope.append(max(-bound, min(bound, m)))
        return slope

    def at(self, seconds: float) -> list[float]:
        if self.loop and self.length > 0.0:
            seconds %= self.length
        seconds = max(0.0, min(self.length, seconds))
        i = max(0, next((k for k, t in enumerate(self.times) if t > seconds), len(self.times)) - 1)
        if i >= len(self.times) - 1:
            return list(self.rows[-1])
        t0, t1 = self.times[i], self.times[i + 1]
        h = max(t1 - t0, 1e-6)
        s = (seconds - t0) / h
        a, b = self.rows[i], self.rows[i + 1]
        if self.eases[i + 1] == 'linear':
            return [p + (q - p) * s for p, q in zip(a, b, strict=True)]
        ma, mb = self.slopes[i], self.slopes[i + 1]
        h00, h10, h01, h11 = 2 * s**3 - 3 * s**2 + 1, s**3 - 2 * s**2 + s, -2 * s**3 + 3 * s**2, s**3 - s**2
        return [h00 * p + h10 * h * u + h01 * q + h11 * h * v for p, q, u, v in zip(a, b, ma, mb, strict=True)]


def _sampled(channels: _Channels, clip: Keyed, seconds: float) -> Pose:
    """The clip's pose at `seconds`, each bone trailing its keys by its lag."""
    now = channels.pose(channels.at(seconds))
    turns = dict(now.turns)
    late: dict[float, Pose] = {}
    for bone in channels.bones:
        lag = LAG.get(bone, 0.0) * clip.drag
        if lag <= 0.0:
            continue
        if lag not in late:
            late[lag] = channels.pose(channels.at(seconds - lag))
        turns[bone] = late[lag].turns[bone]
    return replace(now, turns=turns)


def bake(rig: bpy.types.Object, clip: Keyed) -> bpy.types.Action:
    """The clip as an action named after it, with a slot for `rig`."""
    rest = mocap.rest_turns(rig)
    channels = _Channels(clip)
    frames = max(1, round(channels.length * mocap.FPS))
    turns: dict[str, list[Quaternion]] = {b.name: [] for b in skeleton.BONES}
    hips: list[Vector] = []
    for k in range(frames + 1):
        deltas, offset = _deltas(_sampled(channels, clip, k / mocap.FPS))
        for name, local in mocap.local_turns(rest, deltas).items():
            turns[name].append(local)
        hips.append(rest['Hips'].inverted() @ offset)
    root = [rest['Root'].inverted() @ Vector((0.0, -clip.travel * k / mocap.FPS, 0.0)) for k in range(frames + 1)]
    return mocap.write_action(rig, clip.name, turns, {'Hips': hips, 'Root': root})
