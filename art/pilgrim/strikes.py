"""The pilgrim's sword strikes, keyed by hand (keyed.py), in the side plane so every slash draws its arc on screen.

A strike's clip lasts one second for its whole progress: the game plays it scrubbed to the strike's own progress
(gameplay timing, docs/gameplay.md#fight: the coil ends at 0.14, the hit window at 0.5, the whole strike about a quarter
of a second), so the keys sit at those shares and `DRAG` scales the bones' trailing to it. The blade is on the right
forearm, the arm toward the camera.

Each strike is the whole body's (docs/art-direction.md#strikes): wound up, the blade swept through a wide arc in a few
frames, the body following through past it. The chain is choreographed, each starting where the last ended: a cut down
from behind the head on a lunge; a backhand rising from behind the hip into an arabesque; the finisher, a forward
somersault with the blade at arm's length, drawing a whole circle before slamming down. The down strike pitches the
body over to cut under the feet; the dash strike is a draw cut at full stretch.
"""

from keyed import ANKLE, Key, Keyed, Place, Pose, Turn, bend, forward, knee, trunk

COIL = 0.14
"""The strike's progress where the wind-up ends and the cut begins."""

CUT = 0.3
"""Where the blade has crossed the target, early in the hit window."""

THROUGH = 0.5
"""Where the follow-through ends, with the hit window, and the body settles into its end pose."""

DRAG = 2.5
"""The bones trail by this many times their lag in clip time: the clip plays about four times faster than keyed."""


def _arm(ahead: float, out: float = 0.0) -> Turn:
    """The off (left) arm swung `ahead` degrees forward and `out` degrees out to its side."""
    return (-ahead, 0.0, -out)


def _stance(
    turns: dict[str, Turn], hips: Place, front: float, back: Place, back_sole: float = 0.0, front_sole: float = 0.0
) -> Pose:
    """A pose on the ground: the right (near) foot planted `front` meters ahead, the left at `back`."""
    return Pose(
        turns=turns,
        hips=hips,
        feet={'Right': (front, ANKLE), 'Left': back},
        soles={'Right': front_sole, 'Left': back_sole},
    )


GUARD = _stance(
    {
        **trunk(8.0, -10.0),
        'Head': (-6.0, 0.0, 0.0),
        'RightUpperArm': forward(40.0),
        'RightLowerArm': bend(65.0),
        'LeftUpperArm': _arm(10.0, 10.0),
        'LeftLowerArm': bend(40.0),
    },
    (0.0, -0.08),
    0.18,
    (-0.18, ANKLE),
)
"""Ready to strike: knees soft, the blade arm up before the chest."""

SWING_COIL = _stance(
    {
        **trunk(-8.0, -45.0),
        'Head': (-8.0, 0.0, 0.0),
        'RightUpperArm': forward(215.0),
        'RightLowerArm': bend(100.0),
        'LeftUpperArm': _arm(75.0, 10.0),
        'LeftLowerArm': bend(15.0),
    },
    (0.02, -0.04),
    0.24,
    (-0.2, ANKLE),
)
SWING_CUT = _stance(
    {
        **trunk(28.0, 40.0),
        'Head': (-18.0, 0.0, 0.0),
        'RightUpperArm': forward(60.0),
        'RightLowerArm': bend(0.0),
        'LeftUpperArm': _arm(-50.0, 25.0),
        'LeftLowerArm': bend(25.0),
    },
    (0.22, -0.2),
    0.62,
    (-0.3, ANKLE + 0.04),
    40.0,
)
SWING_THROUGH = _stance(
    {
        **trunk(36.0, 58.0),
        'Head': (-24.0, 0.0, 0.0),
        'RightUpperArm': forward(-45.0),
        'RightLowerArm': bend(10.0),
        'LeftUpperArm': _arm(-25.0, 45.0),
        'LeftLowerArm': bend(30.0),
    },
    (0.26, -0.25),
    0.64,
    (-0.32, ANKLE + 0.05),
    45.0,
)
SWING_END = SWING_THROUGH.but(
    turns={**trunk(30.0, 48.0), 'RightUpperArm': forward(-28.0), 'RightLowerArm': bend(25.0)},
    hips=(0.24, -0.22),
)
"""The first: from high behind the head, down across the front on a lunge, through to low behind the hip."""

SECOND_COIL = SWING_END.but(
    turns={**trunk(38.0, 62.0), 'RightUpperArm': forward(-62.0), 'RightLowerArm': bend(20.0)},
    hips=(0.24, -0.3),
)
SECOND_CUT = _stance(
    {
        **trunk(2.0, -10.0),
        'Head': (-14.0, 0.0, 0.0),
        'RightUpperArm': forward(115.0),
        'RightLowerArm': bend(0.0),
        'LeftUpperArm': _arm(-40.0, 40.0),
        'LeftLowerArm': bend(15.0),
    },
    (0.32, -0.05),
    0.62,
    (-0.4, 0.4),
    50.0,
)
SECOND_THROUGH = _stance(
    {
        **trunk(-16.0, -35.0),
        'Hips': (-4.0, 0.0, 0.0),
        'Neck': (-8.0, 0.0, 0.0),
        'Head': (-16.0, 0.0, 0.0),
        'RightUpperArm': forward(232.0),
        'RightLowerArm': bend(20.0),
        'LeftUpperArm': _arm(-60.0, 55.0),
        'LeftLowerArm': bend(10.0),
    },
    (0.34, 0.02),
    0.6,
    (-0.62, 0.7),
    55.0,
    30.0,
)
SECOND_END = SECOND_THROUGH.but(
    turns={**trunk(-12.0, -28.0), 'RightUpperArm': forward(215.0), 'RightLowerArm': bend(35.0)},
    hips=(0.32, 0.0),
    feet={'Left': (-0.52, 0.56)},
)
"""The second: from low behind, rising past the front to high over the head, the back leg lifted in an arabesque."""

FINISHER_COIL = _stance(
    {
        **trunk(22.0, -20.0),
        'Head': (-24.0, 0.0, 0.0),
        'RightUpperArm': forward(200.0),
        'RightLowerArm': bend(60.0),
        'LeftUpperArm': _arm(-40.0, 30.0),
        'LeftLowerArm': bend(30.0),
    },
    (0.3, -0.3),
    0.56,
    (0.0, ANKLE),
)


def _tucked(flip: float, hips: Place, arm: float) -> Pose:
    """In the somersault: the knees tucked, the blade arm straight along the body, the whole turned `flip` degrees."""
    return Pose(
        turns={
            **trunk(18.0),
            'Head': (6.0, 0.0, 0.0),
            'RightUpperArm': forward(arm),
            'RightLowerArm': bend(0.0),
            'LeftUpperArm': _arm(40.0, 30.0),
            'LeftLowerArm': bend(90.0),
            'RightUpperLeg': forward(105.0),
            'LeftUpperLeg': forward(95.0),
            'RightLowerLeg': knee(115.0),
            'LeftLowerLeg': knee(120.0),
            'RightFoot': (40.0, 0.0, 0.0),
            'LeftFoot': (40.0, 0.0, 0.0),
        },
        hips=hips,
        body=(flip, 0.0),
    )


FINISHER_SLAM = _stance(
    {
        **trunk(42.0, 30.0),
        'Neck': (-16.0, 0.0, 0.0),
        'Head': (-22.0, 0.0, 0.0),
        'RightUpperArm': forward(72.0),
        'RightLowerArm': bend(0.0),
        'LeftUpperArm': _arm(-70.0, 40.0),
        'LeftLowerArm': bend(10.0),
    },
    (0.32, -0.42),
    0.82,
    (0.0, 0.12),
    55.0,
)
FINISHER_END = FINISHER_SLAM.but(
    turns={**trunk(38.0, 26.0), 'RightUpperArm': forward(58.0), 'RightLowerArm': bend(8.0)},
    hips=(0.3, -0.4),
)
"""The finisher: crouched with the blade cocked, a forward somersault with it at arm's length, then slammed down in a
deep lunge."""

AIR = Pose(
    turns={
        **trunk(10.0),
        'RightUpperArm': forward(40.0),
        'RightLowerArm': bend(50.0),
        'LeftUpperArm': _arm(10.0, 30.0),
        'LeftLowerArm': bend(30.0),
        'RightUpperLeg': forward(60.0),
        'LeftUpperLeg': forward(35.0),
        'RightLowerLeg': knee(80.0),
        'LeftLowerLeg': knee(70.0),
        'RightFoot': (30.0, 0.0, 0.0),
        'LeftFoot': (30.0, 0.0, 0.0),
    },
)
DOWN_COIL = AIR.but(
    turns={
        **trunk(-10.0),
        'Head': (-10.0, 0.0, 0.0),
        'RightUpperArm': forward(210.0),
        'RightLowerArm': bend(80.0),
        'LeftUpperArm': _arm(60.0, 30.0),
    },
    body=(-15.0, 0.0),
)
DOWN_CUT = AIR.but(
    turns={
        **trunk(20.0),
        'Head': (10.0, 0.0, 0.0),
        'RightUpperArm': forward(35.0),
        'RightLowerArm': bend(0.0),
        'LeftUpperArm': _arm(-40.0, 40.0),
        'RightUpperLeg': forward(95.0),
        'LeftUpperLeg': forward(80.0),
        'RightLowerLeg': knee(120.0),
        'LeftLowerLeg': knee(115.0),
    },
    body=(45.0, 0.0),
)
DOWN_THROUGH = DOWN_CUT.but(
    turns={'RightUpperArm': forward(-50.0), 'RightLowerArm': bend(10.0), 'LeftUpperArm': _arm(-20.0, 50.0)},
    body=(58.0, 0.0),
)
DOWN_END = DOWN_THROUGH.but(turns={'RightUpperArm': forward(-30.0), 'RightLowerArm': bend(25.0)}, body=(35.0, 0.0))
"""The down strike, in the air: leaning back with the blade high, then the body pitched over, the knees tucked, the
blade swept from over the head to under the feet and behind."""

DASH_COIL = _stance(
    {
        **trunk(20.0, -50.0),
        'Head': (-20.0, 0.0, 0.0),
        'RightUpperArm': forward(-80.0),
        'RightLowerArm': bend(30.0),
        'LeftUpperArm': _arm(70.0, 10.0),
        'LeftLowerArm': bend(20.0),
    },
    (0.1, -0.22),
    0.42,
    (-0.45, ANKLE + 0.03),
    40.0,
)
DASH_CUT = _stance(
    {
        **trunk(34.0, 30.0),
        'Neck': (-10.0, 0.0, 0.0),
        'Head': (-20.0, 0.0, 0.0),
        'RightUpperArm': forward(78.0),
        'RightLowerArm': bend(0.0),
        'LeftUpperArm': _arm(-65.0, 30.0),
        'LeftLowerArm': bend(10.0),
    },
    (0.3, -0.3),
    0.85,
    (-0.5, 0.14),
    65.0,
)
DASH_THROUGH = DASH_CUT.but(
    turns={**trunk(30.0, 46.0), 'RightUpperArm': forward(118.0), 'RightLowerArm': bend(5.0)},
    hips=(0.32, -0.28),
)
"""The dash strike: low, the blade drawn from behind the hip, swept forward and up at full stretch along the dash."""


def strike(name: str, start: Pose, coil: Pose, cut: Pose, through: Pose, end: Pose) -> Keyed:
    """A strike: from `start`, wound up by the coil, the blade swept through the cut and followed through, then
    settled into its end."""
    return Keyed(
        name,
        [
            Key(0.0, start),
            Key(COIL, coil, 'stop'),
            Key(CUT, cut),
            Key(THROUGH, through),
            Key(1.0, end, 'stop'),
        ],
        drag=DRAG,
    )


KEYED: list[Keyed] = [
    strike('strike_swing', GUARD, SWING_COIL, SWING_CUT, SWING_THROUGH, SWING_END),
    strike('strike_second', SWING_END, SECOND_COIL, SECOND_CUT, SECOND_THROUGH, SECOND_END),
    Keyed(
        'strike_finisher',
        [
            Key(0.0, SECOND_END),
            Key(COIL, FINISHER_COIL, 'stop'),
            Key(0.22, _tucked(110.0, (0.25, 0.3), 175.0)),
            Key(0.32, _tucked(250.0, (0.3, 0.38), 170.0)),
            Key(0.42, FINISHER_SLAM.but(body=(360.0, 0.0), hips=(0.32, -0.3))),
            Key(THROUGH, FINISHER_SLAM.but(body=(360.0, 0.0))),
            Key(1.0, FINISHER_END.but(body=(360.0, 0.0)), 'stop'),
        ],
        drag=DRAG,
    ),
    strike('strike_down', AIR, DOWN_COIL, DOWN_CUT, DOWN_THROUGH, DOWN_END),
    strike('strike_dash', GUARD, DASH_COIL, DASH_CUT, DASH_THROUGH, DASH_THROUGH),
]
