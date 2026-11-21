"""Builds the pilgrim: its skeleton (skeleton.py), its body (mannequin.py, until the model exists) and every clip of
clips.py retargeted from motion capture (mocap.py), exported as one glTF scene the game imports.

    blender --background --factory-startup --python art/pilgrim/build.py              writes assets/pilgrim/pilgrim.glb
    blender --background --factory-startup --python art/pilgrim/build.py -- --blend X  also saves the scene to X to look at

Run from the project's root. The export is committed; Blender is never part of the check
(instructions/toolchain.md#blender-mcp).
"""

import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import clips
import mannequin
import mocap
import skeleton

ROOT = os.path.dirname(os.path.dirname(HERE))
OUTPUT = os.path.join(ROOT, 'assets', 'pilgrim', 'pilgrim.glb')


def main(args: list[str]) -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    assert scene is not None
    scene.render.fps = mocap.FPS

    rig = skeleton.build('Pilgrim')
    mannequin.build(rig)
    for clip in clips.CLIPS:
        print(f'clip {clip.name} from {clip.source}')
        mocap.bake(rig, clip, os.path.join(ROOT, 'art', 'mocap'))

    # The exporter offers an action to the rig only once the rig has animation data.
    rig.animation_data_create()
    os.makedirs(os.path.dirname(OUTPUT), exist_ok=True)
    bpy.ops.export_scene.gltf(
        filepath=OUTPUT,
        export_format='GLB',
        export_animations=True,
        export_animation_mode='ACTIONS',
        export_force_sampling=True,
        export_anim_slide_to_zero=True,
        export_reset_pose_bones=True,
        export_def_bones=False,
        export_morph=False,
        export_yup=True,
    )
    print(f'wrote {OUTPUT}')
    if '--blend' in args:
        bpy.ops.wm.save_as_mainfile(filepath=args[args.index('--blend') + 1])


main(sys.argv[sys.argv.index('--') + 1 :] if '--' in sys.argv else [])
