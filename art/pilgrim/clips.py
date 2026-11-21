"""The pilgrim's animation clips: what each is cut from, as data the build (build.py) bakes through mocap.py.

The sources are the CMU Graphics Lab Motion Capture Database (mocap.cs.cmu.edu), in the MotionBuilder-friendly BVH
conversion by B. Hahne (2010, every file opens on the actor's T-pose), kept in art/mocap/cmu/ under their CMU names.
CMU's terms: free for any use, commercial products included; the data itself may not be resold. Credit "the CMU Graphics
Lab Motion Capture Database, funded by NSF EIA-0196217" in the game's credits.

A clip's name is the animation's name in the game; one that loops ends in "_loop", which Godot's importer reads as a
looping animation.
"""

from dataclasses import dataclass, field

type Turn = tuple[float, float, float]


@dataclass(frozen=True)
class Clip:
    """One animation: its source file (under art/mocap/), the span used in seconds after the T-pose frame, and, for a loop,
        the range of cycle lengths (seconds) searched from `start` for the cut where the pose meets itself again. An airborne
        clip's hips hold one height over its root (the game makes the rise and the fall), its lowest sole at the root. `adjust`
    corrects the actor's posture: each bone named turned further (pitch, yaw, roll in degrees, as keyed.py turns them),
    carrying the bones below it."""

    name: str
    source: str
    start: float
    end: float
    cycle: tuple[float, float] | None = None
    airborne: bool = False
    adjust: dict[str, Turn] = field(default_factory=dict)


CLIPS: list[Clip] = [
    # Standing, the actor's head droops: the pilgrim holds it up.
    Clip(
        'idle_loop',
        'cmu/77_02.bvh',
        1.0,
        7.5,
        cycle=(2.5, 5.0),
        adjust={'Chest': (-4.0, 0.0, 0.0), 'Neck': (-14.0, 0.0, 0.0), 'Head': (-10.0, 0.0, 0.0)},
    ),
    # The moments: down on one knee to rest; dying, thrown down on the face.
    Clip('rest', 'cmu/23_03.bvh', 0.4, 2.6),
    Clip('death', 'cmu/90_16.bvh', 3.6, 4.7),
]
