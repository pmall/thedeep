#!/usr/bin/env python3
"""Contact sheet of the look-dev motion reel (see tools/capture.sh and scripts/debug/MotionReel.cs).

    tools/motion_sheet.py RUN_DIR

Reads RUN_DIR/motion/NN-label.png (1080p frames with the camera centered on the player's hips) and
writes RUN_DIR/Motion.png: 1920x1080, a grid of crops around the player in shot order, each labeled.
Being a top-level PNG of the run, it lands in the before/after sheet like any photo spot.
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

COLUMNS, ROWS = 7, 5
SHEET = (1920, 1080)
CROP = (548, 432)  # around the center of each 1080p frame
BACKGROUND = (12, 14, 16)
TEXT = (230, 236, 240)


def font(size):
    for path in ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                 "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf"):
        if Path(path).exists():
            return ImageFont.truetype(path, size)
    return ImageFont.load_default(size=size)


def main():
    run = Path(sys.argv[1])
    frames = sorted((run / "motion").glob("*.png"))
    if not frames:
        print("no motion frames")
        return
    cell = (SHEET[0] // COLUMNS, SHEET[1] // ROWS)
    sheet = Image.new("RGB", SHEET, BACKGROUND)
    draw = ImageDraw.Draw(sheet)
    label_font = font(15)
    for i, path in enumerate(frames[:COLUMNS * ROWS]):
        image = Image.open(path).convert("RGB")
        cx, cy = image.width // 2, image.height // 2
        crop = image.crop((cx - CROP[0] // 2, cy - CROP[1] // 2, cx + CROP[0] // 2, cy + CROP[1] // 2))
        x, y = (i % COLUMNS) * cell[0], (i // COLUMNS) * cell[1]
        sheet.paste(crop.resize(cell, Image.LANCZOS), (x, y))
        draw.text((x + 6, y + 4), path.stem.replace("-", " "), fill=TEXT, font=label_font)
    sheet.save(run / "Motion.png")
    print(f"motion sheet: {len(frames)} shots -> {run / 'Motion.png'}")


if __name__ == "__main__":
    main()
