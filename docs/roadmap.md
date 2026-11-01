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
- **Phase 1, the complete world: all 40 rooms built.**
  - Flooded layer: all 13 rooms; the Harpoon, the Precentor (boss placeholder), the third law.
  - Frozen layer: all 11 rooms; the Drill, Ballast, the Archivist (boss placeholder), the second law.
  - Volcanic layer: all 10 rooms; Heat plating (the Crucible), the Founder (boss placeholder, the Foundry).
  - Core: all 6 rooms (`core.tres`: ancient machinery in a crimson haze); Nirvanary (boss placeholder), the first law, the end card.
  - Built map = the draft: 40 rooms, 82 ways, all 5 abilities, 32 of 120 orders, no softlock; the `MapGoal` is the Heart (reaching it ends the game).
- **Owner playtest 2026-10-31:** saw little: `--room` gave no abilities, so almost every way was sealed. W1 fixed that (`--room` now gives the route's abilities), and gates now say what they are.
- **Proven from inside the rooms (2026-11-01):** the solver no longer trusts a room to be one place. The inside check walks every (room, way in, abilities) state with the planner, which now simulates all five abilities (Mag-grip too). It found the Reliquary's Mag-grip relic walled off by a 7 m pillar since M6, now a pier hanging from the slab. The check also demands room for error: a clumsier pilgrim must take every way the exact one does. That failed in eight rooms whose ladders climbed 3 m per ledge against a 3.2 m jump (the DrownedHall, Cistern, Sluice, Coldstacks, Slag, Kiln, Chimney, Ashfall) and the Orrery's last arc; all are now 2.5 m steps ([gameplay.md](gameplay.md), room metrics).
- **Owner playtest 2026-11-01** ("in good direction"; Mag-grip liked): fixed what it found. The room's name and the pilgrim's position now sit top right in debug builds. Jumps were too exact: a ledge assist (a jump ending up to 0.6 m short of a ledge's top lifts the pilgrim onto it, simulated by the planner too) and coyote time and jump buffer at 0.15 s. Mag-grip slid on along rock past the metal's end, down the Descent's flooded gate, through its water and into the Sluice without Ballast: now only metal holds, and water lets go of a grip. The room check's clumsy pilgrim is now much clumsier (jumps 18% lower, dashes 35% shorter, runs 20% slower, no assist), so no way in the world asks for an exact jump: it found the Coldstacks' 8 m well (a beam now in its middle) and the Crucible's hole up to the Quench.
- **The controls manual (2026-11-01, owner):** debug builds show every control bottom left, keyboard or gamepad as last used, each button lit while held (F8 hides it).
- **Next:** the owner plays from `godot --path . -- --new` to the end (phase 1's done-when) and says where a gate didn't read or a way was unclear; then phase 2 (extended visuals). The bosses (the Founder, Nirvanary) are clones of earlier bodies and brains, placeholders until phase 4.

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

- [x] **W1, reachable and readable.** `--room <Name>` starts with the abilities a route there needs (`MapSolver.Arrival` over the draft; `--abilities` overrides); a gate the pilgrim can't pass yet says so (`GateHints`: bare metal, rings, cracked rock, deep water, heat, each in its color); the map shows the ways not yet taken (a dot if open now, rings in the gate's colors if not, a legend).
- [x] **The volcanic layer**: the Quench, the Basalt, the Crucible (Heat plating relic), the Ashfall, the Bellows, the Pyre, the Foundry (the Founder, a boss placeholder).
- [x] **The core** (6 rooms): its `BiomeProfile` and just enough kit to read as ancient machinery; the Threshold, the Mantle, the Orrery, the Lode, the Effigy (the first law), the Heart (Nirvanary, a boss placeholder; the end).
- [x] **An ending placeholder**: reaching the end closes the run with a card, back to a new game.
- [x] **The whole run proven:** the built map matches the draft (40 rooms, 32 of 120 orders, no softlock), and `PlaythroughCheck` in `tools/check.sh` plays the solver's shortest route from a new game to the end (39 ways, 5 relics, 3 laws, 4 bosses, the end card).
- [x] **Proven from inside the rooms, with room for error:** the inside check (1212 states of room, way in and abilities, all five abilities simulated, no softlock, every relic and the goal reached) and the clumsy pilgrim, in `tools/check.sh`.
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
