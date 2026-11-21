"""Contact sheets of the pilgrim's clips, rendered from the built export (build.py) so they show what the game imports.

    blender --background --factory-startup --python art/pilgrim/preview.py -- OUT_DIR [--glb FILE] [--frames N] [CLIP ...]

Writes OUT_DIR/<clip>.png for every clip (or those named): the top row from the side, as the game's camera sees the
body, the bottom one from three quarters in front, each of N instants (10 by default) evenly over the clip, its time in seconds on it, on a ground with a
mark every half meter, the camera following the clip's travel. Put OUT_DIR under lookdev/ (git-ignored, emptied when the work is done:
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
WIDTH, HEIGHT = 240, 330
VIEWS = (
    (Vector((-6.0, 0.0, 0.95)), True, Vector((-0.78, 1.0, -2.0)), 0.13),
    (Vector((-3.6, -4.8, 1.3)), False, Vector((-0.48, 0.62, -2.0)), 0.08),
)
"""Each row's camera: where it stands, whether it is orthographic, and where its time label sits in its frame and how
big; both look at the body's middle."""


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


def _setup(glb: str) -> tuple[bpy.types.Object, bpy.types.Object]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _scene().render.fps = 60
    bpy.ops.import_scene.gltf(filepath=glb)
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

    _box('Ground', (0.0, 0.0, -0.05), (80.0, 80.0, 0.1), mannequin.material('Ground', (0.4, 0.41, 0.43), 0.8, 0.0))
    mark = mannequin.material('Mark', (0.12, 0.12, 0.14), 0.8, 0.0)
    for i in range(-60, 61):
        _box(f'Mark{i}', (0.0, i * 0.5, 0.0), (80.0, 0.012, 0.004), mark)

    camera = _add(bpy.data.objects.new('Camera', bpy.data.cameras.new('Camera')))
    scene.camera = camera
    label = _add(bpy.data.objects.new('Label', bpy.data.curves.new('Label', 'FONT')))
    label.parent = camera
    assert isinstance(label.data, bpy.types.TextCurve)
    label.data.materials.append(mark)
    return rigs[0], camera


def _label(text: str, at: Vector, size: float) -> None:
    label = bpy.data.objects['Label']
    assert isinstance(label.data, bpy.types.TextCurve)
    label.data.body = text
    label.data.size = size
    label.location = at


def _aim(camera: bpy.types.Object, at: Vector, orthographic: bool, follow: Vector) -> None:
    """The camera at `at` from the body, which has travelled `follow` on the ground (the clip's root motion)."""
    camera.location = at + follow
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


def _sheet(rows: list[list[array.array[float]]], frames: int) -> array.array[float]:
    """The tiles laid out, `rows[0]` on top (Blender's images run bottom row first)."""
    sheet = array.array('f')
    for tiles in reversed(rows):
        for y in range(HEIGHT):
            for tile in tiles:
                sheet.extend(tile[y * WIDTH * 4 : (y + 1) * WIDTH * 4])
    return sheet


def main(out: str, glb: str, frames: int, only: list[str]) -> None:
    rig, camera = _setup(glb)
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
        for at, orthographic, label_at, label_size in VIEWS:
            tiles = []
            for k in range(frames):
                frame = first + (last - first) * k / frames
                whole = math.floor(frame)
                scene.frame_set(whole, subframe=frame - whole)
                pose = rig.pose
                assert pose is not None
                root = rig.matrix_world @ pose.bones['Root'].head
                _aim(camera, at, orthographic, Vector((root.x, root.y, 0.0)))
                _label(f'{(frame - first) / scene.render.fps:.2f}', label_at, label_size)
                tiles.append(_render(os.path.join(out, '_tile.png')))
            rows.append(tiles)
        image = bpy.data.images.new(action.name, WIDTH * frames, HEIGHT * len(VIEWS), alpha=True)
        image.pixels.foreach_set(_sheet(rows, frames))
        image.filepath_raw = os.path.join(out, f'{action.name}.png')
        image.file_format = 'PNG'
        image.save()
        print(f'sheet {image.filepath_raw}')


arguments = sys.argv[sys.argv.index('--') + 1 :]
glb_path, frame_count = GLB, 10
if '--glb' in arguments:
    glb_path = arguments.pop(arguments.index('--glb') + 1)
    arguments.remove('--glb')
if '--frames' in arguments:
    frame_count = int(arguments.pop(arguments.index('--frames') + 1))
    arguments.remove('--frames')
main(arguments[0], glb_path, frame_count, arguments[1:])
