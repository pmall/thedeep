# Art direction

Read this before any visual work. Graphics and art direction are the owner's top priority after "the game is playable".

## The look

- Dark and heavy, not pastel.
- **The palette tracks depth:** cold teal and rust near the surface, shifting to deep red and ember orange near the core. Machine colors follow the same palette.
- Environments: ruined pilgrim machinery, drowned halls of ancient servers, industrial cathedrals.
- **Three depth layers:** a blurred foreground, the play plane, and a hazy backdrop with parallax. The camera is a narrow-FOV perspective camera (not orthographic) so these layers really move at different speeds.
- Gameplay readability first: the player, hazards and grippable **metal** versus non-grippable **rock** must read at a glance in every lighting setup.

## Production rules

1. **Procedural only, pushed as far as possible** (owner decision). All visuals come from code: shaders, generated meshes, lighting, fog, particles, post-processing. No imported textures or models for now.
2. **Everything visual is swappable.** Another art source comes later. Materials, kit pieces and machine parts are referenced through resources, so a procedural version can be replaced by an authored one without touching gameplay or rooms.
3. **Visuals never decide gameplay.** Rock versus metal is the physics layer, not the material. Collision shapes are simple and kept separate from visual meshes.
4. **Judge with images, not code.** For every visual change, render captures before and after and look at them (see [testing.md](testing.md)). Keep within the frame budget: 1080p at 60 fps on the owner's RTX 3060, target ≤ 10 ms per frame.
5. The player and the machines share one **modular part system**. Building it is both an art task and the foundation of the bestiary ([long-term.md](long-term.md)).

## Reference board

*To be filled in at the start of the next session from the owner's screenshots.*

For each reference image:
- **Source:** the game or artist and the scene.
- **What to take:** palette, lighting, composition, depth layering, materials, effects or mood, and what *not* to take.
- **Concrete targets:** checkable items that feed roadmap milestones M1–M4, for example "light shafts visible in dust, roughly 20% of rooms".

Image files, if any, go in `docs/references/` (Godot ignores the `docs/` folder).

## Current state

Greybox only:
- rock blocks are grey-brown, metal blocks blue-grey
- a teal ambient light and a cold key light, plus one rust-orange lamp
- depth fog, depth of field
- scattered backdrop and foreground boxes
- a capsule player with an emissive visor

All of this is placeholder, to be replaced by roadmap Phase 1.
