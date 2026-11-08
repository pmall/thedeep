# Art direction

Read this before any visual work. Every room is built to this direction. In the prototype, `docs/roadmap.md#stage-bricks` and `docs/roadmap.md#pilgrim` are where the look is pushed to its bar, so that the real game is assembled from pieces that already look right. How a material or effect is made is in its shader's header comment (`assets/shaders/`).

## The look `#look`

**Overall style:** the **ambiance of *Metroid Dread*** (its white areas with a touch of purple; north star [`perfect.jpg`](references/perfect.jpg), "perfect" per the owner) and a **character in the style of *NieR: Automata***. The references are about colors and graphical style, not about props or venues. Details in `docs/art-direction.md#references`. Everything should feel **graceful** (`docs/vision.md#graceful`).

- **A bright stage in a dark frame.** The play area is pale and misty (blue-white surfaces, lavender haze, white light shafts). The edges of the screen are dark silhouettes. The result is moody and high-contrast, never flat pastel.
- **White plus one accent.** The base is pale blue-white and cool grey. The accent is a saturated **purple/magenta**, used sparingly, roughly 10–15% of the screen at most.
- **The palette tracks depth:** pale and misty near the surface, with the accent and light getting warmer with depth, down to dark amber and ember near the core. Machine colors follow the same palette (`docs/art-direction.md#machine-palette`). *Decided 2026-11-07, owner's delegation.*
- Environments: ruined pilgrim machinery, drowned halls of ancient servers, industrial cathedrals. (The setting decides the props, not the references.)
- **Three depth layers:** a blurred foreground, the play plane, and a hazy backdrop with parallax (the camera: `docs/architecture.md#tech-stack`).
- Gameplay readability first: the player, hazards and grippable **metal** versus non-grippable **rock** must read at a glance in every lighting setup.

## The signature `#signature`

Owner, 2026-11-01. The game gets a **visual signature**: one global pass over the image that makes every frame look painted by the same hand, so the game is recognized from a single screenshot. The owner's first idea: **gouache**, a little painted. A shader only this game uses. It is designed first in `docs/roadmap.md#stage-bricks`, before the rest of the visual work. This section is its brief.

**Where it stands** `#signature-state`: the pass is `PaintPass` (start reading there); a layer's `BiomeProfile` names its `PaintStyle` (`resources/paint/`). Three variants are built: **Gouache** (restrained), **Poster** (broader strokes, few flat values) and **Scripture** (the paper's grain is the machines' script, the strokes lean toward a machine's hand; at 1080p its glyphs read as a fine grain of script marks in flat midtones close up, and fade into texture in wide shots). Each biome may take its own style (owner, 2026-11-02). **The pick and its tuning** (decided 2026-11-07 and 2026-11-08, owner's delegation): the flooded and volcanic layers and the core paint with Gouache as built: judged in their rooms, it reads painted and each layer apart at a glance (the flooded grey-violet, the volcanic amber and ash, the core blood red), and the tunings tried there changed nothing visible or, in the core, only darkened its already murky walls (`docs/art-direction.md#art-kept`). The frozen layer paints with **Poster, frozen** (`frozen.tres`, Poster with a steadier hand, a smoother paper and more of the play plane's detail kept): the ice reads clean and flat instead of blotchy, and the pilgrim stays the darkest figure on it. The sequel's brine paints with Poster as built (not yet seen through the tuned pass in a capture). Scripture stays available, unused. F9 cycles them in play, then shows the frame unpainted; `--paint NAME` and `tools/capture.sh --paint NAME` render one. While the paint is on, it replaces the camera's depth of field: the brush simplifies the far, and the blur would smear its strokes. **Its cost**: about 0.9 ms of GPU per frame for the pass itself, and 0.3–0.4 ms over the unpainted frame with its depth of field (Narthex 5.4 against 5.1 ms, Scriptorium 5.2 against 4.8, Choir 6.1 against 5.7; measured 2026-11-08). The strokes are laid at half resolution (a stroke is several pixels wide; the composite's guided upsample keeps their edges against shapes), which costs a quarter of laying them at full resolution, with strokes a little softer; the brush keeps one variance per sector instead of three. Left of it, if it is ever needed: the brush (about 0.24 ms) and the half-res strokes (about 0.25 ms) are its largest steps. Bursts of 10–20 frames at 20–30 ms GPU (in the brush's Kuwahara step, `THEDEEP_PAINT_TIMING=1 tools/capture.sh`, read in the perf file's `slowest_gpu` and `paint_steps_avg_ms`) show in about half the capture recordings on the Linux desktop, where Godot's window goes through Xwayland, and none when it runs as a native Wayland window; the pass's work is fixed and nothing else uses the GPU. They are the Linux desktop's problem, not the game's, and are not chased (owner, 2026-11-07); the game keeps Godot's default display driver, since forcing Wayland broke `godot --path .`. Once a capture run hung in its window at full GPU load until killed (the Choir, 2026-11-08), and the same room rendered normally right after; it may be the same desktop trouble. The single slow frames at fixed frames in some rooms (Kiln #280, the Sacristy's four) follow garbage-collection stalls and come without the paint too.

What gouache means here:

- **Flat, matte, opaque color** laid in areas, with soft brush edges, not photographic gradients. Detail simplified where the eye doesn't need it (the backdrop, the haze), kept where it does (the play plane, the pilgrim).
- **Strokes that follow form**: brushwork oriented along edges and surfaces (an anisotropic Kuwahara-type filter, or strokes driven by the scene's normals and depth), not a uniform blur.
- **Pigment and paper**: a fine grain and granulation in flat areas, a slightly darker rim where colors meet, as paint pools at a stroke's edge. Anchored to the world or to surfaces where it can be, so it doesn't slide over a moving image like a dirty screen.
- **A limited palette**: colors pulled toward the biome palette, so a frame looks mixed from a few pigments.
- **Ours alone**: something tied to the game's world, such as a grain that, up close, is made of the machines' glyphs and data, or strokes that tighten and turn mechanical as the pilgrim breaks its laws. Variants should try at least one idea no off-the-shelf filter gives.

Constraints:

- **Readability first**: the pilgrim, hazards, interactables and metal versus rock must read at least as well as without the pass. Gameplay edges are never smeared away.
- **Graceful**: restraint. Painted, not noisy. It must stay calm in motion: no shimmer, crawling or flicker as the camera moves (judge it from video, not only stills).
- **Inside the frame budget** (`docs/principles.md#performance`): the rooms already sit near 9.6 ms p99, so GPU time must be won back or the pass made cheap (a lower-resolution pass, a compute effect).
- **Swappable and tunable**: one `CompositorEffect`-style pass with its strengths in the `BiomeProfile` (per layer), and a switch to see the frame without it for comparison.
- **Process**: two or three variants rendered on the same photo spots and as a short video; the owner picks; then it is tuned per biome, and every visual judged afterwards is judged through it.

## Production rules `#production-rules`

- **Procedural first, pushed to the maximum; Blender for the pilgrim** `#procedural-first` (owner, 2026-11-01; Blender for the pilgrim, 2026-11-02). All visuals come from code first: shaders, generated meshes, lighting, fog, particles, post-processing. The game must look good this way alone. Blender (an MCP the owner provides) comes in for the pilgrim, once its procedural version has been pushed as far as it goes (`docs/roadmap.md#pilgrim`), to finish what code does worst on it (`docs/roadmap.md#beyond-procedural`). Whether it is used for anything else is the owner's call, later. Never use Blender to make up for a procedural look that wasn't pushed.
- **Everything visual is swappable** `#swappable`. Materials, kit pieces and machine parts are referenced through resources, so a procedural version can be replaced by an authored one without touching gameplay or rooms. A material is a `.tres` referenced by scenes, never a shader assigned in code.
- **Visuals never decide gameplay** `#visuals-never-gameplay`. Rock versus metal is the physics layer, not the material. Collision is simple boxes (`Block`), kept apart from visual meshes: kit pieces are visual only.
- **Judge with images, not code** `#judge-images`. For every visual change, render captures before and after and look at them (`docs/testing.md#lookdev`), within the frame budget (`docs/principles.md#performance`).
- **One part system** `#shared-parts` for the pilgrim and the machines. Building it is both an art task and the foundation of the bestiary (`docs/long-term.md#machines-distinct`).
- **The play plane is the brightest layer** `#midground`: pieces behind it use the darker, duller `*_mid` materials, so only grippable metal reads as bright pale metal. Keep heavy rust off metal where the grip read matters most: its value gets close to rock.
- **Machines against the pilgrim** `#machine-palette`: native machines take the biome palette, shells at mid value with dark limbs, the saturated accent only on small devotional plates; the pilgrim keeps its own colors (human-built, foreign at every depth) and stays the highest-contrast figure. Eyes turn the hazard color only on hostile machines, alert or striking (color means gameplay).

## Reference board `#references`

The owner's screenshots are in `docs/references/` (Godot ignores the `docs/` folder). **They are about colors and graphical style only. Don't copy their props, venues or layouts.** The owner's split:

- **Ambiance (world, light, color): *Metroid Dread*,** especially its white areas with a touch of purple.
- **Character (the android): *NieR: Automata*.**
- Earlier references, now secondary: *Ori*, *Hollow Knight: Silksong*, *Castlevania: Belmont's Curse*.

To view the `.avif` file, convert it first: `ffmpeg -i in.avif out.png` in the scratchpad.

### Ambiance: *Metroid Dread* `#ambiance`

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
  - Light shafts with visible particles in about 20% of rooms; vignette plus foreground silhouettes darken the edges.
  - White slash and spark VFX for the strikes: the slash follows the blade's own arc (`docs/art-direction.md#strikes`).

### Character: *NieR: Automata* `#character`

Images: [`nier-tank-catwalk.jpg`](references/nier-tank-catwalk.jpg), [`nier-amusement-park.jpeg`](references/nier-amusement-park.jpeg), [`nier-factory-furnace.png`](references/nier-factory-furnace.png) (all show 2B or 9S in the 2D side-scrolling sections).

- **Silhouette:** a slender, elegant humanoid android, human proportions, small next to the world. Instantly readable at gameplay size.
- **Colors:** mostly black, with white/silver highlights (hair, trims) and very little else. It stays readable against the pale Dread world through value contrast (dark on light). One small glowing element can carry the purple accent.
- **Motion:** graceful and fast, acrobatic, with flowing secondary motion (skirt or coat tails, hair, ribbons, cables) trailing behind moves. Never upright and locked (owner feedback, 2026-11-08: the walk and run were too rigid, "like it has some stick up the ass"): the body moves loose over the legs, every part answering the stride and the changes of speed. How far each part goes is set in `PilgrimMotion` (decided 2026-11-08, owner's delegation).
- **Strikes are sword moves** `#strikes` (owner feedback, 2026-11-08: "the combat animation could be more developed too. Like a sword move. One hit must look cool, but a 3 hit combo should be more choreographed. Dynamism!"). Decided 2026-11-08 under the owner's delegation, for the owner to overturn: in the *NieR: Automata* spirit, a short pale blade, lit from within so it reads as a bright edge on any backdrop, springs out of the near forearm for a strike and folds away after (no new item, no story change; a part of the pilgrim's assembly, swappable like any other, `docs/art-direction.md#swappable`), and each strike leaves a slash along the blade's real arc, a crisp white edge over a pale veil, that fades within about a tenth of a second and ends with the follow-through.
  - **One hit:** a short coil (the anticipation), a fast committed cut, a follow-through, then its end pose held a moment before it lets go. The whole body drives it: the hips turn first, the chest after them, the arm last and the hand whipping behind; standing, the near foot steps into it.
  - **The chain is choreographed:** each hit starts from where the last ended, and the rhythm builds. A forehand cut from over the shoulder down through the front; a backhand dropped low behind and swept back up through the front, the body rising with it, ending high; then the finisher, arched back on a raised knee, the blade cocked far behind the head, brought over the top down into a deep lunge, the biggest arc, the most weight and the longest hold.
  - **The same style for the rest:** the down strike draws the knees up to the chest and sweeps the blade from ahead down under them; the dash strike draws the blade from the hip low along the dash up through the front.
  - The arms cut in the side plane while the body turns under them, so every slash draws its arc on the screen. The poses and their timing are in `StrikeMotion`; the slash is `SlashTrail` over the core's `BladeTrail`. The gameplay timing (`docs/gameplay.md#fight`) is unchanged: the animation fits inside it.
- **Fits the theme:** NieR's androids are human-built machines, as is ours ([theme.md](theme.md)), so it should look foreign among the native machines.
- **Concrete targets:**
  - The pilgrim follows this: humanoid, dark with white highlights, flowing secondary motion, built from the part system.
  - In every capture, the android is the highest-contrast element on the play plane.

### Summary: the game's look in one line `#look-in-one-line`

A pale, misty *Dread* world in white, grey and a touch of purple, where color means gameplay, crossed by a dark, elegant *NieR* android.

## Open issues `#art-open`

What the visual work still owes (`docs/roadmap.md#stage-bricks`, `docs/roadmap.md#pilgrim`):

- Flooded back walls (decided 2026-11-07, owner's delegation): the Narthex and the Sacristy have a dressed back wall at 9 m, as the Cell's, with a warm lamp raking it; it reads at the edges and in the wide shots, but where a shaft's beam crosses the room the lit haze in front still veils it to a pale plane (the Narthex's shrine, the Sacristy's dais). The open halls keep the haze's architecture. Each wall costs about 0.5 ms of GPU on average.
- Frozen: the Underice's water is clearer (decided 2026-11-07, owner's delegation; `water_section_clear.tres`, that cave only): its section sank the play plane, twenty meters and more under the surface, almost wholly into the deep color, which is why its lamps never reached the camera. Its walls, ice and floor now read, and the pilgrim's outline reads against them (see the pilgrim's line below).
- The pilgrim's contrast against dark backdrops is settled (decided 2026-11-08, owner's delegation): every black part of it (suit, coat, the shell of the hands, forearms, shins and feet, the joints) carries a faint cool edge along the silhouette that needs no light (`edge_light` in the android shader), one setting for every room, and it loosens as the figure gets small on screen so it never thins under a pixel in a wide shot. Judged through the paint in the darkest spots (the Underice's water, the Coldstacks' and the Sounding's racks, the Quench's coolant hall, the Effigy): the whole figure, legs included, reads at a glance at play size and stays black, not lamp-lit, and the Narthex's bright rooms see no change. The Underice's wide shot from outside its water stays veiled, the pilgrim with the rest of the room; the play camera is never that far. The cap of the hair is still one smooth dome over its ragged locks: its crown (strands or a parting) waits for the Blender MCP (`docs/roadmap.md#beyond-procedural`) (decided 2026-11-07, owner's delegation).
- Interactables (decided 2026-11-07, owner's delegation): their glows keep their kind's hue through the tonemapper (`token_glow`'s `chroma`, at a lower energy, so no blob); the defense reads azure and the harpoon hoop gold, the key still a light amber in the Scriptorium's bright light. A code is green now, off every biome's accent; it is not yet seen in a capture.

## Decisions `#art-decisions`

- **Looks kept as they are** `#art-kept` (decided 2026-11-07, owner's delegation): the flooded play plane's brightness (a little darker and bluer than `perfect.jpg` outside the shaft), the core's murky walls and the flooded rooms' wide shots, the machines' plated limbs with their banded trims and the flyers' collar, Rimefall. The harpoon anchor's far and spent looks are dimmer (`HarpoonAnchor`).
- **No SDFGI for now** (2026-10-24): +1.7 ms average and p99 over budget for a barely visible change in this lighting. VoxelGI needs a bake. Revisit when rooms get darker interiors.
