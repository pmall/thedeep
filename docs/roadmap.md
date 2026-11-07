# Roadmap — The Deep

The plan we follow from session to session: the owner's direction, where we are, and what comes next. Keep it short. **Status** says where things stand now, never what each session did: that is in `git log`.

## Direction (decided by the owner) `#direction`

- **Four stages** `#four-stages` (owner, 2026-11-01). Each stage is finished before the next one starts:
  - **A. The prototype** *(current)*: every brick of the game ready, beautiful and modular, proven in the 40-room prototype world. The prototype is our **playground**, and it stays one after this stage (`docs/roadmap.md#stage-prototype`).
  - **B. The plan on paper**: the real game's map as a network of rooms, with a table describing every room. Reviewed and approved by the owner before anything is built (`docs/roadmap.md#stage-plan`).
  - **C. The real game**: built from the plan, beside the prototype, from the prototype's bricks (`docs/roadmap.md#stage-real-game`).
  - **D. Polish**: what makes it rich: many machine species, the real bosses, secrets and traps in every room, narrative, story, dialogue, voices, cutscenes, audio, authored final assets (`docs/roadmap.md#stage-polish`).

  Why: the owner is now ambitious for the game. The prototype's world (40 rooms, 5 abilities, the solver) proved the structure. Before growing it, every reusable piece must exist and be good, so that the big world becomes a puzzle assembled from finished pieces. *"If we make a sequel, we make a new map, I have all the tools modularized at my disposal, boom, we implement it."*
- **Programming principles** (owner, 2026-11-01): [principles.md](principles.md), which every code change follows.
- **Procedural first, Blender for the pilgrim** (owner, 2026-11-01 and 2026-11-02): `docs/art-direction.md#procedural-first`.
- **A visual signature** (owner, 2026-11-01): `docs/art-direction.md#signature`.
- **Three long-term objectives shape the architecture**: [long-term.md](long-term.md).

## Status `#status`

- **The prototype world is built and proven**: 42 rooms over the flooded, frozen and volcanic layers and the core, 5 abilities, a key, a lever, a secret and an ambush, 4 placeholder bosses, 3 law breaks, the end card ([map-draft.md](../worlds/prototype/map-draft.md)). The checks walk it from inside every room with a clumsy pilgrim and play it from a new game to the end (`docs/testing.md#check`).
- **The plan's room table and its check** are built (`docs/roadmap.md#stage-plan`), both worlds' tables filled from their rooms, and the network is drawn for the owner (`tools/map.sh`).
- **Last owner playtest** (2026-11-01): "in good direction"; Mag-grip liked.
- **Waiting on the owner** (2026-11-07): only the owner's playthrough of the prototype and its sign-off, feel included. The owner told the agent to decide every other question itself (owner, 2026-11-07); each such decision is marked "decided 2026-11-07, owner's delegation" where it lives, for the owner to overturn, and the agent continues the decided work.
- **Current item:** `docs/roadmap.md#stage-bricks`, starting with the signature (needs the desktop's GPU, `docs/toolchain.md#gpu`): the pass and three variants are built and each biome's style is picked (`docs/art-direction.md#signature-state`); next it is tuned per biome and its average cost won back.
- **The stage bricks besides the signature**: the frozen layer is dressed (its fall, the Choir's organ, Rimefall's shelf face, back walls in every frozen room, the Underice and Sounding walls seen through their water), machines have armored limbs from the shared parts, and the interactables (locked doors and their locks, levers, keys, power-ups, the false wall, the harpoon anchors) are made, each reading by its kind's color and shape (`docs/bricks.md`); what is left is in `docs/art-direction.md#art-open`.
- **The game systems** (`docs/roadmap.md#game-systems`) were done (2026-11-02): each brick has a working example in the prototype and a check that plays it in place: keys and locked doors (the Sacristy behind the Narthex's west door, its key on the Scriptorium's gallery), power-ups (the Sacristy's and the Cell's), the dialogue box (the Narthex's Lamplighter), an ambush (the Pyre), a switch and its mechanism (the Kiln's lever, on the main road), a hidden passage (the Cell behind the Sacristy's false wall). A code is the key brick with another kind: the Pyre's litany, on the Belfry's lookout, opens the seal on the Ashfall's low door to the Pyre (checked in place).
- **Room transitions** were rebuilt (2026-11-06) as in the classics: each exit's trigger covers its whole opening, the room check holds every exit to its real opening, and the travel check crosses every exit low, mid and high and back (`docs/gameplay.md#leaving-a-room`).
- **The pilgrim** (`docs/roadmap.md#pilgrim`) has begun with its animation: besides the run, leap, dash and landing, it now swims (a paddle and flutter kick, treading water when still), dives as a spinning spike, casts the Harpoon (arm cocked behind the head, whipped along the line) and reaches along it, sinks feet first on Ballast's tanks and rises sweeping its arms down, darts flat in an air dash (distinct from the ground lunge), spears both arms ahead to bore cracked rock, strikes with its arm (each strike of the chain its own swing; a down strike tucks the legs), recoils when hurt, kneels to rest at a shrine, opens to take a relic and collapses in death, clings low on Mag-grip surfaces (clamping on and absorbing each corner) and vaults the edge the ledge assist lifts it onto (`PilgrimMotion`; the `Swim.png`, `Moments.png`, `Grip.png`, `Bore.png` and `Motion.png` reels show them; the `Harpoon.png` reel's camera misses the throw, and the bore's and air dash's lean reads shallower on screen than posed). Next (decided 2026-11-07, owner's delegation): the contrast against dark server racks (`docs/art-direction.md#art-open`); secondary motion (coat, braid, cables) belongs to `docs/roadmap.md#stage-polish`, the hair's crown to the Blender MCP (`docs/roadmap.md#beyond-procedural`).
- **Feel and fight** (`docs/roadmap.md#feel-fight`) has begun: the fight moveset is in (`docs/gameplay.md#fight`: a chain to a finisher, a down strike that bounces, a dash strike, the hit stop; `FightCheck`). Its feel, the camera's easing and the flow between moves wait for the owner's play.
- **The sequel world is built and signed off by the owner** (2026-11-02; `docs/roadmap.md#sequel-test`, its first half): 7 rooms on one layer in a new biome variant (brine), two abilities, a key and its door, a lever and its gate, a power-up, a conversation, water, an ending ([network.md](../worlds/sequel/network.md)); `--world sequel` plays it, and the check proves and plays it like the prototype. What it forced into the code became bricks: the network check holds a world to the abilities it uses, the game's own lines live in one book every world's extends (with a check that every line named is in the book), and `tools/capture.sh --world` renders any world. From the owner's play: the Harpoon's target off screen is now pointed at from the screen's edge. **Left of this item:** the owner plays the prototype through and signs it off.

---

## Stage A: the prototype *(current)* `#stage-prototype`

**The goal:** every brick in [bricks.md](bricks.md) is **ready**: beautiful, modular, checked, and used in the prototype world. The prototype's 40 rooms are where bricks are proven, not content to polish: a room gets the attention its bricks need, no more. Bosses, machines and law breaks stay working placeholders (their richness is stage D), but the systems behind them must be ready.

The items, in order. Each one ends with its bricks marked in [bricks.md](bricks.md).

- [x] **The seams** `#seams` (done 2026-11-01): the core in its own project, the engine's nodes thin adapters over it, no `!` outside exports, a world as data (`docs/architecture.md#layout`).
- [ ] **The signature and the stage bricks** `#stage-bricks` (the visuals, already under way). First **the signature**, by its brief and process (`docs/art-direction.md#signature`). Then every stage brick of each biome brought to the bar of [art-direction.md](art-direction.md) and past it: materials, backgrounds and architecture, water, heat, ice, light, particles, the block kit's look. Left from the visual work: `docs/art-direction.md#art-open`. Done when: the signature is chosen and in place, and every biome reads at a glance and looks "very cool" to the owner in their captures, within the frame budget.
- [ ] **The pilgrim** `#pilgrim`: the character fully made: its look in the *NieR: Automata* spirit (the hair, the coat, contrast against dark backdrops), a rig and animation for every move and every ability (run, jump, land, dash, air dash, ledge assist, grip and its corners, harpoon throw and zip, bore and dive, swim, sink and rise, hurt, death, rest, taking a relic), effects; its secondary motion is stage D's (`docs/roadmap.md#stage-polish`). Very dynamic animation: anticipation, follow-through, weight. Procedural first, pushed as far as it goes; only then, where it can't reach the bar (likely the head, the hair, the body's finest forms), Blender finishes it (`docs/roadmap.md#beyond-procedural`). Done when: every state reads at a glance in motion, in the look-dev reels and in the owner's play.
- [ ] **Feel and fight** `#feel-fight`: hyper-dynamic, fluid play: moves chain and carry momentum, smooth Mag-grip corners, harpoon, dive and swim flowing into each other; the camera eased; a small fight moveset (chained swings, an air attack, a dash strike or similar: a few moves, done well) with hits that land (hit stop, sparks, knockback) and health that reads. Numbers stay in `player_tuning.tres` (`docs/principles.md#data-first`).
- [x] **The game systems** `#game-systems` (done 2026-11-02, looks to come with `docs/roadmap.md#stage-bricks`): the bricks the real game's rooms will need, one working example of each in the prototype:
  - **Dialogue box**: a speaker and lines in turn, advanced by the player; lines from the line book. Graceful and small (the owner plays on a gamepad and wants on-screen UI small).
  - **Power-ups**: one kind of loot, a permanent bonus to strength or defense. Nothing more.
  - **Inventory**: keys and codes, held and shown small; **locked doors** that one opens. Items join the requirement language of exits, so the solver proves the world with them (no key behind its own door, no softlock).
  - **Room kinds** the plan will use: a **hidden passage** (the secret room's way in), an **ambush** (an arena that seals while a wave of machines stands: the trap room), a **switch and mechanism** (an enigma's piece).
  - Items, power-ups and opened doors in the save; keys and doors in the gate hints and the map screen.
- [ ] **The sequel test and sign-off** `#sequel-test`:
  - **The sequel test:** a small second world (a handful of rooms, a new arrangement, one biome variant) built from content alone, **with no code change**. Whatever it forces into the code was a missing brick: fix it as a brick. It stays as a fixture the check loads.
  - The owner plays the prototype from `godot --path . -- --new` to the end, with no debug keys, always knowing what a gate needs, and is happy with it.
- **Done when:** every brick in [bricks.md](bricks.md) is ready, the sequel test passes, and the owner signs the prototype off.

## Stage B: the plan on paper `#stage-plan`

The real game's map, designed before a single room is built. Two files per world, read by the code and checked on every build, plus a drawing for review:

- **The network**: every node a room, every edge a way between rooms with its requirement (nothing, an ability, abilities together or in alternative, an item), one way or both. The same language as [map-draft.md](../worlds/prototype/map-draft.md), extended with items, secrets and power-ups. Much bigger than the prototype: many paths, loops, shortcuts, secret rooms. The solver proves it like the prototype: no softlock, every room reachable, the order-freedom targets, every item placed before its door.
- **The room table** (a `.tsv` beside the network): one row per room: name, layer (its biome is the world's for that layer), kinds (a closed set: `RoomKind`), what happens there, its machines, its items, notes; `RoomTable` has the format. The room check holds the table and the network to each other (`RoomTable.Check`: same rooms, a relic, boss, ambush or power-up room where the network has one, a secret room entered only by hidden ways, the items where the network places them). Built, with both worlds' tables (`worlds/prototype/rooms.tsv`, `worlds/sequel/rooms.tsv`); `tools/map.sh` lists each room's kinds. Power-ups are a kind with their own line in the network (decided 2026-11-07, owner's delegation), and the room check holds those lines to the built rooms' power-ups too.
- **A drawing**: the network rendered for the owner to read on paper (generated from the files, never drawn by hand): `tools/map.sh` writes it.
- **Size and abilities**: the real game's size and length (the prototype was planned for about 2 hours, *Inside*-dense) and its abilities (the same five or more; the bricks don't assume a count) are the agent's proposal in this plan (decided 2026-11-07, owner's delegation).
- **Done when:** the owner approves the plan as a whole. Nothing is built before that.

## Stage C: the real game `#stage-real-game`

Built from the approved plan, beside the prototype (its own world folder; the prototype stays the playground and keeps being checked). Rooms are assembled from the bricks; a room that needs something new means a new brick, built and proven in the prototype first. Each built room is held to the plan by the same checks as the prototype's draft (the markers are the truth; plan and rooms agree).

## Stage D: polish `#stage-polish`

Only once the real game plays from start to end:
- **The pilgrim's secondary motion** (coat, braid, cables; moved here from `docs/roadmap.md#pilgrim`, decided 2026-11-07, owner's delegation).
- **The bestiary**: species grown toward 150–200 behaviorally distinct machines ([long-term.md](long-term.md)), the real bosses, NPCs.
- **Every room made rich**: its secrets, its swarms, its secret passages, its enigma, the paths toward the end.
- **Story**: the law breaks as real moments, the ending, dialogue, scripture and creeds written for real ([theme.md](theme.md) and its open questions), cutscenes. All text stays data in the line book.
- **Audio and voices** (`docs/roadmap.md#audio`).
- **Release:** main menu, pause, settings (graphics quality tiers, audio, controls remapping), save slots, a bestiary screen, controller polish, accessibility options, exported Linux and Windows builds. *Release work goes here (decided 2026-11-07, owner's delegation).*

### Audio `#audio`

Deferred by the owner. Ask before planning it. Two parts:
- **Ambience** (room tones, machinery, water). Keep hooks cheap: rooms and biome profiles can later reference ambience.
- **High-quality voiced dialogue** (owner wish). Generated voices, via an ElevenLabs MCP server or local TTS models, to be decided then. Machine voices can be processed (filters, vocoder-like effects) per species or layer. This is why every line is data with a stable id: voice files are generated per line id in a batch and re-generated when text changes.

### Beyond procedural art `#beyond-procedural`

For the pilgrim (`docs/art-direction.md#procedural-first`), once its procedural version has gone as far as it can in `docs/roadmap.md#pilgrim`: the owner provides a **Blender MCP** so the agent can model the parts code does worst (the head, the hair, the body's finest forms), painted by the game's own shaders so they match everything else. Thanks to the swap rule (`docs/art-direction.md#swappable`) this means replacing materials, kit meshes and part meshes, not rebuilding rooms or machines.
