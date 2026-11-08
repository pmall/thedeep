#!/usr/bin/env python3
"""Doc section ids (AGENTS.md#doc-rules): every id is unique, and every `<file>.md#<id>` citation, in docs or code,
names an id that exists in that file. Run from the project root; tools/check.sh runs it first.
    tools/docrefs.py
An id is an empty anchor `<a id="id"></a>` at the end of a heading, a list item or a table row of a tracked Markdown
file, invisible once rendered; the old backticked form is reported so it never comes back."""

import pathlib
import re
import subprocess
import sys

TAG = re.compile(r'<a id="([a-z0-9-]+)"></a>')
OLD_TAG = re.compile(r'`#([a-z0-9-]+)`')
CITE = re.compile(r'([A-Za-z0-9_./-]+\.md)#([a-z0-9-]+)')
BINARY = {'.png', '.jpg', '.jpeg', '.avif', '.import', '.uid', '.pyc'}


def tracked(*patterns: str) -> list[pathlib.Path]:
    out = subprocess.run(
        ['git', 'ls-files', '-co', '--exclude-standard', *patterns], capture_output=True, text=True, check=True
    )
    return [pathlib.Path(p) for p in out.stdout.split() if pathlib.Path(p).is_file()]


def defines_ids(line: str) -> bool:
    s = line.lstrip()
    return s.startswith(('#', '- ', '* ', '| ', '**')) or s[:1].isdigit()


problems: list[str] = []
ids: dict[str, tuple[pathlib.Path, int]] = {}
for doc in tracked('*.md'):
    for n, line in enumerate(doc.read_text().splitlines(), 1):
        for m in OLD_TAG.finditer(line):
            problems.append(f'{doc}:{n}: id {m.group(0)} is backticked; write it as <a id="{m.group(1)}"></a>')
        if not defines_ids(line):
            continue
        for m in TAG.finditer(line):
            i = m.group(1)
            if i in ids:
                problems.append(f'{doc}:{n}: id #{i} is already defined at {ids[i][0]}:{ids[i][1]}')
            else:
                ids[i] = (doc, n)

for f in tracked():
    if f.suffix in BINARY:
        continue
    try:
        text = f.read_text()
    except UnicodeDecodeError:
        continue
    for n, line in enumerate(text.splitlines(), 1):
        for m in CITE.finditer(line):
            name, i = m.group(1), m.group(2)
            if i not in ids:
                problems.append(f'{f}:{n}: {name}#{i}: no such id')
            elif ids[i][0].name != pathlib.Path(name).name:
                problems.append(f'{f}:{n}: {name}#{i}: the id is in {ids[i][0]}')

for p in problems:
    print(p)
print(f'DOCS {"OK" if not problems else "FAILED"}: {len(ids)} ids, {len(problems)} problems')
sys.exit(1 if problems else 0)
