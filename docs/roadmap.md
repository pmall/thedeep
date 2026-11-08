# Roadmap — The Deep

The owner's plan and the open items, in order. Where the work stands is [state.md](state.md). A finished item is deleted (`AGENTS.md#doc-rules`); what exists is in the code and [bricks.md](bricks.md).

## The plan <a id="direction"></a>

**Four stages** <a id="four-stages"></a> (owner), each finished before the next starts:

- **A. The prototype** *(current)*: every brick of the game ready, beautiful and reusable, proven in the prototype world, which stays the playground after this stage (`docs/roadmap.md#stage-prototype`).
- **B. The plan on paper**: the real game's bigger map as a network of rooms and a table of every room, approved by the owner before anything is built (`docs/roadmap.md#stage-plan`).
- **C. The real game**: built from the plan with the prototype's bricks (`docs/roadmap.md#stage-real-game`).
- **D. Polish**: the details (`docs/roadmap.md#stage-polish`).

Why bricks: the real game, or a sequel, is then assembled from finished pieces instead of rewritten (`docs/principles.md#bricks`).

______________________________________________________________________

## Stage A: the prototype *(current)* <a id="stage-prototype"></a>

A room gets the attention its bricks need, no more. Bosses, machines and law breaks stay working placeholders (their richness is stage D), with the systems behind them ready.

The open items, in order:

1. **The art pass** <a id="art-restore"></a> (owner: first priority). The effects that flickered are deleted, and the look comes back on the bare render under `docs/art-direction.md#stable-image`:
   - [ ] **A guard**: the fast check fails on a shader that reads the screen texture or has a per-frame random term, so the deleted causes can't come back.
   - [ ] **Baked surfaces**: a tool bakes the procedural patterns (rock strata and joints, cracks, masonry, ice fractures and bubbles, frost) into tileable albedo, normal and roughness textures with mipmaps, and the materials sample them instead of computing lines per pixel.
   - [ ] **The frozen layer** (the owner's favorite, the reference for the others): back to its look before the deletion (the captures at commit `0cf4b04`), judged by the owner.
   - [ ] **The atmosphere**: depth and height fog in the Environment instead of volumetric fog, underwater tint and haze as a fog or a material, light shafts as soft meshes, glints as small mipmapped textures. A gentle vignette may return (it depends only on screen position).
   - [ ] **The flooded layer and the core**, the same way, the flooded one made less bland. The volcanic layer's look comes with `docs/roadmap.md#volcanic-rethink`.
   - Done when every biome reads at a glance and looks very cool to the owner, without flicker, within the frame budget.
2. **The pilgrim, from zero** <a id="pilgrim"></a> (owner). The current character, its procedural animation and its halo are thrown away. The bar is *Ori*'s animation (a 3D-rendered character) with *NieR: Automata*'s dynamism; the look is `docs/art-direction.md#character`. First the environment for top-quality animation, then the character: modeled and rigged in Blender (the owner installs the Blender MCP), animated from real motion capture (free libraries and Mixamo first), keyed by hand, blended in the game with its feet held to the ground. Every move and ability: run, jump, land, dash, air dash, ledge assist, grip and its corners, harpoon throw and zip, bore and dive, swim, sink and rise, the strikes, hurt, death, rest, taking a relic. Its secondary motion is stage D's. Done when every state reads at a glance in motion, in the reels and in the owner's play.
3. **The rest of the owner's review** <a id="review-1108"></a>:
   - **Rooms that agree at their seams** <a id="seam-coherence"></a>: whatever crosses an exit continues on the other side. An elevator's shaft and car exist in both rooms, water never ends at a seam (a hole in a roof never leads under water that has no bottom), a hole in a floor lands under the matching opening above. A rule in `docs/gameplay.md#leaving-a-room`, a check over both worlds, then every vertical exit of the prototype fixed to it.
   - **The volcanic layer, rethought** <a id="volcanic-rethink"></a>: lava is a real body with a depth you can read (never a thin skin you might cross), touching it has one clear rule, heat reads as something the player understands (not bands), the Slag's collision bugs are gone, and the layer looks finished. Designed first (its rules in `docs/gameplay.md`, its look in `docs/art-direction.md`), then built.
   - **Readable law breaks and gates** <a id="law-readable"></a>: a law break says what to do and the player does it on purpose (not by mashing); the Pyre's litany door says what it needs and where to look. The real law breaks (stage D) are scenes where the player, as the pilgrim's conscience, makes it break the law (owner).
4. **Feel and fight** <a id="feel-fight"></a>: moves chain and carry momentum (Mag-grip corners, harpoon, dive and swim flowing into each other), the camera eased, the fight's hits land and health reads (`docs/gameplay.md#fight`), tuned from the owner's play.
5. **Every brick ready** <a id="bricks-ready"></a>: each brick of [bricks.md](bricks.md) still short of ready is brought there (look, feel, reuse).

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
