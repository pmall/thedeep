#!/usr/bin/env python3
"""Contact sheet of a look-dev reel (see tools/capture.sh and scripts/debug/MotionReel.cs).

    tools/motion_sheet.py RUN_DIR [FOLDER SHEET COLUMNSxROWS CROP_WxCROP_H]

Reads RUN_DIR/FOLDER/NN-label.png (1080p frames centered on the subject) and writes RUN_DIR/SHEET.png:
1920x1080, a grid of crops around the center of each frame in shot order, each labeled. Being a top-level
PNG of the run, it lands in the before/after sheet like any photo spot. Defaults: the pilgrim's motion
reel (motion, Motion, 7x5, 548x432).
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

SHEET_SIZE = (1920, 1080)
BACKGROUND = (12, 14, 16)
TEXT = (230, 236, 240)


def font(size):
    for path in ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                 "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf"):
        if Path(path).exists():
            return ImageFont.truetype(path, size)
    return ImageFont.load_default(size=size)


def pair(text):
    a, b = text.split("x")
    return int(a), int(b)


def main():
    run = Path(sys.argv[1])
    folder = sys.argv[2] if len(sys.argv) > 2 else "motion"
    name = sys.argv[3] if len(sys.argv) > 3 else "Motion"
    columns, rows = pair(sys.argv[4]) if len(sys.argv) > 4 else (7, 5)
    crop_size = pair(sys.argv[5]) if len(sys.argv) > 5 else (548, 432)
    frames = sorted((run / folder).glob("*.png"))
    if not frames:
        print(f"no {folder} frames")
        return
    cell = (SHEET_SIZE[0] // columns, SHEET_SIZE[1] // rows)
    sheet = Image.new("RGB", SHEET_SIZE, BACKGROUND)
    draw = ImageDraw.Draw(sheet)
    label_font = font(15)
    for i, path in enumerate(frames[:columns * rows]):
        image = Image.open(path).convert("RGB")
        cx, cy = image.width // 2, image.height // 2
        crop = image.crop((cx - crop_size[0] // 2, cy - crop_size[1] // 2, cx + crop_size[0] // 2, cy + crop_size[1] // 2))
        x, y = (i % columns) * cell[0], (i // columns) * cell[1]
        sheet.paste(crop.resize(cell, Image.LANCZOS), (x, y))
        draw.text((x + 6, y + 4), path.stem.replace("-", " "), fill=TEXT, font=label_font)
    out = run / f"{name}.png"
    sheet.save(out)
    print(f"{folder} sheet: {len(frames)} shots -> {out}")


if __name__ == "__main__":
    main()
