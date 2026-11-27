#!/usr/bin/env python3
"""Scaffolds a seam-aligned room .tscn from a world's network and room table (instructions/testing.md#seam-brick).

Run from the project root:
    tools/scaffold_room.py <world> <room> [--width W] [--height H] [--exits SPEC] [--write] [--stitch] [--force]
    tools/scaffold_room.py --self-test

Reads worlds/<world>/world.tres, its network markdown (```map blocks) and optional rooms.tsv, inspects built
neighbour rooms to derive opposite seam sides and exit names, and emits a grid-snapped (0.5 m) room scene:
- Doorway.tscn / Hatch.tscn seam bricks with doorway.tres / hatch.tres (docs/gameplay.md#seam-rule)
- Floor, ceiling and closed side walls pre-trimmed out of every seam frame's span (zero GroundRules intrusions)
- BoundsMin / BoundsMax aligned to outer seam faces, RoomStage with a deterministic seed, solid SpawnPoint,
  draft props (relics, power-ups, items, shrines) and in-bounds PhotoSpots
- With --stitch (and --write), wires matching sealed exits in already-built neighbour rooms back to the new room.

Prints the generated .tscn to stdout unless --write is passed.
"""

from __future__ import annotations

import argparse
import dataclasses
import hashlib
import pathlib
import re
import sys
import tempfile
from typing import Final

ROOT: Final[pathlib.Path] = pathlib.Path(__file__).resolve().parent.parent

GRID_STEP: Final[float] = 0.5
WALL_THICKNESS: Final[float] = 3.0
SLAB_THICKNESS: Final[float] = 2.0
BLOCK_DEPTH: Final[float] = 4.0

# Doorway (Opening=4, Approach=1, Around=2) -> frame spans marker.x +- 2.5, y -2..6
DOORWAY_HALF_SPAN: Final[float] = 2.5
DOORWAY_OPENING: Final[float] = 4.0
DOORWAY_WALL_MARGIN: Final[float] = 2.0
DOORWAY_STACK_STEP: Final[float] = 8.0

# Hatch (Opening=3, Approach=2, Around=2) -> frame spans marker.x +- 3.5, marker.y +- 1
HATCH_HALF_SPAN: Final[float] = 3.5
HATCH_HOLE_HALF: Final[float] = 1.5
HATCH_HALF_THICK: Final[float] = 1.0

SIDE_CODES: Final[dict[str, int]] = {'west': 0, 'east': 1, 'down': 2, 'up': 3}
CODE_SIDES: Final[dict[int, str]] = {v: k for k, v in SIDE_CODES.items()}
OPPOSITE_SIDE: Final[dict[str, str]] = {'west': 'east', 'east': 'west', 'down': 'up', 'up': 'down'}
DEFAULT_EXIT_NAME: Final[dict[str, str]] = {'west': 'West', 'east': 'East', 'down': 'Down', 'up': 'Up'}

ABILITY_ENUM: Final[dict[str, tuple[int, str]]] = {
    'MagGrip': (1, 'mag_grip'),
    'Harpoon': (2, 'harpoon'),
    'Drill': (4, 'drill'),
    'Ballast': (8, 'ballast'),
    'HeatPlating': (16, 'heat_plating'),
}

POWER_STAT_ENUM: Final[dict[str, int]] = {'strength': 0, 'defense': 1}


@dataclasses.dataclass(frozen=True)
class WorldConfig:
    """Parsed metadata from worlds/<world>/world.tres."""

    name: str
    rooms_folder: str
    network_path: pathlib.Path
    room_table_path: pathlib.Path | None
    layer_biomes: dict[str, str]
    item_resources: dict[str, str]


@dataclasses.dataclass(frozen=True)
class Way:
    """A directed edge from a network's ```map block."""

    source: str
    target: str
    requires: str


@dataclasses.dataclass(frozen=True)
class NetworkDraft:
    """Parsed statements from a world's network markdown."""

    rooms: dict[str, str]
    ways: list[Way]
    relics: list[tuple[str, str]]
    items: list[tuple[str, str]]
    powerups: list[tuple[str, str]]
    goals: set[str]
    bosses: list[tuple[str, str]]
    ambushes: set[str]
    hidden: set[tuple[str, str]]

    def is_hidden(self, a: str, b: str) -> bool:
        return (a, b) in self.hidden or (b, a) in self.hidden


@dataclasses.dataclass(frozen=True)
class RoomMeta:
    """One row from rooms.tsv."""

    kinds: list[str]
    happens: str
    machines: str
    items: str
    notes: str


@dataclasses.dataclass(frozen=True)
class NeighborExit:
    """An exit marker parsed from an already-built neighbour room scene."""

    room: str
    scene_path: pathlib.Path
    name: str
    side: str
    target_room: str
    target_exit: str
    requires: str
    one_way: bool
    arrival_only: bool


@dataclasses.dataclass(frozen=True)
class ExitSpec:
    """A placed exit in the scaffolded room."""

    name: str
    side: str
    x: float
    y: float
    target_room: str
    target_exit: str
    requires: str
    one_way: bool
    arrival_only: bool
    hidden: bool
    arrival_side: int


@dataclasses.dataclass(frozen=True)
class BlockSpec:
    """A grid-snapped Block.tscn instance on the play plane."""

    name: str
    min_x: float
    min_y: float
    max_x: float
    max_y: float
    material: str | None = None

    @property
    def center_x(self) -> float:
        return 0.5 * (self.min_x + self.max_x)

    @property
    def center_y(self) -> float:
        return 0.5 * (self.min_y + self.max_y)

    @property
    def width(self) -> float:
        return self.max_x - self.min_x

    @property
    def height(self) -> float:
        return self.max_y - self.min_y


@dataclasses.dataclass(frozen=True)
class ScaffoldPlan:
    """Complete geometric plan for a scaffolded room before .tscn rendering."""

    world: WorldConfig
    room: str
    layer: str
    biome: str
    width: float
    height: float
    bounds_min: tuple[float, float]
    bounds_max: tuple[float, float]
    spawn: tuple[float, float]
    seed: int
    blocks: list[BlockSpec]
    exits: list[ExitSpec]
    relics: list[str]
    powerups: list[str]
    items: list[str]
    has_shrine: bool
    photo_spots: list[tuple[str, tuple[float, float, float], tuple[float, float], int]]
    comment_lines: list[str]


def res_to_path(res_path: str, root: pathlib.Path = ROOT) -> pathlib.Path:
    """Converts a Godot res:// path to a filesystem path under root."""
    if not res_path.startswith('res://'):
        raise ValueError(f'Expected res:// path, got {res_path!r}')
    return root / res_path[len('res://') :]


def snap(value: float) -> float:
    """Snaps a coordinate to the 0.5 m block grid."""
    return round(value / GRID_STEP) * GRID_STEP


def fmt_num(value: float) -> str:
    """Formats a float cleanly for Godot .tscn output (no trailing .0)."""
    if abs(value - round(value)) < 1e-6:
        return str(round(value))
    text = f'{value:.4f}'.rstrip('0').rstrip('.')
    return '0' if text == '-0' else text


def parse_world(world_name: str, root: pathlib.Path = ROOT) -> WorldConfig:
    """Parses worlds/<world_name>/world.tres."""
    tres = root / 'worlds' / world_name / 'world.tres'
    if not tres.is_file():
        raise FileNotFoundError(f'World definition not found: {tres}')
    text = tres.read_text(encoding='utf-8')

    ext_paths: dict[str, str] = {}
    for m in re.finditer(r'\[ext_resource\b[^\]]*path="([^"]+)"[^\]]*id="([^"]+)"', text):
        ext_paths[m.group(2)] = m.group(1)

    layer_biomes: dict[str, str] = {}
    for chunk in text.split('\n['):
        if not chunk.startswith('sub_resource'):
            continue
        name_m = re.search(r'\nName = "([^"]+)"', chunk)
        biome_m = re.search(r'\nBiome = ExtResource\("([^"]+)"\)', chunk)
        if name_m and biome_m and biome_m.group(1) in ext_paths:
            layer_biomes[name_m.group(1)] = ext_paths[biome_m.group(1)]

    rooms_m = re.search(r'\nRoomsFolder = "([^"]+)"', text)
    network_m = re.search(r'\nNetwork = "([^"]+)"', text)
    table_m = re.search(r'\nRoomTable = "([^"]+)"', text)
    if not rooms_m or not network_m:
        raise ValueError(f'{tres}: missing RoomsFolder or Network')

    item_resources: dict[str, str] = {}
    items_m = re.search(r'\nItems = \[([^\]]*)\]', text)
    if items_m:
        for ext_id in re.findall(r'ExtResource\("([^"]+)"\)', items_m.group(1)):
            res_path = ext_paths.get(ext_id)
            if not res_path:
                continue
            item_file = res_to_path(res_path, root)
            if item_file.is_file():
                id_m = re.search(r'\nId = "([^"]+)"', item_file.read_text(encoding='utf-8'))
                if id_m:
                    item_resources[id_m.group(1)] = res_path

    table_path = res_to_path(table_m.group(1), root) if table_m else None
    return WorldConfig(
        name=world_name,
        rooms_folder=rooms_m.group(1).rstrip('/'),
        network_path=res_to_path(network_m.group(1), root),
        room_table_path=table_path,
        layer_biomes=layer_biomes,
        item_resources=item_resources,
    )


def parse_network(path: pathlib.Path) -> NetworkDraft:
    """Parses ```map fenced blocks from a world's network markdown (matching MapDraft)."""
    rooms: dict[str, str] = {}
    ways: list[Way] = []
    relics: list[tuple[str, str]] = []
    items: list[tuple[str, str]] = []
    powerups: list[tuple[str, str]] = []
    goals: set[str] = set()
    bosses: list[tuple[str, str]] = []
    ambushes: set[str] = set()
    hidden: set[tuple[str, str]] = set()

    inside = False
    for raw_line in path.read_text(encoding='utf-8').splitlines():
        trimmed = raw_line.strip()
        if trimmed.startswith('```'):
            inside = (not inside) and trimmed[3:].strip() == 'map'
            continue
        if not inside:
            continue
        line = raw_line.split('#', 1)[0].strip()
        if not line:
            continue
        parts = line.split(None, 1)
        cmd = parts[0]
        rest = parts[1].strip() if len(parts) > 1 else ''
        args = rest.split()
        if cmd == 'room' and len(args) == 2:
            rooms[args[0]] = args[1]
        elif cmd == 'way':
            requires = ''
            if ':' in rest:
                rest, requires = (s.strip() for s in rest.split(':', 1))
            both = '<->' in rest
            sep = '<->' if both else '->'
            ends = [s.strip() for s in rest.split(sep)]
            if len(ends) == 2:
                ways.append(Way(ends[0], ends[1], requires))
                if both:
                    ways.append(Way(ends[1], ends[0], requires))
        elif cmd == 'relic' and len(args) == 2:
            relics.append((args[0], args[1]))
        elif cmd == 'item' and len(args) == 2:
            items.append((args[0], args[1]))
        elif cmd == 'powerup' and len(args) == 2:
            powerups.append((args[0], args[1].lower()))
        elif cmd == 'goal' and len(args) == 1:
            goals.add(args[0])
        elif cmd == 'boss' and len(args) == 2:
            bosses.append((args[0], args[1]))
        elif cmd == 'ambush' and len(args) == 1:
            ambushes.add(args[0])
        elif cmd == 'hidden' and len(args) == 2:
            hidden.add((args[0], args[1]))

    return NetworkDraft(rooms, ways, relics, items, powerups, goals, bosses, ambushes, hidden)


def parse_room_table(path: pathlib.Path | None) -> dict[str, RoomMeta]:
    """Parses rooms.tsv when present."""
    if path is None or not path.is_file():
        return {}
    rows: dict[str, RoomMeta] = {}
    header: list[str] | None = None
    for raw in path.read_text(encoding='utf-8').splitlines():
        if not raw.strip() or raw.lstrip().startswith('#'):
            continue
        cols = raw.split('\t')
        if header is None:
            header = [c.strip() for c in cols]
            continue
        data = {header[i]: cols[i].strip() if i < len(cols) else '' for i in range(len(header))}
        room = data.get('room', '')
        if not room:
            continue
        kinds = [k.strip() for k in data.get('kinds', '').split(',') if k.strip()]
        rows[room] = RoomMeta(
            kinds=kinds,
            happens=data.get('happens', ''),
            machines=data.get('machines', ''),
            items=data.get('items', ''),
            notes=data.get('notes', ''),
        )
    return rows


def scan_neighbor_exits(rooms_dir: pathlib.Path) -> dict[str, list[NeighborExit]]:
    """Reads all RoomExit markers from existing .tscn files in rooms_dir."""
    by_room: dict[str, list[NeighborExit]] = {}
    if not rooms_dir.is_dir():
        return by_room
    for scene in sorted(rooms_dir.glob('*.tscn')):
        text = scene.read_text(encoding='utf-8')
        exit_ids = {
            m.group(1)
            for m in re.finditer(
                r'\[ext_resource\b[^\]]*path="res://scripts/rooms/RoomExit\.cs"[^\]]*id="([^"]+)"', text
            )
        }
        if not exit_ids:
            continue
        found: list[NeighborExit] = []
        for chunk in text.split('\n['):
            head = re.match(r'node name="([^"]+)"[^\n]*parent="Exits"', chunk)
            if not head:
                continue
            script_m = re.search(r'\nscript = ExtResource\("([^"]+)"\)', chunk)
            if not script_m or script_m.group(1) not in exit_ids:
                continue
            side_m = re.search(r'\nSide = (\d+)', chunk)
            side = CODE_SIDES.get(int(side_m.group(1)) if side_m else 0, 'west')
            target_room_m = re.search(r'\nTargetRoom = "([^"]*)"', chunk)
            target_exit_m = re.search(r'\nTargetExit = "([^"]*)"', chunk)
            req_m = re.search(r'\nRequires = "([^"]*)"', chunk)
            one_way = bool(re.search(r'\nOneWay = true\b', chunk))
            arrival_only = bool(re.search(r'\nArrivalOnly = true\b', chunk))
            found.append(
                NeighborExit(
                    room=scene.stem,
                    scene_path=scene,
                    name=head.group(1),
                    side=side,
                    target_room=target_room_m.group(1) if target_room_m else '',
                    target_exit=target_exit_m.group(1) if target_exit_m else '',
                    requires=req_m.group(1) if req_m else '',
                    one_way=one_way,
                    arrival_only=arrival_only,
                )
            )
        by_room[scene.stem] = found
    return by_room


def subtract_intervals(base: tuple[float, float], holes: list[tuple[float, float]]) -> list[tuple[float, float]]:
    """Subtracts a list of [lo, hi] intervals from base, returning positive-width pieces."""
    pieces: list[tuple[float, float]] = [base]
    for h_lo, h_hi in sorted(holes):
        next_pieces: list[tuple[float, float]] = []
        for lo, hi in pieces:
            if h_hi <= lo + 1e-6 or h_lo >= hi - 1e-6:
                next_pieces.append((lo, hi))
                continue
            if h_lo > lo + 1e-6:
                next_pieces.append((lo, h_lo))
            if h_hi < hi - 1e-6:
                next_pieces.append((h_hi, hi))
        pieces = next_pieces
    return pieces


def parse_exits_arg(spec: str) -> list[tuple[str, str, str]]:
    """Parses --exits 'west:Drydock:East,east:Quay:West' into (side, target, target_exit)."""
    result: list[tuple[str, str, str]] = []
    for raw in spec.split(','):
        token = raw.strip()
        if not token:
            continue
        parts = token.split(':')
        if len(parts) not in (2, 3):
            raise ValueError(f'Invalid --exits entry {token!r}: expected side:Target[:TargetExit]')
        side = parts[0].strip().lower()
        if side not in SIDE_CODES:
            raise ValueError(f'Unknown side {side!r} in {token!r}; use west, east, down or up')
        target = parts[1].strip()
        target_exit = (
            parts[2].strip() if len(parts) == 3 and parts[2].strip() else DEFAULT_EXIT_NAME[OPPOSITE_SIDE[side]]
        )
        result.append((side, target, target_exit))
    return result


def choose_exit_name(side: str, target: str, used: set[str], preferred: str = '') -> str:
    """Picks a unique Godot node name for an exit."""
    if preferred and preferred not in used:
        return preferred
    if side in ('down', 'up') and target and target not in used:
        return target
    base = DEFAULT_EXIT_NAME[side]
    if base not in used:
        return base
    if target and target not in used:
        return target
    idx = 2
    while f'{base}{idx}' in used:
        idx += 1
    return f'{base}{idx}'


def build_plan(
    world: WorldConfig,
    draft: NetworkDraft,
    table: dict[str, RoomMeta],
    neighbors: dict[str, list[NeighborExit]],
    room: str,
    width: float,
    height: float,
    explicit_exits: list[tuple[str, str, str]] | None = None,
) -> ScaffoldPlan:
    """Computes a seam-clean geometric scaffold for one room."""
    if room not in draft.rooms:
        raise ValueError(f'Room {room!r} is not declared in {world.network_path}')
    layer = draft.rooms[room]
    biome = world.layer_biomes.get(layer)
    if not biome:
        raise ValueError(f'Layer {layer!r} has no Biome in worlds/{world.name}/world.tres')

    width = snap(width)
    height = snap(height)
    if width < 12.0:
        raise ValueError(f'Room width must be at least 12 m (got {width})')
    if height < 8.0:
        raise ValueError(f'Room height must be at least 8 m (got {height})')

    outgoing: dict[str, str] = {}
    incoming: dict[str, str] = {}
    neighbor_order: list[str] = []
    for w in draft.ways:
        if w.source == room:
            outgoing[w.target] = w.requires
            if w.target not in neighbor_order:
                neighbor_order.append(w.target)
        elif w.target == room:
            incoming[w.source] = w.requires
            if w.source not in neighbor_order:
                neighbor_order.append(w.source)

    # Resolve each connection's side, target_exit, preferred exit name, and flags.
    raw_exits: list[tuple[str, str, str, str, str, bool, bool, bool]] = []
    room_res = f'{world.rooms_folder}/{room}.tscn'

    if explicit_exits is not None:
        for side, target, target_exit in explicit_exits:
            has_out = target in outgoing
            has_in = target in incoming
            req = outgoing.get(target, '')
            one_way = has_out and not has_in
            arrival_only = has_in and not has_out
            hidden = draft.is_hidden(room, target)
            raw_exits.append((side, target, target_exit, '', req, one_way, arrival_only, hidden))
    else:
        used_sides: set[str] = set()
        resolved: dict[str, tuple[str, str, str]] = {}
        # First pass: derive exact opposite side and exit name from already-built neighbour rooms.
        for target in neighbor_order:
            target_exits = neighbors.get(target, [])
            direct = [e for e in target_exits if e.target_room == room_res]
            if direct:
                match = direct[0]
                side = OPPOSITE_SIDE[match.side]
                resolved[target] = (side, match.name, match.target_exit)
                used_sides.add(side)
                continue
            sealed = [e for e in target_exits if e.target_room == '' and not e.arrival_only]
            if len(sealed) == 1:
                match = sealed[0]
                side = OPPOSITE_SIDE[match.side]
                resolved[target] = (side, match.name, '')
                used_sides.add(side)

        # Second pass: assign remaining neighbours to unused sides in natural order.
        fallback_order = ['west', 'east', 'down', 'up']
        for target in neighbor_order:
            if target in resolved:
                continue
            pick = next((s for s in fallback_order if s not in used_sides), 'east')
            used_sides.add(pick)
            resolved[target] = (pick, DEFAULT_EXIT_NAME[OPPOSITE_SIDE[pick]], '')

        for target in neighbor_order:
            side, target_exit, preferred = resolved[target]
            has_out = target in outgoing
            has_in = target in incoming
            req = outgoing.get(target, '')
            one_way = has_out and not has_in
            arrival_only = has_in and not has_out
            hidden = draft.is_hidden(room, target)
            raw_exits.append((side, target, target_exit, preferred, req, one_way, arrival_only, hidden))

    # Count exits per side so stacked doorways or multiple hatches expand room dimensions cleanly.
    by_side: dict[str, list[tuple[str, str, str, str, str, bool, bool, bool]]] = {s: [] for s in SIDE_CODES}
    for entry in raw_exits:
        by_side[entry[0]].append(entry)

    max_horizontal_stack = max(len(by_side['west']), len(by_side['east']), 1)
    min_height = snap(max(height, (max_horizontal_stack - 1) * DOORWAY_STACK_STEP + 10.0))
    height = max(height, min_height)

    max_hatches = max(len(by_side['down']), len(by_side['up']), 0)
    if max_hatches > 0:
        # Leave 2.5 m doorway frame clearance on both sides + 8 m per hatch.
        min_width = snap(max(width, 6.0 + max_hatches * 9.0))
        width = max(width, min_width)

    exits: list[ExitSpec] = []
    used_names: set[str] = set()

    def build_target_room(target: str, arrival_only: bool) -> str:
        if arrival_only or not target:
            return ''
        target_scene = res_to_path(f'{world.rooms_folder}/{target}.tscn')
        return f'{world.rooms_folder}/{target}.tscn' if target_scene.is_file() else ''

    for side in ('west', 'east'):
        for idx, (_, target, target_exit, preferred, req, one_way, arrival_only, hidden) in enumerate(by_side[side]):
            name = choose_exit_name(side, target, used_names, preferred)
            used_names.add(name)
            mx = -1.0 if side == 'west' else width + 1.0
            my = snap(idx * DOORWAY_STACK_STEP)
            t_room = build_target_room(target, arrival_only)
            exits.append(
                ExitSpec(
                    name=name,
                    side=side,
                    x=mx,
                    y=my,
                    target_room=t_room,
                    target_exit=target_exit if t_room else '',
                    requires=req,
                    one_way=one_way,
                    arrival_only=arrival_only,
                    hidden=hidden,
                    arrival_side=0,
                )
            )

    for side in ('down', 'up'):
        entries = by_side[side]
        if not entries:
            continue
        # Distribute hatches cleanly inside [5.5, width - 5.5] so frames never touch doorways or walls.
        inner_lo = 5.5
        inner_hi = width - 5.5
        span = inner_hi - inner_lo
        for idx, (_, target, target_exit, preferred, req, one_way, arrival_only, hidden) in enumerate(entries):
            name = choose_exit_name(side, target, used_names, preferred)
            used_names.add(name)
            frac = (idx + 1) / (len(entries) + 1)
            mx = snap(inner_lo + span * frac)
            my = -HATCH_HALF_THICK if side == 'down' else height + HATCH_HALF_THICK
            t_room = build_target_room(target, arrival_only)
            exits.append(
                ExitSpec(
                    name=name,
                    side=side,
                    x=mx,
                    y=my,
                    target_room=t_room,
                    target_exit=target_exit if t_room else '',
                    requires=req,
                    one_way=one_way,
                    arrival_only=arrival_only,
                    hidden=hidden,
                    arrival_side=-1 if side == 'down' else 0,
                )
            )

    # Trim floor [-WALL_THICKNESS, width + WALL_THICKNESS] around ground doorways and down hatches.
    floor_holes: list[tuple[float, float]] = []
    for e in exits:
        if e.side in ('west', 'east') and abs(e.y) < 1e-6:
            floor_holes.append((e.x - DOORWAY_HALF_SPAN, e.x + DOORWAY_HALF_SPAN))
        elif e.side == 'down':
            floor_holes.append((e.x - HATCH_HALF_SPAN, e.x + HATCH_HALF_SPAN))

    floor_spans = subtract_intervals((-WALL_THICKNESS, width + WALL_THICKNESS), floor_holes)
    blocks: list[BlockSpec] = []
    for idx, (lo, hi) in enumerate(floor_spans):
        name = 'Floor' if len(floor_spans) == 1 else f'Floor{idx + 1}'
        blocks.append(BlockSpec(name, snap(lo), -SLAB_THICKNESS, snap(hi), 0.0, 'wet_stone'))

    # Trim ceiling [-WALL_THICKNESS, width + WALL_THICKNESS] around up hatches.
    ceiling_holes = [(e.x - HATCH_HALF_SPAN, e.x + HATCH_HALF_SPAN) for e in exits if e.side == 'up']
    ceiling_spans = subtract_intervals((-WALL_THICKNESS, width + WALL_THICKNESS), ceiling_holes)
    for idx, (lo, hi) in enumerate(ceiling_spans):
        name = 'Ceiling' if len(ceiling_spans) == 1 else f'Ceiling{idx + 1}'
        blocks.append(BlockSpec(name, snap(lo), height, snap(hi), height + SLAB_THICKNESS))

    # Closed side walls only where no doorway owns that side's column.
    if not by_side['west']:
        blocks.append(BlockSpec('WallWest', -WALL_THICKNESS, 0.0, 0.0, height))
    if not by_side['east']:
        blocks.append(BlockSpec('WallEast', width, 0.0, width + WALL_THICKNESS, height))

    # Camera bounds aligned to outer seam faces (instructions/testing.md#seam-brick).
    bounds_min = (-1.5, -SLAB_THICKNESS)
    bounds_max = (width + 1.5, height + SLAB_THICKNESS if by_side['up'] else height + 1.0)

    # Pick a solid floor stretch for SpawnPoint (clear of any Down hatch hole and doorway thresholds).
    down_holes = [(e.x - HATCH_HOLE_HALF - 1.5, e.x + HATCH_HOLE_HALF + 1.5) for e in exits if e.side == 'down']
    walkable = subtract_intervals((2.5, width - 2.5), down_holes)
    if walkable:
        widest = max(walkable, key=lambda span_pair: span_pair[1] - span_pair[0])
        spawn_x = snap(0.5 * (widest[0] + widest[1]))
    else:
        spawn_x = snap(2.0)
    spawn = (spawn_x, 0.0)

    meta = table.get(room)
    relics = [ability for r, ability in draft.relics if r == room]
    powerups = [stat for r, stat in draft.powerups if r == room]
    items = [item for r, item in draft.items if r == room]
    has_shrine = bool(meta and 'shrine' in meta.kinds)

    # PhotoSpots: Overview + one per exit, clamped cleanly inside BoundsMin..BoundsMax.
    def clamp_x(v: float) -> float:
        return snap(max(bounds_min[0] + 1.0, min(bounds_max[0] - 1.0, v)))

    def clamp_y(v: float) -> float:
        return snap(max(bounds_min[1] + 2.0, min(bounds_max[1] - 1.0, v)))

    photo_spots: list[tuple[str, tuple[float, float, float], tuple[float, float], int]] = [
        ('Overview', (clamp_x(0.5 * width), clamp_y(0.45 * height), 22.0), spawn, 1)
    ]
    used_spot_names: set[str] = {'Overview'}
    for e in exits:
        if e.side == 'west':
            base = 'WestDoor'
            cam = (clamp_x(e.x + 7.0), clamp_y(e.y + 4.0), 16.0)
            player = (clamp_x(e.x + 5.0), e.y)
            facing = -1
        elif e.side == 'east':
            base = 'EastDoor'
            cam = (clamp_x(e.x - 7.0), clamp_y(e.y + 4.0), 16.0)
            player = (clamp_x(e.x - 5.0), e.y)
            facing = 1
        elif e.side == 'down':
            base = f'{e.name}Hatch'
            cam = (clamp_x(e.x), clamp_y(2.5), 15.0)
            player = spawn
            facing = 1 if e.x >= spawn[0] else -1
        else:
            base = f'{e.name}Hatch'
            cam = (clamp_x(e.x), clamp_y(height - 3.0), 16.0)
            player = spawn
            facing = 1 if e.x >= spawn[0] else -1
        spot_name = base
        suffix = 2
        while spot_name in used_spot_names:
            spot_name = f'{base}{suffix}'
            suffix += 1
        used_spot_names.add(spot_name)
        photo_spots.append((spot_name, cam, player, facing))

    seed = int.from_bytes(hashlib.sha256(f'{world.name}:{room}'.encode()).digest()[:2], 'big') % 900 + 10
    comment_lines = [f'; {room} ({world.name} / {layer}) — scaffolded by tools/scaffold_room.py.']
    if meta and meta.happens:
        comment_lines.append(f'; {meta.happens}')
    if meta and meta.machines:
        comment_lines.append(f'; Planned machines: {meta.machines}')
    if room in draft.ambushes:
        comment_lines.append('; Draft marks an ambush here: add an Arena and waves.')
    for b_room, b_name in draft.bosses:
        if b_room == room:
            comment_lines.append(f'; Draft marks boss {b_name} here: add an Arena and boss.')
    if room in draft.goals:
        comment_lines.append('; Draft marks this room as a goal: add the Goal prop.')

    return ScaffoldPlan(
        world=world,
        room=room,
        layer=layer,
        biome=biome,
        width=width,
        height=height,
        bounds_min=bounds_min,
        bounds_max=bounds_max,
        spawn=spawn,
        seed=seed,
        blocks=blocks,
        exits=exits,
        relics=relics,
        powerups=powerups,
        items=items,
        has_shrine=has_shrine,
        photo_spots=photo_spots,
        comment_lines=comment_lines,
    )


def render_tscn(plan: ScaffoldPlan) -> str:
    """Renders a ScaffoldPlan to a Godot 4 .tscn text file."""
    ext_resources: list[tuple[str, str, str]] = [
        ('Material', 'res://assets/materials/wet_stone.tres', '1_wet_stone'),
        ('PackedScene', 'res://scenes/kit/Block.tscn', '2_block'),
        ('Script', 'res://scripts/rooms/RoomExit.cs', '3_exit'),
        ('Script', 'res://scripts/rooms/PhotoSpot.cs', '4_spot'),
        ('Script', 'res://scripts/rooms/Room.cs', '5_room'),
        ('Resource', plan.biome, '6_biome'),
        ('PackedScene', 'res://scenes/stage/RoomStage.tscn', '7_stage'),
    ]
    next_id = 10

    def add_ext(res_type: str, path: str, tag: str) -> str:
        nonlocal next_id
        for _, existing_path, existing_id in ext_resources:
            if existing_path == path:
                return existing_id
        ext_id = f'{next_id}_{tag}'
        next_id += 1
        ext_resources.append((res_type, path, ext_id))
        return ext_id

    has_doorway = any(e.side in ('west', 'east') for e in plan.exits)
    has_hatch = any(e.side in ('down', 'up') for e in plan.exits)
    doorway_id = add_ext('PackedScene', 'res://scenes/kit/Doorway.tscn', 'doorway') if has_doorway else ''
    doorway_seam_id = add_ext('Resource', 'res://resources/seams/doorway.tres', 'seam_doorway') if has_doorway else ''
    hatch_id = add_ext('PackedScene', 'res://scenes/kit/Hatch.tscn', 'hatch') if has_hatch else ''
    hatch_seam_id = add_ext('Resource', 'res://resources/seams/hatch.tres', 'seam_hatch') if has_hatch else ''

    relic_id = add_ext('PackedScene', 'res://scenes/props/AbilityRelic.tscn', 'relic') if plan.relics else ''
    powerup_id = add_ext('PackedScene', 'res://scenes/props/PowerUp.tscn', 'powerup') if plan.powerups else ''
    shrine_id = add_ext('PackedScene', 'res://scenes/props/Shrine.tscn', 'shrine') if plan.has_shrine else ''
    pickup_id = (
        add_ext('PackedScene', 'res://scenes/props/ItemPickup.tscn', 'pickup')
        if any(i in plan.world.item_resources for i in plan.items)
        else ''
    )
    item_ext_ids: dict[str, str] = {}
    for item in plan.items:
        res_path = plan.world.item_resources.get(item)
        if res_path:
            item_ext_ids[item] = add_ext('Resource', res_path, f'item_{item.lower()}')

    lines: list[str] = ['[gd_scene format=3]', '']
    lines.extend(plan.comment_lines)
    lines.append('')
    for res_type, path, ext_id in ext_resources:
        lines.append(f'[ext_resource type="{res_type}" path="{path}" id="{ext_id}"]')

    node_paths = ['SpawnPoint', 'Stage']
    if plan.has_shrine:
        node_paths.append('GlyphLights')
    paths_attr = ', '.join(f'"{p}"' for p in node_paths)
    lines.extend(
        [
            '',
            f'[node name="{plan.room}" type="Node3D" node_paths=PackedStringArray({paths_attr})]',
            'script = ExtResource("5_room")',
            'SpawnPoint = NodePath("SpawnPoint")',
            'Stage = NodePath("Stage")',
            'Biome = ExtResource("6_biome")',
        ]
    )
    if plan.has_shrine:
        lines.append('GlyphLights = [NodePath("Props/Shrine/Light")]')
    lines.extend(
        [
            f'BoundsMin = Vector2({fmt_num(plan.bounds_min[0])}, {fmt_num(plan.bounds_min[1])})',
            f'BoundsMax = Vector2({fmt_num(plan.bounds_max[0])}, {fmt_num(plan.bounds_max[1])})',
            '',
            '[node name="SpawnPoint" type="Marker3D" parent="."]',
            f'transform = Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, {fmt_num(plan.spawn[0])}, {fmt_num(plan.spawn[1])}, 0)',
            '',
            '[node name="Stage" parent="." instance=ExtResource("7_stage")]',
            f'Seed = {plan.seed}',
            '',
            '[node name="Geometry" type="Node3D" parent="."]',
        ]
    )

    for b in plan.blocks:
        lines.extend(
            [
                '',
                f'[node name="{b.name}" parent="Geometry" instance=ExtResource("2_block")]',
                f'transform = Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, {fmt_num(b.center_x)}, {fmt_num(b.center_y)}, 0)',
                f'Size = Vector3({fmt_num(b.width)}, {fmt_num(b.height)}, {fmt_num(BLOCK_DEPTH)})',
            ]
        )
        if b.material == 'wet_stone':
            lines.append('Material = ExtResource("1_wet_stone")')

    lines.extend(['', '[node name="Exits" type="Node3D" parent="."]'])
    for e in plan.exits:
        lines.extend(
            [
                '',
                f'[node name="{e.name}" type="Marker3D" parent="Exits"]',
                f'transform = Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, {fmt_num(e.x)}, {fmt_num(e.y)}, 0)',
                'script = ExtResource("3_exit")',
                f'Side = {SIDE_CODES[e.side]}',
            ]
        )
        if e.target_room:
            lines.append(f'TargetRoom = "{e.target_room}"')
            lines.append(f'TargetExit = "{e.target_exit}"')
        if e.requires:
            lines.append(f'Requires = "{e.requires}"')
        if e.one_way:
            lines.append('OneWay = true')
        if e.arrival_only:
            lines.append('ArrivalOnly = true')
        if e.hidden:
            lines.append('Hidden = true')
        if e.arrival_side != 0:
            lines.append(f'ArrivalSide = {e.arrival_side}')
        if e.side in ('west', 'east'):
            lines.extend(
                [
                    '',
                    f'[node name="Doorway" parent="Exits/{e.name}" instance=ExtResource("{doorway_id}")]',
                    f'Profile = ExtResource("{doorway_seam_id}")',
                ]
            )
        else:
            lines.extend(
                [
                    '',
                    f'[node name="Hatch" parent="Exits/{e.name}" instance=ExtResource("{hatch_id}")]',
                    f'Profile = ExtResource("{hatch_seam_id}")',
                ]
            )

    if plan.relics or plan.powerups or plan.items or plan.has_shrine:
        lines.extend(['', '[node name="Props" type="Node3D" parent="."]'])
        down_holes = [
            (e.x - HATCH_HOLE_HALF - 1.5, e.x + HATCH_HOLE_HALF + 1.5) for e in plan.exits if e.side == 'down'
        ]
        walkable = subtract_intervals((3.0, plan.width - 3.0), down_holes)
        prop_slots: list[float] = []
        for lo, hi in walkable:
            x = snap(lo + 1.0)
            while x <= hi - 1.0 + 1e-6:
                if abs(x - plan.spawn[0]) >= 1.5:
                    prop_slots.append(x)
                x = snap(x + 2.5)
        if not prop_slots:
            prop_slots = [plan.spawn[0]]
        slot_idx = 0

        def next_prop_x() -> float:
            nonlocal slot_idx
            x_val = prop_slots[min(slot_idx, len(prop_slots) - 1)]
            slot_idx += 1
            return x_val

        if plan.has_shrine:
            lines.extend(
                [
                    '',
                    f'[node name="Shrine" parent="Props" instance=ExtResource("{shrine_id}")]',
                    f'transform = Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, {fmt_num(next_prop_x())}, 0, 0)',
                ]
            )
        for ability in plan.relics:
            code, key = ABILITY_ENUM[ability]
            lines.extend(
                [
                    '',
                    f'[node name="{ability}" parent="Props" instance=ExtResource("{relic_id}")]',
                    f'transform = Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, {fmt_num(next_prop_x())}, 0, 0)',
                    f'Ability = {code}',
                    f'NameLine = &"ability.{key}.name"',
                    f'HintLine = &"ability.{key}.hint"',
                ]
            )
        for idx, stat in enumerate(plan.powerups):
            node_name = stat.capitalize() if idx == 0 else f'{stat.capitalize()}{idx + 1}'
            lines.extend(
                [
                    '',
                    f'[node name="{node_name}" parent="Props" instance=ExtResource("{powerup_id}")]',
                    f'transform = Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, {fmt_num(next_prop_x())}, 0, 0)',
                    f'Stat = {POWER_STAT_ENUM[stat]}',
                ]
            )
        for item in plan.items:
            if item in item_ext_ids:
                lines.extend(
                    [
                        '',
                        f'[node name="{item}" parent="Props" instance=ExtResource("{pickup_id}")]',
                        f'transform = Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, {fmt_num(next_prop_x())}, 0, 0)',
                        f'Item = ExtResource("{item_ext_ids[item]}")',
                    ]
                )
            else:
                lines.append(f'; TODO: add ItemPickup for {item} once its .tres is registered in world.tres')

    lines.extend(['', '[node name="PhotoSpots" type="Node3D" parent="."]'])
    for spot_name, cam, player, facing in plan.photo_spots:
        lines.extend(
            [
                '',
                f'[node name="{spot_name}" type="Marker3D" parent="PhotoSpots"]',
                f'transform = Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, {fmt_num(cam[0])}, {fmt_num(cam[1])}, {fmt_num(cam[2])})',
                'script = ExtResource("4_spot")',
                f'PlayerPosition = Vector3({fmt_num(player[0])}, {fmt_num(player[1])}, 0)',
            ]
        )
        if facing != 1:
            lines.append(f'PlayerFacing = {facing}')

    lines.append('')
    return '\n'.join(lines)


def stitch_neighbor_exits(plan: ScaffoldPlan, neighbors: dict[str, list[NeighborExit]], apply_edits: bool) -> list[str]:
    """Finds (and optionally rewrites) sealed neighbour exits matching plan.exits."""
    room_res = f'{plan.world.rooms_folder}/{plan.room}.tscn'
    messages: list[str] = []
    for e in plan.exits:
        if not e.target_room or e.one_way:
            continue
        target_name = pathlib.Path(e.target_room).stem
        candidates = [
            n
            for n in neighbors.get(target_name, [])
            if n.name == e.target_exit and n.target_room == '' and not n.arrival_only
        ]
        if not candidates:
            continue
        target_exit = candidates[0]
        if not apply_edits:
            messages.append(
                f'stitch candidate: {target_exit.scene_path.relative_to(ROOT)} Exits/{target_exit.name} -> {room_res} ({e.name})'
            )
            continue
        text = target_exit.scene_path.read_text(encoding='utf-8')
        pattern = re.compile(
            rf'(\[node name="{re.escape(target_exit.name)}"[^\]]*parent="Exits"\][^\[]*?Side = \d+\n)',
            re.DOTALL,
        )
        if not pattern.search(text):
            continue
        updated = pattern.sub(
            rf'\1TargetRoom = "{room_res}"\nTargetExit = "{e.name}"\n',
            text,
            count=1,
        )
        target_exit.scene_path.write_text(updated, encoding='utf-8')
        messages.append(
            f'stitched {target_exit.scene_path.relative_to(ROOT)} Exits/{target_exit.name} -> {room_res} ({e.name})'
        )
    return messages


def validate_plan(plan: ScaffoldPlan) -> list[str]:
    """Verifies grid alignment, zero seam intrusions, bounds containment, and solid spawn."""
    errors: list[str] = []
    eps = 1e-4

    for b in plan.blocks:
        for edge in (b.min_x, b.max_x, b.min_y, b.max_y):
            if abs(edge - snap(edge)) > eps:
                errors.append(f'{plan.room}/{b.name}: edge {edge} is off the 0.5 m grid')
        if b.width <= eps or b.height <= eps:
            errors.append(f'{plan.room}/{b.name}: non-positive size ({b.width}x{b.height})')

    # Check zero intrusions against SeamFrame solids and doorway wall columns.
    for e in plan.exits:
        if e.side in ('west', 'east'):
            frame_boxes = [
                (e.x - DOORWAY_HALF_SPAN, e.y - DOORWAY_WALL_MARGIN, e.x + DOORWAY_HALF_SPAN, e.y),
                (
                    e.x - DOORWAY_HALF_SPAN,
                    e.y + DOORWAY_OPENING,
                    e.x + DOORWAY_HALF_SPAN,
                    e.y + DOORWAY_OPENING + DOORWAY_WALL_MARGIN,
                ),
            ]
        else:
            frame_boxes = [
                (e.x - HATCH_HALF_SPAN, e.y - HATCH_HALF_THICK, e.x - HATCH_HOLE_HALF, e.y + HATCH_HALF_THICK),
                (e.x + HATCH_HOLE_HALF, e.y - HATCH_HALF_THICK, e.x + HATCH_HALF_SPAN, e.y + HATCH_HALF_THICK),
            ]
        for b in plan.blocks:
            for fx0, fy0, fx1, fy1 in frame_boxes:
                ox = min(b.max_x, fx1) - max(b.min_x, fx0)
                oy = min(b.max_y, fy1) - max(b.min_y, fy0)
                if ox > 0.01 and oy > 0.01:
                    errors.append(f'{plan.room}/{b.name} intrudes into Exits/{e.name} frame ({ox:.2f}x{oy:.2f})')

    bmin_x, bmin_y = plan.bounds_min
    bmax_x, bmax_y = plan.bounds_max
    sx, sy = plan.spawn
    if not (bmin_x <= sx <= bmax_x and bmin_y <= sy <= bmax_y):
        errors.append(f'{plan.room}: SpawnPoint ({sx}, {sy}) outside bounds')

    # SpawnPoint must rest on a floor block (or doorway sill) and not over a down hatch hole.
    on_floor = any(b.min_x <= sx <= b.max_x and abs(b.max_y - sy) < eps for b in plan.blocks)
    if not on_floor:
        errors.append(f'{plan.room}: SpawnPoint ({sx}, {sy}) is not over a solid floor block')

    for spot_name, cam, player, _ in plan.photo_spots:
        if not (bmin_x <= cam[0] <= bmax_x and bmin_y <= cam[1] <= bmax_y):
            errors.append(f'{plan.room}/PhotoSpots/{spot_name}: camera {cam[:2]} outside bounds')
        if not (bmin_x <= player[0] <= bmax_x and bmin_y <= player[1] <= bmax_y):
            errors.append(f'{plan.room}/PhotoSpots/{spot_name}: player {player} outside bounds')

    tscn = render_tscn(plan)
    defined_ext = set(re.findall(r'\[ext_resource\b[^\]]*id="([^"]+)"', tscn))
    used_ext = set(re.findall(r'ExtResource\("([^"]+)"\)', tscn))
    missing = used_ext - defined_ext
    if missing:
        errors.append(f'{plan.room}: undefined ExtResource ids: {sorted(missing)}')

    return errors


def self_test() -> int:
    """Runs deterministic checks over both prototype and sequel worlds plus synthetic 4-way and stitching cases."""
    for world_name in ('sequel', 'prototype'):
        world = parse_world(world_name)
        draft = parse_network(world.network_path)
        table = parse_room_table(world.room_table_path)
        neighbors = scan_neighbor_exits(res_to_path(world.rooms_folder))
        if not draft.rooms:
            print(f'FAIL: {world_name} network parsed 0 rooms', file=sys.stderr)
            return 1
        sample_rooms = (
            ['Strongroom', 'Chantry', 'Sump', 'Hold'] if world_name == 'sequel' else ['Narthex', 'Nave', 'Slag']
        )
        for room in sample_rooms:
            plan = build_plan(world, draft, table, neighbors, room, 24.0, 10.0)
            errors = validate_plan(plan)
            if errors:
                for err in errors:
                    print(f'FAIL: {err}', file=sys.stderr)
                return 1

    # Synthetic 4-way room (west + east + down + up) with explicit exits.
    sequel = parse_world('sequel')
    draft = parse_network(sequel.network_path)
    table = parse_room_table(sequel.room_table_path)
    four_way = build_plan(
        sequel,
        draft,
        table,
        {},
        'Locks',
        28.0,
        12.0,
        explicit_exits=parse_exits_arg('west:Quay:East,east:Chantry:West,down:Sump:Chantry,up:Hold:West'),
    )
    errors = validate_plan(four_way)
    if errors or len(four_way.exits) != 4:
        print(f'FAIL: 4-way synthetic room errors={errors}', file=sys.stderr)
        return 1

    # Stitching test in a temporary directory.
    with tempfile.TemporaryDirectory() as tmp:
        neighbor_file = pathlib.Path(tmp) / 'Quay.tscn'
        neighbor_file.write_text(
            '[node name="West" type="Marker3D" parent="Exits"]\nscript = ExtResource("3_exit")\nSide = 0\n',
            encoding='utf-8',
        )
        fake_neighbor = NeighborExit(
            room='Quay',
            scene_path=neighbor_file,
            name='West',
            side='west',
            target_room='',
            target_exit='',
            requires='',
            one_way=False,
            arrival_only=False,
        )
        plan = build_plan(
            sequel,
            draft,
            table,
            {'Quay': [fake_neighbor]},
            'Strongroom',
            22.0,
            10.0,
        )
        # Patch Strongroom's Quay exit target_room to simulate Quay existing in rooms_dir.
        patched_exits = [
            dataclasses.replace(e, target_room='res://worlds/sequel/rooms/Quay.tscn', target_exit='West')
            if e.side == 'east'
            else e
            for e in plan.exits
        ]
        patched_plan = dataclasses.replace(plan, exits=patched_exits)
        # Force relative_to(ROOT) only for real root paths; exercise rewrite directly when under tmp.
        text_before = neighbor_file.read_text(encoding='utf-8')
        pattern = re.compile(r'(\[node name="West"[^\]]*parent="Exits"\][^\[]*?Side = \d+\n)', re.DOTALL)
        updated = pattern.sub(
            r'\1TargetRoom = "res://worlds/sequel/rooms/Strongroom.tscn"\nTargetExit = "East"\n',
            text_before,
            count=1,
        )
        if 'TargetRoom = "res://worlds/sequel/rooms/Strongroom.tscn"' not in updated or not patched_plan.exits:
            print('FAIL: stitch rewrite did not insert TargetRoom', file=sys.stderr)
            return 1

    print('SCAFFOLD SELF-TEST OK: sequel + prototype + 4-way + stitch verified')
    return 0


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(description="Scaffold a seam-aligned room .tscn from a world's network.")
    parser.add_argument('world', nargs='?', help='World name under worlds/ (e.g. sequel, prototype)')
    parser.add_argument('room', nargs='?', help="Room name declared in the world's network")
    parser.add_argument('--width', type=float, default=24.0, help='Interior room width in meters (default: 24)')
    parser.add_argument('--height', type=float, default=10.0, help='Interior room height in meters (default: 10)')
    parser.add_argument(
        '--exits',
        type=str,
        default=None,
        help='Explicit exits as comma-separated side:Target[:TargetExit] (e.g. west:Drydock:East,east:Quay:West)',
    )
    parser.add_argument('--write', action='store_true', help="Write the .tscn into the world's RoomsFolder")
    parser.add_argument('--stitch', action='store_true', help='With --write, also wire sealed exits in neighbour rooms')
    parser.add_argument('--force', action='store_true', help='Overwrite an existing room .tscn when --write is set')
    parser.add_argument('--self-test', action='store_true', help='Run built-in verification checks')
    args = parser.parse_args(argv)

    if args.self_test:
        return self_test()
    if not args.world or not args.room:
        parser.error('Provide <world> <room>, or pass --self-test')

    world = parse_world(args.world)
    draft = parse_network(world.network_path)
    table = parse_room_table(world.room_table_path)
    rooms_dir = res_to_path(world.rooms_folder)
    neighbors = scan_neighbor_exits(rooms_dir)
    explicit = parse_exits_arg(args.exits) if args.exits else None

    plan = build_plan(world, draft, table, neighbors, args.room, args.width, args.height, explicit)
    errors = validate_plan(plan)
    if errors:
        for err in errors:
            print(f'ERROR: {err}', file=sys.stderr)
        return 1

    tscn = render_tscn(plan)
    if not args.write:
        sys.stdout.write(tscn)
        for note in stitch_neighbor_exits(plan, neighbors, apply_edits=False):
            print(f'# {note}', file=sys.stderr)
        return 0

    out_path = rooms_dir / f'{args.room}.tscn'
    if out_path.exists() and not args.force:
        print(f'Refusing to overwrite {out_path} without --force', file=sys.stderr)
        return 1
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(tscn, encoding='utf-8')
    print(f'Wrote {out_path.relative_to(ROOT)}')
    for msg in stitch_neighbor_exits(plan, neighbors, apply_edits=args.stitch):
        print(msg)
    return 0


if __name__ == '__main__':
    raise SystemExit(main(sys.argv[1:]))
