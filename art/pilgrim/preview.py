"""Contact sheets of the pilgrim's clips, rendered from the built export (build.py) so they show what the game imports.

    blender --background --factory-startup --python art/pilgrim/preview.py -- OUT_DIR [CLIP ...]

Writes OUT_DIR/<clip>.png for every clip (or those named): the top row from the side, as the game's camera sees the
body, the bottom one from three quarters in front, each of `FRAMES` instants evenly over the clip, on a ground with a
mark every half meter. Put OUT_DIR under lookdev/ (git-ignored, emptied when the work is done:
instructions/testing.md#lookdev-clear).
"""

import array
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import mannequin

GLB = os.path.join(os.path.dirname(os.path.dirname(HERE)), 'assets', 'pilgrim', 'pilgrim.glb')
FRAMES = 10
WIDTH, HEIGHT = 240, 330
VIEWS = (
    (Vector((-6.0, 0.0, 0.95)), True),
    (Vector((-3.6, -4.8, 1.3)), False),
)
"""Where each row's camera stands, and whether it is orthographic; both look at the body's middle."""


def _scene() -> bpy.types.Scene:
    scene = bpy.context.scene
    assert scene is not None
    return scene


def _add(obj: bpy.types.Object) -> bpy.types.Object:
    _scene().collection.objects.link(obj)
    return obj


def _box(
    name: str, at: tuple[float, float, float], size: tuple[float, float, float], surface: bpy.types.Material
) -> None:
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=at)
    box = bpy.context.object
    assert box is not None and isinstance(box.data, bpy.types.Mesh)
    box.name = name
    box.scale = size
    box.data.materials.append(surface)


def _setup() -> tuple[bpy.types.Object, bpy.types.Object]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=GLB)
    rigs = [o for o in bpy.data.objects if o.type == 'ARMATURE']
    assert len(rigs) == 1, 'one armature in the export'
    scene = _scene()
    scene.render.resolution_x, scene.render.resolution_y = WIDTH, HEIGHT
    eevee = scene.eevee
    assert eevee is not None
    eevee.taa_render_samples = 16

    world = bpy.data.worlds.new('Preview')
    world.use_nodes = True
    tree = world.node_tree
    assert tree is not None
    tree.nodes['Background'].inputs['Color'].default_value = (0.55, 0.57, 0.6, 1.0)  # type: ignore[attr-defined]
    scene.world = world
    sun = _add(bpy.data.objects.new('Sun', bpy.data.lights.new('Sun', 'SUN')))
    sun.rotation_euler = (math.radians(50.0), math.radians(10.0), math.radians(-40.0))

    _box('Ground', (0.0, 0.0, -0.05), (12.0, 12.0, 0.1), mannequin.material('Ground', (0.4, 0.41, 0.43), 0.8, 0.0))
    mark = mannequin.material('Mark', (0.12, 0.12, 0.14), 0.8, 0.0)
    for i in range(-8, 9):
        _box(f'Mark{i}', (0.0, i * 0.5, 0.0), (6.0, 0.012, 0.004), mark)

    camera = _add(bpy.data.objects.new('Camera', bpy.data.cameras.new('Camera')))
    scene.camera = camera
    return rigs[0], camera


def _aim(camera: bpy.types.Object, at: Vector, orthographic: bool) -> None:
    camera.location = at
    camera.rotation_euler = (Vector((0.0, 0.0, 0.9)) - at).to_track_quat('-Z', 'Y').to_euler()
    data = camera.data
    assert isinstance(data, bpy.types.Camera)
    data.type = 'ORTHO' if orthographic else 'PERSP'
    data.ortho_scale = 2.3
    data.lens = 50.0


def _render(path: str) -> array.array[float]:
    """The scene rendered, as RGBA floats, bottom row first."""
    scene = _scene()
    scene.render.filepath = path
    bpy.ops.render.render(write_still=True)
    image = bpy.data.images.load(path)
    pixels = array.array('f', bytes(4 * WIDTH * HEIGHT * 4))
    image.pixels.foreach_get(pixels)
    bpy.data.images.remove(image)
    os.remove(path)
    return pixels


def _sheet(rows: list[list[array.array[float]]]) -> array.array[float]:
    """The tiles laid out, `rows[0]` on top (Blender's images run bottom row first)."""
    sheet = array.array('f')
    for tiles in reversed(rows):
        for y in range(HEIGHT):
            for tile in tiles:
                sheet.extend(tile[y * WIDTH * 4 : (y + 1) * WIDTH * 4])
    return sheet


def main(out: str, only: list[str]) -> None:
    rig, camera = _setup()
    os.makedirs(out, exist_ok=True)
    scene = _scene()
    for action in [a for a in bpy.data.actions if not only or a.name in only]:
        data = rig.animation_data or rig.animation_data_create()
        assert data is not None
        data.action = action
        if action.slots:
            data.action_slot = action.slots[0]
        first, last = float(action.frame_range[0]), float(action.frame_range[1])
        rows: list[list[array.array[float]]] = []
        for at, orthographic in VIEWS:
            _aim(camera, at, orthographic)
            tiles = []
            for k in range(FRAMES):
                frame = first + (last - first) * k / FRAMES
                whole = math.floor(frame)
                scene.frame_set(whole, subframe=frame - whole)
                tiles.append(_render(os.path.join(out, '_tile.png')))
            rows.append(tiles)
        image = bpy.data.images.new(action.name, WIDTH * FRAMES, HEIGHT * len(VIEWS), alpha=True)
        image.pixels.foreach_set(_sheet(rows))
        image.filepath_raw = os.path.join(out, f'{action.name}.png')
        image.file_format = 'PNG'
        image.save()
        print(f'sheet {image.filepath_raw}')


arguments = sys.argv[sys.argv.index('--') + 1 :]
main(arguments[0], arguments[1:])
