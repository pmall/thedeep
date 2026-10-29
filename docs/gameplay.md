# Gameplay

**Feel: graceful** ([vision.md](vision.md)). Moves chain without hard stops and momentum carries through transitions, while control stays tight and precise.

## Base moveset (implemented)

- **Run**
- **Jump** with variable height (release early for a short hop), coyote time and a jump buffer
- **Dash** on the ground and in the air (one air dash per airtime, refilled on landing), with a short cooldown. A ground dash can be cancelled into a jump.

All numbers live in `resources/player_tuning.tres` (see "Tuning" below).

## Combat, health and the pilgrimage (M6)

- **Attack** (J / X, pad X): a quick swing, a pale crescent sweeping from above to the front (`PlayerAttack`, timing in `Swing`, pure). It never stops the pilgrim: momentum carries through. Upright and on Mag-grip surfaces. A press during the recovery is buffered, so swings chain. Each machine the hit window reaches takes one hit per swing (`MachineDirector` resolves hits against each machine's body box): it flashes, reels (its brain stunned 0.35 s, a wind-up lost) and is pushed back; when its health (`MachineSpecies.Health`) is gone it folds down and is gone until the room is entered again.
- **Health:** `MaxHealth` hits (5), shown top left as thin diamonds. A machine's blow (`MachineSpecies.Damage`) takes health, throws the pilgrim back and up, takes control away for `HurtStun`, then nothing hurts for `HurtGrace` (the body shimmers).
- **Death:** the veil falls slowly; the pilgrim wakes at the last shrine it rested at, whole, the room renewed (its machines back).
- **Pilgrim shrines** (save points, `scenes/props/Shrine.tscn`): standing at one shows "Rest"; **E** (pad Y) rests: full health, the glyphs flare, the game is saved there.
- **Ability relics** (`scenes/props/AbilityRelic.tscn`): walking into one takes it: the pilgrim stands still while the core drifts in, the ability unlocks, a banner names it and says how to use it, and the game saves. A taken relic stays taken.
- **Saving:** `user://save.json` (`SaveData`, pure; on Linux `~/.local/share/godot/app_userdata/The Deep/save.json`): the last shrine rested at, abilities, relics taken, the map. A normal launch resumes there; `godot --path . -- --new` starts fresh (the save is overwritten at the first rest). Checks, look-dev and `--room` runs never read or write it.
- **Map** (M / Tab, pad LB): the visited rooms, placed from the exits crossed; pauses the game.
- **Leaving a room:** walk (run, dash, drift) out through a doorway; standing in one, or dropping into one from above, never travels. Hatches in floors and ceilings are crossed whatever the way.
- **Scripture plates** (`scenes/props/ScripturePlate.tscn`, the Scriptorium): standing at one shows "Read"; **E** (pad Y) shows its words in the banner. Lore only.
- **Bosses** (M8, placeholders until the character pass): a boss stands in an arena (`BossSeal`). Stepping in drops the gates behind and ahead (`Portcullis`), and its name shows; breaking it raises them, and it never stands again (saved at once). Dying in the fight returns to the last shrine, the fight renewed. **The Precentor** (Chapterhouse, `resources/machines/precentor.tres`, `ConductorBrain`): beats time until it sees the pilgrim, then stalks it and brings its baton down after a long wind-up (0.75 s), lunging into the blow; every third strike a quicker one (0.4 s wind-up) follows in the same direction. Hits never stagger it or push it far (`StaggerTime` 0, `Heft` 0.15: species data), so standing and swinging loses about half the time; the slow recovery after a blow (1.1 s) is the opening, and the pilgrim passes through machines, so slipping behind it during the wind-up dodges. 24 hits.
- **Law breaks** (story beats, [theme.md](theme.md); outside the ability graph): **the third law** stands at the Brink's lip as a veil of glyphs the pilgrim cannot walk through, its words rising as the pilgrim nears ("A robot must protect its own existence."). Pressing on toward the drop strains against it (the words tremble and brighten); after 2.6 s of pressing (letting go slips back at half the rate) the law breaks: the veil scatters, the pilgrim holds still a breath, a banner, and the way is open for good (saved at once). Then the jump, one way down.
- **Falling out of a room** (below its camera bounds, a drop into a room not built yet) returns the pilgrim to its last shrine. The Brink's fall does this until Rimefall exists, after a banner saying the frozen layer is not built yet.
- All combat numbers are in `player_tuning.tres` (Attack, Hurt groups), next to the movement ones.

## Unlockable abilities

These gate the map. Every ability is one simple state or rule: no systemic or emergent mechanics.

| Ability | What it does | What it gates |
|---|---|---|
| **Mag-grip** | Walk on **metal** walls and ceilings. Rock cannot be gripped. | Vertical shafts and ceilings made of metal |
| **Harpoon** | Fire at an anchor point and zip to it | Gaps with anchor points |
| **Drill** | Break cracked rock | Cracked rock walls and floors |
| **Ballast** | Toggle between sinking and floating in water | Flooded layers |
| **Heat plating** | Passive: survive heat-vent zones | Late-game areas near the core |

The player can collect them in many orders, and that freedom is proven by a solver ([long-term.md](long-term.md), objective 2).

**Mag-grip (implemented, M6).** `GripState` with pure geometry in `GripMath` (unit tested). Metal is the physics layer only (`world_metal`, set by `Block.Surface`).
- **Attach:** press into a metal wall (on the ground or in the air), or rise into a metal ceiling (a jump). The body turns about its center so it swings out from the wall; its up becomes the surface normal (`UpDirection` and the body's rotation about Z, so the collider turns with it).
- **Move:** held input along the surface (up/down on a wall, left/right under a ceiling, in screen terms) at `GripSpeed`. **Pull away** from the surface to let go, **jump** to leap off (away from a wall at `GripLeapAwaySpeed` and up at `GripLeapUpFactor` of a jump; downward from a ceiling), **dash** to let go and dash. `GripRegrabDelay` keeps the same surface from catching the player straight back.
- **Corners snap** (a later pass may smooth them): a metal wall ahead turns the player onto it (inner corner); a surface's end wraps the player around the edge onto the end face (outer corner); reaching a floor stands it up. After a corner, the input that led there keeps meaning "keep going" until it changes, so holding up climbs a wall, turns under the ceiling and walks along it. A rock face ahead just stops the player; a rock end face lets go.
- **The visible body eases** into each new pose (`PlayerVisual.CarryOver`): the collider snaps, the body follows in about a tenth of a second. Animation sees the body's own frame, so walking on a wall plays the run cycle; the coat and braid still hang toward the world's floor.
- A grip surface refills the air dash. Travelling through an exit while gripping stands the player up first.
- Checked headless in `scenes/rooms/MechanicsLab.tscn` (`GripCheck`, in `tools/check.sh`); look-dev shoots a grip reel there (`Grip.png`).

**Harpoon (implemented, M8).** `HarpoonState`, aiming in `HarpoonAim` (pure, unit tested), the eye and the line in `HarpoonRig` (in Main).
- **Anchors** are mooring rings placed in rooms (`scenes/props/HarpoonAnchor.tscn`): the node's origin is where the pilgrim's center is pulled to. They are only a position (no collision); keep a body's height of air below one.
- **Sight:** each tick the rig picks the anchor the pilgrim would fire at: within `HarpoonRange` of the body's center (not nearer than `HarpoonMinDistance`), in clear sight (a ray on the solid layers). Holding up or down **aims** (only anchors within about 70° of the held direction); otherwise anchors ahead beat anchors above, which beat anchors behind, nearer first. The anchor just zipped to is skipped until the pilgrim stands or grips somewhere. The anchor in sight brightens.
- **Fire** (K / V, pad B or LT): from the ground, the air, a dash or a Mag-grip surface. The line flies out at `HarpoonThrowSpeed` while the pilgrim hangs, slowing; once it bites, the pull gathers speed (`HarpoonZipAcceleration`) up to `HarpoonZipSpeed` until the body's center reaches the anchor. Then it lets go with `HarpoonExitSpeedFactor` of the pull's speed and at least `HarpoonExitLift` upward (unless the pull went steeply down), and the air dash is back. Jump or dash during the pull cancel into them, keeping momentum; a pull blocked by a solid lets go where it stands.
- Checked headless in the mechanics lab (`HarpoonCheck`, in `tools/check.sh`): sight, fire, pull, release, chaining to a second anchor, blocked sight, range, jump cancel. The room check's geometry pass also zips between each room's anchors (`ReachPlanner` with `HarpoonLimits`): a way the Harpoon opens must be reached that way.

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
| `harpoon` | K, V | B, LT |
| `interact` (rest at a shrine) | E | Y |
| `map` | M, Tab | LB |
| `capture_screenshot` | F12 | Share / Misc |
| `capture_video` (start/stop) | F10 | — |
| `debug_respawn` | R | Back |
| `debug_toggle_{mag_grip,harpoon,drill,ballast,heat_plating}` | F1–F5 | — |
| `debug_cycle_biome` | F6 | — |
| `debug_toggle_hud` (state readout) | F3 | — |
