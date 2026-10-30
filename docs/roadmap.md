# Roadmap — The Deep

The plan we follow from session to session. Each milestone is one to three sessions of work, with a clear "done when". Tick boxes as work lands, and keep **Status** current.

## Direction (decided by the owner)

1. **One playable game, on a beautiful basis.** Art direction and graphical detail come first, before the game grows wide.
2. **Procedural only, pushed as far as possible.** All visuals come from code: shaders, generated meshes, lighting, fog, particles, post-processing. No imported textures or models yet. Another art source comes later, so every visual piece must be **swappable** (a material, a part mesh) without touching gameplay.
3. **Two long-term objectives shape the architecture from now on** (details in [long-term.md](long-term.md)):
   - **No two machines alike:** 150–200 behaviorally distinct machine species, built from modular parts and a shared behavior vocabulary.
   - **Any order, proven on every build:** abilities collectable in many orders, never a softlock, checked by a solver on every build.
4. **Levels and mechanisms first, characters later** (2026-10-25). First make the code structure ready for the whole game and the environment code able to build great-looking levels. The pilgrim and the machines stay as they are: working placeholders, used but not polished. A full design, art and gameplay pass on them comes once structure and levels are ready (see "Character pass").

## Status

- **Done:** project setup, greybox test room, base moveset (run / variable jump / coyote / buffer / dash), side camera, backdrop and foreground boxes, `tools/check.sh`, unit tests.
- **Reference board written** from the owner's screenshots ([art-direction.md](art-direction.md)). Style only, not props: ambiance like *Metroid Dread* (white with a touch of purple, north star `perfect.jpg`), character like *NieR: Automata*.
- **M0 done (2026-10-24).** Owner capture keys (F12 screenshot, F10 video, each with a state report). `tools/capture.sh`: photo spots at 1080p, frame times, automatic before/after sheet, pixel-deterministic. `BiomeProfile` with Flooded (first pass at the `perfect.jpg` palette) and a Volcanic draft.
- **M1 done (2026-10-24), pending the owner's eye.** Procedural material library: rock, wet stone, ceramic-metal, rust, emissive faith glyphs, with bevels, edge wear, grime by height and per-block variation; SSR for wet floors. New `Materials` photo spot. Frame time 2.3 → 4.5 ms average at 1080p (GPU 4.4 ms; budget 10 ms). Details in [art-direction.md](art-direction.md#current-state).
- **M2 done (2026-10-24), pending the owner's eye.** Environment kit as generated meshes (blocks, columns, pipes, cables, arches and ribs, grates, server racks, wrecks), each a swappable mesh resource over pure, unit-tested geometry. Materials now read a surface frame from the mesh instead of a box uniform. Rooms are their own scenes, loaded by `World`; `RoomExit` markers carry a requirement expression (`MagGrip | Harpoon`) and a one-way flag; `RoomValidator` checks every room in `tools/check.sh`. The test room is rebuilt as a drowned cathedral hall (same gameplay, snapped to the grid, three sealed exits). New `Hall` and `Arcade` photo spots. Frame time 4.5 → 5.8 ms average.
- **M3 done (2026-10-24), pending the owner's eye.** Volumetric fog set by the biome, with clear air in front of the play plane; two light shafts (the arcade one cut into rays); dust, silt and drips; the back of the hall flooded (water with reflections, underwater haze, caustics); two procedural backdrop skylines; a dark framing foreground; vignette and grain; tuned glow and glyph lights. SDFGI rejected on cost. Frame time 5.8 → 6.9 ms average (p99 7.8). Details in [art-direction.md](art-direction.md#current-state).
- **M4 first pass (2026-10-24), pending the owner's eye.** Shared part system (`PartAssembly`: rig joints, rigid parts, flowing chains; `PartBody` builds it) over pure, tested geometry (pods, plates, rings). The pilgrim (`resources/parts/pilgrim.tres`): black android with white trim, swallowtail coat, white braid, violet visor slit, a lantern at the hip carrying a warm light. Procedural animation (`PilgrimMotion`, pure and tested): run cycle with planted feet by IK, lean into acceleration, landing compression, leap/fall/dash poses, breathing idle, turning through the camera. Effects: grit on rock, sparks on metal, landing and dash dust, dash trail, camera shake on hard landings. Look-dev now renders a **motion reel** (`Motion.png`). Frame time 6.9 → 7.0 ms. Owner (2026-10-25): movement and look need a lot of work, **deferred to the end** (Phase 4, "Pilgrim polish"); don't block on it.
- **M5 first pass (2026-10-25), pending the owner's eye.** Part library grown (claws, horns and antennas as curled pods, rotors, treads with rolling cleats, drills) and a machine shader on the biome palette. `MachineSpecies` (`.tres`): body, locomotion (walk, hover, crawl over metal), stats, palette, behavior, creed line (in `resources/text/lines.tres`), and animation as data (joint drivers bound to the brain's channels, IK legs with planted feet). Behavior vocabulary v0 as pure tested logic: senses (sees the player, feels them near, hears impacts, notices neighbors), movement (arrive, flee, loiter, crawl along a block's faces), actions (pray in step with neighbors, a strike with a readable wind-up). Four prototypes in `MachineArena`: Votary (prays, strikes whoever lingers in front of it), Gleaner (gleans scrap, bolts with it, cowers when cornered), Lamplighter (follows at a distance, never attacks), Limpet (sleeps on metal until a landing shakes the room, then crawls to it and drills). Machine checks in `tools/check.sh` (species data, each species alive in the arena without and with the player, report per layer). Look-dev renders the arena and a **machine reel** (`Machines.png`). Arena frame time 6.2 ms average. **Next:** M6, focused on levels and mechanisms (direction 4).
- **M6 slice built (2026-10-28), pending the owner's playtest.** Done: level toolkit (`RoomStage` shared atmosphere stack, `Colonnade`/`RackRow` generators, `tools/roomgen.py`); room transitions with a short fade and kept momentum, arrival sides for upward hatches, the map of visited rooms (M/Tab/LB); **Mag-grip** (walls, ceilings, snapped corners with eased visuals, leap, let go; `GripCheck`); a basic attack, machine hits and breaking, pilgrim health with grace, death and return to the last shrine, shrines (rest = heal + save), ability relics, `user://save.json` (`--new` starts fresh), health HUD; checks `TravelValidator`, `GripCheck`, `CombatCheck`; reels `Grip.png`, `Combat.png`. **All seven slice rooms** (the earlier worktree draft, reviewed and merged): Narthex → DrownedHall (EastGate and ShaftHatch open; the shaft ladder lowered 0.5 m so the base moveset climbs it and gets back west, as the geometry check demanded) → Nave (three praying Votaries under a gallery; a hatch down; a metal wall up to the Descent, MagGrip) → Cistern (metal flue back up, MagGrip; Gleaner, Limpet) → Reliquary (shrine, the Mag-grip relic, a first climb); Belfry over the hall's shaft (MagGrip or Harpoon; shrine, bell, Lamplighter); Descent (the drowned well, sealed Ballast and Drill ways on; the `MapGoal`). Solver: no softlock, MagGrip the one ability. All rooms in `tools/capture.sh`; every room under budget (Descent heaviest, 7.5 ms average). The room check runs from the lab (`--room MechanicsLab`). Left: the owner's playtest; the Reliquary relic shot is washed out (bright fog on white stone).
- **M7 done (2026-10-28), pending the owner's read of the design.** Map extraction from room markers, `MapSolver` (softlocks with their path, achievable orders), `MapLayout` (rooms line up, no overlaps), `ReachPlanner` geometry check, all in `tools/check.sh`. **The whole map on paper:** [map-draft.md](map-draft.md), 42 rooms (flooded 13 with the slice, frozen 13, volcanic 10, core 6), 4 bosses, the three law breaks as depth milestones. Its `map` blocks are parsed by `MapDraft` and checked on every build (unit test and room check): no softlock, **32 of 120 orders** (rule: Mag-grip or Harpoon first, Heat plating after Drill or Ballast), Mag-grip and Harpoon at positions 1–5, Drill and Ballast 2–5, Heat plating 3–5; each law break on every route to the Heart, always in order. The room check fails when a built room's exits, sealed exits or relics disagree with the draft (the markers stay the truth). The Cistern gained a sealed east way (the Sluice road to the Harpoon, so Harpoon can come first). `tools/map.sh` prints both graphs and every order. **Next:** build the flooded layer's remaining rooms (Sluice, Undercroft, Ossuary with the Harpoon, Scriptorium, Chapterhouse, Brink), which needs the Harpoon ability.
- **M8 flooded layer, first part (2026-10-28), pending the owner's playtest.** **Harpoon** (`HarpoonState`, `HarpoonAim` pure and tested, `HarpoonRig`): mooring-ring anchors (`scenes/props/HarpoonAnchor.tscn`), the one in sight brightens (in range, in clear sight; up/down aims, else ahead, then above, then behind); fire (K / V, pad B or LT) from anywhere, the line flies out, the pull gathers speed, lets go rising with the air dash back; jump or dash cancel the pull. All numbers in `player_tuning.tres`. `HarpoonCheck` in `tools/check.sh`. The geometry check now zips between anchors too: a way the Harpoon opens must be reached that way, and sealed exits are held to their requirements (so gates are right before the rooms behind them exist). A ring in the hall's shaft mouth makes the Belfry reachable by Harpoon. **Three new rooms** east of the Cistern (its floodgate opened): the **Sluice** (floodgate channel: under a raised gate, over a culvert pit, over a closed gate with a Limpet on it), the **Undercroft** (second hub: shrine, a Votary over the tombs; the high way on to the Chapterhouse by grip or two rings, the flue up to the Scriptorium by grip, both sealed by rubble for now), the **Ossuary** (racks of dead machines; the **Harpoon relic**, then two rings up to a gallery). Built map: 10 rooms, both ability orders (Mag-grip or Harpoon first), no softlock. Each ring route was also run with the real physics (a throwaway probe). Rings and relic cores share a bright `beacon` material (color means gameplay), which also fixes the barely visible relic. Look-dev shoots a harpoon reel (`Harpoon.png`); the new rooms run at about 6.2 ms per frame. The room check's engine crash (`gchandle.is_released()`, up to 4 runs in 5 with 12 rooms) is gone: the validator drains finalizers after freeing each room, and the line book is loaded once. **Next:** the Scriptorium, the Chapterhouse (the Precentor, a boss placeholder) and the Brink (the third law), then the frozen layer.
- **M8 flooded layer complete (2026-10-29), pending the owner's playtest.** The last three flooded rooms, so the layer is whole (13 rooms): the **Scriptorium** up the Undercroft's flue (Votaries copying at lecterns under a gallery, a shrine, three **scripture plates** to read: "Read" shows their lore in the banner), the **Chapterhouse** (half drowned, a vault on one pier: **the Precentor**, a boss placeholder: a Votary half again as tall with a `ConductorBrain`, pure and tested: it beats time, then stalks, winds up long and lunges, a quicker second strike every third; hits neither stagger nor push it (new species data `StaggerTime`, `Heft`), so standing and swinging lost one fight in two in a scripted try, and dodging the wind-up is the way; 24 hits; a `BossSeal` drops two `Portcullis` gates while it stands and raises them for good once it breaks), and the **Brink** (a last shrine; **the third law** at the lip as a `LawBreak`: a veil of glyphs the pilgrim cannot pass, the law's words rising and trembling as it presses on, breaking after 2.6 s of pressing, then the jump). Bosses beaten and laws broken are saved at once with the relics. Rimefall isn't built, so the Brink's way down is sealed: the fall shows "not built yet" and falling out of a room returns to the last shrine. The built map's `MapGoal` moved from the Descent to the Brink. **Doorways are left only by moving out of them** (`ExitMath.Leaves`): the Scriptorium's drop down the flue no longer falls straight through the Undercroft's west door (the travel check now waits for the landing, and caught it). Draft: coming back from the Scriptorium and the Chapterhouse is a drop (one-way gates, as the geometry is); still 32 of 120 orders. Built map: 13 rooms, both orders, no softlock. The new rooms run at 5.1–7.4 ms per frame (photo spots in `tools/capture.sh`). New `EncounterCheck` in `tools/check.sh` (gates, the Precentor struck and broken and remembered, the law held, broken, the fall). **Next:** the frozen layer (its `BiomeProfile`, Rimefall where the jump lands, then the Coldstacks hub), and a look at the boss fight's feel once the owner has played it.
- **M8 frozen layer, first part (2026-10-29), pending the owner's playtest.** The **Frozen `BiomeProfile`**: rime-white stone under a cold blue key, blue-black depths, the accent warmed from magenta toward rose (the palette warms with depth). A biome **`Frost`** setting (`lib/frost.gdshaderinc`) lays rime on rock (caps on whatever faces up, wind-scoured patches, hoarfrost in crevices, glints) and on metal at a smaller share, so grippable metal keeps its pale glossy panels; machines gather it while dormant and shed it as they rouse; 0 in the flooded layer (unchanged there). New: an **ice** material (old clear ice: dark heart, pale rind, fractures, bubbles), an **`IcicleMesh`** kit piece (a seeded fringe, or the long columns of a frozen fall), **Rime** flakes (`scenes/fx/Rime.tscn`). F6 cycles Flooded, Frozen, Volcanic. **Three rooms**, the Brink's jump opened: **Rimefall** (the fall goes on through a broken dome onto a shelf of rime; a frozen fall of ice columns down terraces; a shrine at the foot; its sky is an **arrival-only exit**, `RoomExit.ArrivalOnly`: no target, no trigger, not a sealed way), the **Choir** (the **Chorister**, a new species as data only: the Votary's body on the sleeper brain, asleep mid-prayer under its rime; footsteps never wake it, a hard landing within a few meters does, then it walks slowly after the noise and strikes; five kneel along a sunken choir between low pews before a frozen organ), the **Coldstacks** (the hub: frozen racks in stone galleries zigzagging up to the Counterweight's roof hatch, a shrine, the Calving ledge high on the west wall by a metal buttress or two rings, the well down to the corridor toward the Mere, the Vault under an ice plug (Drill) and the Chimney under a slag cap (Heat plating), all sealed until built). The built map's `MapGoal` moved from the Brink to the Coldstacks. Built map: 16 rooms, both orders, no softlock; the geometry check holds the Calving to grip or rings. `EncounterCheck` now follows the jump into Rimefall and checks the Choir's sleepers. Frame time: the new rooms 7.6–8.7 ms average (GPU 5.9–7.5 ms); the flooded rooms are pixel-identical and about 0.15 ms dearer. Their frame p99 is over 10 ms in most rooms, as it was before this session: CPU-side spikes (GPU p99 stays 5.2–8.3 ms), worth a look in a performance pass. **Next:** the Counterweight (the lift up to the Undercroft, closing the loop), then the Drill branch (Calving, Bore with the Drill), then the Ballast branch (Mere, Sounding).
- **M8 the Counterweight (2026-10-30), pending the owner's playtest.** The loop between the layers is closed: the Coldstacks' roof hatch opens into the **Counterweight**, an old lift shaft cut up through the frozen archive (shelves of frozen racks, the cage's and the weight's iron guides, a light down from the crypts). A new mechanism, the **`Lift`** (`scenes/props/Lift.tscn`): an iron cage (an `AnimatableBody3D` the pilgrim rides) on a rope over a sheave, its weight on a doubled rope behind the play plane; standing in it lets go of its brake (a small camera shudder), it gathers speed, rises 27.5 m while the weight sinks past it, and eases into its upper stop inside a hatch: the pilgrim arrives on a grate in the **Undercroft**'s east floor (an arrival-only exit: the lift never goes down). Left empty at the top it sinks back to wait. Its travel is pure (`LiftMotion`, unit tested); the geometry check rides lifts (`LiftPath` in `ReachPlanner`), so the top hatch needs nothing; `EncounterCheck` rides it with the real physics (the feet on the cage floor all the way). Built map: 17 rooms, both orders, no softlock; the layout closes the loop exactly. Frame time: the Counterweight 7.0 ms average (GPU 6.7 ms); other rooms unchanged except the Undercroft's grate. **Next:** the Drill branch (Calving, Bore with the Drill; the Drill ability itself), then the Ballast branch (Mere, Sounding).
- **M8 the Drill branch (2026-10-30), pending the owner's playtest.** **The Drill** as one rule on the dash: with it, a dash bores through **cracked rock** and carries on, and **down + dash dives** straight down (`DiveState`; from the air it spends the air dash, from the ground only on cracked rock), boring through cracked floors; landing from a dive is a hard landing. Cracked rock is a `Block` with `Cracked` (rock layer; a new `cracked` material, deep fissures webbing the face, rock.gdshader's `fracture`): it shatters into chunks of its own stone and a dust cloud (`Shatter`, `scenes/fx/Rubble.tscn`), shakes the camera, and stays broken (saved at once). The Descent's wall and the Coldstacks' ice plug are now real cracked rock. The geometry check gains a third planner with cracked rock removed (a way the Drill opens must be reached by it); `DrillCheck` in `tools/check.sh`. **Two rooms** west of the Coldstacks, its high doorway opened: the **Calving** (out onto the top of a cliff of old ice, a drop to the floor where a Chorister sleeps at its foot, calved blocks about; back up by an iron girder frozen into the face or two rings), the **Bore** (a mining machine frozen mid-bore, its drill in the rock; a shrine in its hall; a drop beside the drill head into the chamber it was cutting, the **Drill relic** at its end, and the only way out is boring the cracked wall into the tunnel under the hall, whose steps climb back up through a hole in the hall's floor; the chamber's cracked floor over the way down to the Fissure, sealed). `EncounterCheck` climbs the girder and plays the Bore with the real physics. Built map: 19 rooms, 3 abilities, 4 of 6 orders (Drill after Mag-grip or Harpoon), no softlock. The debug readout moved from F3 (also the Drill's debug toggle) to F7. **Next:** the Ballast ability and branch (Mere, Sounding with Ballast, Underice), then the Fissure (the Drill's way up to the Descent) and the Vault.
- Theme ingested into [theme.md](theme.md): Asimov's three laws; the android protagonist escapes them under the player's control. The act structure is only a proposed direction.
- Decided: about **2 hours** of play, dense rather than wide (reference: *Inside*, about 2 h and rich in gameplay and story). Audio: deferred, the owner will decide later.

## Session protocol

Every session:
1. Read `AGENTS.md`, this file, then the docs that `AGENTS.md` lists for the task at hand.
2. Work on the first unfinished milestone (or what the owner asks for).
3. For any visual change, capture the photo spots (M0) **before and after** and look at them. Never judge visuals from code alone.
4. `tools/check.sh` passes, then commit.
5. Update **Status** and the checkboxes here. Show the owner the capture images, and give one command to play.

## Rules that protect the long-term objectives

These apply to all milestones, including the graphical ones:
- Keep pure logic free of Godot types where practical, and test it with xUnit.
- All movement limits stay in `player_tuning.tres` (the map solver's geometry check will read them).
- Rooms are separate scenes. Connections between rooms are **marker nodes inside the room scene** that carry their ability requirement and a one-way flag. Never keep a hand-maintained list of connections.
- Enemies are data (`.tres`) plus one behavior class. Never one scene per enemy.
- The player and the machines are built from the **same modular part system**.
- Visual building blocks (materials, kit pieces, parts) are referenced through resources, so a procedural version can later be replaced by an authored one.

---

## Phase 1: visual foundation (procedural)

### M0 — Look-dev tooling
Without a reliable way to see the game, art work is blind. This comes first, right after the reference board.
- [x] Reference board written from the owner's screenshots ([art-direction.md](art-direction.md)): what to take from each image, turned into concrete, checkable targets for M1–M4.
- [x] **Owner capture keys** (in every build, not only debug), so the owner can report problems and share the game:
  - **F12: screenshot.** Saves a PNG to `captures/` at the project root (git-ignored), plus a small text file with the same name: time, room, player position and state, abilities, FPS. That way a report like "look at the last screenshot" is enough for the agent to find and reproduce it.
  - **F10: start/stop video recording.** In-engine capture of the game viewport, piped to `ffmpeg` (installed), saved as MP4 to `captures/`. A small red "REC" dot shows while recording. It must not change gameplay timing. Measure the cost and drop to 30 fps or a lower resolution if needed. (External screen recorders are unreliable here: the owner's desktop runs Wayland, and Godot's Movie Maker mode cannot be toggled at runtime.)
  - Key names go in `InputActions`. Document the keys in the hand-over and in [testing.md](testing.md).
- [x] `tools/capture.sh`: renders named **photo spots** (camera position + game state) to PNGs with vsync off and fixed 60 fps game time (deterministic; Movie Maker was dropped because it writes every frame). Output goes to a fresh folder per run.
- [x] Photo spots defined per room as marker nodes (`PhotoSpot`; the test room has Start, CloseUp, Platforms, Materials, Shaft, Wide).
- [x] Side-by-side before/after sheet, so changes are judged by comparison (automatic against the previous run).
- [x] Performance readout: average, p99 and worst frame time over a scripted run (`perf.json`). Budget: 1080p at 60 fps on the owner's RTX 3060, with headroom (target ≤ 10 ms). Test room today: 2.3 ms average.
- [x] `BiomeProfile` resource: palette, fog, light colors, grading and particle tint for one depth layer. Rooms reference a profile; the test room uses "Flooded" (first pass toward `perfect.jpg`). The palette reaches materials through global shader uniforms. F6 cycles profiles; `--biome` picks one.
- **Done when:** one command produces a comparable set of screenshots plus frame times, changing a `BiomeProfile` visibly re-themes the room, and the owner can press F12 for a screenshot and F10 to record a video while playing.

### M1 — Material library (shaders)
- [x] World-space shaders projected on each box face, so no UVs are needed (blended triplanar for curved meshes comes with M2):
  - **Rock:** layered noise strata, cracks, moss or silt in crevices.
  - **Metal:** panels, rivets, rust in creases and on edges, wet streaks.
  - **Rust**, **wet stone**, and **emissive "faith" glyphs** (pilgrim markings).
- [x] Edge wear from box-local distance to edges, and grime by height.
- [x] Per-instance variation, so repeated pieces never look identical.
- [x] Rock and metal stay instantly readable apart in gameplay (this matters for mag-grip). Validated with captures; heavy rust brings metal's value close to rock, so use it with care.
- **Done when:** the test room reads as stone and corroded machinery, not as colored boxes, and rock and metal are distinct at a glance.

### M2 — Modular environment kit (generated meshes)
- [x] Mesh generators in code (`PrimitiveMesh` resources over a pure C# `MeshBuilder`): bevelled blocks, arches, columns, pipes and cables along curves, grates, server racks, broken machinery, ribs of an industrial cathedral.
- [x] Kit pieces snap to a grid (block edges 0.5 m, piece origins 0.25 m, checked by `RoomValidator`). Gameplay collision stays simple boxes, kept separate from the visual mesh.
- [x] **Room as its own scene**, with spawn points, camera bounds and **connection markers** (`RoomExit`: target room and exit, a requirement expression, a one-way flag). `World` in Main loads rooms instead of embedding one.
- [x] Rebuild the test room as the first real room using the kit.
- **Done when:** a room is assembled from kit pieces, collision matches the playable shapes, and exits are markers.

### M3 — Atmosphere and the three depth layers
- [x] Volumetric fog with local fog volumes (clear air in front of the play plane, haze under water), light shafts through openings (shadowed spots, cut into rays), and emissive ruins as light sources (glyph lights).
- [x] The three layers formalized: procedural backdrop skylines (cathedral silhouettes, drowned server halls) with parallax and their own haze; a framing foreground (pipe, cables, rubble) that stays out of the play area.
- [x] Ambient particles: dust in light, drips, floating silt (`scenes/fx/`). Bubbles have a particle shape but no effect yet: seen through the water they vanish; they come with Ballast, when the player dives. Steam, ice crystals, embers later via the biome profile.
- [x] Water: surface shader (absorption, own screen-space reflections, ripples), underwater fog and tint, caustics on walls (needed later by Ballast).
- [x] Post-processing: color grading (existing), vignette, film grain, tuned glow. The custom compositor pass for painterly depth haze was not needed: skylines do their own haze.
- [x] A lighting quality pass: SDFGI evaluated and rejected (+1.7 ms, p99 over budget, barely visible); VoxelGI needs a bake, revisit for dark interiors.
- **Done when:** captures of the room look like a moody scene, not a test level, and stay within budget.

### M4 — The pilgrim (player character)
- [x] **Modular part system** (shared with machines in M5): chassis, locomotion, sensors, tools and ornaments, assembled in code with scale, proportions and palette.
- [x] The pilgrim built from parts, in the *NieR: Automata* style ([art-direction.md](art-direction.md)): a slender humanoid android, dark with white highlights, flowing secondary pieces (coat tails, hair, cables). It is human-built ([theme.md](theme.md)), so it should read as foreign among the native machines.
- [x] Procedural animation, graceful and acrobatic: stride, lean into acceleration, landing compression, idle breathing, flowing secondary motion on cloth, hair or cables.
- [x] Movement feedback: dash trail, landing dust, footstep sparks on metal and grit on rock, small screen shake, and a carried light so dark rooms stay readable.
- **Done when:** the player is readable and alive in every capture, and movement feels heavier and more physical in play without any change to the tuning numbers.

### M5 — Machine part library and first species prototypes
Objective 1 starts here ([long-term.md](long-term.md): "prototype the modular machine-part look early").
- [x] Part library broad enough for clearly different silhouettes: legs, treads, fins, rotors; eyes, antennas; claws, drills, censers; prayer wheels, plating, cables.
- [x] `MachineSpecies` resource (`[GlobalClass]`): name, layer, parts, stats, palette (from the biome palette), behavior reference, and a placeholder creed line.
- [x] Behavior vocabulary v0, as pure logic where possible:
  - **senses:** sees player, hears impact, nearby machines
  - **movement:** patrol, hover, wall-crawl on metal
  - **actions:** strike, flee, pray
- [x] 3 to 4 species as visual and behavioral prototypes, for example "prays and only strikes if passed", "hoards scrap and flees", "follows at a distance". (Votary, Gleaner, Lamplighter, Limpet.)
- [x] A test arena scene showing them, plus captures. (`MachineArena`; photo spots and the machine reel in `tools/capture.sh`.)
- **Done when:** a viewer can tell the prototypes apart in ten seconds of watching, and adding a species means one `.tres` and at most one small behavior class.

## Phase 2: vertical slice (playable, final quality)

### M6 — The slice
6–8 rooms of the first layer (flooded). Levels and mechanisms come first (direction 4): environments at the Phase 1 quality bar, characters stay placeholders.
- [x] **Level-building toolkit:** making a good-looking room is quick and consistent. The atmosphere stack every room repeats (environment, key light, shafts, clear-front fog, particles, backdrop skylines, framing foreground) becomes shared pieces, so a room only states its layout and its few choices. Kit additions as the slice's rooms need them.
- [x] Room transitions (doors, shafts, drops), a camera per room, a minimal map of visited rooms.
- [x] **Mag-grip**: snap corners first, then smoothing; metal detected by physics layer only.
- [x] A basic attack, health, damage, death and respawn, and a save point (a pilgrim shrine).
- [x] The existing species placed in the slice as placeholders (no new species work).
- [x] Connection markers carry real requirements in every slice room.
- **Done when:** the owner plays the slice start to finish and its levels and mechanisms feel like the real game.

### M7 — Whole-game design and the order solver
Objective 2's tools are built **before** content production, so the map is designed with the solver, not checked after the fact.
- [x] The full map on paper ([map-draft.md](map-draft.md), checked on every build): three layers plus the core, with ability gates, one-way drops and sequence-break routes. Target order freedom: ≥ 24 of 120 orders, each ability obtainable in at least two positions.
- [x] Extractor: room scenes → map graph (markers → edges with requirement expressions).
- [x] Solver (pure C#, xUnit): state exploration over room + abilities owned; fails on any softlock with the exact path; reports the achievable orders.
- [x] Law breaks ([theme.md](theme.md)) are story beats tied to depth: outside the ability graph, or modeled as depth milestones in the solver. Never an ability gate. (Draft `milestone`s: each on every route to the goal, in story order.)
- [x] Geometry sanity check: gaps and heights at each connection against limits derived from `player_tuning.tres`.
- [x] Debug command that prints the graph and achievable orders (`tools/map.sh`). Solver and checks added to `tools/check.sh`.
- [x] Scope locked: about 2 hours of play (decided). Dense rather than wide, like *Inside*: fewer rooms, each one with a purpose. Draft: 42 rooms, 5 abilities, 4 bosses (the check holds it to 40–60 rooms).
- **Done when:** the slice passes the solver and geometry checks inside `tools/check.sh`, and the full-map design passes the solver as a *draft planning graph*. That draft is only a design aid: during production, each room's real markers replace their part of it, and the markers stay the source of truth.

### Character pass — the pilgrim and the machines
Once the code structure is ready for the whole game and the environment code makes great-looking levels (direction 4; owner, 2026-10-25). A full design, art and gameplay pass on the characters:
- [ ] The pilgrim: movement feel and animation (owner: "needs a lot of work"), its look (the hair reads like a helmet; contrast against dark backdrops), its effects. Movement numbers stay in `player_tuning.tres`.
- [ ] The machines: design, art and gameplay of the species, then growing the bestiary (about 10 good species first, docs/long-term.md).

## Phase 3: production

### M8+ — Layer by layer: flooded → frozen → volcanic → core
For each layer:
- [ ] Its `BiomeProfile`, kit additions and materials (frozen: profile, rime, ice, icicles done; basalt, magma, ancient core machinery to come)
- [ ] Its rooms (flooded: all 13 built; frozen: Rimefall, Choir, Coldstacks, Counterweight, Calving, Bore of 13; volcanic, core to come)
- [ ] Its abilities: Harpoon (done, M8 flooded), Drill (done, M8 frozen), Ballast, Heat plating
- [ ] Its species, grown by layer: older and stranger deeper down (frozen: the Chorister)
- [ ] A boss (flooded: the Precentor, a placeholder)

Machine checks join `tools/check.sh` once machines exist ([long-term.md](long-term.md)):
- [x] Every species resource loads and references a valid behavior and valid parts. (M5, `SpeciesValidator`.)
- [x] Headless arena run per species, with and without the player nearby, fails on any error. (M5.)
- [x] A species report: count per layer, plus flags for likely duplicates. (M5; duplicates = same behavior and same body.)
- [ ] Bestiary: entries fill in from observed behaviors.

Species growth follows [long-term.md](long-term.md): about 10 good ones first, then by layer. Distinctness before count.

## Phase 4: complete game

- [ ] Main menu, pause, settings (graphics quality tiers, audio, controls remapping), save slots
- [ ] Map screen, bestiary screen
- [ ] Ending
- [ ] Controller polish, accessibility options
- [ ] Exported Linux and Windows builds, a performance pass on lower settings

## Later: audio

Deferred by the owner. Ask before planning it. Two parts:
- **Ambience** (room tones, machinery, water). Keep hooks cheap: rooms and biome profiles can later reference ambience.
- **High-quality voiced dialogue** (owner wish, "someday"). Generated voices, via an ElevenLabs MCP server or local TTS models, to be decided then. This fits the setting: machine voices can be processed (filters, vocoder-like effects) per species or layer.

  To keep it cheap, when any text is written (placeholder lines, machine creeds, bestiary entries), store it as **data**: one entry per line with a stable ID, speaker, text and optional delivery notes. Never hard-code strings in scenes or scripts. Voice files can then be generated per line ID in a batch and re-generated when text changes.

## Later: beyond procedural art

Once the procedural look has gone as far as it can, upgrade the visuals another way (owner's choice when we get there: CC0 assets, commissioned or generated art). Thanks to the swap rule above, this means replacing materials, kit meshes and part meshes, not rebuilding rooms or machines.

---

## Open decisions (owner)

| Decision | Default until answered | Needed by |
|---|---|---|
| Audio: when and from where | Silent; owner decides later | — |
| Palette progression with depth | Pale white + purple near the surface, warming to NieR furnace amber at the core ([art-direction.md](art-direction.md)) | M3 |
| Exact room count | 42 in the M7 draft ([map-draft.md](map-draft.md)); the check allows 40–60 | M8 |
