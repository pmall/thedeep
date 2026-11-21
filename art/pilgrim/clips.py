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
    the range of cycle lengths (seconds) searched from `start` for the cut where the pose meets itself again."""

    name: str
    source: str
    start: float
    end: float
    cycle: tuple[float, float] | None = None


CLIPS: list[Clip] = [
    Clip('idle_loop', 'cmu/77_02.bvh', 1.0, 7.5, cycle=(2.5, 5.0)),
    Clip('run_loop', 'cmu/127_08.bvh', 0.15, 1.1, cycle=(0.55, 0.85)),
]
