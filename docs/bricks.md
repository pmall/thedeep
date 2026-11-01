# Bricks — the catalog

Every piece a world is built from ([principles.md](principles.md), "Bricks, not rooms"). The prototype's job is to get each one **ready**; the real game is then assembled from them ([roadmap.md](roadmap.md)). Where the code behind a brick lives is in [architecture.md](architecture.md); this file is the world builder's view: what you can place, and how far along it is.

**Status:** **ready**: works, checked, at the visual bar, reusable as is. **working**: works and is checked, but its look, feel or generality still needs its prototype pass. **missing**: not built yet. Keep this table current when a brick is added or changes status.

## Stage: the look of a place

| Brick | What it is | Status | Prototype item |
|---|---|---|---|
| `BiomeProfile` | a depth layer's look: palette, light, fog, grading, frost, water colors, backdrop style | working (flooded, frozen, volcanic, core) | P2 |
| Material library | rock (strata, wet, cracked, basalt), metal (panels, rust), ice, water, magma, glyphs, heat haze, machine, android; `*_mid` for the midground | working | P2 |
| `RoomStage` | the shared atmosphere stack every room instances: environment, key light, clear front fog, skylines, architecture in the haze, underglow, framing, dust | working | P2 |
| Skylines | far silhouettes by style: cathedral, server halls, foundry, machinery, crags | working | P2 |
| `BackdropArchitecture` | rows of kit pieces at three depths: nave, stacks, foundry, machinery | working | P2 |
| `WallDressing` | a room's back wall dressed as a foundry, machinery or an archive | working | P2 |
| `Colonnade`, `RackRow`, `Spinner` | piers and arches; rows of racks; old machinery still turning | working | P2 |
| Kit meshes | block, column, pipe, cable, arch/rib, grate, rack, wreck, icicle, stack | working | P2 |
| Light shafts, lamps, glyph lights | a room's own lights, colored by the biome | working | P2 |
| Ambient particles | dust, silt, drips, rime, embers | working | P2 |
| `WaterBody` (scenery) | a water surface with reflections, caustics and underwater haze | working | P2 |
| Screen grade | vignette and grain from the biome | working | P2 |

## Level: what the pilgrim plays on

| Brick | What it is | Status | Prototype item |
|---|---|---|---|
| `Block` | gameplay box: rock (not grippable), metal (Mag-grip), cracked (the Drill) | ready | — |
| `RoomExit` | a way out: doorway or hatch, requirement expression, one way, arrival only | ready | P1 (items in requirements) |
| `HarpoonAnchor` | a mooring ring the Harpoon zips to | working (look) | P2 |
| `WaterBody` (swim) | water the pilgrim swims in; deep water gates Ballast | ready | — |
| `HeatZone` | heat that burns without Heat plating | working (look) | P2 |
| `Lift` | a counterweighted cage that carries the pilgrim up | ready | — |
| `Portcullis` | a gate of bars that falls and rises | working | — |
| `MapGoal` | where the journey ends | ready | — |
| `PhotoSpot` | a look-dev camera position | ready | — |
| Locked door + key or code | a way that needs an item from the inventory | missing | P5 |
| Hidden passage | a false wall or floor that gives way: the secret room's entrance | missing | P5 |
| Switch and mechanism | a lever or plate that opens or moves something in the room (an enigma's piece) | missing | P5 |

## The pilgrim

| Brick | What it is | Status | Prototype item |
|---|---|---|---|
| Body and look | part assembly, android materials | working (hair reads like a helmet; contrast on dark backdrops) | P3 |
| Animation | procedural: gait, lean, landing, air and dash poses; secondary motion | working (placeholder) | P3 |
| Base moveset | run, jump, ledge assist, dash, air dash | ready (rules), feel pass to come | P4 |
| Abilities | Mag-grip, Harpoon, Drill (dash bore and dive), Ballast, Heat plating | ready (rules), animation to come | P3, P4 |
| Fight moves | one swing that chains | working (one move) | P4 |
| Effects | grit, sparks, dust bursts by surface, dash trail | working | P3 |
| Health and grace | diamonds, hurt, grace, death and return | working | P4 |

## Encounters

| Brick | What it is | Status | Prototype item |
|---|---|---|---|
| `Machine` + `MachineSpecies` | one node for every machine; a species is data (body, locomotion, stats, palette, behavior, animation) | ready (framework) | — |
| Brains | Prayer, Hoarder, Follower, Sleeper, Conductor, Swoop | working placeholders | polish (the bestiary) |
| Species | 10 placeholders (Votary, Lamplighter, Gleaner, Limpet, Chorister, Precentor, Archivist, Hierarch, Founder, Nirvanary) | working placeholders | polish |
| `BossSeal` | a boss arena: gates fall while the boss stands | working | — |
| Ambush | an arena that seals while a wave of machines stands (the trap room) | missing | P5 |

## Story and text

| Brick | What it is | Status | Prototype item |
|---|---|---|---|
| Line book | every line as data: id, speaker, text, delivery notes | ready | — |
| `Banner`, `HintLine` | a title and a line; one quiet line low on the screen | working | P5 |
| `Proclamation` | a line spoken when the pilgrim comes near | working | P5 |
| `ScripturePlate` | lore read at a lectern | working | P5 |
| `LawBreak` | a law holding the pilgrim at a lip until it presses through | working | — |
| Dialogue box | a conversation: speaker, lines in turn, advanced by the player | missing | P5 |

## Items and progress

| Brick | What it is | Status | Prototype item |
|---|---|---|---|
| `AbilityRelic` | takes an ability | working | P3 (the moment) |
| Power-up | one kind of loot: a permanent bonus to strength or defense | missing | P5 |
| Inventory | keys and codes held; shown small | missing | P5 |

## Systems

| Brick | What it is | Status | Prototype item |
|---|---|---|---|
| World | which rooms, network, table, biomes and line book make one world, and where it starts | missing (the prototype is wired in: `scenes/rooms/`, `docs/map-draft.md`, `DepthLayer`) | P1 |
| `Session`, `Shrine`, save | shrines, death and return, relics, the save | working | P5 (items in the save) |
| Room travel | crossing an exit under a fade, arriving with momentum | ready | — |
| Map screen | visited rooms and the ways not taken | working | P5 |
| Gate hints | a gate the pilgrim can't pass yet says what it needs | ready | P5 (keys) |
| End card | the end of a run | working placeholder | — |
| Camera | follow, look-ahead, room bounds | working | P4 |

## Proof

| Brick | What it is | Status |
|---|---|---|
| Map solver | no softlock, ability orders, from the network | ready |
| Reach planner | the moveset and all five abilities simulated inside each room, with a clumsy pilgrim | ready |
| Room, travel, species and lab checks | headless, in `tools/check.sh` | ready |
| Network and room table check | the real game's plan on paper: graph and table agree, targets met | missing (stage B) |
