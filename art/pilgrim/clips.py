"""The pilgrim's animation clips: what each is cut from, as data the build (build.py) bakes through mocap.py.

The sources are the CMU Graphics Lab Motion Capture Database (mocap.cs.cmu.edu), in the MotionBuilder-friendly BVH
conversion by B. Hahne (2010, every file opens on the actor's T-pose), kept in art/mocap/cmu/ under their CMU names.
CMU's terms: free for any use, commercial products included; the data itself may not be resold. Credit "the CMU Graphics
Lab Motion Capture Database, funded by NSF EIA-0196217" in the game's credits.

A clip's name is the animation's name in the game; one that loops ends in "_loop", which Godot's importer reads as a
looping animation.
"""

from dataclasses import dataclass


@dataclass(frozen=True)
class Clip:
    """One animation: its source file (under art/mocap/), the span used in seconds after the T-pose frame, and, for a loop,
    the range of cycle lengths (seconds) searched from `start` for the cut where the pose meets itself again. An airborne
    clip's hips hold one height over its root (the game makes the rise and the fall), its lowest sole at the root."""

    name: str
    source: str
    start: float
    end: float
    cycle: tuple[float, float] | None = None
    airborne: bool = False


CLIPS: list[Clip] = [
    Clip('idle_loop', 'cmu/77_02.bvh', 1.0, 7.5, cycle=(2.5, 5.0)),
    Clip('run_loop', 'cmu/127_08.bvh', 0.15, 1.1, cycle=(0.55, 0.85)),
    # The air: a jump from standing tucks the knees up, one from a run splits into a leap; both reach for the ground
    # falling. Landing from standing sinks and rises.
    Clip('jump', 'cmu/127_26.bvh', 0.58, 0.86, airborne=True),
    Clip('leap', 'cmu/127_21.bvh', 0.66, 0.98, airborne=True),
    Clip('fall', 'cmu/127_26.bvh', 0.9, 1.08, airborne=True),
    Clip('land', 'cmu/127_26.bvh', 1.08, 1.5),
    # The dash: the leap's stretch on the ground; in the air, a dive with the arms ahead.
    Clip('dash', 'cmu/127_21.bvh', 0.7, 0.82),
    Clip('air_dash', 'cmu/127_23.bvh', 0.4, 0.62, airborne=True),
    # The moments: down on one knee to rest; dying, thrown down on the face.
    Clip('rest', 'cmu/23_03.bvh', 0.4, 2.6),
    Clip('death', 'cmu/90_16.bvh', 3.6, 4.7),
]
