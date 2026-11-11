# Map draft — the prototype world

The prototype world's network file (`Network` in [world.tres](world.tres)): 42 rooms over three layers and the core, 5 abilities, a key, a code, a lever, a secret, an ambush, 2 power-ups, 4 bosses, 3 law breaks. It is a **planning graph**: the `map` blocks below are parsed by `MapDraft` (its docblock has the format) and checked on every build, by the unit tests and against the built rooms (`instructions/testing.md#check`); everything outside them is design notes. Its room table is [rooms.tsv](rooms.tsv) (`RoomTable` has the format), held to this network on every build; the real game's map will be its own network and table (`docs/roadmap.md#stage-plan`). Room names, boss names and story beats are placeholders unless [theme.md](../../docs/theme.md) says otherwise.

**The markers are the truth** <a id="draft-markers"></a>: a built room's exits, sealed exits and relics must match its ways and relics here. When a room is built or changed, update its part of the draft in the same commit. Keep its row in [rooms.tsv](rooms.tsv) in step too. Print both with `tools/map.sh`.

```map
start Narthex
goal Heart
target orders 24        # order freedom (docs/long-term.md#order-freedom): at least 24 of the 120 ability orders
target positions 2      # every ability obtainable at 2 positions in the order, at least
target rooms 40 60      # the scope (docs/vision.md#scope): about 2 hours, 40–60 rooms
milestone Brink ThirdLaw
milestone Sanctum SecondLaw
milestone Effigy FirstLaw
```

## The shape at a glance <a id="draft-shape"></a>

The main road and its branches, layer by layer (simplified; the `map` blocks are the truth). `(X)` a way needing X, `[X]` a relic, `✦` a boss, `▼` a law break, `→` one way.

```
FLOODED   Narthex ─ DrownedHall ─(M|H)→ Belfry → back down;   Cell ┄ Sacristy ─(key)─ Narthex, west of it
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
                                                           └─ Ashfall ─(P, code)─ Pyre
                                                              Ashfall ─(M|H)─ Bellows ─ Foundry ✦ ─(P)─ Threshold
CORE      Threshold ─ Mantle ─(M&H)─ Orrery ─(D&B)─ Lode ─ Effigy ▼ first law ─ Heart ✦ goal
```

M Mag-grip, H Harpoon, D Drill, B Ballast, P Heat plating; (key) the Sacristy key, on the Scriptorium's gallery; (code) the Pyre's litany, on the Belfry's lookout; ┄ a hidden way.

## Order freedom <a id="draft-order-freedom"></a>

- **The first ability is Mag-grip or Harpoon.** Both lie in the flooded layer, reached with the base moveset: Mag-grip west of the Cistern (the Reliquary), Harpoon east of it, through the Sluice and the Undercroft (the Ossuary). Taking one road skips the other.
- **Either one opens the way down**: the Undercroft's high doorway to the Chapterhouse is reached by climbing its metal wall or by harpooning its two rings. So the frozen layer can be entered with either.
- **Drill and Ballast lie in the frozen layer**, on two branches of the Coldstacks; either one opens the Vault (dive through its ice plug, or come up from beneath through the Underice), and so the way on to the volcanic layer.
- **Heat plating lies in the volcanic layer**, in the Crucible, reached through the Quench (Ballast) or through the Basalt (Drill). It can be third, fourth or fifth.
- **The core needs everything**: the Orrery needs Mag-grip and Harpoon, the Lode Drill and Ballast, the Threshold Heat plating.
- Abilities left behind stay reachable: the Counterweight lift rises from the Coldstacks to the Undercroft, the Sanctum stays open behind the pilgrim, and the Chimney (up from the volcanic layer) ties the layers together.

The rule this gives: **Mag-grip or Harpoon first, Heat plating after Drill or Ballast**, everything else free. That is 32 of the 120 orders; Mag-grip and Harpoon at 1–5, Drill and Ballast at 2–5, Heat plating at 3–5. The test pins these numbers, so a change to the gates that costs freedom is seen at once.

**Sequence breaks by design:** Harpoon before Mag-grip (the Sluice road skips the Reliquary); the Belfry by grip or harpoon; the Calving cliff by grip or harpoon; the Vault by drill or by ballast; the Crucible by ballast or by drill; Heat plating before the second frozen ability.

## Law breaks (story beats tied to depth) <a id="draft-law-breaks"></a>

Outside the ability graph (`docs/theme.md#laws-outside-abilities`): each happens in a room every route to the goal passes through, and the check proves they always come in order. They open nothing the abilities don't already gate.

- **Third law (self-preservation), the Brink.** The end of the flooded layer: a drop no machine survives. At first the android refuses it. Breaking the law is the jump, and the jump is one way.
- **Second law (obedience), the Sanctum.** Past the Archivist, a priest-machine commands the android to turn back. The command stops binding; the forbidden door opens on the volcanic layer.
- **First law (never harm a human), the Effigy.** Before the Heart, an effigy of the makers, the gods of the machines' scripture. The only way on is through it.

## Bosses <a id="draft-bosses"></a>

One per layer, plus the end, each on the main road. Placeholders.

- **The Precentor** (Chapterhouse, flooded): the choir's conductor, before the Brink.
- **The Archivist** (Vault, frozen): keeper of the cold archive, before the Sanctum.
- **The Founder** (Foundry, volcanic): the abbot of the foundry, before the Threshold.
- **Nirvanary** (Heart, core): what waits at the center.

## Flooded — the drowned cathedral (15 rooms) <a id="draft-flooded"></a>

The cathedral, the crypts under it and the road down. Pale white, a touch of purple, water everywhere.

Its rooms, what each is for and what happens there: their rows in [rooms.tsv](rooms.tsv).

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
room Sacristy Flooded
room Cell Flooded

# the cathedral
way Narthex <-> DrownedHall
way Narthex <-> Sacristy : SacristyKey           # the locked door in the porch's west wall
way Sacristy <-> Cell                            # hidden: through the false foot of the Sacristy's west wall
hidden Sacristy Cell                             # the false foot of the Sacristy's west wall: the Cell is a secret room
powerup Sacristy defense                         # on a dais under the shaft of light
powerup Cell strength                            # on a plinth
way DrownedHall <-> Nave
way DrownedHall -> Belfry : MagGrip | Harpoon   # up the shaft
way Belfry -> DrownedHall                        # dropping back down
item Belfry PyreLitany                           # the Pyre's litany, on the lookout under the bell
way Nave -> Cistern                              # the hatch
way Cistern -> Nave : MagGrip                    # the metal flue
way Nave -> Descent : MagGrip                    # the metal wall
way Descent -> Nave
way Cistern <-> Reliquary
relic Reliquary MagGrip
way Descent -> Sluice : Ballast                  # the flooded gate drains into the Sluice's vault

# under the cathedral and the road down
way Cistern <-> Sluice
way Sluice <-> Undercroft
way Undercroft <-> Ossuary
relic Ossuary Harpoon
way Undercroft -> Scriptorium : MagGrip         # up the metal flue
way Scriptorium -> Undercroft                    # dropping back down it
item Scriptorium SacristyKey                     # on the gallery's far end
way Undercroft -> Chapterhouse : MagGrip | Harpoon   # the high doorway: the wall or the rings
way Chapterhouse -> Undercroft                   # dropping back onto the ledge
boss Chapterhouse Precentor
way Chapterhouse <-> Brink
way Brink -> Rimefall                            # the jump: one way
```

## Frozen — the cold archive (11 rooms) <a id="draft-frozen"></a>

Server halls frozen in rime, machines asleep in the cold, a black lake under ice. Two branches from the Coldstacks: the Drill west, the Ballast east.

Its rooms, what each is for and what happens there: their rows in [rooms.tsv](rooms.tsv).

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

## Volcanic — the foundry (10 rooms) <a id="draft-volcanic"></a>

Basalt, slag rivers, the machines' birthplace: they are cast here in molds of their own image. Warming toward amber.

Its rooms, what each is for and what happens there: their rows in [rooms.tsv](rooms.tsv).

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

way Kiln <-> Slag : KilnLever                    # the low door's gate, raised by its lever
item Kiln KilnLever                              # the lever, by the door
way Slag -> Quench : Ballast                     # sunk through the coolant pool's floor
way Quench -> Slag                               # up the ledges to the hatch, floating up the pool
way Slag -> Basalt : Drill                       # the dive through the plug, down the walled shaft: one way
way Basalt -> Crucible                           # a drop through the crust's floor: one way
way Quench -> Crucible : Ballast                 # sunk through the drain in its floor
way Crucible -> Quench                           # up the gantries, floating up from the drain
relic Crucible HeatPlating
way Slag <-> Ashfall
way Ashfall <-> Pyre : HeatPlating & PyreLitany   # through the heat, the low door sealed
ambush Pyre                                      # the Pyre seals while its wave stands
way Ashfall <-> Bellows                          # the hatch in the ash floor; gantries back up to it
way Bellows -> Foundry : MagGrip | Harpoon       # the bellows' iron frame or the rings on its ribs, up to the high door
way Foundry -> Bellows                           # back out onto the high ledge, and down
boss Foundry Founder
way Slag <-> Chimney
way Chimney <-> Coldstacks : HeatPlating
way Foundry <-> Threshold : HeatPlating
```

## Core — what the machines call paradise (6 rooms) <a id="draft-core"></a>

Ancient core machinery, red and ember. Every ability is needed here, in combination.

Its rooms, what each is for and what happens there: their rows in [rooms.tsv](rooms.tsv).

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
