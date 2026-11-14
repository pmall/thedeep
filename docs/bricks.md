# Bricks — the catalog

Every piece a world is built from (`instructions/principles.md#bricks`). This file is the world builder's view: what you can place, and how far along it is. What a brick is and how to configure it is in its type's docblock; a brick that doesn't exist yet is described by the roadmap item that builds it.

**Status** <a id="brick-status"></a>: **ready**: works, checked, at the visual bar, reusable as is. **working**: works and is checked, but its look, feel or generality still needs its pass. **missing**: not built yet. **Item**: the open roadmap item that makes it ready. Keep this file current when a brick is added or changes status.

## Stage: the look of a place <a id="bricks-stage"></a>

All working; brought to ready under `docs/roadmap.md#bricks-ready`.

- `BiomeProfile` (flooded, frozen, volcanic, core; brine, the sequel world's variant of frozen)
- Material library (`assets/materials/`, `assets/shaders/`)
- Baked surface sets (`SurfaceSet`, in `assets/textures/surfaces/`): strata, the volcanic layer's basalt columns, masonry, ice, cracks, the metal's panels and coating, the machines' glyph bands, the frozen layer's rime (frost, one global set every material that frosts shares)
- `RoomStage`
- Skylines (`backdrop_silhouette.gdshader`)
- `BackdropArchitecture` (its rows `BackdropRowData`: a layer's on its `BiomeProfile`, a room's on its `RoomStage`)
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
| `RoomExit` | ready | — |
| `Doorway` (a doorway's sill and lintel, from a `SeamProfile` both rooms of the way share, `resources/seams/`, and the wall it is cut in, over the lintel and under the sill out to the room's own solids (`SeamFrame.Wall`; `Head` or `Foot` false where a route runs over or under it), in the look of the room's wall it meets, `Material` only to override it; placed by `instructions/testing.md#seam-brick`) | working (reads as an opening in a wall in the rooms captured; no room turns its wall off) | — |
| `Hatch` (the slab either side of a hatch's hole, or of a chasm's open floor, from the same kind of `SeamProfile`, matched to its room like a `Doorway` and curbed at its hole, a metal one in rock lined with metal at its hole; placed by `instructions/testing.md#seam-brick`; `Flooded`, on a `Doorway` too, runs the room's water through the opening, and `Standpipe` on a flooded hatch in a roof holds it down into the room's lower water; a `Lift` whose `Through` names the exit takes its stops from the profile) | working (ten seams framed, three flooded hatches and one flooded doorway, two standpipes, one lift shaft) | — |
| `HarpoonAnchor` (its state shown: `AnchorLook`) | ready | — |
| `AnchorPointer` (the edge chevron toward a ring in sight off screen) | ready | — |
| `WaterBody` (swim) | ready | — |
| `SlagBody` (`scenes/kit/SlagBody.tscn`: placed at its surface and sized to a basin of blocks, a bed and two banks the room check holds) | ready (the Basalt, the Slag, the Crucible's moulds, the Pyre's altar) | — |
| `HeatColumn` (`scenes/kit/HeatColumn.tscn`: placed at its foot on rock and sized up to the roof, hood or exit that ends it, or through a hatch by naming it (`instructions/testing.md#seam-brick`); a gate the room check holds to having no way round) | ready (the Ashfall, the Kiln, the Basalt, the Chimney, the Coldstacks, the Pyre, the Foundry, the Threshold) | — |
| `Lift` | ready | — |
| `Portcullis` (`scenes/props/Portcullis.tscn`: a gate of iron bars and three beams that drops across a way and lifts, sized by `Width` and `Height`; used by arenas, boss seals, ambushes and a `LockedDoor`) | ready | — |
| `MapGoal` | ready | — |
| `PhotoSpot` | ready | — |
| `LockedDoor` (a key's lock or a code's seal, on an exit; `DoorLock`, one per kind; on screen it says its item and the room that lies in, the room check holding each item's line to both) | ready | — |
| Hidden passage: `FalseWall` (found by walking in, played by `Secrets`) over a `RoomExit.Hidden` way | ready | — |
| `Lever` (a switch: sets a mechanism, an item of kind Mechanism) and its gate (a `LockedDoor` for it) | ready | — |

## The pilgrim <a id="bricks-pilgrim"></a>

| Brick | Status | Item |
| -- | -- | -- |
| Body, look and animation | missing (the current ones are replaced from zero) | `docs/roadmap.md#pilgrim` |
| Base moveset (`docs/gameplay.md#moveset`) | ready | — |
| Abilities (`docs/gameplay.md#abilities`) | ready | — |
| Fight moves (`PlayerAttack` over the core's `Combo` and `Swing`; each strike's numbers in `player_tuning.tres`) | ready (their look is the pilgrim's) | — |
| Effects (`PlayerEffects`) | working | `docs/roadmap.md#pilgrim` |
| Health and grace (`Vitality`, shown by `HealthHud`) | ready | — |

## Encounters <a id="bricks-encounters"></a>

| Brick | Status | Item |
| -- | -- | -- |
| `Machine` + `MachineSpecies` | ready (framework) | — |
| Brains (`core/machines/brains/`) | working placeholders | `docs/roadmap.md#stage-polish` (the bestiary) |
| Species (`resources/machines/`, 10 placeholders) | working placeholders | `docs/roadmap.md#stage-polish` |
| `BossSeal` | ready | — |
| `Ambush` (with `BossSeal`, over the core's `ArenaFight`, played by `Arenas`) | ready | — |

## Story and text <a id="bricks-text"></a>

| Brick | Status | Item |
| -- | -- | -- |
| Line book (`LineBook`: the game's own, `resources/lines.tres`, which each world's extends) | ready | — |
| `Banner`, `HintLine` (lines said together are read in turn, each held for its reading time, `ReadingTime`; both on the style's plate, placed by one layout, `TextStyle.Places`; the line check holds every line each can say to its box; a hint whose reason is gone is withdrawn) | ready | — |
| `Proclamation` (spoken: the banner's `Voice.Spoken`, its speaker a line id) | ready | — |
| `ScripturePlate` (its glyphs glow while its words are on screen; its post, plate and glyphs take a placement's own materials; read by `PlateCheck`) | ready | — |
| `LawBreak` (broken by a held act on its `ActionPrompt`, never a push; the pilgrim's refusal on the `HintLine`; its lines per room; its words in the style's `WorldFont`, drawn over the room's solids) | working | `docs/roadmap.md#stage-polish` (the real law-break scenes) |
| `ActionPrompt` (a verb and its button, pressed or held; the button a `ButtonGlyph` drawn by a `ButtonPainter`, as in the `ControlsManual`; `Reach` keeps it clear of what stands beside it) | ready | — |
| Dialogue: `Conversation` (its lines `DialogueLine`s, each naming its speaker by a line id; its "Talk" placed by `TalkPrompt`), `DialogueBox` (in the banner's places, `DialoguePlace`), played by `Conversations` (over `DialogueFlow`, revealed at a `RevealPace`) | ready | — |

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
| `Session`, `Shrine`, save | ready | — |
| Room travel (`RoomTravel`) | ready | — |
| Map screen (`MapScreen`) | ready | — |
| Gate hints (`GateHints`; a locked door's line is the `Keyring`'s) | ready | — |
| End card (`EndCard`) | working placeholder | `docs/roadmap.md#stage-polish` |
| Camera (`SideCamera` over the core's `CameraFollow`) | ready | — |

## Proof <a id="bricks-proof"></a>

All ready: the map solver (`MapSolver`), the reach planner (`ReachPlanner`), the room, travel, species and lab checks (`instructions/testing.md#check`), the route check playing every room's ground routes as the planner flies them (`RouteCheck`, over `GroundRoutes`), required exports (`RequiredExports`), the line check (`LineCheck`), the network and room table check (`MapDraft`, `RoomTable`, kinds in `RoomKind`) and its drawing (`MapDrawing`, written by `tools/map.sh`).
