"""Room authoring helper: writes a readable room .tscn from a short Python description.

The generated .tscn is the source of truth (docs/godot-csharp.md); this only saves typing when a room is
first laid out. Usage, from a throwaway script:

    import sys; sys.path.insert(0, 'tools'); from roomgen import Room
    r = Room('Narthex', bounds=((-2, -2), (38, 22)), spawn=(4, 0), doc='The entrance ...')
    r.block('Floor', (-4, -2), (40, 0), look='wet_stone')
    r.exit('East', (37, 0), (1, 4), 'DrownedHall', 'WestGate')
    r.photo('Start', (8, 4, 22), player=(4, 0))
    r.write()

Blocks are given by their lower-left and upper-right corners on the play plane (edges on the 0.5 m grid);
kit pieces by their origin (on the 0.25 m grid). Run tools/check.sh afterwards: RoomValidator checks both.
"""

import os

# Resources rooms use, by short name.
EXT = {
    # scripts
    'room': ('Script', 'res://scripts/rooms/Room.cs'),
    'exit': ('Script', 'res://scripts/rooms/RoomExit.cs'),
    'goal': ('Script', 'res://scripts/map/MapGoal.cs'),
    'spot': ('Script', 'res://scripts/rooms/PhotoSpot.cs'),
    'machine': ('Script', 'res://scripts/machines/Machine.cs'),
    'water_body': ('Script', 'res://scripts/rooms/WaterBody.cs'),
    'scatter': ('Script', 'res://scripts/rooms/BackdropScatter.cs'),
    'colonnade': ('Script', 'res://scripts/rooms/dressing/Colonnade.cs'),
    'rackrow': ('Script', 'res://scripts/rooms/dressing/RackRow.cs'),
    'column': ('Script', 'res://scripts/kit/ColumnMesh.cs'),
    'arch': ('Script', 'res://scripts/kit/ArchMesh.cs'),
    'blockmesh': ('Script', 'res://scripts/kit/BlockMesh.cs'),
    'grate': ('Script', 'res://scripts/kit/GrateMesh.cs'),
    'rack': ('Script', 'res://scripts/kit/RackMesh.cs'),
    'wreck': ('Script', 'res://scripts/kit/WreckMesh.cs'),
    'pipe': ('Script', 'res://scripts/kit/PipeMesh.cs'),
    'cables': ('Script', 'res://scripts/kit/CableMesh.cs'),
    # scenes
    'block': ('PackedScene', 'res://scenes/kit/Block.tscn'),
    'stage': ('PackedScene', 'res://scenes/stage/RoomStage.tscn'),
    'dust': ('PackedScene', 'res://scenes/fx/Dust.tscn'),
    'silt': ('PackedScene', 'res://scenes/fx/Silt.tscn'),
    'drips': ('PackedScene', 'res://scenes/fx/Drips.tscn'),
    'shrine': ('PackedScene', 'res://scenes/props/Shrine.tscn'),
    'relic': ('PackedScene', 'res://scenes/props/AbilityRelic.tscn'),
    # resources
    'flooded': ('Resource', 'res://resources/biomes/flooded.tres'),
    'votary': ('Resource', 'res://resources/machines/votary.tres'),
    'gleaner': ('Resource', 'res://resources/machines/gleaner.tres'),
    'lamplighter': ('Resource', 'res://resources/machines/lamplighter.tres'),
    'limpet': ('Resource', 'res://resources/machines/limpet.tres'),
}
for m in ['rock', 'wet_stone', 'metal', 'rust', 'glyphs', 'cable', 'stone_mid', 'metal_mid', 'rust_mid',
          'backdrop_stone', 'backdrop_rust', 'foreground', 'backdrop_cathedral', 'backdrop_halls', 'water', 'underwater']:
    EXT[m] = ('Material', f'res://assets/materials/{m}.tres')

IDENTITY = (1, 0, 0, 0, 1, 0, 0, 0, 1)
# Light shaft orientations (row-major bases, as in .tscn): from the upper left, and straight down (tilted toward the camera).
SHAFT_DIAGONAL = (-0.7071, 0.6288, -0.3234, 0, 0.4573, 0.8893, 0.7071, 0.6288, -0.3234)
SHAFT_DOWN = (-1, 0, 0, 0, 0.1414, 0.9899, 0, 0.9899, -0.1414)


def num(v):
    if isinstance(v, bool):
        return 'true' if v else 'false'
    if isinstance(v, int):
        return str(v)
    if isinstance(v, float):
        r = round(v, 4)
        return str(int(r)) if r == int(r) else repr(r)
    return str(v)


def vec(*v):
    return f'Vector{len(v)}({", ".join(num(x) for x in v)})'


def rot_y(deg):
    import math
    c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    return (round(c, 4), 0, round(s, 4), 0, 1, 0, round(-s, 4), 0, round(c, 4))


def rot_z(deg):
    import math
    c, s = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    return (round(c, 4), round(-s, 4), 0, round(s, 4), round(c, 4), 0, 0, 0, 1)


def transform(pos, basis=IDENTITY):
    return f'Transform3D({", ".join(num(x) for x in basis)}, {", ".join(num(x) for x in pos)})'


class Raw(str):
    """A value written as is (a SubResource(...), an array, a NodePath)."""


class Room:
    def __init__(self, name, bounds, spawn, doc='', biome='flooded', in_game=True):
        self.name, self.bounds, self.spawn, self.doc, self.biome, self.in_game = name, bounds, spawn, doc, biome, in_game
        self.ext_ids = {}
        self.subs = []          # (id, type, props)
        self.nodes = []         # (comment, header, props)
        self.groups = set()
        self.shafts, self.lamps, self.glyph_lights = [], [], []
        self.stage_props = {}
        self.sub_count = 0

    # ---- resources
    def ext(self, key):
        if key not in self.ext_ids:
            self.ext_ids[key] = f'{len(self.ext_ids) + 1}_{key}'
        return Raw(f'ExtResource("{self.ext_ids[key]}")')

    def sub(self, kind, script=None, id=None, **props):
        """A sub-resource; `kind` is a kit script name (column, arch, pipe…) or a Godot type (QuadMesh)."""
        self.sub_count += 1
        sid = id or f'{(script or kind)}_{self.sub_count}'
        if kind in EXT and EXT[kind][0] == 'Script':
            p = {'script': self.ext(kind)}
            p.update(props)
            self.subs.append((sid, 'PrimitiveMesh', p))
        else:
            self.subs.append((sid, kind, props))
        return Raw(f'SubResource("{sid}")')

    # ---- nodes
    def _group(self, path):
        parts = path.split('/')
        for i in range(1, len(parts)):
            g = '/'.join(parts[:i])
            if g not in self.groups:
                self.groups.add(g)
                parent = '.' if i == 1 else '/'.join(parts[:i - 1])
                self.nodes.append(('', f'[node name="{parts[i - 1]}" type="Node3D" parent="{parent}"]', {}))
        name = parts[-1]
        parent = '.' if len(parts) == 1 else '/'.join(parts[:-1])
        return name, parent

    def node(self, path, type=None, instance=None, comment='', node_paths=None, **props):
        name, parent = self._group(path)
        head = f'[node name="{name}"'
        if type:
            head += f' type="{type}"'
        head += f' parent="{parent}"'
        if node_paths:
            head += f' node_paths=PackedStringArray({", ".join(chr(34) + n + chr(34) for n in node_paths)})'
        if instance:
            head += f' instance={self.ext(instance)}'
        head += ']'
        self.nodes.append((comment, head, props))
        if path not in self.groups:
            self.groups.add(path)
        return path

    def stage(self, **props):
        """Choices on the shared RoomStage (Seed, DustDensity, PillarDensity, FrameBelow, FrameAbove…)."""
        self.stage_props.update(props)

    def block(self, path, lo, hi, surface='rock', look=None, depth=4, collision_only=False, comment='', z=0):
        """A gameplay block from corner `lo` to `hi` (X, Y). surface: rock or metal; look: a material override."""
        if '/' not in path:
            path = 'Geometry/' + path
        cx, cy = (lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2
        props = {'transform': Raw(transform((cx, cy, z))), 'Size': Raw(vec(hi[0] - lo[0], hi[1] - lo[1], depth))}
        if surface == 'metal':
            props['Surface'] = 1
        if look:
            props['Material'] = self.ext(look)
        if collision_only:
            props['CollisionOnly'] = True
        return self.node(path, instance='block', comment=comment, **props)

    def mesh(self, path, mesh, material, pos, basis=IDENTITY, cast=True, comment=''):
        props = {'transform': Raw(transform(pos, basis)), 'mesh': mesh, 'material_override': self.ext(material)}
        if not cast:
            props['cast_shadow'] = 0
        return self.node(path, type='MeshInstance3D', comment=comment, **props)

    def scripted(self, path, script, pos, comment='', basis=IDENTITY, **props):
        p = {'transform': Raw(transform(pos, basis)), 'script': self.ext(script)}
        p.update(props)
        return self.node(path, type='Node3D', comment=comment, **p)

    def relic(self, name, pos, ability='MagGrip', comment=''):
        """An ability relic standing on the floor at `pos` (Ability: MagGrip, Harpoon, Drill, Ballast, HeatPlating)."""
        values = {'MagGrip': 1, 'Harpoon': 2, 'Drill': 4, 'Ballast': 8, 'HeatPlating': 16}
        return self.node('Props/' + name, instance='relic', comment=comment, transform=Raw(transform((pos[0], pos[1], 0))),
                         Ability=values[ability])

    def exit(self, name, pos, size, target='', target_exit='', requires='', one_way=False, arrival_side=0, comment=''):
        props = {'transform': Raw(transform((pos[0], pos[1], 0))), 'script': self.ext('exit')}
        if target:
            props['TargetRoom'] = f'res://scenes/rooms/{target}.tscn'
            props['TargetExit'] = target_exit
        if requires:
            props['Requires'] = requires
        if one_way:
            props['OneWay'] = True
        if arrival_side:
            props['ArrivalSide'] = arrival_side
        props['Size'] = Raw(vec(*size))
        return self.node('Exits/' + name, type='Marker3D', comment=comment, **props)

    def goal(self, name, pos, comment=''):
        """Where the journey ends, for the map solver (a MapGoal marker)."""
        return self.node('Exits/' + name, type='Marker3D', comment=comment, transform=Raw(transform((pos[0], pos[1], 0))),
                         script=self.ext('goal'))

    def machine(self, name, species, pos, facing=1, comment='', **props):
        p = {'transform': Raw(transform((pos[0], pos[1], 0))), 'script': self.ext('machine'),
             'Species': self.ext(species), 'StartFacing': facing}
        p.update(props)
        return self.node('Machines/' + name, type='CharacterBody3D', comment=comment, **p)

    def shaft(self, name, pos, basis=SHAFT_DIAGONAL, energy=6.0, fog=12.0, angle=10.0, range=80.0, comment=''):
        self.shafts.append('Shafts/' + name)
        return self.node('Shafts/' + name, type='SpotLight3D', comment=comment, transform=Raw(transform(pos, basis)),
                         light_energy=float(energy), light_volumetric_fog_energy=float(fog), shadow_enabled=True,
                         spot_range=float(range), spot_attenuation=0.25, spot_angle=float(angle), spot_angle_attenuation=2.0)

    def lamp(self, path, pos, energy=2.5, range=10.0, shadow=True, comment=''):
        self.lamps.append(path)
        return self.node(path, type='OmniLight3D', comment=comment, transform=Raw(transform(pos)),
                         light_energy=float(energy), omni_range=float(range), shadow_enabled=shadow)

    def glyphs(self, path, pos, size, light=0.5, light_range=2.5, comment=''):
        """Faith glyphs on a quad at `pos` (on a block's front face: Z = its depth / 2 + 0.01) with their own soft light."""
        quad = self.sub('QuadMesh', size=Raw(vec(*size)))
        self.node(path, type='MeshInstance3D', comment=comment, transform=Raw(transform(pos)), mesh=quad,
                  material_override=self.ext('glyphs'), cast_shadow=0)
        light_path = path + 'Light'
        self.glyph_lights.append(light_path)
        self.node(light_path, type='OmniLight3D', transform=Raw(transform((pos[0], pos[1], pos[2] + 0.7))),
                  light_energy=float(light), light_volumetric_fog_energy=0.0, omni_range=float(light_range))

    def shrine(self, name, pos, comment=''):
        """A pilgrim shrine (save point) standing on the floor at `pos`; its light joins the glyph lights."""
        self.glyph_lights.append(f'Props/{name}/Light')
        return self.node('Props/' + name, instance='shrine', comment=comment, transform=Raw(transform((pos[0], pos[1], 0))))

    def particles(self, path, effect, pos, extents, density=None, comment='', **props):
        p = {'transform': Raw(transform(pos)), 'Extents': Raw(vec(*extents))}
        if density is not None:
            p['Density'] = float(density)
        p.update(props)
        return self.node(path, instance=effect, comment=comment, **p)

    def water(self, pos, size, depth=2.5, comment=''):
        return self.node('Water', type='Node3D', comment=comment, transform=Raw(transform(pos)), script=self.ext('water_body'),
                         Size=Raw(vec(*size)), Depth=float(depth), SurfaceMaterial=self.ext('water'),
                         UnderwaterMaterial=self.ext('underwater'))

    def photo(self, name, cam, player, facing=1, fov=None, comment='', hud=False):
        props = {'transform': Raw(transform(cam)), 'script': self.ext('spot'), 'PlayerPosition': Raw(vec(player[0], player[1], 0))}
        if facing != 1:
            props['PlayerFacing'] = facing
        if fov:
            props['Fov'] = float(fov)
        if hud:
            props['ShowHud'] = True
        return self.node('PhotoSpots/' + name, type='Marker3D', comment=comment, **props)

    # ---- output
    def _props(self, props):
        out = []
        for k, v in props.items():
            if isinstance(v, Raw):
                out.append(f'{k} = {v}')
            elif isinstance(v, str):
                out.append(f'{k} = "{v}"')
            elif isinstance(v, (list, tuple)):
                out.append(f'{k} = [{", ".join(str(x) for x in v)}]')
            else:
                out.append(f'{k} = {num(v)}')
        return out

    def text(self):
        root_paths = ['SpawnPoint', 'Stage'] + (['Lamps'] if self.lamps else []) + (['Shafts'] if self.shafts else []) \
            + (['GlyphLights'] if self.glyph_lights else [])
        root = {'script': self.ext('room'), 'SpawnPoint': Raw('NodePath("SpawnPoint")'), 'Stage': Raw('NodePath("Stage")'),
                'Biome': self.ext(self.biome)}
        if self.lamps:
            root['Lamps'] = Raw('[' + ', '.join(f'NodePath("{p}")' for p in self.lamps) + ']')
        if self.shafts:
            root['Shafts'] = Raw('[' + ', '.join(f'NodePath("{p}")' for p in self.shafts) + ']')
        if self.glyph_lights:
            root['GlyphLights'] = Raw('[' + ', '.join(f'NodePath("{p}")' for p in self.glyph_lights) + ']')
        if not self.in_game:
            root['InGame'] = False
        (x0, y0), (x1, y1) = self.bounds
        root['BoundsMin'] = Raw(vec(x0, y0))
        root['BoundsMax'] = Raw(vec(x1, y1))
        self.ext('stage')

        lines = ['[gd_scene format=3]', '']
        for d in self.doc.strip().splitlines():
            lines.append('; ' + d if d else ';')
        if self.doc:
            lines.append('')
        for key, rid in self.ext_ids.items():
            typ, path = EXT[key]
            lines.append(f'[ext_resource type="{typ}" path="{path}" id="{rid}"]')
        lines.append('')
        for sid, typ, props in self.subs:
            lines.append(f'[sub_resource type="{typ}" id="{sid}"]')
            lines += self._props(props)
            lines.append('')
        paths = ', '.join(f'"{p}"' for p in root_paths)
        lines.append(f'[node name="{self.name}" type="Node3D" node_paths=PackedStringArray({paths})]')
        lines += self._props(root)
        lines.append('')
        lines.append('[node name="SpawnPoint" type="Marker3D" parent="."]')
        lines.append(f'transform = {transform((self.spawn[0], self.spawn[1], 0))}')
        lines.append('')
        lines.append('; The shared atmosphere stack (environment, key light, clear front air, skylines, far pillars, frame, dust).')
        lines.append(f'[node name="Stage" parent="." instance={self.ext("stage")}]')
        lines += self._props(self.stage_props)
        lines.append('')
        for comment, head, props in self.nodes:
            for c in comment.strip().splitlines():
                lines.append('; ' + c)
            lines.append(head)
            lines += self._props(props)
            lines.append('')
        return '\n'.join(lines).rstrip() + '\n'

    def write(self, path=None):
        path = path or os.path.join(os.path.dirname(__file__), '..', 'scenes', 'rooms', f'{self.name}.tscn')
        with open(path, 'w') as f:
            f.write(self.text())
        return path
