"""The pilgrim's moves keyed by hand (keyed.py) where motion capture has nothing to give: the Mag-grip's crawl, the water,
the Drill's dive and bore, the Harpoon's throw and zip, the recoil when hurt, the ledge vault and taking a relic. Each names the pose or cue it shows (PilgrimClips in the game); a walking loop travels on its root so the
game matches its rate to the speed.
"""

import math

from keyed import ANKLE, Key, Keyed, Pose, Turn, bend, cycle, forward, trunk

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


# The water: lying on it in a front crawl, the body rolling to each stroke, the head turned to breathe, the legs
# fluttering; still, upright, treading it.
STROKE_CYCLE = 0.9
STROKE_TRAVEL = 4.5
"""Seconds per stroke cycle (both arms), and its speed, m/s: the game's swimming speed (player_tuning.tres: SwimSpeed)."""

PULL = 0.55
"""The share of the cycle an arm pulls under the water; it recovers over it, faster, for the rest."""


def _stroke_arm(phase: float) -> tuple[float, float]:
    """An arm's swing forward from along the body (180: reaching ahead; under the body as it falls, over it, negative,
    as it comes back) and its elbow's bend, `phase` through its own stroke (0: entering ahead)."""
    if phase < PULL:
        q = phase / PULL
        eased = q * q * (3.0 - 2.0 * q)
        return 180.0 - 180.0 * eased, 10.0 + 70.0 * math.sin(math.pi * q)
    q = (phase - PULL) / (1.0 - PULL)
    return -180.0 * q, 20.0 + 85.0 * math.sin(math.pi * q)


def _stroke(phase: float) -> Pose:
    right, right_elbow = _stroke_arm(phase)
    left, left_elbow = _stroke_arm((phase + 0.5) % 1.0)
    left -= 360.0 if phase >= 0.5 else 0.0  # wound on round, never back
    roll = 38.0 * math.sin(2.0 * math.pi * (phase - 0.15))
    breathe = 45.0 * max(0.0, math.sin(2.0 * math.pi * (phase - 0.6))) ** 2
    kick = math.sin(6.0 * math.pi * phase)
    return Pose(
        turns={
            'Hips': (76.0, 0.0, 0.0),
            **trunk(6.0, roll),
            'Neck': (-26.0, -0.3 * roll, 0.0),
            'Head': (-24.0, -breathe, 0.0),
            'RightUpperArm': (-right, 0.0, 14.0),
            'RightLowerArm': bend(right_elbow),
            'LeftUpperArm': (-left, 0.0, -14.0),
            'LeftLowerArm': bend(left_elbow),
            'RightUpperLeg': (6.0 + 16.0 * kick, 0.0, 0.0),
            'RightLowerLeg': (14.0 + 12.0 * kick, 0.0, 0.0),
            'LeftUpperLeg': (6.0 - 16.0 * kick, 0.0, 0.0),
            'LeftLowerLeg': (14.0 - 12.0 * kick, 0.0, 0.0),
            'RightFoot': (50.0, 0.0, 0.0),
            'LeftFoot': (50.0, 0.0, 0.0),
        },
        hips=(-0.1, -0.02),
    )


def _tread(phase: float) -> Pose:
    """Treading water: upright, the legs turning in alternate circles, the hands sculling under the surface."""
    a = 2.0 * math.pi * phase
    scull = math.sin(2.0 * a)
    return Pose(
        turns={
            **trunk(10.0, 6.0 * math.sin(a)),
            'Neck': (-6.0, 0.0, 0.0),
            'Head': (-8.0, 0.0, 0.0),
            'RightUpperArm': (-15.0, 0.0, 62.0),
            'RightLowerArm': (-30.0 - 20.0 * scull, 25.0 * scull, 0.0),
            'RightHand': (0.0, 0.0, 20.0 * scull),
            'LeftUpperArm': (-15.0, 0.0, -62.0),
            'LeftLowerArm': (-30.0 - 20.0 * scull, -25.0 * scull, 0.0),
            'LeftHand': (0.0, 0.0, -20.0 * scull),
            'RightUpperLeg': (-(35.0 + 20.0 * math.sin(a)), 0.0, 8.0 * math.cos(a)),
            'RightLowerLeg': (70.0 + 25.0 * math.cos(a), 0.0, 0.0),
            'LeftUpperLeg': (-(35.0 - 20.0 * math.sin(a)), 0.0, -8.0 * math.cos(a)),
            'LeftLowerLeg': (70.0 - 25.0 * math.cos(a), 0.0, 0.0),
            'RightFoot': (-10.0, 0.0, 0.0),
            'LeftFoot': (-10.0, 0.0, 0.0),
        },
        hips=(0.0, 0.03 * math.sin(2.0 * a)),
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

KEYED: list[Keyed] = [
    cycle('grip_loop', CRAWL_CYCLE, CRAWL_STRIDE / (CRAWL_CYCLE / 2.0), _crawl),
    cycle('swim_loop', STROKE_CYCLE, STROKE_TRAVEL, _stroke, samples=36),
    cycle('tread_loop', 1.4, 0.0, _tread, samples=16),
    cycle('sink_loop', 2.4, 0.0, _sink, samples=8),
    Keyed('rise', [Key(0.0, RISE_GATHERED), Key(0.35, RISE_PUSHED)]),
    Keyed('dive', [Key(0.0, DIVE)]),
    Keyed('bore', [Key(0.0, BORE)]),
    Keyed('throw', [Key(0.0, THROW_COCKED), Key(0.12, THROW_COCKED), Key(0.26, THROW_CAST)]),
    Keyed('zip', [Key(0.0, ZIP)]),
    Keyed('hurt', [Key(0.0, STAND), Key(0.07, RECOIL), Key(0.45, STAND)]),
    Keyed('vault', [Key(0.0, STAND), Key(0.1, VAULT_HAND), Key(0.3, VAULT_OVER), Key(0.42, STAND)]),
    Keyed('relic', [Key(0.0, STAND), Key(0.8, RELIC)]),
]
