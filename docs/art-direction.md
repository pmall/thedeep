# Art direction

Read this before any visual work. Every room is built to this direction. In the prototype, `docs/roadmap.md#stage-bricks` and `docs/roadmap.md#pilgrim` are where the look is pushed to its bar, so that the real game is assembled from pieces that already look right. How a material or effect is made is in its shader's header comment (`assets/shaders/`).

## The look <a id="look"></a>

**Overall style:** the **ambiance of *Metroid Dread*** (its white areas with a touch of purple; north star [`perfect.jpg`](references/perfect.jpg), "perfect" per the owner) and a **character in the style of *NieR: Automata***. The references are about colors and graphical style, not about props or venues. Details in `docs/art-direction.md#references`. Everything should feel **graceful** (`docs/vision.md#graceful`).

- **A bright stage in a dark frame.** The play area is pale and misty (blue-white surfaces, lavender haze, white light shafts). The edges of the screen are dark silhouettes. The result is moody and high-contrast, never flat pastel.
- **White plus one accent.** The base is pale blue-white and cool grey. The accent is a saturated **purple/magenta**, used sparingly, roughly 10–15% of the screen at most.
- **The palette tracks depth:** pale and misty near the surface, with the accent and light getting warmer with depth, down to dark amber and ember near the core. Machine colors follow the same palette (`docs/art-direction.md#machine-palette`). *Decided 2026-11-07, owner's delegation.*
- Environments: ruined pilgrim machinery, drowned halls of ancient servers, industrial cathedrals. (The setting decides the props, not the references.)
- **Three depth layers:** a blurred foreground, the play plane, and a hazy backdrop with parallax (the camera: `docs/architecture.md#tech-stack`).
- Gameplay readability first: the player, hazards and grippable **metal** versus non-grippable **rock** must read at a glance in every lighting setup.

## Production rules <a id="production-rules"></a>

- **Procedural first, pushed to the maximum; Blender for the pilgrim** <a id="procedural-first"></a> (owner, 2026-11-01; Blender for the pilgrim, 2026-11-02). All visuals come from code first: shaders, generated meshes, lighting, fog, particles, post-processing. The game must look good this way alone. Blender (an MCP the owner provides) comes in for the pilgrim, once its procedural version has been pushed as far as it goes (`docs/roadmap.md#pilgrim`), to finish what code does worst on it (`docs/roadmap.md#beyond-procedural`). Whether it is used for anything else is the owner's call, later. Never use Blender to make up for a procedural look that wasn't pushed.
- **Everything visual is swappable** <a id="swappable"></a>. Materials, kit pieces and machine parts are referenced through resources, so a procedural version can be replaced by an authored one without touching gameplay or rooms. A material is a `.tres` referenced by scenes, never a shader assigned in code.
- **Visuals never decide gameplay** <a id="visuals-never-gameplay"></a>. Rock versus metal is the physics layer, not the material. Collision is simple boxes (`Block`), kept apart from visual meshes: kit pieces are visual only.
- **Judge with images, not code** <a id="judge-images"></a>. For every visual change, render captures before and after and look at them (`docs/testing.md#lookdev`), within the frame budget (`docs/principles.md#performance`).
- **No flicker, ever** <a id="stable-image"></a> (owner, 2026-11-08: "the goal is no flickering"). Held by construction:
  - Anti-aliasing stays on: 4x MSAA and TAA.
  - Nothing random per pixel per frame (no grain, no dither).
  - No pass over the finished image that decides per pixel (posterize, outlines, painterly filters). Color grading stays in the Environment.
  - Fine surface detail comes from textures with mipmaps (procedural patterns are baked to textures), never computed per pixel in a material. Procedural math in a shader only draws shapes several pixels wide at the gameplay camera's distance.
  - Text in the world is a distance-field font with mipmaps.
- **One part system** <a id="shared-parts"></a> for the pilgrim and the machines. Building it is both an art task and the foundation of the bestiary (`docs/long-term.md#machines-distinct`).
- **The play plane is the brightest layer** <a id="midground"></a>: pieces behind it use the darker, duller `*_mid` materials, so only grippable metal reads as bright pale metal. Keep heavy rust off metal where the grip read matters most: its value gets close to rock.
- **Machines against the pilgrim** <a id="machine-palette"></a>: native machines take the biome palette, shells at mid value with dark limbs, the saturated accent only on small devotional plates; the pilgrim keeps its own colors (human-built, foreign at every depth) and stays the highest-contrast figure. Eyes turn the hazard color only on hostile machines, alert or striking (color means gameplay).

## Reference board <a id="references"></a>

The owner's screenshots are in `docs/references/` (Godot ignores the `docs/` folder). **They are about colors and graphical style only. Don't copy their props, venues or layouts.** The owner's split:

- **Ambiance (world, light, color): *Metroid Dread*,** especially its white areas with a touch of purple.
- **Character (the android): *NieR: Automata*.**
- Earlier references, now secondary: *Ori*, *Hollow Knight: Silksong*, *Castlevania: Belmont's Curse*.

To view the `.avif` file, convert it first: `ffmpeg -i in.avif out.png` in the scratchpad.

### Ambiance: *Metroid Dread* <a id="ambiance"></a>

Images: [`perfect.jpg`](references/perfect.jpg) (**north star**), [`dread-white-lab.jpeg`](references/dread-white-lab.jpeg), [`dread-station-melee.avif`](references/dread-station-melee.avif).

- **Colors:** pale blue-white and cool grey base, lavender-grey mist, with saturated magenta-purple as the only strong decor color (`perfect.jpg`). Black for structure and the void below the play plane. Small warm accents on the player pop against it.
- **Color means gameplay:** the world is almost monochrome, so saturated emissive colors (like Dread's red hazard pads and door locks) are reserved for hazards, interactables, doors and pickups.
- **Lighting:** cool key light, one strong light shaft visible in drifting particles, rim light on glossy pale surfaces, a gentle bloom on emissives.
- **Surfaces:** clean, smooth, slightly glossy "ceramic-metal" with crisp bevels and panel lines, with wear and grime kept subtle. Readable shapes, never noisy.
- **Depth:** dark framing foreground, bright play plane, hazy backdrop, with fog between them. Edges of the screen darker than the center.
- **Effects:** crisp white arcs and sparks on hits, soft volumetric mist.
- **Concrete targets:**
  - The Flooded `BiomeProfile` reproduces the `perfect.jpg` palette: pale base, lavender fog, magenta accent. Side-by-side capture against `perfect.jpg` to judge it.
  - A color-rule check in captures: saturated emissive only on gameplay elements.
  - A glossy pale ceramic-metal material with bevels and rim light; rock stays clearly different from metal.
  - Light shafts with visible particles in about 20% of rooms; foreground silhouettes darken the edges.
  - White slash and spark VFX for the strikes: the slash follows the blade's own arc (`docs/art-direction.md#strikes`).

### Character: *NieR: Automata* <a id="character"></a>

Images: [`nier-tank-catwalk.jpg`](references/nier-tank-catwalk.jpg), [`nier-amusement-park.jpeg`](references/nier-amusement-park.jpeg), [`nier-factory-furnace.png`](references/nier-factory-furnace.png) (all show 2B or 9S in the 2D side-scrolling sections).

- **Silhouette:** a slender, elegant humanoid android, human proportions, small next to the world. Instantly readable at gameplay size.
- **Colors:** mostly black, with white/silver highlights (hair, trims) and very little else. It stays readable against the pale Dread world through value contrast (dark on light). One small glowing element can carry the purple accent.
- **Motion:** graceful and fast, acrobatic, with flowing secondary motion (skirt or coat tails, hair, ribbons, cables) trailing behind moves. Never upright and locked (owner feedback, 2026-11-08: the walk and run were too rigid, "like it has some stick up the ass"): the body moves loose over the legs, every part answering the stride and the changes of speed. How far each part goes is set in `PilgrimMotion` (decided 2026-11-08, owner's delegation).
- **Strikes are sword moves** <a id="strikes"></a> (owner feedback, 2026-11-08: "the combat animation could be more developed too. Like a sword move. One hit must look cool, but a 3 hit combo should be more choreographed. Dynamism!"). Decided 2026-11-08 under the owner's delegation, for the owner to overturn: in the *NieR: Automata* spirit, a short pale blade, lit from within so it reads as a bright edge on any backdrop, springs out of the near forearm for a strike and folds away after (no new item, no story change; a part of the pilgrim's assembly, swappable like any other, `docs/art-direction.md#swappable`), and each strike leaves a slash along the blade's real arc, a crisp white edge over a pale veil, that fades within about a tenth of a second and ends with the follow-through.
  - **One hit:** a short coil (the anticipation), a fast committed cut, a follow-through, then its end pose held a moment before it lets go. The whole body drives it: the hips turn first, the chest after them, the arm last and the hand whipping behind; standing, the near foot steps into it.
  - **The chain is choreographed:** each hit starts from where the last ended, and the rhythm builds. A forehand cut from over the shoulder down through the front; a backhand dropped low behind and swept back up through the front, the body rising with it, ending high; then the finisher, arched back on a raised knee, the blade cocked far behind the head, brought over the top down into a deep lunge, the biggest arc, the most weight and the longest hold.
  - **The same style for the rest:** the down strike draws the knees up to the chest and sweeps the blade from ahead down under them; the dash strike draws the blade from the hip low along the dash up through the front.
  - The arms cut in the side plane while the body turns under them, so every slash draws its arc on the screen. The poses and their timing are in `StrikeMotion`; the slash is `SlashTrail` over the core's `BladeTrail`. The gameplay timing (`docs/gameplay.md#fight`) is unchanged: the animation fits inside it.
- **Fits the theme:** NieR's androids are human-built machines, as is ours ([theme.md](theme.md)), so it should look foreign among the native machines.
- **Concrete targets:**
  - The pilgrim follows this: humanoid, dark with white highlights, flowing secondary motion, built from the part system.
  - In every capture, the android is the highest-contrast element on the play plane.

### Summary: the game's look in one line <a id="look-in-one-line"></a>

A pale, misty *Dread* world in white, grey and a touch of purple, where color means gameplay, crossed by a dark, elegant *NieR* android.

## Open issues <a id="art-open"></a>

What the visual work still owes (`docs/roadmap.md#stage-bricks`, `docs/roadmap.md#pilgrim`):

- Flooded back walls (decided 2026-11-07, owner's delegation): the Narthex and the Sacristy have a dressed back wall at 9 m, as the Cell's, with a warm lamp raking it; it reads at the edges and in the wide shots, but where a shaft's beam crosses the room the lit haze in front still veils it to a pale plane (the Narthex's shrine, the Sacristy's dais). The open halls keep the haze's architecture. Each wall costs about 0.5 ms of GPU on average.
- Frozen: the Underice's water is clearer (decided 2026-11-07, owner's delegation; `water_section_clear.tres`, that cave only): its section sank the play plane, twenty meters and more under the surface, almost wholly into the deep color, which is why its lamps never reached the camera. Its walls, ice and floor now read, and the pilgrim's outline reads against them (see the pilgrim's line below).
- The pilgrim's contrast against dark backdrops is settled (decided 2026-11-08, owner's delegation): every black part of it (suit, coat, the shell of the hands, forearms, shins and feet, the joints) carries a faint cool edge along the silhouette that needs no light (`edge_light` in the android shader), one setting for every room, and it loosens as the figure gets small on screen so it never thins under a pixel in a wide shot. Judged in the darkest spots (the Underice's water, the Coldstacks' and the Sounding's racks, the Quench's coolant hall, the Effigy): the whole figure, legs included, reads at a glance at play size and stays black, not lamp-lit, and the Narthex's bright rooms see no change. The Underice's wide shot from outside its water stays veiled, the pilgrim with the rest of the room; the play camera is never that far. The cap of the hair is still one smooth dome over its ragged locks: its crown (strands or a parting) waits for the Blender MCP (`docs/roadmap.md#beyond-procedural`) (decided 2026-11-07, owner's delegation).
- Interactables (decided 2026-11-07, owner's delegation): their glows keep their kind's hue through the tonemapper (`token_glow`'s `chroma`, at a lower energy, so no blob); the defense reads azure and the harpoon hoop gold, the key still a light amber in the Scriptorium's bright light. A code is green now, off every biome's accent; it is not yet seen in a capture.

## Decisions <a id="art-decisions"></a>

- **Looks kept as they are** <a id="art-kept"></a> (decided 2026-11-07, owner's delegation): the flooded play plane's brightness (a little darker and bluer than `perfect.jpg` outside the shaft), the core's murky walls and the flooded rooms' wide shots, the machines' plated limbs with their banded trims and the flyers' collar, Rimefall. The harpoon anchor's far and spent looks are dimmer (`HarpoonAnchor`).
- **No SDFGI for now** (2026-10-24): +1.7 ms average and p99 over budget for a barely visible change in this lighting. VoxelGI needs a bake. Revisit when rooms get darker interiors.
