#!/usr/bin/env python3
"""Lists what a room scene places near a seam (instructions/testing.md#seam-brick): every node with a box (a `Size`) that
overlaps the span, with its extent on the play plane, and every other node whose origin lies in it. Run from the project root.
    tools/seamblocks.py worlds/prototype/rooms/Nave.tscn 56.5 63.5 [-2 0]
x0 x1 (and y0 y1, optional) are the frame's span in the room's own meters: a doorway's or a hatch's, as the recipe gives it.
Reads the scene as text: rotated nodes are listed by their origin only."""

import pathlib
import re
import sys


def main(path: str, x0: float, x1: float, y0: float, y1: float) -> None:
    for chunk in pathlib.Path(path).read_text(encoding='utf-8').split('\n['):
        head = re.match(r'node name="([^"]+)"([^\n]*)', chunk)
        origin = re.search(r'Transform3D\(([^)]*)\)', chunk)
        if not head or not origin:
            continue
        parent = re.search(r'parent="([^"]+)"', head.group(2))
        name = f'{parent.group(1) if parent else "."}/{head.group(1)}'
        ox, oy, oz = (float(v) for v in origin.group(1).split(',')[9:12])
        size = re.search(r'\nSize = Vector3\(([^)]*)\)', chunk)
        if size:
            sx, sy, sz = (float(v) for v in size.group(1).split(','))
            lo, hi, bottom, top = ox - sx / 2, ox + sx / 2, oy - sy / 2, oy + sy / 2
            if hi > x0 and lo < x1 and top > y0 and bottom < y1:
                print(f'{name:40} x {lo:.2f}..{hi:.2f}  y {bottom:.2f}..{top:.2f}  z {oz:.2f} depth {sz:.2f}')
        elif x0 <= ox <= x1 and y0 <= oy <= y1:
            print(f'{name:40} at {ox:.2f}, {oy:.2f}, {oz:.2f}')


if __name__ == '__main__':
    if len(sys.argv) not in (4, 6):
        sys.exit(__doc__)
    span = [float(v) for v in sys.argv[2:]] + ([] if len(sys.argv) == 6 else [-1e9, 1e9])
    main(sys.argv[1], *span)
