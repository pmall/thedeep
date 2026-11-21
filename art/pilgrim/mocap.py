"""Motion capture onto the pilgrim: a BVH clip read in Blender, retargeted onto the skeleton (skeleton.py) and baked
into one action per clip of the table (clips.py).

Retargeting copies each bone's turn away from the T-pose, in world space: the source's T-pose (the first frame of every
CMU file of art/mocap/cmu/) and the skeleton's rest pose both stand straight with the arms out, so a bone turned by some
rotation from its T-pose in the capture is turned by the same rotation from its rest in the pilgrim, whatever the two
skeletons' bone axes. The hips' sway is scaled by the ratio of the two legs' lengths, and set on the ground where the clip's feet plant. The capture is first turned
about the vertical so that the actor's travel (or, standing, its front) faces the pilgrim's front.

The game moves the body, so the body never carries the actor's travel: the hips' ground track (`_ground_track`) goes
onto the Root bone (the game measures a clip's speed from it and never shows it), leaving the hips only the
sway around it. A looping clip is cut where the pose comes back to its start and the
small difference left is spread over the cycle, so it loops without a pop.
"""

import math
import os
from dataclasses import dataclass, replace

import bpy
import skeleton
from bpy_extras import anim_utils
from clips import Clip
from mathutils import Quaternion, Vector

FPS = 60
"""Keys per second of a baked clip."""

SOURCE_OF: dict[str, str] = {
    'Hips': 'Hips',
    'Spine': 'LowerBack',
    'Chest': 'Spine',
    'UpperChest': 'Spine1',
    'Neck': 'Neck1',
    'Head': 'Head',
    **{
        side + ours: side + theirs
        for side in ('Left', 'Right')
        for ours, theirs in (
            ('Shoulder', 'Shoulder'),
            ('UpperArm', 'Arm'),
            ('LowerArm', 'ForeArm'),
            ('Hand', 'Hand'),
            ('UpperLeg', 'UpLeg'),
            ('LowerLeg', 'Leg'),
            ('Foot', 'Foot'),
            ('Toes', 'ToeBase'),
        )
    },
}
"""For each of the pilgrim's bones that follows the capture, the CMU bone it follows (the others keep their rest)."""


@dataclass(frozen=True)
class Pose:
    """The capture at one instant (capture units): each followed bone's world rotation, the point between the two hip
    joints, the line across from the left hip joint to the right, and the height of the lower ankle."""

    turns: dict[str, Quaternion]
    hips: Vector
    across: Vector
    ankle: float

    def front(self) -> Vector:
        """Where the actor faces, on the ground."""
        return Vector((-self.across.y, self.across.x, 0.0)).normalized()


class Source:
    """One BVH file imported as an armature; its first frame is the actor's T-pose."""

    def __init__(self, path: str) -> None:
        self.fps = _frame_rate(path)
        before = set(bpy.data.objects)
        bpy.ops.import_anim.bvh(filepath=path, global_scale=1.0, update_scene_fps=False, update_scene_duration=False)
        imported = [o for o in bpy.data.objects if o not in before]
        assert len(imported) == 1 and imported[0].type == 'ARMATURE', path
        self.rig = imported[0]
        self.tpose = self.at_frame(1.0)
        pose = self.rig.pose
        assert pose is not None
        bones = pose.bones
        self.leg_length = float((bones['LeftUpLeg'].head - bones['LeftFoot'].head).length)
        """Hip joint to ankle in the T-pose, capture units."""
        self.ankle_height = float(bones['LeftFoot'].head.z - bones['LeftToeBase'].head.z)
        """How high the ankle stands over the ground on a flat foot: its height over the toe in the T-pose."""
        action = self.rig.animation_data.action if self.rig.animation_data else None
        last = action.frame_range[1] if action else 2.0
        self.duration = (float(last) - 2.0) / self.fps
        """Seconds of motion after the T-pose frame."""

    def at(self, seconds: float) -> Pose:
        """The capture `seconds` after its T-pose frame."""
        return self.at_frame(2.0 + seconds * self.fps)

    def at_frame(self, frame: float) -> Pose:
        scene = bpy.context.scene
        assert scene is not None
        whole = math.floor(frame)
        scene.frame_set(whole, subframe=frame - whole)
        pose = self.rig.pose
        assert pose is not None
        world = self.rig.matrix_world
        turns = {ours: (world @ pose.bones[theirs].matrix).to_quaternion() for ours, theirs in SOURCE_OF.items()}
        left, right = (world @ pose.bones[f'{side}UpLeg'].head for side in ('Left', 'Right'))
        ankle = min((world @ pose.bones[f'{side}Foot'].head).z for side in ('Left', 'Right'))
        return Pose(turns, (left + right) / 2, right - left, ankle)

    def remove(self) -> None:
        """Takes the imported armature out of the file, its motion too, so the export holds only the pilgrim's clips."""
        data = self.rig.data
        motion = self.rig.animation_data.action if self.rig.animation_data else None
        bpy.data.objects.remove(self.rig)
        if motion is not None:
            bpy.data.actions.remove(motion)
        if isinstance(data, bpy.types.Armature):
            bpy.data.armatures.remove(data)


def _frame_rate(path: str) -> float:
    with open(path, encoding='ascii') as bvh:
        for line in bvh:
            if line.strip().startswith('Frame Time:'):
                return 1.0 / float(line.split(':')[1])
    raise ValueError(f'{path}: no Frame Time')


def _yaw_to_front(direction: Vector) -> Quaternion:
    """The turn about Z that brings `direction` (on the ground) onto the pilgrim's front, -Y."""
    return Quaternion((0.0, 0.0, 1.0), -math.atan2(direction.x, -direction.y))


def _heading(source: Source, start: float, end: float) -> Quaternion:
    """The turn about Z that brings the actor's travel over [start, end] (or, standing, its front at the start) onto -Y."""
    travel = source.at(end).hips - source.at(start).hips
    travel.z = 0.0
    if travel.length < 0.2 * source.leg_length:
        return _yaw_to_front(source.at(start).front())
    return _yaw_to_front(travel)


def _distance(a: Pose, b: Pose, units: float) -> float:
    """How far two poses are apart: the bones' angles, and the hips' height in leg lengths."""
    angles = sum(
        a.turns[n].rotation_difference(b.turns[n]).angle
        for n in (
            'Hips',
            'Spine',
            'LeftUpperLeg',
            'RightUpperLeg',
            'LeftLowerLeg',
            'RightLowerLeg',
            'LeftUpperArm',
            'RightUpperArm',
            'LeftLowerArm',
            'RightLowerArm',
        )
    )
    return float(angles + 4.0 * abs(a.hips.z - b.hips.z) / units)


def _loop_span(source: Source, clip: Clip) -> tuple[float, float]:
    """The clip's span: for a loop, the start near `clip.start` and the cycle in `clip.cycle` that meet best."""
    if clip.cycle is None:
        return clip.start, clip.end
    units = source.leg_length
    step = 1.0 / source.fps
    shortest, longest = clip.cycle
    best = (math.inf, clip.start, clip.start + shortest)
    for s in range(-6, 7):
        start = clip.start + 2 * s * step
        if start < 0.0:
            continue
        first, after = source.at(start), source.at(start + 4 * step)
        end = start + shortest
        while end <= min(start + longest, clip.end):
            cost = _distance(first, source.at(end), units) + _distance(after, source.at(end + 4 * step), units)
            if cost < best[0]:
                best = (cost, start, end)
            end += step
    print(f'  {clip.name}: loop {best[1]:.3f}..{best[2]:.3f} s, mismatch {best[0]:.3f}')
    return best[1], best[2]


def bake(rig: bpy.types.Object, clip: Clip, mocap_root: str) -> bpy.types.Action:
    """The clip retargeted onto `rig` as an action named after it, with a slot for `rig`."""
    source = Source(os.path.join(mocap_root, clip.source))
    clip = replace(clip, end=min(clip.end, source.duration))
    start, end = _loop_span(source, clip)
    # The actor is turned whole about the vertical (heading); its T-pose, the reference every bone's turn is taken
    # from, is turned to face the pilgrim's rest (upright).
    heading = _heading(source, start, end)
    upright = _yaw_to_front(source.tpose.front())
    scale = (skeleton.HIP_HEIGHT - skeleton.ANKLE_HEIGHT) / source.leg_length
    frames = max(2, round((end - start) * FPS))
    poses = [source.at(start + (end - start) * k / frames) for k in range(frames + 1)]

    rest = rest_turns(rig)
    tpose = source.tpose
    tracks = _ground_track([p.hips for p in poses], looping=clip.cycle is not None)
    # The ground is where the feet plant: the lowest ankle of the clip, a flat foot's ankle height above it (the T-pose
    # frame is not standing on it).
    ankle = source.ankle_height
    ground = min(p.ankle for p in poses) - ankle
    # In the air, the hips hold the height that puts the clip's lowest sole on the root.
    reach = max(p.hips.z - p.ankle for p in poses)

    turns: dict[str, list[Quaternion]] = {b.name: [] for b in skeleton.BONES if b.name in SOURCE_OF}
    hips: list[Vector] = []
    travel: list[Vector] = []
    for k, pose in enumerate(poses):
        deltas = {
            name: heading @ pose.turns[name] @ tpose.turns[name].inverted() @ upright.inverted() for name in SOURCE_OF
        }
        for name, local in local_turns(rest, deltas).items():
            turns[name].append(local)
        # The ground track goes onto the root; what is left is the sway around it.
        track = tracks[k]
        ground_track = heading @ ((track - tracks[0]) * scale)
        travel.append(Vector((ground_track.x, ground_track.y, 0.0)))
        sway = heading @ ((pose.hips - track) * scale)
        if clip.airborne:
            height = skeleton.ANKLE_HEIGHT + reach * scale
        else:
            height = skeleton.ANKLE_HEIGHT + (pose.hips.z - ground - ankle) * scale
        hips.append(Vector((sway.x, sway.y, height - skeleton.HIP_HEIGHT)))

    if clip.cycle is not None:
        _close_loop(turns, hips)
    source.remove()
    locations = {
        'Hips': [rest['Hips'].inverted() @ h for h in hips],
        'Root': [rest['Root'].inverted() @ t for t in travel],
    }
    return write_action(rig, clip.name, turns, locations)


TRACK_SMOOTHING = 0.25
"""Seconds either side of an instant a one-shot clip's ground track averages the hips over."""


def _ground_track(hips: list[Vector], looping: bool) -> list[Vector]:
    """The ground track the hips sway around: for a loop, the straight line between its two ends (a cycle travels
    evenly); otherwise the hips' own way over the ground, smoothed, so a clip that slows or stops keeps its body over
    the root."""
    n = len(hips) - 1
    if looping:
        return [hips[0].lerp(hips[n], k / n) for k in range(n + 1)]
    reach = max(1, round(TRACK_SMOOTHING * FPS))
    track = []
    for k in range(n + 1):
        window = hips[max(0, k - reach) : min(n, k + reach) + 1]
        mean = sum(window, Vector()) / len(window)
        track.append(Vector((mean.x, mean.y, hips[k].z)))
    return track


def _close_loop(turns: dict[str, list[Quaternion]], hips: list[Vector]) -> None:
    """Spreads each channel's last-to-first difference over the cycle, so the last key equals the first."""
    n = len(hips) - 1
    for keys in turns.values():
        error = keys[0] @ keys[n].inverted()
        for k in range(n + 1):
            keys[k] = Quaternion().slerp(error, k / n) @ keys[k]
    error_v = hips[0] - hips[n]
    for k in range(n + 1):
        hips[k] = hips[k] + error_v * (k / n)


def rest_turns(rig: bpy.types.Object) -> dict[str, Quaternion]:
    """Each bone's rest orientation in the armature."""
    armature = rig.data
    assert isinstance(armature, bpy.types.Armature)
    return {b.name: b.matrix_local.to_quaternion() for b in armature.bones}


def local_turns(rest: dict[str, Quaternion], deltas: dict[str, Quaternion]) -> dict[str, Quaternion]:
    """Each bone's turn in its parent's frame (its pose channel) that turns it by `deltas[bone]` from its rest, in
    the world; a bone without a delta keeps its rest relative to its parent (and so follows it)."""
    world: dict[str, Quaternion] = {}
    local: dict[str, Quaternion] = {}
    for bone in skeleton.BONES:
        parent_world = world[bone.parent] if bone.parent else Quaternion()
        parent_rest = rest[bone.parent] if bone.parent else Quaternion()
        if bone.name in deltas:
            world[bone.name] = deltas[bone.name] @ rest[bone.name]
            local[bone.name] = rest[bone.name].inverted() @ parent_rest @ parent_world.inverted() @ world[bone.name]
        else:
            world[bone.name] = parent_world @ parent_rest.inverted() @ rest[bone.name]
    return local


def write_action(
    rig: bpy.types.Object, name: str, turns: dict[str, list[Quaternion]], locations: dict[str, list[Vector]]
) -> bpy.types.Action:
    """The action: each followed bone's local turn, and the bones that move (the hips, the root) offset from their rest
    in their own frame."""
    action = bpy.data.actions.new(name)
    action.use_fake_user = True
    slot = action.slots.new(id_type='OBJECT', name=rig.name)
    bag = anim_utils.action_ensure_channelbag_for_slot(action, slot)
    for bone, keys in turns.items():
        path = f'pose.bones["{bone}"].rotation_quaternion'
        previous = Quaternion()
        values: list[list[float]] = [[], [], [], []]
        for q in keys:
            if q.dot(previous) < 0.0:
                q = Quaternion((-q.w, -q.x, -q.y, -q.z))
            previous = q
            for i in range(4):
                values[i].append(q[i])
        for i in range(4):
            _curve(bag.fcurves.new(path, index=i, group_name=bone), values[i])
    for bone, offsets in locations.items():
        path = f'pose.bones["{bone}"].location'
        for i in range(3):
            _curve(bag.fcurves.new(path, index=i, group_name=bone), [o[i] for o in offsets])
    return action


def _curve(curve: bpy.types.FCurve, values: list[float]) -> None:
    curve.keyframe_points.add(len(values))
    coords: list[float] = []
    for k, v in enumerate(values):
        coords += (float(k), v)
    curve.keyframe_points.foreach_set('co', coords)
    for point in curve.keyframe_points:
        point.interpolation = 'LINEAR'
    curve.update()
