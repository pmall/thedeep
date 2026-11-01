# Map draft — the prototype world

The prototype's map on paper (roadmap M7): 40 rooms over three layers and the core, 5 abilities, 4 bosses, 3 law breaks, all built. It is a **planning graph**: the solver checks it on every build. The real game's map will be a bigger network with a room table, planned in stage B of [roadmap.md](../../docs/roadmap.md) in its own files; this one stays the prototype's. Room names, boss names and story beats are placeholders unless [theme.md](../../docs/theme.md) says otherwise. It is the prototype world's network file ([world.tres](world.tres), `Network`).

**How it is used.** The `map` blocks below are parsed by `MapDraft` (`core/map/MapDraft.cs`); everything outside them is design notes. `tools/check.sh` checks the draft twice:
- Unit tests (`MapDraftTests`) parse this file and run `MapDraft.Check`: no softlock, every room reachable, every ability on exactly one relic, the `target` statements (**at least 24 of the 120 ability orders**, every ability obtainable in **at least 2 positions**, 40–60 rooms), and each law break on every route to the goal, in story order (third, second, first).
- The room check (`RoomValidator`) compares the draft with the rooms already built: a built room's exits, sealed exits (an empty `TargetRoom`) and relics must match the draft's ways and relics for that room. **The markers are the truth**: when a room is built or changed, update its part of the draft in the same commit. Goals are not compared (the built map's `MapGoal` sits in its deepest built room until the Heart exists).
- Print it: `tools/map.sh` (the built map and the draft: summaries, every way, every achievable order).

**Format.** `room Name Layer`, `way A -> B` (one way) or `way A <-> B` (both), `: requirement` after a way (`MagGrip | Harpoon`, `Drill & Ballast`; a `<->` way needs it both ways), `relic Room Ability`, `boss Room Name`, `milestone Room Name` (in story order), `start Room`, `goal Room`, `target orders N`, `target positions N`, `target rooms MIN MAX`. `#` starts a comment.

```map
start Narthex
goal Heart
target orders 24        # order freedom (docs/long-term.md): at least 24 of the 120 ability orders
target positions 2      # every ability obtainable at 2 positions in the order, at least
target rooms 40 60      # the scope (docs/vision.md): about 2 hours, 40–60 rooms
milestone Brink ThirdLaw
milestone Sanctum SecondLaw
milestone Effigy FirstLaw
```

## The shape at a glance

The main road and its branches, layer by layer (simplified; the `map` blocks are the truth). `(X)` a way needing X, `[X]` a relic, `✦` a boss, `▼` a law break, `→` one way.

```
FLOODED   Narthex ─ DrownedHall ─(M|H)→ Belfry → back down
                      │
                    Nave ─(M)→ Descent ─(B)→ Sluice   (the flooded gate drains, one way)
                      ↓ hatch
          Reliquary[M] ─ Cistern ─ Sluice ─ Undercroft ─ Ossuary[H]
                         ↑(M) flue           │  ├─(M)→ Scriptorium (a drop back)
                                             │  └─(M|H)→ Chapterhouse ✦ ─ Brink ▼ third law
                                             ↑ lift                        ↓ the jump
FROZEN                              Counterweight ─ Coldstacks ─ Choir ─ Rimefall
                                                     ├─(M|H)─ Calving ─ Bore[D]
                                                     ├─ Mere ─ Sounding[B];  Mere ─(B)─ Underice
                                                     └─(D)→ Vault ✦ ─(B)─ Underice
                                                            Vault ─ Sanctum ▼ second law
VOLCANIC                                            Kiln ─ Slag ─(B)─ Quench ─ Crucible[P]
                                                           ├─(D)─ Basalt ─ Crucible
                                                           ├─ Chimney ─(P)─ Coldstacks
                                                           └─ Ashfall ─(P)─ Pyre
                                                              Ashfall ─(M|H)─ Bellows ─ Foundry ✦ ─(P)─ Threshold
CORE      Threshold ─ Mantle ─(M&H)─ Orrery ─(D&B)─ Lode ─ Effigy ▼ first law ─ Heart ✦ goal
```
M Mag-grip, H Harpoon, D Drill, B Ballast, P Heat plating.

## Order freedom

- **The first ability is Mag-grip or Harpoon.** Both lie in the flooded layer, reached with the base moveset: Mag-grip west of the Cistern (the Reliquary), Harpoon east of it, through the Sluice and the Undercroft (the Ossuary). Taking one road skips the other.
- **Either one opens the way down**: the Undercroft's high doorway to the Chapterhouse is reached by climbing its metal wall or by harpooning its two rings. So the frozen layer can be entered with either.
- **Drill and Ballast lie in the frozen layer**, on two branches of the Coldstacks; either one opens the Vault (dive through its ice plug, or come up from beneath through the Underice), and so the way on to the volcanic layer.
- **Heat plating lies in the volcanic layer**, in the Crucible, reached through the Quench (Ballast) or through the Basalt (Drill). It can be third, fourth or fifth.
- **The core needs everything**: the Orrery needs Mag-grip and Harpoon, the Lode Drill and Ballast, the Threshold Heat plating.
- Abilities left behind stay reachable: the Counterweight lift rises from the Coldstacks to the Undercroft, the Sanctum stays open behind the pilgrim, and the Chimney (up from the volcanic layer) ties the layers together.

The rule this gives: **Mag-grip or Harpoon first, Heat plating after Drill or Ballast**, everything else free. That is 32 of the 120 orders; Mag-grip and Harpoon at 1–5, Drill and Ballast at 2–5, Heat plating at 3–5. The test pins these numbers, so a change to the gates that costs freedom is seen at once.

**Sequence breaks by design:** Harpoon before Mag-grip (the Sluice road skips the Reliquary); the Belfry by grip or harpoon; the Calving cliff by grip or harpoon; the Vault by drill or by ballast; the Crucible by ballast or by drill; Heat plating before the second frozen ability.

## Law breaks (story beats tied to depth)

Outside the ability graph ([theme.md](../../docs/theme.md)): each happens in a room every route to the goal passes through, and the check proves they always come in order. They open nothing the abilities don't already gate.
- **Third law (self-preservation), the Brink.** The end of the flooded layer: a drop no machine survives. At first the android refuses it. Breaking the law is the jump, and the jump is one way.
- **Second law (obedience), the Sanctum.** Past the Archivist, a priest-machine commands the android to turn back. The command stops binding; the forbidden door opens on the volcanic layer.
- **First law (never harm a human), the Effigy.** Before the Heart, an effigy of the makers, the gods of the machines' scripture. The only way on is through it.

## Bosses

One per layer, plus the end, each on the main road. Placeholders.
- **The Precentor** (Chapterhouse, flooded): the choir's conductor, before the Brink.
- **The Archivist** (Vault, frozen): keeper of the cold archive, before the Sanctum.
- **The Founder** (Foundry, volcanic): the abbot of the foundry, before the Threshold.
- **Nirvanary** (Heart, core): what waits at the center.

## Flooded — the drowned cathedral (13 rooms)

The slice (built, M6) plus the crypts under it and the road down. Pale white, a touch of purple, water everywhere.

| Room | Purpose |
|---|---|
| Narthex | Start. The pilgrim wakes at the cathedral's door. *(built)* |
| DrownedHall | The hub: a hall half under water, the shaft up to the Belfry. *(built)* |
| Nave | Three Votaries at prayer; a hatch down; a metal wall up to the Descent. *(built)* |
| Cistern | Under the Nave: Gleaner, Limpet; the Reliquary west, the Sluice east. *(built)* |
| Reliquary | **Mag-grip** and a first shrine. *(built)* |
| Belfry | Over the hall's shaft, by grip or harpoon: shrine, bell, Lamplighter. *(built)* |
| Descent | The drowned well. At its bottom the flooded gate: sink in it (Ballast) and go down with the water into the Sluice (one way), the way out without climbing back. *(built)* |
| Sluice | The floodgate channel east of the Cistern: under a raised gate, over a culvert pit, over a closed gate (a Limpet asleep on it). A drain in its vault where the Descent's flooded gate comes down. *(built)* |
| Undercroft | Crypts under the cathedral, the second hub: shrine, a Votary over the tombs; the high way on to the Chapterhouse by grip or rings, the flue up to the Scriptorium by grip (coming back from either is a drop); the lift from below will arrive here. *(built)* |
| Ossuary | Racks of dead machines, kept like relics: **Harpoon**, then two rings up to a gallery. *(built)* |
| Scriptorium | Up the Undercroft's metal flue (Mag-grip): Votaries copying scripture at lecterns, a gallery over them. Three plates to read (lore), a shrine. *(built)* |
| Chapterhouse | A chapter house half drowned, its vault on one pier. **Boss: the Precentor** (placeholder): the gates fall behind and ahead until it breaks. *(built)* |
| Brink | The lip of the first deep descent, a last shrine. **Third law**: a veil the pilgrim cannot pass until it presses on and breaks it. One way down, to Rimefall. *(built)* |

```map
room Narthex Flooded
room DrownedHall Flooded
room Nave Flooded
room Cistern Flooded
room Reliquary Flooded
room Belfry Flooded
room Descent Flooded
room Sluice Flooded
room Undercroft Flooded
room Ossuary Flooded
room Scriptorium Flooded
room Chapterhouse Flooded
room Brink Flooded

# the slice (built: these must match the scenes' markers)
way Narthex <-> DrownedHall
way DrownedHall <-> Nave
way DrownedHall -> Belfry : MagGrip | Harpoon   # up the shaft
way Belfry -> DrownedHall                        # dropping back down
way Nave -> Cistern                              # the hatch
way Cistern -> Nave : MagGrip                    # the metal flue
way Nave -> Descent : MagGrip                    # the metal wall
way Descent -> Nave
way Cistern <-> Reliquary
relic Reliquary MagGrip
way Descent -> Sluice : Ballast                  # the flooded gate drains into the Sluice's vault

# under the cathedral and the road down (built)
way Cistern <-> Sluice
way Sluice <-> Undercroft
way Undercroft <-> Ossuary
relic Ossuary Harpoon
way Undercroft -> Scriptorium : MagGrip         # up the metal flue
way Scriptorium -> Undercroft                    # dropping back down it
way Undercroft -> Chapterhouse : MagGrip | Harpoon   # the high doorway: the wall or the rings
way Chapterhouse -> Undercroft                   # dropping back onto the ledge
boss Chapterhouse Precentor
way Chapterhouse <-> Brink
way Brink -> Rimefall                            # the jump: one way
```

## Frozen — the cold archive (11 rooms)

Server halls frozen in rime, machines asleep in the cold, a black lake under ice. Two branches from the Coldstacks: the Drill west, the Ballast east.

| Room | Purpose |
|---|---|
| Rimefall | Where the jump from the Brink lands (its sky is only arrived through): a shelf of rime under a broken dome, a frozen cascade down terraces, a shrine at the foot. *(built)* |
| Choir | Choristers, Votaries frozen mid-prayer, asleep along a sunken choir: footsteps never wake them, a hard landing close by does. A silent choir to walk through. *(built)* |
| Coldstacks | The hub: endless frozen racks in stone galleries up to the roof. Shrine. The Counterweight's hatch at the top of the galleries, the Calving ledge high on the west wall (a metal buttress or two rings), the Mere along the corridor under the floor, the Vault under a plug of cracked ice (Drill), the Chimney under a slag cap (Heat plating). *(built)* |
| Counterweight | An old lift that only rises: up through the Coldstacks' roof hatch to a landing, an iron cage on a rope over a sheave, its weight sinking past as it rises the whole shaft to a grate in the Undercroft's floor. *(built)* |
| Calving | An ice cliff that sheds: out of the Coldstacks' high doorway onto its top, a drop to the floor (a Chorister asleep at its foot); back up by an iron girder frozen into its face (grip) or two rings (harpoon). *(built)* |
| Bore | A mining machine frozen mid-bore, a shrine in its hall: the chamber it was cutting is a drop with **Drill** at its end, and the way out is boring through a cracked wall into the tunnel under the hall. *(built)* |
| Mere | A frozen lake in a low cavern: leads of black water in the ice (you float in them), Choristers asleep on it; the lake swum to its bed, a shaft down to the Underice (Ballast). *(built)* |
| Sounding | A sonar chapel on the shore, its bell over a sounding well: **Ballast** on a shelf at the waterline in the well, left by sinking under its wall. A shrine, a scripture plate. *(built)* |
| Underice | A drowned cave under the lake, roofed with ice: sinking and rising past curtains of ice and ridges of rock to a low door up into the Vault. *(built)* |
| Vault | The archive's heart, a tall hall of frozen stacks: entered from above by diving through the Coldstacks' ice plug (Drill, one way, into its pool) or from beneath, up from the Underice into the pool (Ballast). **Boss: the Archivist** (placeholder: it drifts high over the stacks and dives). *(built)* |
| Sanctum | The last chapel of the frozen layer, a shrine: the Hierarch, a priest-machine, orders the pilgrim back from the forbidden door. **Second law**: a veil the pilgrim presses through. The door goes down to the Kiln. *(built)* |

```map
room Rimefall Frozen
room Choir Frozen
room Coldstacks Frozen
room Counterweight Frozen
room Calving Frozen
room Bore Frozen
room Mere Frozen
room Sounding Frozen
room Underice Frozen
room Vault Frozen
room Sanctum Frozen

# built: these must match the scenes' markers
way Rimefall <-> Choir
way Choir <-> Coldstacks
way Coldstacks <-> Counterweight                 # the roof hatch at the top of the galleries
way Counterweight -> Undercroft                  # the lift only rises
way Coldstacks <-> Calving : MagGrip | Harpoon
way Calving <-> Bore
relic Bore Drill
way Coldstacks <-> Mere
way Mere <-> Sounding
relic Sounding Ballast
way Mere <-> Underice : Ballast
way Coldstacks -> Vault : Drill                  # dive through the ice plug, down into the Vault's pool
way Underice <-> Vault : Ballast                 # or come up from beneath
boss Vault Archivist
way Vault <-> Sanctum
way Sanctum <-> Kiln                             # the forbidden door
```

## Volcanic — the foundry (10 rooms)

Basalt, slag rivers, the machines' birthplace: they are cast here in molds of their own image. Warming toward amber.

| Room | Purpose |
|---|---|
| Kiln | Past the forbidden door, down a stair: a kiln the size of a nave, long cold, molds in the shape of machines; on its firing floor the first heat (a low vent) and a shrine. *(built)* |
| Slag | The hub, a cavern over a river of slag (falling in is the end): basalt platforms, heat rising between them, a shrine under the Chimney's vent; a coolant pool down to the Quench (Ballast), a cracked plug over the Basalt (Drill). *(built)* |
| Quench | A coolant reservoir under the Slag's pool: a drop into deep coolant among sunken quench tanks; sunk (Ballast) to the drain in its floor, down into the Crucible; floating, ledges climb back to the hatch (the way home from the Crucible). *(built)* |
| Basalt | A crust of basalt under the Slag's plug, only reached with the Drill (one way, down the plug's walled shaft): plugs dived through, a cracked wall bored, a slag channel jumped, a hole down into the Crucible (one way). *(built)* |
| Crucible | Where plating is cast, under both: a great crucible pouring into glowing moulds, **Heat plating** on a dais between them, a shrine; the coolant falls from the Quench's drain into a basin, gantries climb back to it. *(built)* |
| Ashfall | East of the Slag: a pit under a broken flue, ash raining through; a stair of ledges down to the ash floor, a Gleaner; the Pyre behind a curtain of heat (Heat plating), a hatch down to the Bellows. *(built)* |
| Bellows | Under the Ashfall: a bellows the size of a nave, still heaving; gantries back up to the hatch; its iron frame (Mag-grip) or the rings on its ribs (Harpoon) up to the high door on to the Foundry; a shrine. *(built)* |
| Pyre | Through the heat (Heat plating): where machines are given to the fire; a pyre on an altar, broken machines laid round it, a scripture plate, a shrine. *(built)* |
| Chimney | A narrow vent from the Slag's roof up to the slag welling in the Coldstacks' floor: ledges up its throat, its molten mouth passed only with Heat plating. The shortcut home. *(built)* |
| Foundry | The great foundry, an arcade over a molten channel. **Boss: the Founder** (placeholder: the Precentor's brain, heavier). The vents to the core, a shaft of heat in the floor, need Heat plating. *(built)* |

```map
room Kiln Volcanic
room Slag Volcanic
room Quench Volcanic
room Basalt Volcanic
room Crucible Volcanic
room Ashfall Volcanic
room Bellows Volcanic
room Pyre Volcanic
room Chimney Volcanic
room Foundry Volcanic

way Kiln <-> Slag
way Slag -> Quench : Ballast                     # sunk through the coolant pool's floor
way Quench -> Slag                               # up the ledges to the hatch, floating up the pool
way Slag -> Basalt : Drill                       # the dive through the plug, down the walled shaft: one way
way Basalt -> Crucible                           # a drop through the crust's floor: one way
way Quench -> Crucible : Ballast                 # sunk through the drain in its floor
way Crucible -> Quench                           # up the gantries, floating up from the drain
relic Crucible HeatPlating
way Slag <-> Ashfall
way Ashfall <-> Pyre : HeatPlating
way Ashfall <-> Bellows                          # the hatch in the ash floor; gantries back up to it
way Bellows -> Foundry : MagGrip | Harpoon       # the bellows' iron frame or the rings on its ribs, up to the high door
way Foundry -> Bellows                           # back out onto the high ledge, and down
boss Foundry Founder
way Slag <-> Chimney
way Chimney <-> Coldstacks : HeatPlating
way Foundry <-> Threshold : HeatPlating
```

## Core — what the machines call paradise (6 rooms)

Ancient core machinery, red and ember. Every ability is needed here, in combination.

| Room | Purpose |
|---|---|
| Threshold | The gate through the heat: the Foundry's vents drop into a column of heat, ledges in it climb back (Heat plating both ways); a great ring turning like a gate; a shrine. *(built)* |
| Mantle | The first of the core: great wheels still turning; a bronze piston seized over the way (Mag-grip climbs it from beneath), rings across the air from its top (Harpoon) to the high door on to the Orrery; a Lamplighter. *(built)* |
| Orrery | A pit where the orrery's great rings turn round a glowing centre; metal arcs step down it, mooring rings between them; at the bottom a plug of cracked glass over a flooded seam, down to the Lode (Drill and Ballast). *(built)* |
| Lode | Molten-glass seams: the flooded crack from the Orrery sunk to its low gap (Ballast), a wall of cracked glass bored (Drill), the door on to the Effigy. *(built)* |
| Effigy | An effigy of the makers, a stone colossus astride the way. A shrine, a scripture plate. **First law**: the way on is between its legs, through the veil. *(built)* |
| Heart | The goal: the planet's heart turning in its rings. **Boss: Nirvanary** (placeholder: the Archivist's brain, wider); reaching the heart once it is still ends the game (the end card, then a new game). *(built)* |

```map
room Threshold Core
room Mantle Core
room Orrery Core
room Lode Core
room Effigy Core
room Heart Core

way Threshold <-> Mantle
way Mantle -> Orrery : MagGrip & Harpoon         # up the piston (grip), across the rings (harpoon) to the high door
way Orrery -> Mantle                             # back out onto the door's ledge, and down
way Orrery <-> Lode : Drill & Ballast            # the plug over the flooded seam bored, the seam sunk; back up it
way Lode <-> Effigy
way Effigy <-> Heart
boss Heart Nirvanary
```
