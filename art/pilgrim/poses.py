"""The pilgrim's clips keyed by hand (keyed.py), for what motion capture doesn't give: the sword strikes.

A strike's clip lasts one second for its whole progress: the game plays it scrubbed to the strike's own progress
(gameplay timing, docs/gameplay.md#fight: the coil ends at 0.14, the cut at 0.5), so the keys sit at those shares. The
blade is on the right forearm, the arm toward the camera; an arm's angle is written as how far it swings forward from
hanging (`forward`). Each strike starts where the one before ends, and the finisher is the biggest, deepest and longest
held (docs/art-direction.md#strikes).
"""

from keyed import Key, Keyed, Pose, Turn

COIL = 0.14
"""The strike's progress where the wind-up ends and the cut begins."""

CUT = 0.36
"""Where the blade has crossed the target, early in the hit window."""

THROUGH = 0.55
"""Where the follow-through ends and the end pose holds."""

ANKLE = 0.085


def forward(degrees: float) -> Turn:
    """An upper arm swung `degrees` forward from hanging at the side (180: straight up; more: behind the head)."""
    return (-degrees, 0.0, 0.0)


def bend(degrees: float) -> Turn:
    """A forearm bent `degrees` at the elbow, the way it folds: toward the front of the upper arm."""
    return (-degrees, 0.0, 0.0)


def trunk(lean: float, twist: float = 0.0) -> dict[str, Turn]:
    """The spine and the chest leaning `lean` degrees forward between them, the chest turned `twist` degrees toward the
    near (right) shoulder going forward."""
    return {'Spine': (lean * 0.5, -twist * 0.4, 0.0), 'Chest': (lean * 0.5, -twist * 0.6, 0.0)}


GUARD = Pose(
    turns={
        **trunk(8.0, -10.0),
        'Neck': (-4.0, 0.0, 0.0),
        'Head': (-4.0, 0.0, 0.0),
        'RightUpperArm': forward(35.0),
        'RightLowerArm': bend(55.0),
        'LeftUpperArm': forward(-10.0),
        'LeftLowerArm': bend(40.0),
    },
    hips=(0.0, -0.06),
    feet={'Right': (0.16, ANKLE), 'Left': (-0.16, ANKLE)},
)
"""Ready to strike: knees soft, the blade arm up before the chest."""


def struck(pose: Pose, turns: dict[str, Turn], front: float, back: float, lower: float, ahead: float = 0.0) -> Pose:
    return pose.but(turns=turns, hips=(ahead, -lower), feet={'Right': (front, ANKLE), 'Left': (back, ANKLE)})


SWING_COIL = struck(
    GUARD,
    {**trunk(-4.0, -25.0), 'RightUpperArm': forward(200.0), 'RightLowerArm': bend(80.0), 'Head': (-8.0, 0.0, 0.0)},
    0.2,
    -0.2,
    0.04,
)
SWING_CUT = struck(
    GUARD,
    {**trunk(18.0, 25.0), 'RightUpperArm': forward(45.0), 'RightLowerArm': bend(0.0), 'LeftUpperArm': forward(-35.0)},
    0.38,
    -0.22,
    0.1,
    0.06,
)
SWING_END = struck(
    GUARD,
    {**trunk(22.0, 35.0), 'RightUpperArm': forward(28.0), 'RightLowerArm': bend(8.0), 'LeftUpperArm': forward(-40.0)},
    0.4,
    -0.22,
    0.12,
    0.08,
)
"""The first swing: from high behind the head, down across the front, through to low behind."""

SECOND_COIL = struck(
    SWING_END,
    {**trunk(16.0, 45.0), 'RightUpperArm': forward(-30.0), 'RightLowerArm': bend(20.0)},
    0.36,
    -0.26,
    0.13,
    0.06,
)
SECOND_CUT = struck(
    SWING_END,
    {
        **trunk(-6.0, -20.0),
        'RightUpperArm': forward(150.0),
        'RightLowerArm': bend(0.0),
        'LeftUpperArm': forward(10.0),
        'Head': (-10.0, 0.0, 0.0),
    },
    0.18,
    -0.3,
    0.02,
    0.02,
)
SECOND_END = struck(
    SWING_END,
    {
        **trunk(-8.0, -30.0),
        'RightUpperArm': forward(178.0),
        'RightLowerArm': bend(10.0),
        'LeftUpperArm': forward(20.0),
        'Head': (-12.0, 0.0, 0.0),
    },
    0.16,
    -0.3,
    0.0,
    0.0,
)
"""The second: rising from low behind to high over the head."""

FINISHER_COIL = struck(
    SECOND_END,
    {
        **trunk(-14.0, -30.0),
        'RightUpperArm': forward(210.0),
        'RightLowerArm': bend(95.0),
        'LeftUpperArm': forward(60.0),
        'Head': (-14.0, 0.0, 0.0),
    },
    0.12,
    -0.28,
    -0.04,
)
FINISHER_CUT = struck(
    SECOND_END,
    {
        **trunk(34.0, 30.0),
        'RightUpperArm': forward(55.0),
        'RightLowerArm': bend(0.0),
        'LeftUpperArm': forward(-50.0),
        'Head': (6.0, 0.0, 0.0),
    },
    0.62,
    -0.5,
    0.3,
    0.14,
)
FINISHER_END = struck(
    SECOND_END,
    {
        **trunk(40.0, 40.0),
        'RightUpperArm': forward(32.0),
        'RightLowerArm': bend(8.0),
        'LeftUpperArm': forward(-60.0),
        'Head': (10.0, 0.0, 0.0),
    },
    0.66,
    -0.52,
    0.34,
    0.16,
)
"""The finisher: up on the toes, the blade behind the head, then a lunge and an overhead cut all the way down."""

AIR = Pose(
    turns={
        **trunk(10.0),
        'RightUpperArm': forward(30.0),
        'RightLowerArm': bend(40.0),
        'LeftUpperArm': forward(10.0),
        'LeftLowerArm': bend(30.0),
    },
    feet={'Right': (0.12, 0.3), 'Left': (-0.1, 0.22)},
)
DOWN_COIL = AIR.but(
    turns={**trunk(-6.0), 'RightUpperArm': forward(150.0), 'RightLowerArm': bend(60.0)},
    feet={'Right': (0.2, 0.45), 'Left': (0.05, 0.4)},
)
DOWN_CUT = AIR.but(
    turns={
        **trunk(28.0),
        'RightUpperArm': forward(30.0),
        'RightLowerArm': bend(0.0),
        'LeftUpperArm': forward(60.0),
        'Head': (12.0, 0.0, 0.0),
    },
    feet={'Right': (0.0, 0.42), 'Left': (-0.15, 0.36)},
)
DOWN_END = AIR.but(
    turns={
        **trunk(30.0),
        'RightUpperArm': forward(15.0),
        'RightLowerArm': bend(5.0),
        'LeftUpperArm': forward(70.0),
        'Head': (14.0, 0.0, 0.0),
    },
    feet={'Right': (0.0, 0.4), 'Left': (-0.15, 0.34)},
)
"""The down strike, in the air: knees tucked, the blade swept from before the face to under the feet."""

DASH_COIL = struck(
    GUARD,
    {**trunk(14.0, -30.0), 'RightUpperArm': forward(60.0), 'RightLowerArm': bend(80.0)},
    0.3,
    -0.3,
    0.12,
    0.04,
)
DASH_CUT = struck(
    GUARD,
    {
        **trunk(28.0, 35.0),
        'RightUpperArm': forward(92.0),
        'RightLowerArm': bend(0.0),
        'LeftUpperArm': forward(-50.0),
        'Head': (-10.0, 0.0, 0.0),
    },
    0.62,
    -0.55,
    0.22,
    0.14,
)
"""The dash strike: the arm drawn back, then a thrust straight ahead along the dash, the body stretched behind it."""


def strike(name: str, start: Pose, coil: Pose, cut: Pose, end: Pose) -> Keyed:
    """A strike: from `start`, wound up by the coil, cutting fast, followed through, then its end held."""
    return Keyed(
        name,
        [
            Key(0.0, start),
            Key(COIL, coil, 'out'),
            Key(CUT, cut, 'in'),
            Key(THROUGH, end, 'out'),
            Key(1.0, end),
        ],
    )


KEYED: list[Keyed] = [
    strike('strike_swing', GUARD, SWING_COIL, SWING_CUT, SWING_END),
    strike('strike_second', SWING_END, SECOND_COIL, SECOND_CUT, SECOND_END),
    strike('strike_finisher', SECOND_END, FINISHER_COIL, FINISHER_CUT, FINISHER_END),
    strike('strike_down', AIR, DOWN_COIL, DOWN_CUT, DOWN_END),
    strike('strike_dash', GUARD, DASH_COIL, DASH_CUT, DASH_CUT),
]
