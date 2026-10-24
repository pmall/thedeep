# Art direction

Read this before any visual work. Graphics and art direction are the owner's top priority after "the game is playable".

## The look

**North star: [`perfect.jpg`](references/perfect.jpg)** (owner: "perfect"). It shows the white side of *Metroid Dread* with a touch of purple, staged like *NieR: Automata*'s 2D side-scrolling sections. Details in the reference board below.

- **A bright stage in a dark frame.** The play area is pale and misty (blue-white stone, lavender haze, white light shafts). The edges of the screen are dark silhouettes. The result is moody and high-contrast, never flat pastel.
- **White plus one accent.** The base is pale blue-white and cool grey. The accent is a saturated **purple/magenta**, used sparingly (growths, cables, glyphs, ornaments), roughly 10–15% of the screen at most.
- **The palette tracks depth:** the base stays pale and misty near the surface, then the accent and light get warmer with depth, ending in NieR-style furnace amber and ember near the core (see the board). Machine colors follow the same palette. *Proposed; owner to confirm.*
- Environments: ruined pilgrim machinery, drowned halls of ancient servers, industrial cathedrals, and huge pale machine idols (the shrine in `perfect.jpg`).
- **Three depth layers:** a blurred foreground, the play plane, and a hazy backdrop with parallax. The camera is a narrow-FOV perspective camera (not orthographic) so these layers really move at different speeds.
- Gameplay readability first: the player, hazards and grippable **metal** versus non-grippable **rock** must read at a glance in every lighting setup.

## Production rules

1. **Procedural only, pushed as far as possible** (owner decision). All visuals come from code: shaders, generated meshes, lighting, fog, particles, post-processing. No imported textures or models for now.
2. **Everything visual is swappable.** Another art source comes later. Materials, kit pieces and machine parts are referenced through resources, so a procedural version can be replaced by an authored one without touching gameplay or rooms.
3. **Visuals never decide gameplay.** Rock versus metal is the physics layer, not the material. Collision shapes are simple and kept separate from visual meshes.
4. **Judge with images, not code.** For every visual change, render captures before and after and look at them (see [testing.md](testing.md)). Keep within the frame budget: 1080p at 60 fps on the owner's RTX 3060, target ≤ 10 ms per frame.
5. The player and the machines share one **modular part system**. Building it is both an art task and the foundation of the bestiary ([long-term.md](long-term.md)).

## Reference board

From the owner's screenshots in `docs/references/` (Godot ignores the `docs/` folder). The owner loves *Metroid Dread*, especially its white areas with a touch of purple, and *NieR: Automata* when it plays as a 2D side-scroller. To view the `.avif` file, convert it first: `ffmpeg -i in.avif out.png` in the scratchpad.

### [`perfect.jpg`](references/perfect.jpg): *Metroid Dread*, the white shrine with purple foliage. **North star.**
- **What to take:**
  - **Palette:** pale blue-white stone and metal, lavender-grey mist, with saturated magenta-purple foliage as the only strong color. The player's small warm accents (orange, green) pop against it.
  - **Lighting:** one strong cool light shaft from top left, visible in drifting particles. Glossy pale surfaces catch rim light.
  - **Composition:** a huge carved idol, centered behind the play plane, gives the room its identity. Dark statues and foliage frame both screen edges. The floor is a pale, cracked slab that reads instantly as walkable.
  - **Depth:** three clear layers (dark framing foreground, bright midground, hazy backdrop), with fog separating them.
- **Not to take:** organic trees as-is. Translate the purple foliage into machine-world equivalents: cable tangles, crystalline growths, prayer ribbons, corroded coral.
- **Concrete targets:**
  - The Flooded `BiomeProfile` reproduces this palette: pale base, lavender fog, magenta accent (M0, M3).
  - At least one "idol room" per layer: a giant pale machine shrine behind the play plane, the landmark of that room (M2, M3).
  - Light shafts with visible particles in about 20% of rooms (M3).
  - Screen edges darker than the center in most rooms, through foreground silhouettes plus a vignette (M3).
  - A glossy pale "ceramic-metal" material with crisp bevels and rim light (M1).

### [`dread-white-lab.jpeg`](references/dread-white-lab.jpeg): *Metroid Dread*, white lab corridor
- **What to take:** an almost monochrome world (white, grey, black) where **color only means gameplay**. Red glowing pads and door locks show hazards and interactables. Black structural beams frame clean white panels, with steam and ceiling strip lights.
- **Not to take:** sterile, flat walls over a whole layer. Keep wear and grime.
- **Concrete targets:**
  - A color rule, checked in captures: a saturated emissive color is reserved for gameplay (hazards, interactables, doors, pickups). Decor uses the base palette and the purple accent only.
  - Steam and strip lights in the kit and particle sets (M2, M3).

### [`dread-station-melee.avif`](references/dread-station-melee.avif): *Metroid Dread*, station interior with a melee counter
- **What to take:** walls made of dense **modular panels** (hatches, vents, pipes, screens) that are readable but never noisy. A crisp **white slash arc** and spark burst on hits. A black void below the floor gives a strong silhouette edge to the play plane. Cyan holo-screens as small lights.
- **Not to take:** the busy green and yellow palette of this area.
- **Concrete targets:**
  - Kit wall panels with layered depth (inset hatches, vents, pipes) (M2).
  - A white arc and spark VFX for the basic attack (M4, M6).
  - The edge of the play plane is always readable against what is behind it.

### [`nier-tank-catwalk.jpg`](references/nier-tank-catwalk.jpg): *NieR: Automata*, side-scrolling run on a catwalk along a giant tank
- **What to take:** **scale**. A tiny figure against gigantic industrial structures (a tank wall, cranes, lattice towers). Washed-out, sun-bleached haze, with soft, desaturated warm-grey light and hard railing shadows. The camera stays side-on but the world is fully 3D and curves away.
- **Concrete targets:**
  - Each layer has backdrop megastructures at least 10× the player's height (M3).
  - Railings, lattice girders and cables as kit pieces, casting real shadows on the play plane (M2).
  - Strong atmospheric perspective: far objects fade to the fog color (M3).

### [`nier-amusement-park.jpeg`](references/nier-amusement-park.jpeg): *NieR: Automata*, amusement park ride sequence
- **What to take:** a lavender-pink dusk haze over pale ruins. Machine-made celebration (fireworks) in a dead world: melancholy plus wonder. Big curved silhouettes (Ferris wheel, roller-coaster arcs) in the backdrop.
- **Concrete targets:**
  - Backdrop silhouettes with large curves and rings, not only boxes (M2, M3).
  - Moments where the native machines' faith is visible as spectacle: lights, ceremonies, glowing glyphs (M5 and later).

### [`nier-factory-furnace.png`](references/nier-factory-furnace.png): *NieR: Automata*, factory interior
- **What to take:** the deep-layer look. A near-black interior lit by **amber furnace glow** through grates and windows, with teal-grey pipes and scaffolding in the foreground. Small round machines with a single glowing eye (a good base for native species). Minimal, thin UI lines.
- **Concrete targets:**
  - The Volcanic `BiomeProfile`: dark base and amber/ember light sources (M8+).
  - Foreground scaffolding that frames the action without hiding it (M3).
  - Thin, minimal HUD lines, NieR style (Phase 4).

### Summary: the game's look in one line
Pale, misty *Dread* shrines with purple accents near the surface, NieR-scale industrial megastructures throughout, sinking into dark amber furnaces at the core. Color means gameplay. The android reads clean and bright against it all.

## Current state

Greybox only:
- rock blocks are grey-brown, metal blocks blue-grey
- a teal ambient light and a cold key light, plus one rust-orange lamp
- depth fog, depth of field
- scattered backdrop and foreground boxes
- a capsule player with an emissive visor

All of this is placeholder, to be replaced by roadmap Phase 1.
