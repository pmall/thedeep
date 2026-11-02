# Bricks — the catalog

Every piece a world is built from (`docs/principles.md#bricks`). The prototype's job is to get each one **ready**; the real game is then assembled from them (`docs/roadmap.md#four-stages`). This file is the world builder's view: what you can place, and how far along it is. What a brick is and how to configure it is in its type's docblock; a brick that doesn't exist yet is described by the roadmap item that builds it.

**Status** `#brick-status`: **ready**: works, checked, at the visual bar, reusable as is. **working**: works and is checked, but its look, feel or generality still needs its prototype pass. **missing**: not built yet. **Item** is the roadmap item that makes it ready. Keep this file current when a brick is added or changes status.

## Stage: the look of a place `#bricks-stage`

| Brick | Status | Item |
|---|---|---|
| `BiomeProfile` (flooded, frozen, volcanic, core) | working | `docs/roadmap.md#stage-bricks` |
| Material library (`assets/materials/`, `assets/shaders/`) | working | `docs/roadmap.md#stage-bricks` |
| `RoomStage` | working | `docs/roadmap.md#stage-bricks` |
| Skylines (`backdrop_silhouette.gdshader`) | working | `docs/roadmap.md#stage-bricks` |
| `BackdropArchitecture` | working | `docs/roadmap.md#stage-bricks` |
| `WallDressing` | working | `docs/roadmap.md#stage-bricks` |
| `Colonnade`, `RackRow`, `Spinner` | working | `docs/roadmap.md#stage-bricks` |
| Kit meshes (`scripts/kit/`) | working | `docs/roadmap.md#stage-bricks` |
| Light shafts, lamps, glyph lights (`Room`) | working | `docs/roadmap.md#stage-bricks` |
| `AmbientParticles` | working | `docs/roadmap.md#stage-bricks` |
| `WaterBody` (scenery) | working | `docs/roadmap.md#stage-bricks` |
| Screen grade (`screen_grade.gdshader`) | working | `docs/roadmap.md#stage-bricks` |
| The signature: `PaintPass`, `PaintStyle` (`docs/art-direction.md#signature`) | working (three variants await the owner's pick) | `docs/roadmap.md#stage-bricks` (first) |

## Level: what the pilgrim plays on `#bricks-level`

| Brick | Status | Item |
|---|---|---|
| `Block` (rock, metal, cracked) | ready | — |
| `RoomExit` | ready | — |
| `HarpoonAnchor` | working (look) | `docs/roadmap.md#stage-bricks` |
| `WaterBody` (swim) | ready | — |
| `HeatZone` | working (look) | `docs/roadmap.md#stage-bricks` |
| `Lift` | ready | — |
| `Portcullis` | working | — |
| `MapGoal` | ready | — |
| `PhotoSpot` | ready | — |
| `LockedDoor` (a key's lock or a code's seal, on an exit) | working (look) | `docs/roadmap.md#stage-bricks` |
| Hidden passage | missing | `docs/roadmap.md#game-systems` |
| Switch and mechanism | missing | `docs/roadmap.md#game-systems` |

## The pilgrim `#bricks-pilgrim`

| Brick | Status | Item |
|---|---|---|
| Body and look (`resources/parts/pilgrim.tres`) | working (`docs/art-direction.md#art-open`) | `docs/roadmap.md#pilgrim` |
| Animation (`PilgrimMotion`) | working (placeholder) | `docs/roadmap.md#pilgrim` |
| Base moveset (`docs/gameplay.md#moveset`) | ready (rules), feel pass to come | `docs/roadmap.md#feel-fight` |
| Abilities (`docs/gameplay.md#abilities`) | ready (rules), animation to come | `docs/roadmap.md#pilgrim`, `docs/roadmap.md#feel-fight` |
| Fight moves (`PlayerAttack`) | working (one move) | `docs/roadmap.md#feel-fight` |
| Effects (`PlayerEffects`) | working | `docs/roadmap.md#pilgrim` |
| Health and grace | working | `docs/roadmap.md#feel-fight` |

## Encounters `#bricks-encounters`

| Brick | Status | Item |
|---|---|---|
| `Machine` + `MachineSpecies` | ready (framework) | — |
| Brains (`core/machines/brains/`) | working placeholders | `docs/roadmap.md#stage-polish` (the bestiary) |
| Species (`resources/machines/`, 10 placeholders) | working placeholders | `docs/roadmap.md#stage-polish` |
| `BossSeal` | working | — |
| Ambush | missing | `docs/roadmap.md#game-systems` |

## Story and text `#bricks-text`

| Brick | Status | Item |
|---|---|---|
| Line book (`LineBook`) | ready | — |
| `Banner`, `HintLine` | working | `docs/roadmap.md#game-systems` |
| `Proclamation` | working | `docs/roadmap.md#game-systems` |
| `ScripturePlate` | working | `docs/roadmap.md#game-systems` |
| `LawBreak` | working | — |
| Dialogue box | missing | `docs/roadmap.md#game-systems` |

## Items and progress `#bricks-items`

| Brick | Status | Item |
|---|---|---|
| `AbilityRelic` | working | `docs/roadmap.md#pilgrim` (the moment) |
| Power-up | missing | `docs/roadmap.md#game-systems` |
| Items: `ItemDefinition`, `ItemPickup` (keys, codes), held by `Inventory`, played by `Keyring` | working (look; no code in a world yet) | `docs/roadmap.md#stage-bricks` |

## Systems `#bricks-systems`

| Brick | Status | Item |
|---|---|---|
| World (`WorldDefinition`) | ready (the room table comes with the network, `docs/roadmap.md#stage-plan`) | `docs/roadmap.md#sequel-test` |
| `Session`, `Shrine`, save | working | `docs/roadmap.md#game-systems` (items in the save) |
| Room travel (`RoomTravel`) | ready | — |
| Map screen (`MapScreen`) | working | `docs/roadmap.md#game-systems` |
| Gate hints (`GateHints`; a locked door's line is the `Keyring`'s) | ready | — |
| End card (`EndCard`) | working placeholder | — |
| Camera (`SideCamera`) | working | `docs/roadmap.md#feel-fight` |

## Proof `#bricks-proof`

| Brick | Status |
|---|---|
| Map solver (`MapSolver`) | ready |
| Reach planner (`ReachPlanner`) | ready |
| Room, travel, species and lab checks (`docs/testing.md#check`) | ready |
| Required exports (`RequiredExports`) | ready |
| Network and room table check | missing (`docs/roadmap.md#stage-plan`) |
