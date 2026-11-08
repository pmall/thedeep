#!/usr/bin/env python3
"""The stable-image guard (docs/art-direction.md#stable-image): fails on what made a still frame flicker, so the deleted
causes can't come back. Run from the project root; tools/check.sh runs it in the fast and the full check.
    tools/stable_image.py
It first runs its own fixtures (each must be caught, or pass, as written below), then scans the project's files.

Two rules:
- screen-read: a shader that reads a screen buffer of the frame being drawn (`hint_screen_texture`,
  `hint_depth_texture`, `hint_normal_roughness_texture`, or their Godot 3 names), a scene with a BackBufferCopy node,
  or a C# CompositorEffect: each is a pass that decides per pixel from the finished image.
- frame-random: a term that re-rolls with time. In a shader, TIME (or a value computed from it, followed through
  variables, varyings, function arguments and return values) fed to a raw hash (`hash*`, `rand*`, `random*`,
  `fract(sin(...))`) or stepped by `floor`, `ceil`, `round`, `trunc`, `int` or `uint` (a frame counter). In C#, a
  `_Process` or `_PhysicsProcess` that sets a shader parameter and draws from a random source in the same body.
Smooth motion is allowed: TIME through sin, a scroll or the smooth noises of lib/noise.gdshaderinc (they interpolate
between hashed lattice points, so the guard follows TIME into its hashes only at their call sites, never inside that
library). Not caught: a random parameter set every frame from a helper the process method calls, or a high-frequency
smooth noise of TIME; visual shaders are not read.

Shaders are every .gdshader, .gdshaderinc and .glsl file and every Shader or ShaderInclude embedded in a .tscn or .tres.
A file may waive a rule with a comment line `// stable-image: allow <rule>: <reason>`, the reason saying when the
waiver goes; the guard prints every waiver it honors."""

import pathlib
import re
import subprocess
import sys
from collections.abc import Iterator
from dataclasses import dataclass, field

SHADER_SUFFIXES = {'.gdshader', '.gdshaderinc', '.glsl'}
RESOURCE_SUFFIXES = {'.tscn', '.tres'}
NOISE_LIBRARY = 'assets/shaders/lib/noise.gdshaderinc'

SCREEN_READ = re.compile(
    r'\b(hint_screen_texture|hint_depth_texture|hint_normal_roughness_texture|SCREEN_TEXTURE|DEPTH_TEXTURE|'
    r'NORMAL_ROUGHNESS_TEXTURE)\b'
)
WAIVER = re.compile(r'//\s*stable-image:\s*allow\s+(screen-read|frame-random)\s*:\s*(\S.*)')
EMBEDDED = re.compile(r'^code = "((?:[^"\\]|\\.)*)"', re.MULTILINE | re.DOTALL)
BACK_BUFFER = re.compile(r'^\[node [^\]]*type="BackBufferCopy"', re.MULTILINE)
COMPOSITOR = re.compile(r'\bclass\s+\w+\s*(?:<[^>]*>)?\s*:\s*(?:Godot\.)?CompositorEffect\b')
PROCESS = re.compile(r'\boverride\s+void\s+_(?:Physics)?Process\s*\([^)]*\)\s*\{')
SETS_PARAMETER = re.compile(
    r'\b(SetShaderParameter|SetInstanceShaderParameter|GlobalShaderParameterSet|InstanceSetShaderParameter|'
    r'MaterialSetParam)\b|"shader_parameter/'
)
CS_RANDOM = re.compile(
    r'\bGD\.Rand\w*|\.Randf\w*\(|\.Randi\w*\(|\.Next(?:Double|Single|Int64)?\(|\bRandom\.Shared\b|\bnew Random\('
)

HEADER = re.compile(r'\b([A-Za-z_]\w*)\s+([A-Za-z_]\w*)\s*\(([^()]*)\)\s*\{')
KEYWORDS = {'if', 'for', 'while', 'switch', 'return', 'else', 'do'}
ASSIGN = re.compile(r'(?<![\w.])([A-Za-z_]\w*)(?:\.[xyzwrgba]+|\[[^\]]*\])?\s*[+\-*/%]?=(?!=)')
CALL = re.compile(r'\b([A-Za-z_]\w*)\s*\(')
HASH_CALL = re.compile(r'\b(?:hash\w*|rand\w*|random\w*)\s*\(|\bfract\s*\(\s*sin\s*\(')
STEP_CALL = re.compile(r'\b(?:floor|ceil|round|roundEven|trunc|int|uint)\s*\(')
TOP_LEVEL_NAME = re.compile(r'\b(?:varying|uniform|const)\b[^;]*?\b([A-Za-z_]\w*)\s*(?:[:=;\[]|$)')
WORD = re.compile(r'\b[A-Za-z_]\w*\b')


@dataclass
class Shader:
    """One shader's source: where it is and the line its text starts at (embedded code starts mid-file)."""

    path: str
    text: str
    first_line: int = 1
    waivers: dict[str, str] = field(default_factory=dict)


@dataclass
class Function:
    shader: Shader
    name: str
    params: list[str]
    body: str
    body_start: int
    tainted: set[str] = field(default_factory=set)


def strip_comments(text: str) -> str:
    """The text with every comment blanked out, newlines and offsets kept."""

    def blank(m: re.Match[str]) -> str:
        return re.sub(r'[^\n]', ' ', m.group(0))

    return re.sub(r'/\*.*?\*/|//[^\n]*', blank, text, flags=re.DOTALL)


def matching(text: str, start: int, open_: str, close: str) -> int:
    """The offset just past the bracket that closes the one at `start`."""
    depth = 0
    for i in range(start, len(text)):
        if text[i] == open_:
            depth += 1
        elif text[i] == close:
            depth -= 1
            if depth == 0:
                return i + 1
    return len(text)


def split_args(args: str) -> list[str]:
    out, depth, cur = [], 0, ''
    for c in args:
        if c in '([':
            depth += 1
        elif c in ')]':
            depth -= 1
        if c == ',' and depth == 0:
            out.append(cur)
            cur = ''
        else:
            cur += c
    if cur.strip():
        out.append(cur)
    return out


def calls(text: str, pattern: re.Pattern[str]) -> Iterator[tuple[int, str, str]]:
    """Every call matched by `pattern` in `text`: its offset, the callee and its argument text."""
    for m in pattern.finditer(text):
        open_ = m.end() - 1
        yield m.start(), m.group(0), text[open_ + 1 : matching(text, open_, '(', ')') - 1]


def parse(shader: Shader) -> tuple[list[Function], set[str]]:
    """The shader's functions and the names declared at its top level (varyings, uniforms, constants)."""
    text = strip_comments(shader.text)
    depth = [0] * (len(text) + 1)
    d = 0
    for i, c in enumerate(text):
        depth[i] = d
        d += c == '{'
        d -= c == '}'
    functions, top = [], text
    for m in HEADER.finditer(text):
        if depth[m.start()] != 0 or m.group(1) in KEYWORDS or m.group(2) in KEYWORDS:
            continue
        end = matching(text, m.end() - 1, '{', '}')
        params = [WORD.findall(p)[-1] for p in m.group(3).split(',') if WORD.findall(p) and p.strip() != 'void']
        functions.append(Function(shader, m.group(2), params, text[m.end() : end - 1], m.end()))
        top = top[: m.start()] + ' ' * (end - m.start()) + top[end:]
    return functions, set(TOP_LEVEL_NAME.findall(top))


def line_of(shader: Shader, offset: int) -> int:
    return shader.first_line + shader.text.count('\n', 0, offset)


def screen_reads(shader: Shader) -> Iterator[tuple[int, str]]:
    text = strip_comments(shader.text)
    for m in SCREEN_READ.finditer(text):
        yield line_of(shader, m.start()), f'reads a screen buffer ({m.group(1)})'


def frame_randoms(shaders: list[Shader]) -> Iterator[tuple[Shader, int, str]]:
    parsed = [parse(s) for s in shaders]
    functions = [f for fs, _ in parsed for f in fs]
    globals_of = {id(s): names for s, (_, names) in zip(shaders, parsed, strict=True)}
    by_name: dict[str, list[Function]] = {}
    for f in functions:
        by_name.setdefault(f.name, []).append(f)
    tainted_globals: dict[int, set[str]] = {id(s): {'TIME'} for s in shaders}
    time_functions: set[str] = set()

    def is_tainted(f: Function, expr: str) -> bool:
        names = f.tainted | tainted_globals[id(f.shader)]
        return any(w in names for w in WORD.findall(expr)) or any(
            callee.rstrip('( ') in time_functions for _, callee, _ in calls(expr, CALL)
        )

    def entered(f: Function) -> bool:
        """Whether the guard follows time into this function: never into the smooth noise library or a hash."""
        return f.shader.path != NOISE_LIBRARY and not HASH_CALL.match(f.name + '(')

    changed = True
    while changed:
        changed = False
        for f in functions:
            if not entered(f):
                continue
            for piece in re.split(r'[;{}]', f.body):
                for m in ASSIGN.finditer(piece):
                    name = m.group(1)
                    if is_tainted(f, piece[m.end() :]):
                        scope = (
                            tainted_globals[id(f.shader)]
                            if name in globals_of[id(f.shader)] and name not in f.params
                            else f.tainted
                        )
                        if name not in scope:
                            scope.add(name)
                            changed = True
                ret = re.match(r'\s*return\b(.*)', piece, re.DOTALL)
                if ret and f.name not in time_functions and is_tainted(f, ret.group(1)):
                    time_functions.add(f.name)
                    changed = True
            for _, callee, args in calls(f.body, CALL):
                arguments = split_args(args)
                for g in by_name.get(callee.rstrip('( '), []):
                    if not entered(g) or len(g.params) != len(arguments):
                        continue
                    for param, arg in zip(g.params, arguments, strict=True):
                        if param not in g.tainted and is_tainted(f, arg):
                            g.tainted.add(param)
                            changed = True
    for f in functions:
        if not entered(f):
            continue
        for pattern, what in ((HASH_CALL, 'hashes'), (STEP_CALL, 'steps')):
            for offset, callee, args in calls(f.body, pattern):
                if is_tainted(f, args):
                    call = callee.rstrip('( ').replace(' ', '')
                    yield f.shader, line_of(f.shader, f.body_start + offset), f'{call}() {what} TIME in {f.name}()'


def cs_problems(path: str, text: str) -> Iterator[tuple[str, int, str]]:
    text = strip_comments(text)
    for m in COMPOSITOR.finditer(text):
        yield 'screen-read', text.count('\n', 0, m.start()) + 1, 'a CompositorEffect is a pass over the finished image'
    for m in PROCESS.finditer(text):
        body = text[m.end() : matching(text, m.end() - 1, '{', '}')]
        if SETS_PARAMETER.search(body) and CS_RANDOM.search(body):
            yield (
                'frame-random',
                text.count('\n', 0, m.start()) + 1,
                'sets a shader parameter from a random source every frame',
            )


def scan(files: dict[str, str]) -> tuple[list[str], list[str]]:
    """The problems in the given files (path to text), and the waivers honored."""
    shaders: list[Shader] = []
    found: list[tuple[str, str, int, str]] = []
    for path, text in sorted(files.items()):
        suffix = pathlib.PurePath(path).suffix
        if suffix in SHADER_SUFFIXES:
            shaders.append(Shader(path, text))
        elif suffix in RESOURCE_SUFFIXES:
            for m in EMBEDDED.finditer(text):
                code = m.group(1).replace('\\"', '"').replace('\\\\', '\\')
                shaders.append(Shader(path, code, text.count('\n', 0, m.start(1)) + 1))
            for m in BACK_BUFFER.finditer(text):
                found.append(('screen-read', path, text.count('\n', 0, m.start()) + 1, 'a BackBufferCopy node'))
        elif suffix == '.cs':
            found += [(rule, path, line, msg) for rule, line, msg in cs_problems(path, text)]
    for s in shaders:
        s.waivers = {m.group(1): m.group(2).strip() for m in WAIVER.finditer(s.text)}
        found += [('screen-read', s.path, line, msg) for line, msg in screen_reads(s)]
    found += [('frame-random', s.path, line, msg) for s, line, msg in frame_randoms(shaders)]
    waived_rules = {(s.path, rule): reason for s in shaders for rule, reason in s.waivers.items()}
    problems, waivers = [], set()
    for rule, path, line, msg in found:
        if (path, rule) in waived_rules:
            waivers.add(f'{path}: {rule} waived: {waived_rules[path, rule]}')
        else:
            problems.append(f'{path}:{line}: {rule}: {msg}')
    return problems, sorted(waivers)


# The fixtures: (files, the rules the guard must report, in order). Each is scanned alone.
FIXTURES: list[tuple[dict[str, str], list[str]]] = [
    ({'a.gdshader': 'uniform sampler2D s : hint_screen_texture;'}, ['screen-read']),
    ({'a.gdshader': 'uniform sampler2D d : hint_depth_texture, filter_nearest;'}, ['screen-read']),
    (
        {
            'a.gdshader': '// stable-image: allow screen-read: until the water is a material\n'
            'uniform sampler2D s : hint_screen_texture;'
        },
        [],
    ),
    ({'a.gdshader': '// a comment naming hint_screen_texture\nvoid fragment() { ALBEDO = vec3(sin(TIME)); }'}, []),
    (
        {
            'a.tscn': '[sub_resource type="Shader" id="1"]\ncode = "shader_type canvas_item;\nuniform sampler2D s : '
            'hint_screen_texture;\n"\n'
        },
        ['screen-read'],
    ),
    ({'a.tscn': '[node name="Copy" type="BackBufferCopy" parent="."]\n'}, ['screen-read']),
    ({'A.cs': 'public partial class Paint : CompositorEffect { }'}, ['screen-read']),
    # The deleted film grain: a frame counter from TIME, hashed with the pixel.
    (
        {
            'a.gdshader': 'void fragment() {\n\tfloat frame = floor(TIME * 24.0);\n'
            '\tfloat n = hash12(FRAGCOORD.xy + vec2(frame * 37.0));\n\tALBEDO = vec3(n);\n}'
        },
        ['frame-random', 'frame-random'],
    ),
    ({'a.gdshader': 'void fragment() { ALBEDO = vec3(hash12(UV + TIME)); }'}, ['frame-random']),
    (
        {'a.gdshader': 'void fragment() { ALBEDO = vec3(fract(sin(dot(UV, vec2(12.9, 78.2)) + TIME) * 43758.5)); }'},
        ['frame-random'],
    ),
    # Through a helper's argument, a varying and a function returning time.
    (
        {
            'a.gdshader': 'float grain(vec2 p, float t) { return hash12(p * t); }\n'
            'void fragment() { ALBEDO = vec3(grain(UV, TIME * 2.0)); }'
        },
        ['frame-random'],
    ),
    (
        {
            'a.gdshader': 'varying float v_t;\nvoid vertex() { v_t = TIME; }\n'
            'void fragment() { ALBEDO = vec3(hash12(UV * v_t)); }'
        },
        ['frame-random'],
    ),
    (
        {
            'a.gdshader': 'float now() { return TIME * 3.0; }\n'
            'void fragment() { float k = now(); ALBEDO = vec3(hash12(UV + k)); }'
        },
        ['frame-random'],
    ),
    # Smooth motion: sin of TIME, smooth noise scrolled by TIME, a hash of space alone.
    (
        {
            'lib/noise.gdshaderinc': 'float value_noise2(vec2 p) { vec2 i = floor(p); return hash12(i); }',
            'a.gdshader': 'float ripple(vec2 p, float t) { return value_noise2(p + vec2(t * 0.6, 0.0)); }\n'
            'void fragment() {\n\tfloat t = TIME * 0.2;\n\tfloat h = ripple(UV, t) + hash12(floor(UV * 8.0));\n'
            '\tALBEDO = vec3(h * (0.5 + 0.5 * sin(TIME)));\n}',
        },
        [],
    ),
    (
        {
            'A.cs': 'class A { public override void _Process(double delta) { _m.SetShaderParameter(Seed, GD.Randf()); } }'
        },
        ['frame-random'],
    ),
    (
        {
            'A.cs': 'class A { public override void _Ready() { _m.SetShaderParameter(Seed, GD.Randf()); }\n'
            'public override void _Process(double delta) { _m.SetShaderParameter(Hurt, _flash); } }'
        },
        [],
    ),
]


def self_test() -> list[str]:
    failures = []
    for i, (files, expected) in enumerate(FIXTURES):
        files = {p.replace('lib/noise.gdshaderinc', NOISE_LIBRARY): t for p, t in files.items()}
        problems, _ = scan(files)
        rules = [p.split(': ')[1] for p in problems]
        if rules != expected:
            failures.append(f'fixture {i + 1}: expected {expected or "nothing"}, got {problems or "nothing"}')
    return failures


def project_files() -> dict[str, str]:
    out = subprocess.run(['git', 'ls-files', '-co', '--exclude-standard'], capture_output=True, text=True, check=True)
    wanted = SHADER_SUFFIXES | RESOURCE_SUFFIXES | {'.cs'}
    paths = [pathlib.Path(p) for p in out.stdout.splitlines()]
    return {str(p): p.read_text(encoding='utf-8') for p in paths if p.suffix in wanted and p.is_file()}


def main() -> int:
    failures = self_test()
    if failures:
        print('stable-image guard: its own fixtures fail:', *failures, sep='\n  ')
        return 1
    problems, waivers = scan(project_files())
    for w in waivers:
        print(w)
    if problems:
        print(*problems, sep='\n')
        print(f'stable-image guard: {len(problems)} problem(s) (docs/art-direction.md#stable-image)')
        return 1
    print('stable-image guard: ok')
    return 0


if __name__ == '__main__':
    sys.exit(main())
