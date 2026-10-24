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

M1 material library in place (2026-10-24), judged on the test room's photo spots:
- **Rock** (`assets/shaders/rock.gdshader`): warm-neutral matte stone in uneven horizontal strata, sparse tapering cracks, pale silt in crevices and on ledges, chipped paler edges, grime near the bottom of each block. **Wet stone** is the same shader with `wetness`: darker, glossy, flat mirror-like puddles on top faces (the test room floor; reflections come from SSR).
- **Metal** (`metal.gdshader`): pale blue-white glossy ceramic-metal (clearcoat, rim), each face cut into panels that fit it, with seams, a crisp panel bevel, rivets on some panels, faint wet streaks, rust in the creases, coating chipped at the box edges. **Rust** is the same shader with `corrosion` (the shaft's right wall). Panels and gloss keep even rusty metal readable as metal, but its value gets close to rock: keep heavy rust off metal where the grip read matters most.
- **Faith glyphs** (`glyph.gdshader`): generated pilgrim markings on a quad, the biome accent mixed toward white, breathing slowly. Decor, so kept soft: saturated emissives stay for gameplay.
- Every block varies slightly (tone, panel size, crack layout) from a seed taken from its position.
- The palette gained `Rust` and `Silt` in `BiomeProfile`.
- Backdrop pillars use the rock shader without detail (strata and tone only); the magenta hulks use metal panels.

M2 environment kit in place (2026-10-24):
- **Generated meshes** (`scripts/kit/`): bevelled blocks (real chamfers with smooth normals replace M1's fake shader bevel), clustered and faceted columns, pipes with rounded bends and flanges, sagging cable bundles, round and pointed (Gothic) arches, iron ribs with an inner fin, grates, server racks with missing and pulled-out bays, seeded heaps of broken machinery (a fallen tank, a gear, plates, pipe stubs).
- The materials draw on any piece through its surface frame, so pipes get welded seams and collars, arches get strata, and racks get panels with no extra work.
- **Midground rule:** pieces behind the play plane use the `*_mid` materials (darker, duller), so the play plane stays the brightest layer and only grippable metal reads as bright pale metal.
- The test room is now a drowned industrial cathedral: a two-tier arcade (clustered piers and pointed arches below, slim columns and round arches above), two rows of server racks on a wet back floor, a rust pipe run, risers behind the shaft, cables between the capitals, wrecks, and magenta iron ribs far back (they replace M1's floating magenta boxes as the backdrop accent).
- Cost: 5.8 ms average per frame at 1080p (4.5 ms before).

M3 atmosphere in place (2026-10-24):
- **Volumetric fog** everywhere behind the play plane, its density, albedo, self-light and forward scattering set by the biome. A `ClearFront` fog volume with negative density keeps the air in front of the play plane clear, so haze and shafts never wash out gameplay (depth fog still applies).
- **Light shafts** are shadowed spot lights high behind the room (`Shafts`, color from the biome). The arcade shaft falls from the upper left like `perfect.jpg`'s; a shadow-only grate out of frame (`Tracery`) cuts it into rays. The key light only faintly lights the fog (`KeyFogEnergy`), so the shafts stand out.
- **Ambient particles** (`scenes/fx/`): dust motes, floating silt, drips. They take no ambient light and only a tenth of the key light, so motes vanish in the dark and glow in shafts and near lamps; far ones turn into soft bokeh through depth of field.
- **Water**: the back of the hall is flooded (racks, piers and wrecks stand in it). The surface shader shows the scene below sinking into the deep-water color with depth and reflects the scene above with its own short screen-space march (Godot's SSR skips transparent surfaces); ripples are slow and faint, for calm still water. Under the surface, a fog volume in the deep-water color. Rock and metal draw warped caustic filaments under water and reflected onto walls up to 2.5 m above it.
- **Backdrop skylines**: two far quads with procedural silhouettes, the drowned cathedral (bays, lancet windows with mullion and rose, pinnacles, spires) and towers of ancient servers with a few lit bands. They do their own haze toward the biome's light `HazeColor`, with mist rising from below, so they read as pale layered shapes with real parallax.
- **Framing foreground**: a dark pipe along the bottom edge and cables under the ceiling, in front of the play plane and blurred by depth of field; they stay out of the play area.
- **Finishing pass** (`ScreenGrade`): an elliptical vignette toward a dark tint of the palette and a fine midtone grain at 24 Hz, both set per biome. Glow tuned for a gentle bloom on emissives; the faith glyphs now light the stone around them (`GlyphLights`).
- **GI**: SDFGI was tried and rejected for now: +1.7 ms average and p99 over budget (11.4 ms) for a barely visible change in this lighting. VoxelGI needs a bake; revisit when rooms get darker interiors.
- Cost: 6.9 ms average per frame at 1080p, p99 7.8 ms (5.8 ms before M3).

Still missing compared with `perfect.jpg`: the dark android (M4); the player capsule is still grey and low in contrast. The play plane is still a little darker and bluer than the reference outside the shaft; judge with the owner before pushing the palette brighter.
