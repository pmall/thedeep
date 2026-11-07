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

**Where it stands** `#signature-state`: the pass is `PaintPass` (start reading there); a layer's `BiomeProfile` names its `PaintStyle` (`resources/paint/`). Three variants are built: **Gouache** (restrained), **Poster** (broader strokes, few flat values) and **Scripture** (the paper's grain is the machines' script, the strokes lean toward a machine's hand; at 1080p its glyphs read as a fine grain of script marks in flat midtones close up, and fade into texture in wide shots). Each biome may take its own style (owner, 2026-11-02). **The pick** (decided 2026-11-07, owner's delegation): Poster for the frozen layer (the owner's own lean) and its brine variant, Gouache for the flooded and volcanic layers and the core; Scripture stays available, unused. Next: each biome's style tuned to its rooms, and the pass's cost won back. F9 cycles them in play, then shows the frame unpainted; `--paint NAME` and `tools/capture.sh --paint NAME` render one. While the paint is on, it replaces the camera's depth of field: the brush simplifies the far, and the blur would smear its strokes. Its cost to win back: about 0.9 ms of GPU time on average (Scriptorium 5.7 against 4.8 ms, Narthex 5.4 against 4.5 with `--paint none`), and in about half the rooms' frame-time recordings a burst of 10–20 frames at 20–30 ms GPU that steps down by ~3 ms every few frames, at a different frame each run (any room, not tied to room entry or content), never with `--paint none`. The GPU's clocks stay at P0 (1.9 GHz, memory 7.5 GHz, no throttle reason) through a burst, so it is not power management; its cause is still open. The single slow frames at fixed frames in some rooms (Kiln #280, the Sacristy's four) follow garbage-collection stalls and come without the paint too.

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
  - White slash arc and spark VFX for the basic attack.

### Character: *NieR: Automata* `#character`
Images: [`nier-tank-catwalk.jpg`](references/nier-tank-catwalk.jpg), [`nier-amusement-park.jpeg`](references/nier-amusement-park.jpeg), [`nier-factory-furnace.png`](references/nier-factory-furnace.png) (all show 2B or 9S in the 2D side-scrolling sections).
- **Silhouette:** a slender, elegant humanoid android, human proportions, small next to the world. Instantly readable at gameplay size.
- **Colors:** mostly black, with white/silver highlights (hair, trims) and very little else. It stays readable against the pale Dread world through value contrast (dark on light). One small glowing element can carry the purple accent.
- **Motion:** graceful and fast, acrobatic, with flowing secondary motion (skirt or coat tails, hair, ribbons, cables) trailing behind moves.
- **Fits the theme:** NieR's androids are human-built machines, as is ours ([theme.md](theme.md)), so it should look foreign among the native machines.
- **Concrete targets:**
  - The pilgrim follows this: humanoid, dark with white highlights, flowing secondary motion, built from the part system.
  - In every capture, the android is the highest-contrast element on the play plane.

### Summary: the game's look in one line `#look-in-one-line`
A pale, misty *Dread* world in white, grey and a touch of purple, where color means gameplay, crossed by a dark, elegant *NieR* android.

## Open issues `#art-open`

What the visual work still owes (`docs/roadmap.md#stage-bricks`, `docs/roadmap.md#pilgrim`):
- Flooded back walls (decided 2026-11-07, owner's delegation): the enclosed flooded rooms (the Narthex, the Sacristy) get a lit back wall nearer the play plane, about 9 m as the Cell's (the only one dressed): one as far back as the Narthex's (14 m) washes out to a flat pale plane in the haze, and the Sacristy's would lie in its ceiling's shadow. The open halls keep the haze's architecture.
- Frozen: the Underice gets a brighter water look, in that one cave (decided 2026-11-07, owner's delegation). Its dressed wall reads through its water but the cave stays dim (its ice roof shades the key light), and the pilgrim is dark against it. Cold omni lights under the roof, even at energy 3.5 inside the water, barely change its captures: lighting this cave from within goes through how the drowned rooms render their water, not more lamps.
- The pilgrim: against dark server racks the black body loses contrast (the suit's rim is up, not yet enough: a rim light or a lighter backdrop there); to do now. The cap of the hair is still one smooth dome over its ragged locks: its crown (strands or a parting) waits for the Blender MCP (`docs/roadmap.md#beyond-procedural`) (decided 2026-11-07, owner's delegation).
- Interactables (decided 2026-11-07, owner's delegation): their glows get a pushed saturation so each kind reads its own color under the rooms' tonemapper (now the key reads a cream gold, the defense a pale azure in the Sacristy's shaft of light, a harpoon anchor's hoop cream in the Mantle's dark), and a code's violet (its tablet's script, its seal's) moves off the flooded layer's accent.

## Decisions `#art-decisions`

- **Looks kept as they are** `#art-kept` (decided 2026-11-07, owner's delegation): the flooded play plane's brightness (a little darker and bluer than `perfect.jpg` outside the shaft), the core's murky walls and the flooded rooms' wide shots, the machines' plated limbs with their banded trims and the flyers' collar, Rimefall. The harpoon anchor's far and spent looks are dimmer (`HarpoonAnchor`).
- **No SDFGI for now** (2026-10-24): +1.7 ms average and p99 over budget for a barely visible change in this lighting. VoxelGI needs a bake. Revisit when rooms get darker interiors.
