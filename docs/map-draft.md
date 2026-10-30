# Map draft — the whole game

The whole map on paper (roadmap M7): 42 rooms over three layers and the core, 5 abilities, 4 bosses, 3 law breaks. It is a **draft planning graph**: the solver checks it on every build, before the rooms exist. Room names, boss names and story beats are placeholders unless [theme.md](theme.md) says otherwise.

**How it is used.** The `map` blocks below are parsed by `MapDraft` (`scripts/map/MapDraft.cs`); everything outside them is design notes. `tools/check.sh` checks the draft twice:
- Unit tests (`MapDraftTests`) parse this file and run `MapDraft.Check`: no softlock, every room reachable, every ability on exactly one relic, **at least 24 of the 120 ability orders**, every ability obtainable in **at least 2 positions**, 40–60 rooms, and each law break on every route to the goal, in story order (third, second, first).
- The room check (`RoomValidator`) compares the draft with the rooms already built: a built room's exits, sealed exits (an empty `TargetRoom`) and relics must match the draft's ways and relics for that room. **The markers are the truth**: when a room is built or changed, update its part of the draft in the same commit. Goals are not compared (the slice ends at the Descent's `MapGoal`; the game ends at the Heart).
- Print it: `tools/map.sh` (the built map and the draft: summaries, every way, every achievable order).

**Format.** `room Name Layer`, `way A -> B` (one way) or `way A <-> B` (both), `: requirement` after a way (`MagGrip | Harpoon`, `Drill & Ballast`; a `<->` way needs it both ways), `relic Room Ability`, `boss Room Name`, `milestone Room Name` (in story order), `start Room`, `goal Room`. `#` starts a comment.

```map
start Narthex
goal Heart
milestone Brink ThirdLaw
milestone Sanctum SecondLaw
milestone Effigy FirstLaw
```

## The shape at a glance

The main road and its branches, layer by layer (simplified; the `map` blocks are the truth). `(X)` a way needing X, `[X]` a relic, `✦` a boss, `▼` a law break, `→` one way.

```
FLOODED   Narthex ─ DrownedHall ─(M|H)→ Belfry → back down
                      │
                    Nave ─(M)→ Descent ─(B)─ Cryptwater, ─(D)─ Fissure   (shortcuts from below)
                      ↓ hatch
          Reliquary[M] ─ Cistern ─ Sluice ─ Undercroft ─ Ossuary[H]
                         ↑(M) flue           │  ├─(M)→ Scriptorium (a drop back)
                                             │  └─(M|H)→ Chapterhouse ✦ ─ Brink ▼ third law
                                             ↑ lift                        ↓ the jump
FROZEN                              Counterweight ─ Coldstacks ─ Choir ─ Rimefall
                                                     ├─(M|H)─ Calving ─ Bore[D] ─(D)─ Fissure
                                                     ├─ Mere ─ Sounding[B];  Mere ─(B)─ Underice ─(B)─ Cryptwater
                                                     └─(D)─ Vault ✦ ─(B)─ Underice
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
- **Drill and Ballast lie in the frozen layer**, on two branches of the Coldstacks; either one opens the Vault (break its frozen doors, or sink beneath them through the Underice), and so the way on to the volcanic layer.
- **Heat plating lies in the volcanic layer**, in the Crucible, reached through the Quench (Ballast) or through the Basalt (Drill). It can be third, fourth or fifth.
- **The core needs everything**: the Orrery needs Mag-grip and Harpoon, the Lode Drill and Ballast, the Threshold Heat plating.
- Abilities left behind stay reachable: the Counterweight lift rises from the Coldstacks to the Undercroft, the Sanctum stays open behind the pilgrim, and later shortcuts (the Descent's two sealed ways, the Chimney) tie the layers together.

The rule this gives: **Mag-grip or Harpoon first, Heat plating after Drill or Ballast**, everything else free. That is 32 of the 120 orders; Mag-grip and Harpoon at 1–5, Drill and Ballast at 2–5, Heat plating at 3–5. The test pins these numbers, so a change to the gates that costs freedom is seen at once.

**Sequence breaks by design:** Harpoon before Mag-grip (the Sluice road skips the Reliquary); the Belfry by grip or harpoon; the Calving cliff by grip or harpoon; the Vault by drill or by ballast; the Crucible by ballast or by drill; Heat plating before the second frozen ability.

## Law breaks (story beats tied to depth)

Outside the ability graph ([theme.md](theme.md)): each happens in a room every route to the goal passes through, and the check proves they always come in order. They open nothing the abilities don't already gate.
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
| Descent | The drowned well: sealed Ballast and Drill ways down, shortcuts once the frozen layer is open. *(built)* |
| Sluice | The floodgate channel east of the Cistern: under a raised gate, over a culvert pit, over a closed gate (a Limpet asleep on it). *(built)* |
| Undercroft | Crypts under the cathedral, the second hub: shrine, a Votary over the tombs; the high way on to the Chapterhouse by grip or rings, the flue up to the Scriptorium by grip (coming back from either is a drop); the lift from below will arrive here. *(built)* |
| Ossuary | Racks of dead machines, kept like relics: **Harpoon**, then two rings up to a gallery. *(built)* |
| Scriptorium | Up the Undercroft's metal flue (Mag-grip): Votaries copying scripture at lecterns, a gallery over them. Three plates to read (lore), a shrine. *(built)* |
| Chapterhouse | A chapter house half drowned, its vault on one pier. **Boss: the Precentor** (placeholder): the gates fall behind and ahead until it breaks. *(built)* |
| Brink | The lip of the first deep descent, a last shrine. **Third law**: a veil the pilgrim cannot pass until it presses on and breaks it. One way down (to Rimefall, sealed until built). *(built)* |

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
way Descent <-> Cryptwater : Ballast             # sealed: the flooded gate
way Descent <-> Fissure : Drill                  # sealed: the cracked wall

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

## Frozen — the cold archive (13 rooms)

Server halls frozen in rime, machines asleep in the cold, a black lake under ice. Two branches from the Coldstacks: the Drill west, the Ballast east.

| Room | Purpose |
|---|---|
| Rimefall | Where the jump from the Brink lands (its sky is only arrived through): a shelf of rime under a broken dome, a frozen cascade down terraces, a shrine at the foot. *(built)* |
| Choir | Choristers, Votaries frozen mid-prayer, asleep along a sunken choir: footsteps never wake them, a hard landing close by does. A silent choir to walk through. *(built)* |
| Coldstacks | The hub: endless frozen racks in stone galleries up to the roof. Shrine. The Counterweight's hatch at the top of the galleries, the Calving ledge high on the west wall (a metal buttress or two rings), the Mere along the corridor under the floor, the Vault under an ice plug (Drill), the Chimney under a slag cap (Heat plating); all but the hatch sealed until built. *(built)* |
| Counterweight | An old lift that only rises: up through the Coldstacks' roof hatch to a landing, an iron cage on a rope over a sheave, its weight sinking past as it rises the whole shaft to a grate in the Undercroft's floor. *(built)* |
| Calving | An ice cliff that sheds: crossed by grip or harpoon. |
| Bore | A mining machine frozen mid-bore: **Drill**. |
| Fissure | A crack through the bedrock up to the Descent (Drill both ways). |
| Mere | A frozen lake; black water under the ice. |
| Sounding | A sonar chapel on the shore: **Ballast**. |
| Underice | Under the lake, sinking and rising (Ballast). |
| Cryptwater | The flooded stair up to the Descent's gate (Ballast). |
| Vault | The archive's heart, entered by breaking its doors or from beneath. **Boss: the Archivist.** |
| Sanctum | A priest-machine forbids the way on. **Second law.** |

```map
room Rimefall Frozen
room Choir Frozen
room Coldstacks Frozen
room Counterweight Frozen
room Calving Frozen
room Bore Frozen
room Fissure Frozen
room Mere Frozen
room Sounding Frozen
room Underice Frozen
room Cryptwater Frozen
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
way Bore <-> Fissure : Drill
way Coldstacks <-> Mere
way Mere <-> Sounding
relic Sounding Ballast
way Mere <-> Underice : Ballast
way Underice <-> Cryptwater : Ballast
way Coldstacks <-> Vault : Drill                 # break the frozen doors
way Underice <-> Vault : Ballast                 # or come up from beneath
boss Vault Archivist
way Vault <-> Sanctum
way Sanctum <-> Kiln                             # the forbidden door
```

## Volcanic — the foundry (10 rooms)

Basalt, slag rivers, the machines' birthplace: they are cast here in molds of their own image. Warming toward amber.

| Room | Purpose |
|---|---|
| Kiln | Past the forbidden door: a cold kiln, the first heat. Shrine. |
| Slag | The hub over slag rivers. |
| Quench | A coolant reservoir: sink through it (Ballast). |
| Basalt | A crust of basalt: drill through it (Drill). |
| Crucible | Where plating is cast: **Heat plating**. |
| Ashfall | Ash raining through a broken flue. |
| Bellows | A giant bellows machine, its anchors and metal ribs (grip or harpoon). |
| Pyre | Through the vents (Heat plating): where machines are given to the fire. Lore, a shrine. |
| Chimney | A vent up to the Coldstacks (Heat plating): the shortcut home. |
| Foundry | The great foundry. **Boss: the Founder.** The vents to the core need Heat plating. |

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
way Slag <-> Quench : Ballast
way Slag <-> Basalt : Drill
way Quench <-> Crucible
way Basalt <-> Crucible
relic Crucible HeatPlating
way Slag <-> Ashfall
way Ashfall <-> Pyre : HeatPlating
way Ashfall <-> Bellows : MagGrip | Harpoon
way Bellows <-> Foundry
boss Foundry Founder
way Slag <-> Chimney
way Chimney <-> Coldstacks : HeatPlating
way Foundry <-> Threshold : HeatPlating
```

## Core — what the machines call paradise (6 rooms)

Ancient core machinery, red and ember. Every ability is needed here, in combination.

| Room | Purpose |
|---|---|
| Threshold | The gate through the heat. |
| Mantle | The first of the core: old machinery, still turning. |
| Orrery | Metal rings in motion: grip them and harpoon between them (Mag-grip and Harpoon). |
| Lode | Molten-glass seams: drill and sink (Drill and Ballast). |
| Effigy | An effigy of the makers. **First law.** |
| Heart | The goal. **Boss: Nirvanary.** |

```map
room Threshold Core
room Mantle Core
room Orrery Core
room Lode Core
room Effigy Core
room Heart Core

way Threshold <-> Mantle
way Mantle <-> Orrery : MagGrip & Harpoon
way Orrery <-> Lode : Drill & Ballast
way Lode <-> Effigy
way Effigy <-> Heart
boss Heart Nirvanary
```
