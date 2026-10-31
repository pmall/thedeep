# Roadmap — The Deep

The plan we follow from session to session: the owner's direction, where we are, and what comes next. Keep it short. **Status** says where things stand now, not what each session did: that history is in `git log`.

## Direction (decided by the owner)

1. **The order of work** (owner, 2026-10-31). Each phase is finished before the next one starts:
   1. **A complete world to play** *(current)*: every room on the map built, all five abilities, every ability order proven, playable and understandable from a new game to the end.
   2. **Extended visuals.**
   3. **Gameplay feel:** hyper-dynamic, fluid, flowing.
   4. **Characters** (the character pass): the pilgrim, NPCs, enemies, bosses.
   5. **Final touch:** story, dialogue, cutscenes.

   Art matters in every phase and is implied throughout: new rooms follow [art-direction.md](art-direction.md) at the quality bar the built rooms set. Phase 2 is where the visuals are pushed further. Until a phase comes, its part of the game stays a working placeholder: used, not polished. Why this order: at the 2026-10-31 playtest the owner "barely saw anything because the rooms are locked, or I didn't understand something".
2. **Procedural only, pushed to the maximum.** All visuals come from code: shaders, generated meshes, lighting, fog, particles, post-processing. The game must look good with procedural art alone; no imported textures or models. Authored assets (a Blender MCP, owner 2026-10-31) come only at the very end, as the cherry on the cake for polish, never to make up for a procedural look that isn't good enough. Every visual piece stays **swappable** (a material, a part mesh) without touching gameplay, so that polish is cheap.
3. **Two long-term objectives shape the architecture** (details in [long-term.md](long-term.md)):
   - **No two machines alike:** 150–200 behaviorally distinct machine species, built from modular parts and a shared behavior vocabulary. Grown in phase 4.
   - **Any order, proven on every build:** abilities collectable in many orders, never a softlock, checked by a solver on every build. Built (M7), and it holds every room as it is built.

## Status

- **Foundation done** (M0–M7, 2026-10-24 to 2026-10-28): look-dev tools (`tools/capture.sh`, F12/F10 owner captures), the procedural material library, the environment kit, atmosphere, `BiomeProfile`s, the modular part system, the pilgrim and four machine prototypes (placeholders), the level toolkit, room travel and the map screen, combat, health, shrines and saving, the map solver, the geometry check and the whole map on paper ([map-draft.md](map-draft.md): 40 rooms, 5 abilities, 4 bosses, 3 law breaks, 32 of 120 orders).
- **Phase 1, the complete world: 27 of 40 rooms built.**
  - Flooded layer: all 13 rooms; the Harpoon, the Precentor (boss placeholder), the third law.
  - Frozen layer: all 11 rooms; the Drill, Ballast, the Archivist (boss placeholder), the second law.
  - Volcanic layer: 3 of 10 (the Kiln, the Slag, the Chimney). Heat plating works (`HeatZone`) but its relic isn't placed yet.
  - Core: 0 of 6.
  - Built map: 4 abilities, 12 of 24 orders, no softlock; its `MapGoal` is in the Slag.
- **Owner playtest 2026-10-31:** saw little. They started rooms with `--room`, which gives no abilities, so almost every way was sealed. That is the first job below.
- **Next:** W1 below (make the world reachable and readable), then the Quench and the Basalt. The Basalt goes one way down through the Slag's plug with the Drill; that plug's shaft must be walled off from the river and its exit moved to the Slag's bottom, map (231.5, -155). Draft ways to become `Slag -> Quench : Ballast`, `Quench -> Slag`, `Slag -> Basalt : Drill`, `Basalt -> Crucible`.

## Session protocol

Every session:
1. Read `AGENTS.md`, this file, then the docs that `AGENTS.md` lists for the task at hand.
2. Work on the current phase's first unfinished item (or what the owner asks for).
3. For any visual change, capture the photo spots **before and after** and look at them. Never judge visuals from code alone.
4. `tools/check.sh` passes, then commit.
5. Update **Status** here: replace what changed, don't append a session log.
6. Hand over with **one command that reaches the new content with the abilities it needs**, and what to try there. Never hand over a command that drops the owner somewhere they can't move.

When the owner changes direction, rewrite the docs it touches so none of them contradicts it. Don't stack the new rule on top of the old one.

---

## Phase 1: a complete world to play *(current)*

Rooms are built with the level toolkit to the art direction, at the quality bar the built rooms already set. Gates and hazards must read at a glance. Bosses, machines and law breaks stay working placeholders. Use existing species, and add a new one only where a room's design needs it (data first, a brain only for a truly new behavior).

- [ ] **W1, reachable and readable.**
  - `--room <Name>` starts with the abilities a route there needs (from the solver), so any room can be tried.
  - Every gate reads at a glance as what it is (metal, rings, cracked rock, deep water, heat), and a gate the pilgrim can't pass yet says so.
  - The map shows the ways not yet taken.
- [ ] **The volcanic layer** (7 rooms left): the Quench, the Basalt, the Crucible (Heat plating relic), the Ashfall, the Bellows, the Pyre, the Foundry (the Founder, a boss placeholder).
- [ ] **The core** (6 rooms): its `BiomeProfile` and just enough kit to read as ancient machinery; the Threshold, the Mantle, the Orrery, the Lode, the Effigy (the first law), the Heart (Nirvanary, a boss placeholder; the end).
- [ ] **An ending placeholder**: reaching the end closes the run with a card, back to a new game.
- [ ] **The whole run proven:** the built map matches the draft (40 rooms, 32 of 120 orders, no softlock), and a scripted playthrough in `tools/check.sh` plays from a new game to the end.
- **Done when:** the owner plays from `godot --path . -- --new` to the end, with no debug keys, and always knows what a gate needs.

## Phase 2: extended visuals

Every room brought to and past the foundation's quality bar, layer by layer ([art-direction.md](art-direction.md): the open items listed under "Current state", the volcanic and core looks). A performance pass belongs here too: most rooms' frame p99 goes over 10 ms on CPU-side spikes, while the averages stay under budget.

## Phase 3: gameplay feel

Hyper-dynamic, fluid play: movement that chains and carries momentum, smooth Mag-grip corners, harpoon, dive and swim flowing into each other, camera and combat feel. Numbers stay in `player_tuning.tres` ([gameplay.md](gameplay.md)).

## Phase 4: characters (the character pass)

- The pilgrim: its look (the hair reads like a helmet; contrast against dark backdrops), its animation and effects.
- The machines: design, art and gameplay of the species, then growing the bestiary: about 10 good species first, then by layer, toward 150–200 ([long-term.md](long-term.md)). Distinctness before count.
- NPCs, and the bosses designed for real.
- The bestiary: entries filled in from the behaviors the player has seen.

## Phase 5: final touch

Story, dialogue and cutscenes: the law breaks as real moments, the ending, scripture and creeds written for real ([theme.md](theme.md) and its open questions). All text stays data in the line book (see "Later: audio").

## Not yet placed by the owner

- **Release:** main menu, pause, settings (graphics quality tiers, audio, controls remapping), save slots, a bestiary screen, controller polish, accessibility options, exported Linux and Windows builds.

## Later: audio

Deferred by the owner. Ask before planning it. Two parts:
- **Ambience** (room tones, machinery, water). Keep hooks cheap: rooms and biome profiles can later reference ambience.
- **High-quality voiced dialogue** (owner wish, "someday"). Generated voices, via an ElevenLabs MCP server or local TTS models, to be decided then. This fits the setting: machine voices can be processed (filters, vocoder-like effects) per species or layer.

  To keep it cheap, when any text is written (placeholder lines, machine creeds, bestiary entries), store it as **data**: one entry per line with a stable ID, speaker, text and optional delivery notes. Never hard-code strings in scenes or scripts. Voice files can then be generated per line ID in a batch and re-generated when text changes.

## Later: beyond procedural art

The final polish, once the procedural look has gone as far as it can (direction 2): the owner may provide a **Blender MCP** so the agent can model the pieces code does worst (the pilgrim's head and hair, bosses, organic parts, one-off set pieces), painted by the game's own world-space shaders so they match everything else. Procedural stays the base: materials, atmosphere and layouts remain code. Thanks to the swap rule, this means replacing materials, kit meshes and part meshes, not rebuilding rooms or machines.

---

## Open decisions (owner)

| Decision | Default until answered | Needed by |
|---|---|---|
| Audio: when and from where | Silent; owner decides later | — |
| Palette progression with depth | Pale white + purple near the surface, warming to NieR furnace amber at the core ([art-direction.md](art-direction.md)). [long-term.md](long-term.md) says "teal and rust near the surface, red and ember near the core" for machines | Phase 2 |
| Where release work (menus, settings, builds) goes in the order | After phase 5 | Phase 5 |
