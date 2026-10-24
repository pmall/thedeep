# Art direction

Read this before any visual work. Graphics and art direction are the owner's top priority after "the game is playable".

## The look

**Overall style:** the **ambiance of *Metroid Dread*** (its white areas with a touch of purple; north star [`perfect.jpg`](references/perfect.jpg), "perfect" per the owner) and a **character in the style of *NieR: Automata***. The references are about colors and graphical style, not about props or venues. Details in the reference board below. Everything should feel **graceful** ([vision.md](vision.md)): restraint, clean shapes, fluid motion.

- **A bright stage in a dark frame.** The play area is pale and misty (blue-white surfaces, lavender haze, white light shafts). The edges of the screen are dark silhouettes. The result is moody and high-contrast, never flat pastel.
- **White plus one accent.** The base is pale blue-white and cool grey. The accent is a saturated **purple/magenta**, used sparingly, roughly 10–15% of the screen at most.
- **The palette tracks depth:** pale and misty near the surface, with the accent and light getting warmer with depth, down to dark amber and ember near the core. Machine colors follow the same palette. *Proposed; owner to confirm.*
- Environments: ruined pilgrim machinery, drowned halls of ancient servers, industrial cathedrals. (The setting decides the props, not the references.)
- **Three depth layers:** a blurred foreground, the play plane, and a hazy backdrop with parallax. The camera is a narrow-FOV perspective camera (not orthographic) so these layers really move at different speeds.
- Gameplay readability first: the player, hazards and grippable **metal** versus non-grippable **rock** must read at a glance in every lighting setup.

## Production rules

1. **Procedural only, pushed as far as possible** (owner decision). All visuals come from code: shaders, generated meshes, lighting, fog, particles, post-processing. No imported textures or models for now.
2. **Everything visual is swappable.** Another art source comes later. Materials, kit pieces and machine parts are referenced through resources, so a procedural version can be replaced by an authored one without touching gameplay or rooms.
3. **Visuals never decide gameplay.** Rock versus metal is the physics layer, not the material. Collision shapes are simple and kept separate from visual meshes.
4. **Judge with images, not code.** For every visual change, render captures before and after and look at them (see [testing.md](testing.md)). Keep within the frame budget: 1080p at 60 fps on the owner's RTX 3060, target ≤ 10 ms per frame.
5. The player and the machines share one **modular part system**. Building it is both an art task and the foundation of the bestiary ([long-term.md](long-term.md)).

## Reference board

The owner's screenshots are in `docs/references/` (Godot ignores the `docs/` folder). **They are about colors and graphical style only. Don't copy their props, venues or layouts.** The owner's split:
- **Ambiance (world, light, color): *Metroid Dread*,** especially its white areas with a touch of purple.
- **Character (the android): *NieR: Automata*.**

To view the `.avif` file, convert it first: `ffmpeg -i in.avif out.png` in the scratchpad.

### Ambiance: *Metroid Dread*
Images: [`perfect.jpg`](references/perfect.jpg) (**north star**), [`dread-white-lab.jpeg`](references/dread-white-lab.jpeg), [`dread-station-melee.avif`](references/dread-station-melee.avif).
- **Colors:** pale blue-white and cool grey base, lavender-grey mist, with saturated magenta-purple as the only strong decor color (`perfect.jpg`). Black for structure and the void below the play plane. Small warm accents on the player pop against it.
- **Color means gameplay:** the world is almost monochrome, so saturated emissive colors (like Dread's red hazard pads and door locks) are reserved for hazards, interactables, doors and pickups.
- **Lighting:** cool key light, one strong light shaft visible in drifting particles, rim light on glossy pale surfaces, a gentle bloom on emissives.
- **Surfaces:** clean, smooth, slightly glossy "ceramic-metal" with crisp bevels and panel lines, with wear and grime kept subtle. Readable shapes, never noisy.
- **Depth:** dark framing foreground, bright play plane, hazy backdrop, with fog between them. Edges of the screen darker than the center.
- **Effects:** crisp white arcs and sparks on hits, soft volumetric mist.
- **Concrete targets:**
  - The Flooded `BiomeProfile` reproduces the `perfect.jpg` palette: pale base, lavender fog, magenta accent (M0, M3). Side-by-side capture against `perfect.jpg` to judge it.
  - A color-rule check in captures: saturated emissive only on gameplay elements (M1–M3).
  - A glossy pale ceramic-metal material with bevels and rim light; rock stays clearly different from metal (M1).
  - Light shafts with visible particles in about 20% of rooms; vignette plus foreground silhouettes darken the edges (M3).
  - White slash arc and spark VFX for the basic attack (M4, M6).

### Character: *NieR: Automata*
Images: [`nier-tank-catwalk.jpg`](references/nier-tank-catwalk.jpg), [`nier-amusement-park.jpeg`](references/nier-amusement-park.jpeg), [`nier-factory-furnace.png`](references/nier-factory-furnace.png) (all show 2B or 9S in the 2D side-scrolling sections).
- **Silhouette:** a slender, elegant humanoid android, human proportions, small next to the world. Instantly readable at gameplay size.
- **Colors:** mostly black, with white/silver highlights (hair, trims) and very little else. It stays readable against the pale Dread world through value contrast (dark on light). One small glowing element can carry the purple accent.
- **Motion:** graceful and fast, acrobatic, with flowing secondary motion (skirt or coat tails, hair, ribbons, cables) trailing behind moves.
- **Fits the theme:** NieR's androids are human-built machines, as is ours ([theme.md](theme.md)), so it should look foreign among the native machines.
- **Concrete targets:**
  - The M4 pilgrim follows this: humanoid, dark with white highlights, flowing secondary motion, built from the part system.
  - In every capture, the android is the highest-contrast element on the play plane.

### Summary: the game's look in one line
A pale, misty *Dread* world in white, grey and a touch of purple, where color means gameplay, crossed by a dark, elegant *NieR* android.

## Current state

Greybox shapes with a first palette pass (M0):
- the **Flooded** `BiomeProfile` (`resources/biomes/flooded.tres`) gives the `perfect.jpg` family of colors: grey-blue stage, lavender haze, magenta accents on a few backdrop pieces, near-black foreground. Rock is a warm-neutral grey, metal a cool blue-white: they read apart.
- flat colored boxes (one placeholder shader reading the biome palette), depth fog, depth of field, a cool key light, one lamp
- a capsule player with an emissive visor, grey and low in contrast against the pale world (M4 makes it a dark android)
- a **Volcanic** draft profile shows the re-theme works; it is not tuned (rock and metal too close).

Compared with `perfect.jpg` it still lacks the light shaft, vignette and dark frame, surface detail and bloom on a few emissives: M1 and M3. Judge with `tools/capture.sh` against the reference.
