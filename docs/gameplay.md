# Gameplay

**Feel: graceful** ([vision.md](vision.md)). Moves chain without hard stops and momentum carries through transitions, while control stays tight and precise.

## Base moveset (implemented)

- **Run**
- **Jump** with variable height (release early for a short hop), coyote time and a jump buffer
- **Dash** on the ground and in the air (one air dash per airtime, refilled on landing), with a short cooldown. A ground dash can be cancelled into a jump.

All numbers live in `resources/player_tuning.tres` (see "Tuning" below).

## Unlockable abilities (not implemented yet)

These gate the map. Every ability is one simple state or rule: no systemic or emergent mechanics.

| Ability | What it does | What it gates |
|---|---|---|
| **Mag-grip** | Walk on **metal** walls and ceilings. Rock cannot be gripped. | Vertical shafts and ceilings made of metal |
| **Harpoon** | Fire at an anchor point and zip to it | Gaps with anchor points |
| **Drill** | Break cracked rock | Cracked rock walls and floors |
| **Ballast** | Toggle between sinking and floating in water | Flooded layers |
| **Heat plating** | Passive: survive heat-vent zones | Late-game areas near the core |

The player can collect them in many orders, and that freedom is proven by a solver ([long-term.md](long-term.md), objective 2).

**Mag-grip implementation note.** Rotating the player around corners is the hard part:
- Use raycasts toward the surface, plus `up_direction` aligned to the surface normal.
- Make corner transitions snap rather than flow at first, and smooth them only once everything else works.
- Metal versus rock is a data property, the **physics layer** (`world_metal` vs `world_rock`, set by `Block.Surface`). Never infer it from visuals.

Abilities are flags in `AbilitySet` (`Player.Abilities`). In debug builds, F1–F5 toggle them.

## Tuning

- **All movement numbers** (speeds, accelerations, jump height and time to apex, jump cut, gravity multipliers, coyote time, jump and dash buffers, dash distance, duration and cooldown, air dashes) live in `resources/player_tuning.tres` (a `PlayerTuning` resource). Never hard-code them.
- These numbers are also the movement limits that the map's geometry check will read ([long-term.md](long-term.md)).
- Gravity and take-off speed are derived from `JumpHeight` and `JumpTimeToApex`. A full held jump reaches slightly more than `JumpHeight` (about 3.36 m for 3.2) because of the lighter apex gravity.
- Camera and visual feel numbers are `[Export]`s on their scenes (`scenes/camera/SideCamera.tscn`, `PlayerVisual`).

## Controls

Input actions are defined in `project.godot` and mirrored in `scripts/core/InputActions.cs`. Never use string literals for them elsewhere.

| Action | Keyboard | Gamepad |
|---|---|---|
| `move_left` / `move_right` | A / D, ← / → | Left stick, d-pad |
| `move_up` / `move_down` | W / S, ↑ / ↓ | Left stick, d-pad |
| `jump` | Space, Z | A |
| `dash` | Shift, C | RB, RT |
| `attack` | J, X | X |
| `interact` | E | Y |
| `debug_respawn` | R | Back |
| `debug_toggle_{mag_grip,harpoon,drill,ballast,heat_plating}` | F1–F5 | — |
