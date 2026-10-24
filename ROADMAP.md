# Roadmap — The Deep

The plan we follow from session to session. Each milestone is one to three sessions of work, with a clear "done when". Tick boxes as work lands, and keep **Status** current.

## Direction (decided by the owner)

1. **One playable game, on a beautiful basis.** Art direction and graphical detail come first, before the game grows wide.
2. **Procedural only, pushed as far as possible.** All visuals come from code: shaders, generated meshes, lighting, fog, particles, post-processing. No imported textures or models yet. Another art source comes later, so every visual piece must be **swappable** (a material, a part mesh) without touching gameplay.
3. **Two long-term objectives shape the architecture from now on** (details in [LONG_TERM.md](LONG_TERM.md)):
   - **No two machines alike:** 150–200 behaviorally distinct machine species, built from modular parts and a shared behavior vocabulary.
   - **Any order, proven on every build:** abilities collectable in many orders, never a softlock, checked by a solver on every build.

## Status

- **Done:** project setup, greybox test room, base moveset (run / variable jump / coyote / buffer / dash), side camera, backdrop and foreground boxes, `tools/check.sh`, unit tests.
- **Next:** M0, look-dev tooling.

## Session protocol

Every session:
1. Read `AGENTS.md`, this file and, when the milestone touches machines or the map, `LONG_TERM.md`.
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
Without a reliable way to see the game, art work is blind. This comes first.
- [ ] `tools/capture.sh`: renders named **photo spots** (camera position + game state) to PNGs, using Movie Maker mode with vsync off. Output goes to a fresh folder per run.
- [ ] Photo spots defined per room as marker nodes (for example "shaft bottom", "wide view", "player close-up").
- [ ] Side-by-side before/after sheet, so changes are judged by comparison.
- [ ] Performance readout: average and worst frame time over a scripted run. Budget: 1080p at 60 fps on the owner's RTX 3060, with headroom (target ≤ 10 ms).
- [ ] `BiomeProfile` resource: palette, fog, light colors, grading and particle tint for one depth layer. Rooms reference a profile; the test room uses "Flooded".
- **Done when:** one command produces a comparable set of screenshots plus frame times, and changing a `BiomeProfile` visibly re-themes the room.

### M1 — Material library (shaders)
- [ ] Triplanar world-space shaders, so no UVs are needed:
  - **Rock:** layered noise strata, cracks, moss or silt in crevices.
  - **Metal:** panels, rivets, rust in creases and on edges, wet streaks.
  - **Rust**, **wet stone**, and **emissive "faith" glyphs** (pilgrim markings).
- [ ] Edge wear from box-local distance to edges, and grime by height.
- [ ] Per-instance variation, so repeated pieces never look identical.
- [ ] Rock and metal stay instantly readable apart in gameplay (this matters for mag-grip). Validate with captures.
- **Done when:** the test room reads as stone and corroded machinery, not as colored boxes, and rock and metal are distinct at a glance.

### M2 — Modular environment kit (generated meshes)
- [ ] Mesh generators in code (`SurfaceTool`/`ArrayMesh`): bevelled blocks, arches, columns, pipes and cables along curves, grates, server racks, broken machinery, ribs of an industrial cathedral.
- [ ] Kit pieces snap to a grid. Gameplay collision stays simple boxes, kept separate from the visual mesh.
- [ ] **Room as its own scene**, with spawn points, camera bounds and **connection markers** (exits carrying a requirement expression and a one-way flag). Main loads rooms instead of embedding one.
- [ ] Rebuild the test room as the first real room using the kit.
- **Done when:** a room is assembled from kit pieces, collision matches the playable shapes, and exits are markers.

### M3 — Atmosphere and the three depth layers
- [ ] Volumetric fog with local fog volumes, light shafts through openings, and emissive ruins as light sources.
- [ ] The three layers formalized: authored backdrop planes (cathedral silhouettes, drowned server halls) with parallax and haze; a framing foreground (pipes, cables, rubble) that never hides gameplay.
- [ ] Ambient particles: dust in light, drips, bubbles, floating silt; later steam, ice crystals, embers via the biome profile.
- [ ] Water: surface shader, underwater fog and tint, caustics on walls (needed later by Ballast).
- [ ] Post-processing: color grading, vignette, film grain, tuned glow. Possibly a custom compositor pass for the painterly depth haze.
- [ ] A lighting quality pass: GI options (SDFGI/VoxelGI) evaluated against the frame budget.
- **Done when:** captures of the room look like a moody scene, not a test level, and stay within budget.

### M4 — The pilgrim (player character)
- [ ] **Modular part system** (shared with machines in M5): chassis, locomotion, sensors, tools and ornaments, assembled in code with scale, proportions and palette.
- [ ] The pilgrim built from parts: a readable silhouette, lamp/visor, cloak or cables, religious plating.
- [ ] Procedural animation: stride, lean into acceleration, landing compression, idle breathing, secondary motion on cables or cloth.
- [ ] Movement feedback: dash trail, landing dust, footstep sparks on metal and grit on rock, small screen shake, and a carried light so dark rooms stay readable.
- **Done when:** the player is readable and alive in every capture, and movement feels heavier and more physical in play without any change to the tuning numbers.

### M5 — Machine part library and first species prototypes
Objective 1 starts here (`LONG_TERM.md`: "prototype the modular machine-part look early").
- [ ] Part library broad enough for clearly different silhouettes: legs, treads, fins, rotors; eyes, antennas; claws, drills, censers; prayer wheels, plating, cables.
- [ ] `MachineSpecies` resource (`[GlobalClass]`): name, layer, parts, stats, palette (from the biome palette), behavior reference, and a placeholder creed line.
- [ ] Behavior vocabulary v0, as pure logic where possible:
  - **senses:** sees player, hears impact, nearby machines
  - **movement:** patrol, hover, wall-crawl on metal
  - **actions:** strike, flee, pray
- [ ] 3 to 4 species as visual and behavioral prototypes, for example "prays and only strikes if passed", "hoards scrap and flees", "follows at a distance".
- [ ] A test arena scene showing them, plus captures.
- **Done when:** a viewer can tell the prototypes apart in ten seconds of watching, and adding a species means one `.tres` and at most one small behavior class.

## Phase 2: vertical slice (playable, final quality)

### M6 — The slice
6–8 rooms of the first layer (flooded), at the quality bar set in Phase 1.
- [ ] Room transitions (doors, shafts, drops), a camera per room, a minimal map of visited rooms.
- [ ] **Mag-grip**: snap corners first, then smoothing; metal detected by physics layer only.
- [ ] A basic attack, health, damage, death and respawn, and a save point (a pilgrim shrine).
- [ ] About 5 species living in the slice, a third of them non-hostile.
- [ ] Ambient audio pass (see open decisions).
- [ ] Connection markers carry real requirements in every slice room.
- **Done when:** the owner plays the slice start to finish and it feels like the real game.

### M7 — Whole-game design and the order solver
Objective 2's tools are built **before** content production, so the map is designed with the solver, not checked after the fact.
- [ ] The full map on paper: three layers plus the core, with ability gates, one-way drops and sequence-break routes. Target order freedom: ≥ 24 of 120 orders, each ability obtainable in at least two positions.
- [ ] Extractor: room scenes → map graph (markers → edges with requirement expressions).
- [ ] Solver (pure C#, xUnit): state exploration over room + abilities owned; fails on any softlock with the exact path; reports the achievable orders.
- [ ] Geometry sanity check: gaps and heights at each connection against limits derived from `player_tuning.tres`.
- [ ] Debug command that prints the graph and achievable orders. Solver and checks added to `tools/check.sh`.
- [ ] Scope locked, by default about 40–60 rooms, 5 abilities, 3–4 bosses, about 2 hours of play.
- **Done when:** the slice passes the solver and geometry checks inside `tools/check.sh`, and the full-map design passes the solver as a *draft planning graph*. That draft is only a design aid: during production, each room's real markers replace their part of it, and the markers stay the source of truth.

## Phase 3: production

### M8+ — Layer by layer: flooded → frozen → volcanic → core
For each layer:
- [ ] Its `BiomeProfile`, kit additions and materials (ice, basalt, magma, ancient core machinery)
- [ ] Its rooms
- [ ] Its abilities: Harpoon, Drill, Ballast, Heat plating
- [ ] Its species, grown by layer: older and stranger deeper down
- [ ] A boss

Machine checks join `tools/check.sh` once machines exist (`LONG_TERM.md`):
- [ ] Every species resource loads and references a valid behavior and valid parts.
- [ ] Headless arena run per species, with and without the player nearby, fails on any error.
- [ ] A species report: count per layer, plus flags for likely duplicates.
- [ ] Bestiary: entries fill in from observed behaviors.

Species growth follows `LONG_TERM.md`: about 10 good ones first, then by layer. Distinctness before count.

## Phase 4: complete game

- [ ] Main menu, pause, settings (graphics quality tiers, audio, controls remapping), save slots
- [ ] Map screen, bestiary screen
- [ ] Ending
- [ ] Controller polish, accessibility options
- [ ] Exported Linux and Windows builds, a performance pass on lower settings

## Later: beyond procedural art

Once the procedural look has gone as far as it can, upgrade the visuals another way (owner's choice when we get there: CC0 assets, commissioned or generated art). Thanks to the swap rule above, this means replacing materials, kit meshes and part meshes, not rebuilding rooms or machines.

---

## Open decisions (owner)

| Decision | Default until answered | Needed by |
|---|---|---|
| Audio: when and from where (procedural synthesis vs. free sound libraries) | Silent until M6; ambience then | M6 |
| Game length and room count | About 2 h, 40–60 rooms | M7 |
| Reference images for the mood (2–3 screenshots) | Work from the written art direction | M1–M3 (helps a lot) |
