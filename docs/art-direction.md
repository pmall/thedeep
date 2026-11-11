# Art direction

Read this before any visual work. Every room is built to this direction. How a material or effect is made is in its shader's header comment (`assets/shaders/`).

## The look <a id="look"></a>

**Overall style** (owner): the **ambiance of *Metroid Dread*** (its white areas with a touch of purple; north star [`perfect.jpg`](references/perfect.jpg)) and a **character in the style of *NieR: Automata***. The references are about colors and graphical style, not about props or venues (`docs/art-direction.md#references`). Everything should feel **graceful** (`docs/vision.md#graceful`).

- **A bright stage in a dark frame.** The play area is pale and misty (blue-white surfaces, lavender haze, white light shafts). The edges of the screen are dark silhouettes. The result is moody and high-contrast, never flat pastel.
- **White plus one accent.** The base is pale blue-white and cool grey. The accent is a saturated **purple/magenta**, used sparingly, roughly 10–15% of the screen at most.
- **The palette tracks depth:** pale and misty near the surface, the accent and light getting warmer with depth, down to dark amber and ember near the core. Machine colors follow it (`docs/art-direction.md#machine-palette`).
- Environments: ruined pilgrim machinery, drowned halls of ancient servers, industrial cathedrals. The setting decides the props, not the references.
- **Three depth layers:** a blurred foreground, the play plane, and a hazy backdrop with parallax (the camera: `docs/architecture.md#tech-stack`).
- Gameplay readability first: the player, hazards and grippable **metal** versus non-grippable **rock** must read at a glance in every lighting setup.

## Production rules <a id="production-rules"></a>

- **The world is procedural; the pilgrim is modeled** <a id="procedural-first"></a> (owner). Rooms, materials, machines, light, fog and particles come from code (shaders, generated meshes, textures baked by tools) and must look good that way. The pilgrim is modeled, rigged and animated in Blender (`docs/roadmap.md#pilgrim`). Blender for anything else is the owner's call.
- **Everything visual is swappable** <a id="swappable"></a>. Materials, kit pieces and machine parts are referenced through resources, so a procedural version can be replaced by an authored one without touching gameplay or rooms. A material is a `.tres` referenced by scenes, never a shader assigned in code.
- **Visuals never decide gameplay** <a id="visuals-never-gameplay"></a>. Rock versus metal is the physics layer, not the material. Collision is simple boxes (`Block`), kept apart from visual meshes: kit pieces are visual only.
- **Judge with images, not code** <a id="judge-images"></a>. For every visual change, render captures before and after and look at them (`instructions/testing.md#lookdev`), within the frame budget (`instructions/principles.md#performance`).
- **No flicker, ever** <a id="stable-image"></a> (owner). Held by construction; the second and third by a guard in the fast check (`tools/stable_image.py`):
  - Anti-aliasing stays on: 4x MSAA and TAA.
  - Nothing random per pixel per frame (no grain, no dither).
  - No pass over the finished image that decides per pixel (posterize, outlines, painterly filters), and no shader reads the screen. Color grading stays in the Environment. The vignette is allowed: a fixed multiply whose factor depends only on screen position (`vignette.gdshader`).
  - Fine surface detail comes from textures with mipmaps (procedural patterns are baked to textures), never computed per pixel in a material. Procedural math in a shader only draws shapes several pixels wide at the gameplay camera's distance.
  - Text in the world is a distance-field font with mipmaps.
  - No two faces share a plane: they fight for the same depth, and TAA's sub-pixel jitter shows one, then the other. A mesh's top keeps clear of a water surface over it; the room check holds that one (`RoomValidator`).
- **Machines are built from shared parts** <a id="shared-parts"></a>: one part system, the foundation of the bestiary (`docs/long-term.md#machines-distinct`).
- **The play plane is the brightest layer** <a id="midground"></a>: pieces behind it use the darker, duller `*_mid` materials, so only grippable metal reads as bright pale metal. Keep heavy rust off metal where the grip read matters most: its value gets close to rock.
- **Machines against the pilgrim** <a id="machine-palette"></a>: native machines take the biome palette, shells at mid value with dark limbs, the saturated accent only on small devotional plates; the pilgrim keeps its own colors (human-built, foreign at every depth) and stays the highest-contrast figure. Eyes turn the hazard color only on hostile machines, alert or striking (color means gameplay).
- **No SDFGI**: it cost 1.7 ms on average, with p99 over budget, for a barely visible change in this lighting; VoxelGI needs a bake. Revisit if rooms get darker interiors.

## Reference board <a id="references"></a>

The owner's screenshots are in `docs/references/` (Godot ignores `docs/`). **They are about colors and graphical style only. Don't copy their props, venues or layouts.** To view the `.avif`, convert it first: `ffmpeg -i in.avif out.png` in the scratchpad.

### Ambiance: *Metroid Dread* <a id="ambiance"></a>

Images: [`perfect.jpg`](references/perfect.jpg) (**north star**), [`dread-white-lab.jpeg`](references/dread-white-lab.jpeg), [`dread-station-melee.avif`](references/dread-station-melee.avif).

- **Colors:** pale blue-white and cool grey base, lavender-grey mist, saturated magenta-purple as the only strong decor color. Black for structure and the void below the play plane. Small warm accents on the player pop against it.
- **Color means gameplay:** the world is almost monochrome, so saturated emissive colors (like Dread's red hazard pads and door locks) are reserved for hazards, interactables, doors and pickups.
- **Lighting:** cool key light, one strong light shaft visible in drifting particles, rim light on glossy pale surfaces, a gentle bloom on emissives.
- **Surfaces:** clean, smooth, slightly glossy "ceramic-metal" with crisp bevels and panel lines, wear and grime kept subtle. Readable shapes, never noisy.
- **Depth:** dark framing foreground, bright play plane, hazy backdrop, with fog between them. Edges of the screen darker than the center.
- **Effects:** crisp white arcs and sparks on hits, soft mist.
- **Targets:** the Flooded `BiomeProfile` reproduces the `perfect.jpg` palette (judged side by side with it); saturated emissive only on gameplay elements; a glossy pale ceramic-metal clearly apart from rock; light shafts with visible particles in about 20% of rooms; foreground silhouettes darkening the edges.

### Character: *NieR: Automata* <a id="character"></a>

Images: [`nier-tank-catwalk.jpg`](references/nier-tank-catwalk.jpg), [`nier-amusement-park.jpeg`](references/nier-amusement-park.jpeg), [`nier-factory-furnace.png`](references/nier-factory-furnace.png) (2B or 9S in the 2D side-scrolling sections). The bar for its animation is *Ori* (`docs/roadmap.md#pilgrim`).

- **Silhouette:** a slender, elegant humanoid android, human proportions, small next to the world. Instantly readable at gameplay size.
- **Colors:** mostly black, with white/silver highlights (hair, trims) and very little else. It reads against the pale world through value contrast (dark on light). One small glowing element can carry the purple accent. In every capture it is the highest-contrast element on the play plane.
- **Motion:** graceful and fast, acrobatic, with flowing secondary motion (coat tails, hair, cables) trailing behind moves. Never upright and locked (owner): the body moves loose over the legs, every part answering the stride and the changes of speed.
- **Strikes are sword moves** <a id="strikes"></a> (owner). A short pale blade, lit from within so it reads on any backdrop, springs out of the forearm for a strike and folds away after. One hit coils, cuts, follows through and holds its end pose a moment, the whole body driving it. The three-hit chain is choreographed: each hit starts where the last ended and the rhythm builds to the finisher, the biggest arc with the most weight and the longest hold. Each strike leaves a crisp white slash along the blade's arc, fading within about a tenth of a second; the arms cut in the side plane so every slash draws its arc on screen. The gameplay timing (`docs/gameplay.md#fight`) is fixed: the animation fits inside it.
- **Fits the theme:** NieR's androids are human-built machines, as is ours ([theme.md](theme.md)), so it looks foreign among the native machines.

### The look in one line <a id="look-in-one-line"></a>

A pale, misty *Dread* world in white, grey and a touch of purple, where color means gameplay, crossed by a dark, elegant *NieR* android.
