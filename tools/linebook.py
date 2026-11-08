"""Adds lines to a line book (the prototype's, worlds/prototype/lines.tres, unless `path=` names another: a world's, or the game's own,
resources/lines.tres, for lines every world shares) from a throwaway script, or replaces lines with the same id.

    import sys; sys.path.insert(0, 'tools'); from linebook import add
    add([('hint.gate.metal', 'UI', 'Bare metal ...', 'Notes ...')])

Each line is (id, speaker, text, notes). The sub-resource id is the line id with dots as underscores.
"""

import re

PATH = 'worlds/prototype/lines.tres'


def _quote(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'


def add(lines, path=PATH):
    text = open(path).read()
    head, res = text.split('\n[resource]\n')
    for line_id, speaker, words, notes in lines:
        sub = line_id.replace('.', '_')
        block = (
            f'[sub_resource type="Resource" id="{sub}"]\nscript = ExtResource("2_line")\nId = &"{line_id}"\n'
            f'Speaker = {_quote(speaker)}\nText = {_quote(words)}\nNotes = {_quote(notes)}\n'
        )
        existing = re.search(
            r'\[sub_resource type="Resource" id="[^"]+"\]\nscript = ExtResource\("2_line"\)\nId = &"'
            + re.escape(line_id)
            + r'"\n(?:[A-Za-z]+ = .*\n)*',
            head,
        )
        if existing:
            head = head[: existing.start()] + block + head[existing.end() :]
            continue
        head = head.rstrip('\n') + '\n\n' + block
        res = res.replace(']\n', f', SubResource("{sub}")]\n', 1) if 'Lines = [' in res else res
    open(path, 'w').write(head.rstrip('\n') + '\n\n[resource]\n' + res)
