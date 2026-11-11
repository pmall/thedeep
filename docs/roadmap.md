# Roadmap — The Deep

The owner's plan and the open items, in order, opened by where the work stands. A finished item is deleted (`AGENTS.md#doc-rules`); what exists is in the code and [bricks.md](bricks.md).

## Now <a id="status"></a>

- **Stage:** A, the prototype (`docs/roadmap.md#stage-prototype`).
- **Current item:** rooms that agree at their seams (`docs/roadmap.md#seam-coherence`). Next step: the doorway brick, proven on one seam.
- **Waiting on the owner:** nothing blocks. The art pass, judged in play (`docs/roadmap.md#art-restore`); the Blender MCP installed, needed only when the pilgrim starts (`docs/roadmap.md#pilgrim`), last of the stage.

## The plan <a id="direction"></a>

**Four stages** <a id="four-stages"></a> (owner), each finished before the next starts:

- **A. The prototype** *(current)*: every brick of the game ready, beautiful and reusable, proven in the prototype world, which stays the playground after this stage (`docs/roadmap.md#stage-prototype`).
- **B. The plan on paper**: the real game's bigger map as a network of rooms and a table of every room, approved by the owner before anything is built (`docs/roadmap.md#stage-plan`).
- **C. The real game**: built from the plan with the prototype's bricks (`docs/roadmap.md#stage-real-game`).
- **D. Polish**: the details (`docs/roadmap.md#stage-polish`).

Why bricks: the real game, or a sequel, is then assembled from finished pieces instead of rewritten (`instructions/principles.md#bricks`).

______________________________________________________________________

## Stage A: the prototype *(current)* <a id="stage-prototype"></a>

A room gets the attention its bricks need, no more. Bosses, machines and law breaks stay working placeholders (their richness is stage D), with the systems behind them ready.

The open items, in order (owner: the world, its bricks and its look are finished before the character):

1. **The art pass** <a id="art-restore"></a> (owner: first priority). The look is rebuilt on the bare render under `docs/art-direction.md#stable-image`; the owner finds it very good and free of the old flicker. What their play asked for:
   - [ ] **The owner's judgment in play** of the three fixes their play asked for: the Nave's water holding still, water reading as water at a glance (the Sluice), the frozen layer no longer burning white where a light shaft falls (the Rimefall's landing, the Choir's organ). What they ask for next becomes the next tasks.
   - Done when every biome reads at a glance and looks very cool to the owner, without flicker, within the frame budget. The volcanic layer's look comes with `docs/roadmap.md#volcanic-rethink`.
2. **Rooms that agree at their seams** <a id="seam-coherence"></a> (owner: the vertical transitions first; the point is the code a far larger world reuses, never the prototype's rooms themselves): whatever crosses an exit continues on the other side (`docs/gameplay.md#seam-rule`), held by the room check over both worlds. A seam agrees by construction: a doorway brick and a hatch brick each build their room's side of an exit (the opening, the solids around it, the exit marker) from one seam definition both rooms share, so no seam is aligned by hand. Left: the two bricks, each proven on one seam; then every seam of `worlds/prototype/seams.tsv` fixed by placing them and its lines deleted, the hand-placed vertical seams already fixed moved onto the hatch brick; the volcanic seams are fixed with `docs/roadmap.md#volcanic-rethink`, whose rooms change anyway. A task is one brick, or a few seams that only place one (`AGENTS.md#orchestrate`). Each fix is judged on captures of both rooms at the seam. Done when the file holds no seam and every exit of both worlds is built by a seam brick.
3. **The volcanic layer, rethought** <a id="volcanic-rethink"></a>: lava is a real body with a depth you can read (never a thin skin you might cross), touching it has one clear rule, heat reads as something the player understands (not bands), the Slag's collision bugs are gone, and the layer looks finished. Designed first (its rules in `docs/gameplay.md`, its look in `docs/art-direction.md`), then built.
4. **Readable law breaks and gates** <a id="law-readable"></a>: a law break says what to do and the player does it on purpose (not by mashing); the Pyre's litany door says what it needs and where to look. The real law breaks (stage D) are scenes where the player, as the pilgrim's conscience, makes it break the law (owner).
5. **Feel and fight** <a id="feel-fight"></a>: moves chain and carry momentum (Mag-grip corners, harpoon, dive and swim flowing into each other), the camera eased, the fight's hits land and health reads (`docs/gameplay.md#fight`), tuned from the owner's play.
6. **Every brick ready** <a id="bricks-ready"></a>: each brick of [bricks.md](bricks.md) still short of ready is brought there (look, feel, reuse), the pilgrim's own aside (`docs/roadmap.md#pilgrim`).
7. **The pilgrim, from zero** <a id="pilgrim"></a> (owner: last, once the world, its bricks and its look are finished). The current character, its procedural animation and its halo are thrown away. The bar is *Ori*'s animation (a 3D-rendered character) with *NieR: Automata*'s dynamism; the look is `docs/art-direction.md#character`. First the environment for top-quality animation, then the character: modeled and rigged in Blender (the owner installs the Blender MCP), animated from real motion capture (free libraries and Mixamo first), keyed by hand, blended in the game with its feet held to the ground. Every move and ability: run, jump, land, dash, air dash, ledge assist, grip and its corners, harpoon throw and zip, bore and dive, swim, sink and rise, the strikes, hurt, death, rest, taking a relic. Its secondary motion is stage D's. Done when every state reads at a glance in motion, in the reels and in the owner's play.

**Done when** <a id="sequel-test"></a> every brick in [bricks.md](bricks.md) is ready, the sequel world still loads, plays and passes the checks with no code change (whatever it would force into the code is a missing brick, built as one), and the owner plays the prototype from `godot --path . -- --new` to the end with no debug keys, always knowing what a gate needs, and signs it off.

## Stage B: the plan on paper <a id="stage-plan"></a>

The real game's map, designed before a single room is built. Two files per world, read by the code and checked on every build, plus a drawing:

- **The network**: every node a room, every edge a way between rooms with its requirement (nothing, an ability, abilities together or in alternative, an item), one way or both, in the language of [map-draft.md](../worlds/prototype/map-draft.md). Much bigger than the prototype: many paths, loops, shortcuts, secret rooms. The solver proves it like the prototype: no softlock, every room reachable, the order-freedom targets, every item placed before its door.
- **The room table** (a `.tsv` beside the network, `RoomTable` has the format): one row per room with its layer, kinds, what happens there, its machines, items and notes. The room check holds the table and the network to each other.
- **The drawing**: the network rendered for the owner to read, generated by `tools/map.sh`, never drawn by hand.
- **Coherence between rooms** (owner): each way between rooms is meant to be crossed one way (a place where both the Mag-grip and the Harpoon work is a design without intent), and the layers follow each other as a world that makes sense, joined by transitions (the frozen layer opening straight onto the volcanic one doesn't).
- **Size and abilities**: the real game's size and length and its abilities (the same five or more; the bricks don't assume a count) are the agent's proposal in this plan.
- **Done when** the owner approves the plan as a whole. Nothing is built before that.

## Stage C: the real game <a id="stage-real-game"></a>

Built from the approved plan, in its own world folder beside the prototype, which stays the playground and keeps being checked. Rooms are assembled from the bricks; a room that needs something new means a new brick, built and proven in the prototype first. Each built room is held to the plan by the same checks as the prototype's.

## Stage D: polish <a id="stage-polish"></a>

Only once the real game plays from start to end:

- **The pilgrim's secondary motion**: coat, braid, cables.
- **The bestiary**: species grown toward 150–200 behaviorally distinct machines (`docs/long-term.md#machines-distinct`), the real bosses, NPCs.
- **Every room made rich**: its secrets, its swarms, its secret passages, its enigma, the paths toward the end.
- **Story**: the law breaks as real moments, the ending, dialogue, scripture and creeds written for real ([theme.md](theme.md)), cutscenes. All text stays data in the line books.
- **Audio and voices** (`docs/roadmap.md#audio`).
- **Release**: main menu, pause, settings (graphics quality tiers, audio, control remapping), save slots, a bestiary screen, controller polish, accessibility options, exported Linux and Windows builds.

### Audio <a id="audio"></a>

Deferred by the owner: ask before planning it. Two parts:

- **Ambience** (room tones, machinery, water). Keep hooks cheap: rooms and biome profiles can later reference ambience.
- **High-quality voiced dialogue** (owner): generated voices, through an ElevenLabs MCP server or local TTS models, decided then. Machine voices can be processed (filters, vocoder-like effects) per species or layer. This is why every line is data with a stable id: voice files are generated per line id in a batch and regenerated when the text changes.
