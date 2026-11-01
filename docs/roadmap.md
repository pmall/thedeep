# Roadmap — The Deep

The plan we follow from session to session: the owner's direction, where we are, and what comes next. Keep it short. **Status** says where things stand now, not what each session did: that history is in `git log`.

## Direction (decided by the owner)

1. **Four stages** (owner, 2026-11-01, replacing the five phases of 2026-10-31). Each stage is finished before the next one starts:
   - **A. The prototype** *(current)*: every brick of the game ready, beautiful and modular, proven in the 40-room prototype world. The prototype is our **playground**, and it stays one after this stage.
   - **B. The plan on paper**: the real game's map as a network of rooms, with a table describing every room. Reviewed and approved by the owner before anything is built.
   - **C. The real game**: built from the plan, beside the prototype, from the prototype's bricks.
   - **D. Polish**: what makes it rich: many machine species, the real bosses, secrets and traps in every room, narrative, story, dialogue, voices, cutscenes, audio, authored final assets.

   Why: the owner is now ambitious for the game. The prototype's world (40 rooms, 5 abilities, the solver) proved the structure. Before growing it, every reusable piece must exist and be good, so that the big world becomes a puzzle assembled from finished pieces. *"If we make a sequel, we make a new map, I have all the tools modularized at my disposal, boom, we implement it."*
2. **Programming principles** (owner, 2026-11-01): bricks, not rooms; the engine is a detail (core / adapters / content); data first; types tell the truth, zero shortcuts with the type system. All in [principles.md](principles.md), which every code change follows.
3. **Procedural first, pushed as far as it goes; Blender to finish** (owner, 2026-11-01). All visuals come from code first: shaders, generated meshes, lighting, fog, particles, post-processing. When procedural has gone as far as it can, the owner gives access to Blender (an MCP) to finish what code does worst. That may come **at the end of the prototype**, the pilgrim first (a really cool character may need real modeling), not only in polish. Never use Blender to make up for a procedural look that wasn't pushed. Every visual piece stays **swappable** (a material, a part mesh) without touching gameplay, so finishing in Blender is a swap.
4. **A visual signature** (owner, 2026-11-01): one global pass that gives every image the game's own hand, painted, something like gouache, so that people recognize the game from a single frame. A shader no other game has. It is designed in P2 before the rest of the visual work, and everything after is judged through it ([art-direction.md](art-direction.md), "The signature").
5. **Three long-term objectives shape the architecture** ([long-term.md](long-term.md)): no two machines alike (150–200 species, grown in stage D, the framework ready now), any order, proven on every build (the solver; it will prove the real game's network too), and any world from the bricks (the prototype's goal).

## Status

- **Foundation done** (M0–M7, 2026-10-24 to 2026-10-28): look-dev tools (`tools/capture.sh`, F12/F10 owner captures), the material library, the environment kit, atmosphere, `BiomeProfile`s, the modular part system, the pilgrim and placeholder machines, the level toolkit, room travel and the map screen, combat, health, shrines and saving, the map solver and geometry check, the map on paper ([map-draft.md](../worlds/prototype/map-draft.md)).
- **The prototype world is built** (the former phase 1): all 40 rooms over the flooded, frozen and volcanic layers and the core; 5 abilities, 4 placeholder bosses, 3 law breaks, the end card. Built map = draft: 82 ways, 32 of 120 orders, no softlock. Proven from inside the rooms (every room, way in and abilities walked by the planner, with a much clumsier pilgrim for room for error), and `PlaythroughCheck` plays the solver's route from a new game to the end.
- **Owner playtests:** 2026-10-31 (rooms locked: `--room` now gives the route's abilities, gates say what they need) and 2026-11-01 ("in good direction", Mag-grip liked: a ledge assist, coyote time and jump buffer at 0.15 s, only metal holds a grip, water lets go of it, on-screen controls in debug builds).
- **Visual work under way** (now prototype item P2): every room's frame p99 under 10 ms at 1080p, garbage 1–4 KB per frame; the shared stage (architecture in the haze, skylines and light by layer, masonry); back walls dressed (`WallDressing`) in every volcanic and core room and four frozen ones.
- **P1, the seams, done** (2026-11-01): the core is its own project (`core/`, no Godot reference); `Player`, `Session`, `Machine` and the room check hand their rules to it; vectors cross the seam in one place; no `!` outside exports, and the room check fails on a required export left empty; the prototype is a world as data (`worlds/prototype/world.tres`: its rooms, network, layers, line book, start, save), `--world` plays another. The game plays as before (every check unchanged: same map, orders, inside states and playthrough).
- **Next: P2, the signature and the stage bricks** (needs the desktop's GPU, [toolchain.md](toolchain.md)).

## Session protocol

Every session:
1. Read `AGENTS.md`, this file, then the docs that `AGENTS.md` lists for the task at hand. Any code change: [principles.md](principles.md).
2. Work on the current stage's first unfinished item (or what the owner asks for).
3. For any visual change, capture the photo spots **before and after** and look at them. Never judge visuals from code alone.
4. `tools/check.sh` passes, then commit.
5. Update **Status** here, and [bricks.md](bricks.md) when a brick changes status: replace what changed, don't append a session log.
6. Hand over with **one command that reaches the new content with the abilities it needs**, and what to try there. Never hand over a command that drops the owner somewhere they can't move.

When the owner changes direction, rewrite the docs it touches so none of them contradicts it. Don't stack the new rule on top of the old one.

---

## Stage A: the prototype *(current)*

**The goal:** every brick in [bricks.md](bricks.md) is **ready**: beautiful, modular, checked, and used in the prototype world. The prototype's 40 rooms are where bricks are proven, not content to polish: a room gets the attention its bricks need, no more. Bosses, machines and law breaks stay working placeholders (their richness is stage D), but the systems behind them must be ready.

The items, in order. Each one ends with its bricks marked in [bricks.md](bricks.md).

- [x] **P1, the seams** (2026-11-01): the core in its own project (`core/TheDeep.Core.csproj`, referenced by the game and the tests, so the compiler holds the boundary); the adapters made thin (`Player` over `PilgrimControl` and `MoveRules`, `Session` over `Pilgrimage`, `Machine` over `MachineVitals`, `CrawlMotion` and `MachineGait`, the room check over `WorldCheck`), vectors converted in `Numerics`; no `!` outside `[Export]` references (late-built nodes and values created on first use, check drivers' references `required`), `ControlsManual`'s items a closed set, required exports checked by the room check (`RequiredExports`); a world is data (`WorldDefinition`: rooms folder, network with its own `target` statements, start room, layers and their biomes in place of the `DepthLayer` enum, line book, save name), the prototype in `worlds/prototype/`.
- [ ] **P2, the signature and the stage bricks** (the visuals, already under way). First **the signature**: the game's global painted pass ([art-direction.md](art-direction.md), "The signature"), two or three variants rendered on the same photo spots and in motion, the owner picks one, then it is tuned per biome; it must fit the frame budget, so GPU time may have to be won back first. Then every stage brick of each biome brought to the bar of [art-direction.md](art-direction.md) and past it: materials, backgrounds and architecture, water, heat, ice, light, particles, the block kit's look. Left from the visual work: each layer's rooms against their references from the photo spots (the core's murky walls, the frozen open items in [art-direction.md](art-direction.md), the flooded rooms' wide shots), back walls for the remaining frozen and the flooded rooms. Done when: the signature is chosen and in place, and every biome reads at a glance and looks "very cool" to the owner in their captures, within the frame budget.
- [ ] **P3, the pilgrim**: the character fully made: its look in the *NieR: Automata* spirit (the hair, the coat, contrast against dark backdrops), a rig and animation for every move and every ability (run, jump, land, dash, air dash, ledge assist, grip and its corners, harpoon throw and zip, bore and dive, swim, sink and rise, hurt, death, rest, taking a relic), secondary motion (coat, braid, cables), effects. Very dynamic animation: anticipation, follow-through, weight. Procedural first, as far as it goes; where it can't reach the bar (likely the head, the hair, the body's finest forms), this is where Blender may come in, at the end of the prototype (direction 3). Done when: every state reads at a glance in motion, in the look-dev reels and in the owner's play.
- [ ] **P4, feel and fight**: hyper-dynamic, fluid play: moves chain and carry momentum, smooth Mag-grip corners, harpoon, dive and swim flowing into each other; the camera eased; a small fight moveset (chained swings, an air attack, a dash strike or similar: a few moves, done well) with hits that land (hit stop, sparks, knockback) and health that reads. Numbers stay in `player_tuning.tres` ([gameplay.md](gameplay.md)).
- [ ] **P5, the game systems**: the bricks the real game's rooms will need, one working example of each in the prototype:
  - **Dialogue box**: a speaker and lines in turn, advanced by the player; lines from the line book. Graceful and small (the owner plays on a gamepad and wants on-screen UI small).
  - **Power-ups**: one kind of loot, a permanent bonus to strength or defense. Nothing more.
  - **Inventory**: keys and codes, held and shown small; **locked doors** that one opens. Items join the requirement language of exits, so the solver proves the world with them (no key behind its own door, no softlock).
  - **Room kinds** the plan will use: a **hidden passage** (the secret room's way in), an **ambush** (an arena that seals while a wave of machines stands: the trap room), a **switch and mechanism** (an enigma's piece).
  - Items, power-ups and opened doors in the save; keys and doors in the gate hints and the map screen.
- [ ] **P6, the sequel test and sign-off**:
  - **The sequel test:** a small second world (a handful of rooms, a new arrangement, one biome variant) built from content alone, **with no code change**. Whatever it forces into the code was a missing brick: fix it as a brick. It stays as a fixture the check loads.
  - The owner plays the prototype from `godot --path . -- --new` to the end, with no debug keys, always knowing what a gate needs, and is happy with it.
- **Done when:** every brick in [bricks.md](bricks.md) is ready, the sequel test passes, and the owner signs the prototype off.

## Stage B: the plan on paper

The real game's map, designed before a single room is built. Two files per world, read by the code and checked on every build, plus a drawing for review:

- **The network**: every node a room, every edge a way between rooms with its requirement (nothing, an ability, abilities together or in alternative, an item), one way or both. The same language as [map-draft.md](../worlds/prototype/map-draft.md), extended with items, secrets and power-ups. Much bigger than the prototype: many paths, loops, shortcuts, secret rooms. The solver proves it like the prototype: no softlock, every room reachable, the order-freedom targets, every item placed before its door.
- **The room table** (a `.tsv` beside the network): one row per room: name, layer and biome, kinds (hub, corridor, relic, boss, shrine, **secret**, **ambush / enemy trap**, **enigma**, lore, ...), what happens there, its machines, its items, notes. A check holds the table and the network to each other (same rooms, a relic room has its relic, a secret room is entered by a hidden way).
- **A drawing**: the network rendered for the owner to read on paper (generated from the files, never drawn by hand).
- **Done when:** the owner approves the plan. Nothing is built before that.

## Stage C: the real game

Built from the approved plan, beside the prototype (its own world folder; the prototype stays the playground and keeps being checked). Rooms are assembled from the bricks; a room that needs something new means a new brick, built and proven in the prototype first. Each built room is held to the plan by the same checks as the prototype's draft (the markers are the truth; plan and rooms agree).

## Stage D: polish

Only once the real game plays from start to end:
- **The bestiary**: species grown toward 150–200 behaviorally distinct machines ([long-term.md](long-term.md)), the real bosses, NPCs.
- **Every room made rich**: its secrets, its swarms, its secret passages, its enigma, the paths toward the end.
- **Story**: the law breaks as real moments, the ending, dialogue, scripture and creeds written for real ([theme.md](theme.md) and its open questions), cutscenes. All text stays data in the line book.
- **Audio and voices** (see below).
- **Beyond procedural art** (see below), for whatever the end of the prototype left.
- **Release:** main menu, pause, settings (graphics quality tiers, audio, controls remapping), save slots, a bestiary screen, controller polish, accessibility options, exported Linux and Windows builds. *Where release work goes is the owner's to place; default: here.*

### Audio

Deferred by the owner. Ask before planning it. Two parts:
- **Ambience** (room tones, machinery, water). Keep hooks cheap: rooms and biome profiles can later reference ambience.
- **High-quality voiced dialogue** (owner wish). Generated voices, via an ElevenLabs MCP server or local TTS models, to be decided then. Machine voices can be processed (filters, vocoder-like effects) per species or layer. This is why every line is data with a stable id: voice files are generated per line id in a batch and re-generated when text changes.

### Beyond procedural art

Once the procedural look has gone as far as it can (from the end of the prototype, the pilgrim first; direction 3): the owner provides a **Blender MCP** so the agent can model the pieces code does worst (the pilgrim's head and hair, bosses, organic parts, one-off set pieces), painted by the game's own world-space shaders so they match everything else. Procedural stays the base. Thanks to the swap rule this means replacing materials, kit meshes and part meshes, not rebuilding rooms or machines.

---

## Open decisions (owner)

| Decision | Default until answered | Needed by |
|---|---|---|
| The real game's size and length (the prototype was planned for about 2 hours, *Inside*-dense) | Decided in stage B, with the network | Stage B |
| Audio: when and from where | Silent; stage D | Stage D |
| Palette progression with depth | Pale white + purple near the surface, warming to NieR furnace amber at the core ([art-direction.md](art-direction.md)). [long-term.md](long-term.md) says "teal and rust near the surface, red and ember near the core" for machines | P2 |
| The real game's abilities: the same five, or more | The same five; the bricks don't assume a count | Stage B |
| Where release work (menus, settings, builds) goes | Stage D | Stage D |
