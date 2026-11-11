# Bricks — the catalog

Every piece a world is built from (`instructions/principles.md#bricks`). This file is the world builder's view: what you can place, and how far along it is. What a brick is and how to configure it is in its type's docblock; a brick that doesn't exist yet is described by the roadmap item that builds it.

**Status** <a id="brick-status"></a>: **ready**: works, checked, at the visual bar, reusable as is. **working**: works and is checked, but its look, feel or generality still needs its pass. **missing**: not built yet. **Item**: the open roadmap item that makes it ready. Keep this file current when a brick is added or changes status.

## Stage: the look of a place <a id="bricks-stage"></a>

All working; the art pass makes them ready (`docs/roadmap.md#art-restore`).

- `BiomeProfile` (flooded, frozen, volcanic, core; brine, the sequel world's variant of frozen)
- Material library (`assets/materials/`, `assets/shaders/`)
- Baked surface sets (`SurfaceSet`, in `assets/textures/surfaces/`): strata, masonry, ice, cracks, the metal's panels and coating, the machines' glyph bands, the frozen layer's rime (frost, one global set every material that frosts shares)
- `RoomStage`
- Skylines (`backdrop_silhouette.gdshader`)
- `BackdropArchitecture`
- `WallDressing`
- `Colonnade`, `RackRow`, `Spinner`
- Kit meshes (`scripts/kit/`)
- Light shafts (`Room.Shafts`, `LightShaft`), lamps, glyph lights (`Room`)
- The vignette (`scenes/camera/Vignette.tscn`; its strength and color per `BiomeProfile`)
- `AmbientParticles`
- `WaterBody` (scenery)

## Level: what the pilgrim plays on <a id="bricks-level"></a>

| Brick | Status | Item |
| -- | -- | -- |
| `Block` (rock, metal, cracked) | ready | — |
| `RoomExit` | ready (seams: `docs/roadmap.md#seam-coherence`) | — |
| `Doorway` (a doorway's sill and lintel, from a `SeamProfile` both rooms of the way share, `resources/seams/`; placed by `instructions/testing.md#seam-brick`) | working (one seam framed) | `docs/roadmap.md#seam-coherence` |
| `Hatch` (the slab either side of a hatch's hole, or of a chasm's open floor, from the same kind of `SeamProfile`; placed by `instructions/testing.md#seam-brick`; `Flooded`, on a `Doorway` too, runs the room's water through the opening; a `Lift` whose `Through` names the exit takes its stops from the profile) | working (ten seams framed, three flooded hatches and one flooded doorway, one lift shaft) | `docs/roadmap.md#seam-coherence` |
| `HarpoonAnchor` (its state shown: `AnchorLook`) | ready | — |
| `AnchorPointer` (the edge chevron toward a ring in sight off screen) | working (look) | `docs/roadmap.md#bricks-ready` |
| `WaterBody` (swim) | ready | — |
| `SlagBody` (`scenes/kit/SlagBody.tscn`: placed at its surface and sized to a basin of blocks, a bed and two banks the room check holds) | working (the Basalt, the Slag, the Crucible's moulds, the Pyre's altar; the look's final pass) | `docs/roadmap.md#volcanic-rethink` |
| `HeatColumn` (`scenes/kit/HeatColumn.tscn`: placed at its foot on rock and sized up to the roof, hood or exit that ends it, a gate the room check holds to having no way round) | working (the Ashfall, the Kiln, the Basalt, the Chimney, the Coldstacks, the Pyre, the Foundry, the Threshold) | `docs/roadmap.md#volcanic-rethink` |
| `Lift` | ready | — |
| `Portcullis` | working | `docs/roadmap.md#bricks-ready` |
| `MapGoal` | ready | — |
| `PhotoSpot` | ready | — |
| `LockedDoor` (a key's lock or a code's seal, on an exit; `DoorLock`, one per kind) | ready | — |
| Hidden passage: `FalseWall` (found by walking in, played by `Secrets`) over a `RoomExit.Hidden` way | ready | — |
| `Lever` (a switch: sets a mechanism, an item of kind Mechanism) and its gate (a `LockedDoor` for it) | ready | — |

## The pilgrim <a id="bricks-pilgrim"></a>

| Brick | Status | Item |
| -- | -- | -- |
| Body, look and animation | missing (the current ones are replaced from zero) | `docs/roadmap.md#pilgrim` |
| Base moveset (`docs/gameplay.md#moveset`) | ready (rules), feel to tune | `docs/roadmap.md#feel-fight` |
| Abilities (`docs/gameplay.md#abilities`) | ready (rules), feel to tune | `docs/roadmap.md#feel-fight` |
| Fight moves (`PlayerAttack` over the core's `Combo`) | working (feel) | `docs/roadmap.md#feel-fight` |
| Effects (`PlayerEffects`) | working | `docs/roadmap.md#pilgrim` |
| Health and grace | working | `docs/roadmap.md#feel-fight` |

## Encounters <a id="bricks-encounters"></a>

| Brick | Status | Item |
| -- | -- | -- |
| `Machine` + `MachineSpecies` | ready (framework) | — |
| Brains (`core/machines/brains/`) | working placeholders | `docs/roadmap.md#stage-polish` (the bestiary) |
| Species (`resources/machines/`, 10 placeholders) | working placeholders | `docs/roadmap.md#stage-polish` |
| `BossSeal` | working | `docs/roadmap.md#bricks-ready` |
| `Ambush` (with `BossSeal`, over the core's `ArenaFight`, played by `Arenas`) | working | `docs/roadmap.md#bricks-ready` |

## Story and text <a id="bricks-text"></a>

| Brick | Status | Item |
| -- | -- | -- |
| Line book (`LineBook`: the game's own, `resources/lines.tres`, which each world's extends) | ready | — |
| `Banner`, `HintLine` | working | `docs/roadmap.md#bricks-ready` |
| `Proclamation` | working | `docs/roadmap.md#law-readable` |
| `ScripturePlate` | working | `docs/roadmap.md#bricks-ready` |
| `LawBreak` | working | `docs/roadmap.md#law-readable` |
| Dialogue: `Conversation`, `DialogueBox`, played by `Conversations` (over `DialogueFlow`) | working (look) | `docs/roadmap.md#bricks-ready` |

## Items and progress <a id="bricks-items"></a>

| Brick | Status | Item |
| -- | -- | -- |
| `AbilityRelic` | working | `docs/roadmap.md#pilgrim` (the moment) |
| `PowerUp` (strength or defense, for good: `Prowess`), played by `PowerUps` | ready | — |
| Items: `ItemDefinition`, `ItemPickup` (keys, codes), held by `Inventory`, played by `Keyring` | ready | — |

## Systems <a id="bricks-systems"></a>

| Brick | Status | Item |
| -- | -- | -- |
| World (`WorldDefinition`: its network and room table) | ready | — |
| `Session`, `Shrine`, save | working | `docs/roadmap.md#bricks-ready` |
| Room travel (`RoomTravel`) | ready | — |
| Map screen (`MapScreen`) | working | `docs/roadmap.md#bricks-ready` |
| Gate hints (`GateHints`; a locked door's line is the `Keyring`'s) | ready | — |
| End card (`EndCard`) | working placeholder | `docs/roadmap.md#stage-polish` |
| Camera (`SideCamera`) | working | `docs/roadmap.md#feel-fight` |

## Proof <a id="bricks-proof"></a>

All ready: the map solver (`MapSolver`), the reach planner (`ReachPlanner`), the room, travel, species and lab checks (`instructions/testing.md#check`), required exports (`RequiredExports`), the line check (`LineCheck`), the network and room table check (`MapDraft`, `RoomTable`, kinds in `RoomKind`) and its drawing (`MapDrawing`, written by `tools/map.sh`).
