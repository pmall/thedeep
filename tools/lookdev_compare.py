#!/usr/bin/env python3
"""Before/after sheet for two look-dev runs (see tools/capture.sh).

    tools/lookdev_compare.py BEFORE_DIR AFTER_DIR OUT_DIR

For every spot PNG in either run: OUT_DIR/<spot>.png puts before and after side by side, with
how much of the image changed. OUT_DIR/sheet.png stacks all of them. Frame times from both
perf.json files are printed side by side. Needs python3-pil.
"""
import json
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFont

PANEL = (960, 540)  # each side, half of 1080p
HEADER = 44
BACKGROUND = (12, 14, 16)
TEXT = (210, 220, 224)
DIM = (120, 132, 138)
CHANGED = (236, 120, 110)
CHANGE_THRESHOLD = 12  # per-channel difference (0-255) that counts a pixel as changed


def font(size):
    for path in ("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                 "/usr/share/fonts/truetype/noto/NotoSans-Regular.ttf"):
        if Path(path).exists():
            return ImageFont.truetype(path, size)
    return ImageFont.load_default(size=size)


def changed_fraction(a, b):
    """Share of pixels whose largest channel difference exceeds the threshold."""
    if a.size != b.size:
        return 1.0
    diff = ImageChops.difference(a, b).convert("RGB")
    channels = diff.split()
    biggest = ImageChops.lighter(ImageChops.lighter(channels[0], channels[1]), channels[2])
    histogram = biggest.histogram()
    return sum(histogram[CHANGE_THRESHOLD + 1:]) / (a.size[0] * a.size[1])


def panel(image):
    if image is None:
        missing = Image.new("RGB", PANEL, BACKGROUND)
        ImageDraw.Draw(missing).text((PANEL[0] // 2, PANEL[1] // 2), "missing", fill=DIM, font=font(28), anchor="mm")
        return missing
    return image.convert("RGB").resize(PANEL, Image.LANCZOS)


def pair_image(spot, before, after, before_name, after_name):
    changed = changed_fraction(before, after) if before is not None and after is not None else None
    out = Image.new("RGB", (PANEL[0] * 2, PANEL[1] + HEADER), BACKGROUND)
    out.paste(panel(before), (0, HEADER))
    out.paste(panel(after), (PANEL[0], HEADER))
    draw = ImageDraw.Draw(out)
    label = font(18)
    draw.text((12, HEADER // 2), f"before  {before_name}", fill=DIM, font=label, anchor="lm")
    draw.text((PANEL[0] + 12, HEADER // 2), f"after  {after_name}", fill=DIM, font=label, anchor="lm")
    if changed is None:
        verdict, color = "only in one run", CHANGED
    elif changed == 0:
        verdict, color = "identical", DIM
    else:
        verdict, color = f"{changed * 100:.1f}% of pixels changed", CHANGED
    draw.text((PANEL[0] * 2 - 12, HEADER // 2), f"{spot}   {verdict}", fill=TEXT if changed else color, font=label, anchor="rm")
    return out, verdict


def load(directory, name):
    path = directory / name
    return Image.open(path) if path.exists() else None


def perf(directory):
    path = directory / "perf.json"
    return json.loads(path.read_text()) if path.exists() else None


def main():
    if len(sys.argv) != 4:
        sys.exit(__doc__)
    before_dir, after_dir, out_dir = (Path(arg) for arg in sys.argv[1:])
    out_dir.mkdir(parents=True, exist_ok=True)

    names = sorted({p.name for d in (before_dir, after_dir) for p in d.glob("*.png")})
    if not names:
        sys.exit("no spot images in either run")

    pairs = []
    for name in names:
        spot = name.removesuffix(".png")
        image, verdict = pair_image(spot, load(before_dir, name), load(after_dir, name), before_dir.name, after_dir.name)
        image.save(out_dir / name)
        pairs.append(image)
        print(f"  {spot:<28} {verdict}")

    sheet = Image.new("RGB", (pairs[0].width, sum(p.height for p in pairs)), BACKGROUND)
    y = 0
    for image in pairs:
        sheet.paste(image, (0, y))
        y += image.height
    sheet.save(out_dir / "sheet.png")

    before_perf, after_perf = perf(before_dir), perf(after_dir)
    if before_perf and after_perf:
        print("  frame times (ms)        before                     after")
        for key in ("frame", "gpu", "cpu"):
            b, a = before_perf[key], after_perf[key]
            print(f"    {key:<6} avg/p99/worst  {b['avg']:6.2f} {b['p99']:6.2f} {b['worst']:6.2f}"
                  f"       {a['avg']:6.2f} {a['p99']:6.2f} {a['worst']:6.2f}")
    print(f"  sheet: {out_dir / 'sheet.png'}")


if __name__ == "__main__":
    main()
