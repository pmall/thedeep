"""The pilgrim's locomotion keyed by hand (keyed.py), stylized past what capture gives (docs/art-direction.md#character):
the run, the air, the dashes and the landings.

The run is a sprinter's at the game's top speed: a short, springy contact, the heel kicked up behind and the knee
driven high in front, the trunk leaning a little over the legs, the head level, the arms pumping. It is generated per
phase, each foot planted at exactly the root's speed, so it never slides.

The air is scrubbed by the body's climb, not played in time (PilgrimAnimation.Air): the clip's first instant is the
take-off at full rise, its middle the apex, its end falling as fast as it rose; a short hop reaches its apex sooner and
the pose follows the arc the player steers. A jump from standing tucks the knees; one from a run strides through the
air (`leap`), the two blended by the speed across.
"""

import math

from keyed import ANKLE, Key, Keyed, Place, Pose, Turn, bend, cycle, forward, trunk

RUN_SPEED = 7.0
"""The game's top run speed, m/s: the run plays at its own rate there (resources/player_tuning.tres: RunSpeed)."""

RUN_CYCLE = 0.5
"""Seconds per cycle of the run (two steps)."""

STANCE = 0.18
"""The share of the cycle each foot is on the ground."""

CONTACT = 0.32
"""How far ahead of the hips a foot lands, meters."""


def _periodic(points: list[tuple[float, ...]], phase: float) -> tuple[float, ...]:
    """A smooth loop through `points` ((phase, value, ...), phases rising within 0..1), at `phase`."""
    n = len(points)
    phase %= 1.0
    i = max(k for k in range(n) if points[k][0] <= phase) if phase >= points[0][0] else n - 1
    j = (i + 1) % n
    t0, t1 = points[i][0], points[j][0] + (1.0 if j <= i else 0.0)
    at = phase + (1.0 if phase < t0 else 0.0)
    s = (at - t0) / max(t1 - t0, 1e-6)
    a, b, c, d = points[(i - 1) % n], points[i], points[j], points[(j + 1) % n]
    out = []
    for k in range(1, len(b)):
        p0, p1, p2, p3 = a[k], b[k], c[k], d[k]
        out.append(
            0.5
            * (
                2 * p1
                + (-p0 + p2) * s
                + (2 * p0 - 5 * p1 + 4 * p2 - p3) * s * s
                + (-p0 + 3 * p1 - 3 * p2 + p3) * s * s * s
            )
        )
    return tuple(out)


# Where a foot's ankle goes off the ground relative to the hips (forward, height over the ground) and its sole's tip,
# from toe-off (0) to the next contact (1): kicked up behind, the knee driven through high, reaching out and pawing back.
SWING: list[tuple[float, float, float, float]] = [
    (0.0, CONTACT - RUN_SPEED * RUN_CYCLE * STANCE, 0.14, 45.0),
    (0.22, -0.3, 0.42, 70.0),
    (0.42, -0.12, 0.58, 40.0),
    (0.62, 0.14, 0.5, -5.0),
    (0.82, 0.38, 0.27, -20.0),
    (1.0, CONTACT, ANKLE, -4.0),
]


def _swing(s: float) -> tuple[float, float, float]:
    """The foot `s` of the way through its swing (0 toe-off, 1 contact): Catmull-Rom through SWING, ends held."""
    pts = SWING
    i = min(len(pts) - 2, max(k for k in range(len(pts)) if pts[k][0] <= s))
    t0, t1 = pts[i][0], pts[i + 1][0]
    u = (s - t0) / (t1 - t0)
    a, b, c, d = pts[max(i - 1, 0)], pts[i], pts[i + 1], pts[min(i + 2, len(pts) - 1)]
    out = []
    for k in range(1, 4):
        p0, p1, p2, p3 = a[k], b[k], c[k], d[k]
        out.append(
            0.5
            * (
                2 * p1
                + (-p0 + p2) * u
                + (2 * p0 - 5 * p1 + 4 * p2 - p3) * u * u
                + (-p0 + 3 * p1 - 3 * p2 + p3) * u * u * u
            )
        )
    return out[0], out[1], out[2]


# The hips over one step (0 a foot's contact): lowest in the middle of the contact, highest in the flight.
BOB: list[tuple[float, float]] = [(0.0, -0.05), (0.17, -0.085), (0.36, -0.04), (0.7, 0.005)]


def _run(phase: float) -> Pose:
    """The run at `phase` (0: the right foot lands ahead)."""
    step = (2.0 * phase) % 1.0
    (up,) = _periodic(list(BOB), step)
    ahead = 0.05
    feet: dict[str, Place] = {}
    soles: dict[str, float] = {}
    for side, offset in (('Right', 0.0), ('Left', 0.5)):
        p = (phase - offset) % 1.0
        if p < STANCE:
            # Planted: still on the ground while the body passes over it, the heel rising toward the toe-off.
            x = CONTACT - RUN_SPEED * RUN_CYCLE * p
            lift = max(0.0, (p / STANCE - 0.45) / 0.55)
            sole = 45.0 * lift * lift
            feet[side] = (ahead + x, ANKLE + 0.055 * lift * lift)
            soles[side] = sole
        else:
            x, height, sole = _swing((p - STANCE) / (1.0 - STANCE))
            feet[side] = (ahead + x, height)
            soles[side] = sole
    swing = math.cos(2.0 * math.pi * phase)  # 1: the right leg ahead, the left behind
    pump = math.sin(2.0 * math.pi * phase)
    arm = 58.0
    return Pose(
        turns={
            'Hips': (6.0, 12.0 * swing, 4.0 * math.cos(4.0 * math.pi * phase)),
            **trunk(8.0 + 3.0 * math.cos(4.0 * math.pi * phase + 0.6), -18.0 * swing),
            'Neck': (-4.0, 0.0, 0.0),
            'Head': (-8.0 - 2.0 * math.cos(4.0 * math.pi * phase + 1.2), 6.0 * swing, 0.0),
            'RightShoulder': (0.0, 0.0, 0.0),
            'RightUpperArm': (-(14.0 - arm * swing - 10.0 * pump), 0.0, 14.0),
            'RightLowerArm': bend(80.0 - 30.0 * swing),
            'RightHand': (0.0, 0.0, -10.0),
            'LeftUpperArm': (-(14.0 + arm * swing + 10.0 * pump), 0.0, -14.0),
            'LeftLowerArm': bend(80.0 + 30.0 * swing),
            'LeftHand': (0.0, 0.0, 10.0),
        },
        hips=(ahead, up),
        feet=feet,
        soles=soles,
    )


def _air(
    trunk_lean: float, arms: tuple[Turn, Turn, float, float], right: Place, left: Place, soles: tuple[float, float]
) -> Pose:
    """An airborne pose: the trunk's lean, the arms (each upper arm's turn and each elbow's bend), the ankles."""
    right_arm, left_arm, right_elbow, left_elbow = arms
    return Pose(
        turns={
            **trunk(trunk_lean),
            'Head': (-0.4 * trunk_lean - 4.0, 0.0, 0.0),
            'RightUpperArm': right_arm,
            'LeftUpperArm': left_arm,
            'RightLowerArm': bend(right_elbow),
            'LeftLowerArm': bend(left_elbow),
        },
        feet={'Right': right, 'Left': left},
        soles={'Right': soles[0], 'Left': soles[1]},
    )


def _arm(ahead: float, out: float, side: str) -> Turn:
    """An upper arm swung `ahead` degrees forward and `out` degrees out to its side."""
    return (-ahead, 0.0, out if side == 'Right' else -out)


# A jump from standing: stretched up off the ground, one arm thrown up, the knees drawn into a ball at the apex, then
# the legs let down to reach for the ground and the arms spread wide for balance as the fall gathers speed.
JUMP_KEYS = [
    Key(
        0.0,
        _air(
            -6.0,
            (_arm(165.0, 10.0, 'Right'), _arm(-35.0, 15.0, 'Left'), 10.0, 25.0),
            (0.03, 0.1),
            (-0.05, 0.12),
            (55, 60),
        ),
    ),
    Key(
        0.2,
        _air(
            6.0,
            (_arm(120.0, 20.0, 'Right'), _arm(-10.0, 25.0, 'Left'), 35.0, 45.0),
            (0.14, 0.46),
            (-0.04, 0.16),
            (35, 55),
        ),
    ),
    Key(
        0.5,
        _air(
            26.0, (_arm(35.0, 25.0, 'Right'), _arm(15.0, 25.0, 'Left'), 95.0, 85.0), (0.2, 0.62), (0.08, 0.54), (35, 40)
        ),
    ),
    Key(
        0.75,
        _air(
            8.0, (_arm(70.0, 55.0, 'Right'), _arm(-15.0, 45.0, 'Left'), 40.0, 35.0), (0.18, 0.3), (-0.1, 0.36), (25, 30)
        ),
    ),
    Key(
        1.0,
        _air(
            -4.0,
            (_arm(115.0, 65.0, 'Right'), _arm(95.0, 65.0, 'Left'), 25.0, 30.0),
            (0.12, 0.1),
            (-0.16, 0.22),
            (20, 30),
        ),
    ),
]

# A jump from a run: the take-off stride held through the rise, the legs split wide at full stretch, gathered past the
# apex, the lead leg reaching ahead for the landing.
LEAP_KEYS = [
    Key(
        0.0,
        _air(
            18.0,
            (_arm(-55.0, 15.0, 'Right'), _arm(100.0, 15.0, 'Left'), 35.0, 45.0),
            (0.25, 0.52),
            (-0.45, 0.2),
            (0, 60),
        ),
    ),
    Key(
        0.3,
        _air(
            14.0,
            (_arm(-35.0, 35.0, 'Right'), _arm(125.0, 30.0, 'Left'), 25.0, 25.0),
            (0.55, 0.45),
            (-0.6, 0.36),
            (0, 65),
        ),
    ),
    Key(
        0.62,
        _air(
            10.0, (_arm(40.0, 45.0, 'Right'), _arm(80.0, 40.0, 'Left'), 50.0, 40.0), (0.36, 0.4), (-0.32, 0.4), (10, 50)
        ),
    ),
    Key(
        1.0,
        _air(
            -2.0,
            (_arm(105.0, 60.0, 'Right'), _arm(75.0, 60.0, 'Left'), 30.0, 35.0),
            (0.28, 0.14),
            (-0.16, 0.26),
            (5, 35),
        ),
    ),
]

# The dash: a burst forward, low and stretched, the arms flung back.
DASH = Pose(
    turns={
        **trunk(30.0, 10.0),
        'Hips': (8.0, 0.0, 0.0),
        'Neck': (-14.0, 0.0, 0.0),
        'Head': (-16.0, 0.0, 0.0),
        'RightUpperArm': _arm(-65.0, 20.0, 'Right'),
        'LeftUpperArm': _arm(-50.0, 20.0, 'Left'),
        'RightLowerArm': bend(15.0),
        'LeftLowerArm': bend(20.0),
        'RightHand': (-20.0, 0.0, 0.0),
        'LeftHand': (-20.0, 0.0, 0.0),
    },
    hips=(0.1, -0.2),
    feet={'Right': (0.55, ANKLE + 0.02), 'Left': (-0.55, 0.2)},
    soles={'Right': 0.0, 'Left': 60.0},
)
DASH_SET = DASH.but(
    turns={**trunk(22.0, 0.0), 'Neck': (-10.0, 0.0, 0.0), 'Head': (-12.0, 0.0, 0.0)},
    hips=(0.08, -0.16),
    feet={'Right': (0.5, ANKLE), 'Left': (-0.5, 0.17)},
)

# The air dash: flung flat along the dash in a full barrel roll, the arms swept back along the body.
AIR_DASH = Pose(
    turns={
        **trunk(4.0),
        'Head': (-40.0, 0.0, 0.0),
        'RightUpperArm': _arm(-25.0, 12.0, 'Right'),
        'LeftUpperArm': _arm(-25.0, 12.0, 'Left'),
        'RightLowerArm': bend(10.0),
        'LeftLowerArm': bend(10.0),
        'RightUpperLeg': (-4.0, 0.0, 0.0),
        'LeftUpperLeg': (-10.0, 0.0, 0.0),
        'RightLowerLeg': (10.0, 0.0, 0.0),
        'LeftLowerLeg': (25.0, 0.0, 0.0),
        'RightFoot': (50.0, 0.0, 0.0),
        'LeftFoot': (50.0, 0.0, 0.0),
    },
    hips=(0.0, -0.12),
)

STAND: Pose = Pose(
    turns={
        **trunk(3.0),
        'RightUpperArm': forward(6.0),
        'RightLowerArm': bend(14.0),
        'LeftUpperArm': forward(4.0),
        'LeftLowerArm': bend(12.0),
    },
    feet={'Right': (0.1, ANKLE), 'Left': (-0.1, ANKLE)},
)
"""Standing at ease, feet apart in the side plane: where the landings come back to."""


def _crouch(depth: float, lean: float, arms: float) -> Pose:
    return STAND.but(
        turns={
            **trunk(lean),
            'Head': (-0.6 * lean, 0.0, 0.0),
            'RightUpperArm': _arm(arms, 20.0, 'Right'),
            'LeftUpperArm': _arm(arms * 0.6, 20.0, 'Left'),
            'RightLowerArm': bend(30.0),
            'LeftLowerArm': bend(25.0),
        },
        hips=(0.03, -depth),
        feet={'Right': (0.16, ANKLE), 'Left': (-0.16, ANKLE)},
    )


# The hard landing: down on one knee, a hand flat on the ground, the head up, then straight back up.
SLAM = STAND.but(
    turns={
        **trunk(32.0, 15.0),
        'Neck': (-20.0, 0.0, 0.0),
        'Head': (-22.0, 0.0, 0.0),
        'RightUpperArm': _arm(-40.0, 25.0, 'Right'),
        'RightLowerArm': bend(20.0),
        'LeftUpperArm': _arm(70.0, 10.0, 'Left'),
        'LeftLowerArm': bend(10.0),
        'LeftHand': (40.0, 0.0, 0.0),
    },
    hips=(0.0, -0.5),
    feet={'Right': (-0.2, 0.12), 'Left': (0.35, ANKLE)},
    soles={'Right': 60.0, 'Left': 0.0},
)

KEYED: list[Keyed] = [
    cycle('run_loop', RUN_CYCLE, RUN_SPEED, _run, samples=30),
    Keyed('jump', JUMP_KEYS),
    Keyed('leap', LEAP_KEYS),
    Keyed('dash', [Key(0.0, DASH), Key(0.16, DASH_SET), Key(0.3, DASH_SET)]),
    Keyed(
        'air_dash',
        [
            Key(0.0, AIR_DASH.but(body=(70.0, 0.0))),
            Key(0.05, AIR_DASH.but(body=(85.0, 120.0))),
            Key(0.15, AIR_DASH.but(body=(85.0, 360.0)), 'stop'),
            Key(0.3, AIR_DASH.but(body=(80.0, 360.0))),
        ],
    ),
    Keyed(
        'land',
        [
            Key(0.0, _crouch(0.12, 14.0, 30.0)),
            Key(0.07, _crouch(0.24, 24.0, 45.0), 'stop'),
            Key(0.24, _crouch(-0.01, 2.0, 8.0)),
            Key(0.36, STAND, 'stop'),
        ],
    ),
    Keyed(
        'hard_land',
        [
            Key(0.0, SLAM.but(hips=(0.0, -0.3))),
            Key(0.06, SLAM, 'stop'),
            Key(0.28, SLAM.but(hips=(0.0, -0.46)), 'stop'),
            Key(0.46, _crouch(0.02, 6.0, 10.0)),
            Key(0.6, STAND, 'stop'),
        ],
    ),
]
