# Roadmap — The Deep

The owner's plan and the open items, in order, opened by where the work stands. A finished item is deleted (`AGENTS.md#doc-rules`); what exists is in the code and [bricks.md](bricks.md).

## Now <a id="status"></a>

- **Stage:** A, the prototype (`docs/roadmap.md#stage-prototype`).
- **Current item:** the pilgrim, from zero (`docs/roadmap.md#pilgrim`).
- **Next step:** the owner judges the painted pilgrim in `painted-riso` (`docs/roadmap.md#look-presets`), to blend the pilgrim into the drawn look (`docs/roadmap.md#drawn-look`); then a new way to animate (`docs/roadmap.md#animation-method`), proven first on the run alone.
- **Waiting on the owner:** sign-off on the painted pilgrim in `painted-riso`, then the stage's sign-off play: `docs/roadmap.md#to-play`.

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

1. **The pilgrim, from zero** <a id="pilgrim"></a> (owner: last, once the world, its bricks and its look are finished). The current character, its procedural animation and its halo are thrown away. The bar is *Ori*'s animation (a 3D-rendered character) with *NieR: Automata*'s dynamism; the look is `docs/art-direction.md#character`. First the environment for top-quality animation, then the character: modeled and rigged in Blender (`instructions/toolchain.md#blender-mcp`), animated by the method of `docs/roadmap.md#animation-method` (owner: dynamic, precise, *virevoltant*), blended in the game with its feet held to the ground. Every move and ability: run, jump, land, dash, air dash, ledge assist, grip and its corners, harpoon throw and zip, bore and dive, swim, sink and rise, the strikes, hurt, death, rest, taking a relic. Its hair swings on springs; the rest of its secondary motion is stage D's. Done when every state reads at a glance in motion, in the reels and in the owner's play.

   **The character, in order** <a id="animation-env"></a> (the environment is built: `art/pilgrim/build.py` makes the model and every clip, keyed by hand in `art/pilgrim/keyed.py`'s splines or cut from capture, and `PlayerVisual` plays them; every state has its clip):

   1. The pilgrim modeled by script (`art/pilgrim/body.py`), bound to the skeleton so every clip carries over, its hair on springs: a slender android of `docs/art-direction.md#character`. Its look is the owner's to sign off (`docs/roadmap.md#to-play`); `art/pilgrim/preview.py --turnaround` renders it from four sides.
   2. Every clip to the owner's bar, by a method found here (`docs/roadmap.md#animation-method`). Where it stands, in the owner's play: the run is unacceptable (clownish), the jump is good (keep its arc scrubbing and its poses), the strikes are better but not enough; the rest is a first pass. The owner's aim is a way of animating that an agent can carry, not a pile of downloaded clips: this is a training project.
   3. The feet held to uneven ground by `TwoBoneIK3D` once the world has any (its floors are flat boxes: the clips plant the feet, and the run's, the crawl's and the stroke's rates match the speed so they never slide).

   **The drawn look** <a id="drawn-look"></a> (owner: the toon render is close; the world and the pilgrim must blend into one drawn image, the world less clean, the pilgrim less cartoon, with a style signature, never bland). `ToonLook`, `lib/look.gdshaderinc`, `lib/dirt.gdshaderinc` and `lib/bands.gdshaderinc` hold it, off unless a look is chosen, every piece of it in its own files and behind its own value so it can be kept, changed or removed alone. Each direction is a preset in `resources/looks/` (`LookPreset`): F9 cycles them in play, `--look <name>` and `tools/capture.sh --look <name>` choose one. They differ in the world's light (smooth, painted in the pilgrim's flat bands, or drawn as marks: hatching or halftone dots laid on the surfaces, the pilgrim's lit side drawn in the same marks out of its black), brush strokes through the paint, the ink, the dirt, the pilgrim's drawing (its lift out of black, its ink-colored body, its outline) and a grade printing the whole frame in a few inks. A grade alone is a tint, not a signature: the signature is in how the surfaces are drawn, never clean (owner). The world's lead is `painted` under a very subtle trace of `riso-dots`' violet (`painted-riso`) (owner). The pilgrim still does not blend into it (owner): less cartoon and less inked helped little (a thin outline, its own black, soft shading, smooth motion, its marks only in close views), and a radically different direction for it, from scratch, is open. Next, in order:

   1. **The pilgrim blended into the drawing** <a id="look-presets"></a>: the world's look narrowed to `painted-riso`; under banded looks (`WorldBands`), the pilgrim shares `lib/look`'s stepped light, figure-scale brushwork, pigment-grounded plates vs. deep-ink undersuit, muted trims, warm-paper hair and hand-wavering ink outline with no emissive edge halo or rim, awaiting the owner's play (`docs/roadmap.md#to-play`).
   2. Bring what is still smooth into the same drawing (the skylines, the fog, the water, the particles, the machines' shells).
   3. **Two flaws the owner saw** <a id="graphic-flaws"></a>, in every look, not a look's problem: the light is too on or off, and it cuts surfaces in half like a curtain, a geometric cone no real light makes (the Drowned Hall under the east ledges, x 52–55: `captures/2026-11-27_21-05-59.png`, `captures/2026-11-27_21-19-06.png`); and a liquid body in the volcanic layer (and perhaps the water elsewhere) has no back, its box ending abruptly in plain sight behind it. The light wants its root cause found on the owner's screenshots and agreed with the owner before any change: adjusting values (the shafts' energy, rim, shadow blur; stepping only a face's turn) did not touch it.

   Once the owner keeps it, it becomes the game's look: on by default, and `docs/art-direction.md` rewritten to it.

   **The animation method** <a id="animation-method"></a>. Keying poses as numbers and judging still frames is dead (the run came out clownish): invented motion has no anchor. Studios start from reference and stylize it, with an eye on a live preview; the agent does the same in steps it can measure:

   1. **Reference first, never invented numbers.** Real motion from video (open video-to-3D models such as GVHMR or WHAM, on the local RTX 3060: a sprinter, a parkour runner, a fencer, a gymnast, the owner's own phone videos; they use the SMPL body, licensed for research only, which is fine for training and must be solved before any release) or from an open capture dataset made for locomotion (100STYLE: about a hundred styles of walking and running; check its license). Retargeted by `art/pilgrim/mocap.py`'s method.
   2. **Stylized by operations on that motion**, as animators clean capture: find its key poses (the extremes) automatically, retime (hold them, speed the moves between), exaggerate chosen joints by measured factors (a wider arc, a higher knee). Each step stays anchored to a real body.
   3. **Procedural life on top**, as *Overgrowth*'s talk "Animation Bootcamp" (GDC 2014) shows: springs, momentum, IK, the lean, the hair, the air scrubbed along its arc. What already works here stays.
   4. **A judging loop that isn't blind:** each clip rendered beside its reference (onion skin, joint-angle curves) so the agent compares against something real; the owner watches short videos of two or three variants and picks.

   The first experiment is the run alone, three variants (from a sprint video; from a 100STYLE run; the same retimed and exaggerated), side by side for the owner. One acceptable: the method holds, and goes on to the strikes, the swim and the rest. None: find which step fails before going further. The keyed clips (`art/pilgrim/keyed.py`, `locomotion.py`, `strikes.py`, `moves.py`) stay in the game until their replacements win.

**To play** <a id="to-play"></a>: what the owner's eye alone can judge, each line deleted once they have said. An item closes on what the agent can verify; the owner's word reopens it.

- The drawn looks (`docs/roadmap.md#look-presets`): `godot --path . -- --room DrownedHall --abilities all --look painted-riso`, F9 cycling the presets (the name prints in the console), F6 the biome. Say whether the riso trace in `painted-riso` is subtle enough, and what the pilgrim should become to belong in it; also `--room Slag` for the volcanic layer.
- The transitions: `godot --path . -- --room Nave --abilities all` (west and east doorways, the hatch down to the Cistern); the Brink's leap, `--room Brink`. Every doorway now stands in a wall its brick builds (most were never looked at): say where one reads wrong.
- The gates that speak (`docs/gameplay.md#gate-says`): `--room Ashfall --abilities none` (the Pyre's door names its litany and the Belfry as soon as it is on screen, the heat's line read after it), `--room Narthex` (the Sacristy door), `--room Kiln` (its gate). Say whether the lines come too early, too late or too fast.
- The law breaks (`docs/gameplay.md#law-act`): `--room Brink --abilities all`, walk into the law, then hold the prompt. Say whether the hold's length and the refusal read right.
- The volcanic layer, its slag, heat and light: `--room Slag` (walk off into the slag, plated and not), `--room Ashfall --abilities HeatPlating` and without (the heat before the Pyre's door).
- The moves flowing into each other: `godot --path . -- --room MechanicsLab --abilities all`. Jump up into a metal wall (the grip climbs on with the jump's speed), run the grip round corners, zip to the rings and let the release carry you, cancel a zip into a jump. Say where a move stops dead or pops.
- The camera: `godot --path . -- --room DrownedHall --abilities all` (hop along the hall, climb and drop the shaft), `--room Brink` (the long fall). Say whether it lags, floats or rocks on a hop, and whether a fall shows where it lands.
- The fight and health: `godot --path . -- --room MachineArena`. Strike the machines (each blow holds, kicks the view, sparks; the finisher most), let them hit you (the hold before the throw, the edges' flash), and go down to one hit (the last diamond beats). Say whether blows land, and whether the hold is too long or too short.

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

- **The pilgrim's secondary motion**: coat tails, cables (its hair already swings).
- **The bestiary**: species grown toward 150–200 behaviorally distinct machines (`docs/long-term.md#machines-distinct`), the real bosses, NPCs.
- **Every room made rich**: its secrets, its swarms, its secret passages, its enigma, the paths toward the end.
- **Story**: the law breaks as real moments, scenes where the player, as the pilgrim's conscience, makes it break the law (owner), on the act of `docs/gameplay.md#law-act`; then the ending, dialogue, scripture and creeds written for real ([theme.md](theme.md)), cutscenes. All text stays data in the line books.
- **Audio and voices** (`docs/roadmap.md#audio`).
- **Release**: main menu, pause, settings (graphics quality tiers, audio, control remapping), save slots, a bestiary screen, controller polish, accessibility options, exported Linux and Windows builds.

### Audio <a id="audio"></a>

Deferred by the owner: ask before planning it. Two parts:

- **Ambience** (room tones, machinery, water). Keep hooks cheap: rooms and biome profiles can later reference ambience.
- **High-quality voiced dialogue** (owner): generated voices, through an ElevenLabs MCP server or local TTS models, decided then. Machine voices can be processed (filters, vocoder-like effects) per species or layer. This is why every line is data with a stable id: voice files are generated per line id in a batch and regenerated when the text changes.
