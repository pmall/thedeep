"""Generates a placeholder body from another one, scaled (a boss placeholder from a smaller machine's assembly):

    python3 tools/scaleparts.py resources/parts/archivist.tres resources/parts/nirvanary.tres 1.5 "header line" ["old=new" ...]

Joint and part offsets and every mesh dimension are multiplied; `old=new` pairs swap text (a material path). The header
replaces the source's leading comment. The generated .tres is the source of truth from then on.
"""
import re
import sys


def fmt(v):
    r = round(v, 4)
    return str(int(r)) if r == int(r) else repr(r)


def scale_parts(src_path, dst_path, s, header, swaps):
    out = []
    for line in open(src_path).read().split('\n'):
        k = line.split(' = ')[0] if ' = ' in line else ''
        if k in ('Offset', 'Position') and 'Vector3(' in line:
            line = re.sub(r'(Vector3)\(([^)]*)\)', lambda m: f"{m.group(1)}({', '.join(fmt(float(n) * s) for n in m.group(2).split(','))})", line)
        elif k in ('Length', 'Width', 'Thickness', 'Bevel', 'RadiusX', 'RadiusZ', 'Height', 'Radius', 'HubRadius', 'HubHeight', 'Chord'):
            try:
                line = f"{k} = {fmt(float(line.split(' = ')[1]) * s)}"
            except ValueError:
                pass
        elif k == 'Stations':
            nums = [float(x) for x in re.search(r'\(([^)]*)\)', line).group(1).split(',')]
            line = f"Stations = PackedVector4Array({', '.join(fmt(n if i % 4 == 0 else n * s) for i, n in enumerate(nums))})"
        out.append(line)
    i = 1
    while i < len(out) and out[i].startswith(';'):
        i += 1
    text = '\n'.join([out[0]] + [f'; {h}' for h in header] + out[i:])
    for a, b in swaps:
        text = text.replace(a, b)
    open(dst_path, 'w').write(text)


if __name__ == '__main__':
    src, dst, scale, header = sys.argv[1:5]
    scale_parts(src, dst, float(scale), header.split('\n'), [tuple(p.split('=', 1)) for p in sys.argv[5:]])
