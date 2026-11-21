"""The pilgrim's moves keyed by hand (keyed.py) where motion capture has nothing to give: the Mag-grip's crawl, the water,
the Drill's dive and bore, the Harpoon's throw and zip, the recoil when hurt, the ledge vault, taking a relic and the
hard landing. Each names the pose or cue it shows (PilgrimClips in the game); a walking loop travels on its root so the
game matches its rate to the speed.
"""

import math
from collections.abc import Callable

from keyed import ANKLE, Key, Keyed, Pose, Turn, bend, forward, trunk

STAND = Pose(
    turns={
        **trunk(3.0),
        'RightUpperArm': forward(6.0),
        'RightLowerArm': bend(14.0),
        'LeftUpperArm': forward(4.0),
        'LeftLowerArm': bend(12.0),
    },
)
"""Standing at ease: the stance one-shot moves start from and give back to."""


def _arms_out(degrees: float, ahead: float = 0.0) -> dict[str, Turn]:
    """Both arms raised `degrees` out to the sides from hanging, swung `ahead` degrees forward."""
    return {'RightUpperArm': (-ahead, 0.0, degrees), 'LeftUpperArm': (-ahead, 0.0, -degrees)}


def _cycle(name: str, length: float, travel: float, pose_at: Callable[[float], Pose], samples: int = 12) -> Keyed:
    """A loop of `length` seconds sampled from `pose_at(phase)` (phase 0 to 1), its keys linear, its end its start."""
    keys = [Key(length * k / samples, pose_at(k / samples), 'linear') for k in range(samples)]
    keys.append(Key(length, pose_at(0.0), 'linear'))
    return Keyed(name, keys, travel)


# The Mag-grip: a low, clinging crawl along the metal (in the body's own frame, so a wall or a ceiling is the floor).
CRAWL_CYCLE = 0.56
CRAWL_STRIDE = 1.0
"""Seconds per cycle (two steps), and meters each foot travels back while planted (half the cycle)."""


def _crawl_foot(phase: float) -> tuple[float, float]:
    """A foot's ankle in the crawl: planted and sliding back for the first half, lifted forward for the second."""
    half = CRAWL_STRIDE / 2.0
    if phase < 0.5:
        return (half - CRAWL_STRIDE * phase / 0.5, ANKLE)
    q = (phase - 0.5) / 0.5
    eased = q * q * (3.0 - 2.0 * q)
    return (-half + CRAWL_STRIDE * eased, ANKLE + 0.2 * math.sin(math.pi * q))


def _crawl(phase: float) -> Pose:
    swing = math.cos(2.0 * math.pi * phase)
    return Pose(
        turns={
            **trunk(42.0, 8.0 * swing),
            'Neck': (-18.0, 0.0, 0.0),
            'Head': (-16.0, 0.0, 0.0),
            'RightUpperArm': forward(55.0 - 30.0 * swing),
            'RightLowerArm': bend(35.0),
            'LeftUpperArm': forward(55.0 + 30.0 * swing),
            'LeftLowerArm': bend(35.0),
        },
        hips=(0.05, -0.3 - 0.03 * math.cos(4.0 * math.pi * phase)),
        feet={'Right': _crawl_foot(phase), 'Left': _crawl_foot((phase + 0.5) % 1.0)},
    )


# The water: a crawl stroke leaning into it, the legs fluttering.
STROKE_CYCLE = 1.0
STROKE_TRAVEL = 2.6


def _stroke(phase: float) -> Pose:
    turn_ = 360.0 * phase
    kick = math.sin(4.0 * math.pi * phase)
    return Pose(
        turns={
            'Hips': (62.0, 0.0, 0.0),
            **trunk(10.0, 12.0 * math.sin(2.0 * math.pi * phase)),
            'Neck': (-30.0, 0.0, 0.0),
            'Head': (-30.0, 0.0, 0.0),
            'RightUpperArm': forward(180.0 + turn_),
            'RightLowerArm': bend(20.0),
            'LeftUpperArm': forward(360.0 + turn_),
            'LeftLowerArm': bend(20.0),
            'RightUpperLeg': (8.0 + 14.0 * kick, 0.0, 0.0),
            'RightLowerLeg': (12.0, 0.0, 0.0),
            'LeftUpperLeg': (8.0 - 14.0 * kick, 0.0, 0.0),
            'LeftLowerLeg': (12.0, 0.0, 0.0),
            'RightFoot': (35.0, 0.0, 0.0),
            'LeftFoot': (35.0, 0.0, 0.0),
        },
    )


def _sink(phase: float) -> Pose:
    sway = math.sin(2.0 * math.pi * phase)
    return Pose(
        turns={
            **trunk(-4.0),
            'Head': (-10.0, 0.0, 0.0),
            'RightUpperArm': (-(140.0 + 8.0 * sway), 0.0, 20.0),
            'LeftUpperArm': (-(130.0 - 8.0 * sway), 0.0, -20.0),
            'RightLowerArm': bend(25.0 + 6.0 * sway),
            'LeftLowerArm': bend(25.0 - 6.0 * sway),
            'RightFoot': (30.0, 0.0, 0.0),
            'LeftFoot': (30.0, 0.0, 0.0),
        },
    )


RISE_GATHERED = Pose(turns={**trunk(-6.0), 'Head': (-18.0, 0.0, 0.0), **_arms_out(160.0, 10.0)})
RISE_PUSHED = Pose(
    turns={
        **trunk(-8.0),
        'Head': (-22.0, 0.0, 0.0),
        **_arms_out(12.0, -10.0),
        'RightUpperLeg': (12.0, 0.0, 0.0),
        'LeftUpperLeg': (-6.0, 0.0, 0.0),
        'RightLowerLeg': (25.0, 0.0, 0.0),
        'LeftLowerLeg': (15.0, 0.0, 0.0),
        'RightFoot': (35.0, 0.0, 0.0),
        'LeftFoot': (35.0, 0.0, 0.0),
    }
)
"""Rising: the arms gathered high, then swept down to the sides in one push, the head up."""

DIVE = Pose(
    turns={
        'RightUpperArm': forward(178.0),
        'LeftUpperArm': forward(178.0),
        'RightFoot': (55.0, 0.0, 0.0),
        'LeftFoot': (55.0, 0.0, 0.0),
        'Head': (8.0, 0.0, 0.0),
    },
)
"""The Drill's dive: a spike, straight down, arms overhead, legs together and toes pointed."""

BORE = Pose(
    turns={
        **trunk(70.0),
        'Neck': (-30.0, 0.0, 0.0),
        'Head': (-30.0, 0.0, 0.0),
        'RightUpperArm': forward(100.0),
        'LeftUpperArm': forward(100.0),
        'RightUpperLeg': (35.0, 0.0, 0.0),
        'LeftUpperLeg': (45.0, 0.0, 0.0),
        'RightLowerLeg': (20.0, 0.0, 0.0),
        'LeftLowerLeg': (10.0, 0.0, 0.0),
        'RightFoot': (45.0, 0.0, 0.0),
        'LeftFoot': (45.0, 0.0, 0.0),
    },
)
"""The bore: tucked tight behind both arms thrust ahead, a spear."""

THROW_COCKED = Pose(
    turns={
        **trunk(-10.0, -35.0),
        'Head': (-6.0, 0.0, 0.0),
        'RightUpperArm': forward(205.0),
        'RightLowerArm': bend(95.0),
        'LeftUpperArm': forward(60.0),
    },
)
THROW_CAST = Pose(
    turns={
        **trunk(18.0, 30.0),
        'Head': (-10.0, 0.0, 0.0),
        'RightUpperArm': forward(115.0),
        'RightLowerArm': bend(0.0),
        'LeftUpperArm': forward(-30.0),
    },
)
"""The Harpoon's throw: the near arm cocked high behind the head, then whipped through along the line."""

ZIP = Pose(
    turns={
        **trunk(-6.0, 20.0),
        'Head': (-12.0, 0.0, 0.0),
        'RightUpperArm': forward(130.0),
        'RightLowerArm': bend(0.0),
        'LeftUpperArm': forward(-35.0),
        'LeftLowerArm': bend(20.0),
        'RightUpperLeg': (20.0, 0.0, 0.0),
        'LeftUpperLeg': (35.0, 0.0, 0.0),
        'RightLowerLeg': (40.0, 0.0, 0.0),
        'LeftLowerLeg': (25.0, 0.0, 0.0),
        'RightFoot': (30.0, 0.0, 0.0),
        'LeftFoot': (30.0, 0.0, 0.0),
    },
)
"""On the Harpoon's line: the near arm straight along it, the body trailing behind."""

RECOIL = Pose(
    turns={
        **trunk(-24.0, -15.0),
        'Head': (-20.0, 0.0, 0.0),
        'RightUpperArm': (-70.0, 0.0, 35.0),
        'RightLowerArm': bend(35.0),
        'LeftUpperArm': (-60.0, 0.0, -30.0),
        'LeftLowerArm': bend(30.0),
    },
    hips=(-0.06, -0.08),
    feet={'Right': (0.12, ANKLE), 'Left': (-0.2, ANKLE)},
)
"""Hurt: thrown back by the blow, arms flung up."""

VAULT_HAND = Pose(
    turns={**trunk(35.0), 'Head': (-15.0, 0.0, 0.0), 'RightUpperArm': forward(40.0), 'LeftUpperArm': forward(45.0)},
    hips=(0.0, 0.0),
    feet={'Right': (0.05, 0.45), 'Left': (-0.05, 0.4)},
)
VAULT_OVER = Pose(
    turns={**trunk(20.0), 'RightUpperArm': forward(10.0), 'LeftUpperArm': forward(20.0)},
    hips=(0.05, -0.1),
    feet={'Right': (0.2, ANKLE), 'Left': (-0.05, 0.2)},
)
"""The ledge vault: a hand planted on the top, the knees tucked up over the edge, then onto it."""

RELIC = Pose(
    turns={
        **trunk(-16.0),
        'Neck': (-12.0, 0.0, 0.0),
        'Head': (-22.0, 0.0, 0.0),
        'RightUpperArm': (-105.0, 0.0, 35.0),
        'LeftUpperArm': (-95.0, 0.0, -35.0),
        'RightLowerArm': bend(15.0),
        'LeftLowerArm': bend(15.0),
    },
    hips=(0.0, -0.02),
    feet={'Right': (0.1, ANKLE), 'Left': (-0.1, ANKLE)},
)
"""Taking a relic: the chest lifted, the head back, the arms open to receive it."""

CROUCH = Pose(
    turns={
        **trunk(38.0),
        'Head': (-25.0, 0.0, 0.0),
        'RightUpperArm': forward(35.0),
        'LeftUpperArm': forward(-15.0),
        'RightLowerArm': bend(30.0),
    },
    hips=(0.04, -0.36),
    feet={'Right': (0.2, ANKLE), 'Left': (-0.18, ANKLE)},
)
"""The hard landing: the knees taking it, deep."""

KEYED: list[Keyed] = [
    _cycle('grip_loop', CRAWL_CYCLE, CRAWL_STRIDE / (CRAWL_CYCLE / 2.0), _crawl),
    _cycle('swim_loop', STROKE_CYCLE, STROKE_TRAVEL, _stroke, samples=16),
    _cycle('sink_loop', 2.4, 0.0, _sink, samples=8),
    Keyed('rise', [Key(0.0, RISE_GATHERED), Key(0.35, RISE_PUSHED, 'out')]),
    Keyed('dive', [Key(0.0, DIVE)]),
    Keyed('bore', [Key(0.0, BORE)]),
    Keyed('throw', [Key(0.0, THROW_COCKED), Key(0.12, THROW_COCKED), Key(0.26, THROW_CAST, 'in')]),
    Keyed('zip', [Key(0.0, ZIP)]),
    Keyed('hurt', [Key(0.0, STAND), Key(0.07, RECOIL, 'out'), Key(0.45, STAND)]),
    Keyed('vault', [Key(0.0, STAND), Key(0.1, VAULT_HAND, 'out'), Key(0.3, VAULT_OVER), Key(0.42, STAND)]),
    Keyed('relic', [Key(0.0, STAND), Key(0.8, RELIC)]),
    Keyed('hard_land', [Key(0.0, CROUCH), Key(0.12, CROUCH), Key(0.5, STAND)]),
]
